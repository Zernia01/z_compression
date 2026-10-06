using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using ZCompression.Core.Archives;

namespace ZCompression.App;

[ComVisible(true), Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDataObjectAsyncCapability
{
    void SetAsyncMode([MarshalAs(UnmanagedType.Bool)] bool enabled);
    void GetAsyncMode([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    void StartOperation(IntPtr context);
    void InOperation([MarshalAs(UnmanagedType.Bool)] out bool active);
    void EndOperation(int result, IntPtr context, uint effects);
}

[SupportedOSPlatform("windows"), ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class VirtualArchiveDataObject : IDataObject, IDataObjectAsyncCapability, IDisposable
{
    private readonly IReadOnlyList<ArchiveExportEntry> _entries;
    private readonly Func<ArchiveExportEntry, Stream> _open;
    private readonly Action _ended;
    private readonly List<ArchiveContentStream> _streams = [];
    private readonly object _gate = new();
    private readonly short _descriptors = (short)RegisterClipboardFormat("FileGroupDescriptorW");
    private readonly short _contents = (short)RegisterClipboardFormat("FileContents");
    private readonly short _effect = (short)RegisterClipboardFormat("Preferred DropEffect");
    private readonly short _marker = (short)RegisterClipboardFormat("ZCompression.ArchiveSelection");
    private bool _async = true;
    internal bool Active { get; private set; }
    internal Exception? Error { get; private set; }

    internal VirtualArchiveDataObject(IReadOnlyList<ArchiveExportEntry> entries, Func<ArchiveExportEntry, Stream> open, Action ended)
    { _entries = entries; _open = open; _ended = ended; }

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        medium = default;
        var result = QueryGetData(ref format);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
        if (format.cfFormat == _contents)
        {
            if (format.lindex < 0) throw new COMException("A file content index is required.", unchecked((int)0x80040068));
            var entry = _entries[format.lindex];
            var stream = new ArchiveContentStream(entry.Name, entry.Size, () => _open(entry), error => Error ??= error, RegisterStream);
            RegisterStream(stream);
            medium.tymed = TYMED.TYMED_ISTREAM;
            medium.unionmember = Marshal.GetComInterfaceForObject(stream, typeof(IStream));
            return;
        }
        var bytes = format.cfFormat == _descriptors ? CreateDescriptors(_entries) : BitConverter.GetBytes(format.cfFormat == _effect ? 1 : 0);
        var memory = GlobalAlloc(0x42, (nuint)bytes.Length);
        if (memory == IntPtr.Zero) throw new OutOfMemoryException();
        var pointer = GlobalLock(memory);
        if (pointer == IntPtr.Zero) { GlobalFree(memory); throw new OutOfMemoryException(); }
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
        finally { GlobalUnlock(memory); }
        medium.tymed = TYMED.TYMED_HGLOBAL;
        medium.unionmember = memory;
    }

    internal static byte[] CreateDescriptors(IReadOnlyList<ArchiveExportEntry> entries)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Encoding.Unicode, leaveOpen: true);
        writer.Write(entries.Count);
        foreach (var entry in entries)
        {
            writer.Write(0x4004u | (entry.IsDirectory ? 0u : 0x40u) | (entry.Modified is null ? 0u : 0x20u));
            writer.Write(new byte[32]); // CLSID, SIZEL, POINTL
            writer.Write(entry.IsDirectory ? 0x10u : 0x80u);
            writer.Write(0L); writer.Write(0L);
            writer.Write(entry.Modified?.ToUniversalTime().ToFileTimeUtc() ?? 0L);
            writer.Write((uint)((ulong)entry.Size >> 32)); writer.Write((uint)entry.Size);
            var name = Encoding.Unicode.GetBytes(entry.Name);
            writer.Write(name); writer.Write(new byte[520 - name.Length]);
        }
        return memory.ToArray();
    }

    public int QueryGetData(ref FORMATETC format)
    {
        if (format.dwAspect != DVASPECT.DVASPECT_CONTENT) return unchecked((int)0x8004006B);
        if (format.cfFormat == _contents)
        {
            if (format.lindex < -1 || format.lindex >= _entries.Count || (format.lindex >= 0 && _entries[format.lindex].IsDirectory)) return unchecked((int)0x80040068);
            return (format.tymed & TYMED.TYMED_ISTREAM) != 0 ? 0 : unchecked((int)0x80040069);
        }
        if (format.cfFormat != _descriptors && format.cfFormat != _effect && format.cfFormat != _marker) return unchecked((int)0x80040064);
        return (format.tymed & TYMED.TYMED_HGLOBAL) != 0 ? 0 : unchecked((int)0x80040069);
    }
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if (direction != DATADIR.DATADIR_GET) throw new NotSupportedException();
        FORMATETC Format(short id, TYMED medium) => new() { cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = medium };
        return new FormatEnumerator([Format(_descriptors, TYMED.TYMED_HGLOBAL), Format(_contents, TYMED.TYMED_ISTREAM), Format(_effect, TYMED.TYMED_HGLOBAL), Format(_marker, TYMED.TYMED_HGLOBAL)]);
    }
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) { if (release) ReleaseStgMedium(ref medium); }
    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Unsupported storage.", unchecked((int)0x80040069));
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; output.ptd = IntPtr.Zero; return 0x00040130; }
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return unchecked((int)0x80040003); }
    public void DUnadvise(int connection) { }
    public int EnumDAdvise(out IEnumSTATDATA? enumerator) { enumerator = null; return unchecked((int)0x80040003); }
    public void SetAsyncMode(bool enabled) => _async = enabled;
    public void GetAsyncMode(out bool enabled) => enabled = _async;
    public void StartOperation(IntPtr context) => Active = true;
    public void InOperation(out bool active) => active = Active;
    public void EndOperation(int result, IntPtr context, uint effects)
    {
        Active = false;
        if (result < 0) Error ??= result is unchecked((int)0x80004004) or unchecked((int)0x800704C7) ? new OperationCanceledException() : Marshal.GetExceptionForHR(result);
        _ended();
    }
    private void RegisterStream(ArchiveContentStream stream) { lock (_gate) _streams.Add(stream); }
    public void Dispose()
    {
        ArchiveContentStream[] streams;
        lock (_gate) { streams = _streams.ToArray(); _streams.Clear(); }
        foreach (var stream in streams) stream.Dispose();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] internal static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
    [DllImport("ole32.dll")] internal static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class FormatEnumerator(FORMATETC[] formats) : IEnumFORMATETC
{
    private int _position;
    public int Next(int count, FORMATETC[] output, int[]? fetched)
    {
        var received = Math.Min(count, formats.Length - _position);
        Array.Copy(formats, _position, output, 0, received); _position += received;
        if (fetched is { Length: > 0 }) fetched[0] = received;
        return received == count ? 0 : 1;
    }
    public int Skip(int count) { var available = formats.Length - _position; _position += Math.Min(count, available); return available >= count ? 0 : 1; }
    public int Reset() { _position = 0; return 0; }
    public void Clone(out IEnumFORMATETC enumerator) => enumerator = new FormatEnumerator(formats) { _position = _position };
}
