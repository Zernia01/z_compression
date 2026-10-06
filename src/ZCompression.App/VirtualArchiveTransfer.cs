using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using ZCompression.Core.Archives;

namespace ZCompression.App;

internal sealed class VirtualArchiveTransfer
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<IntPtr> _marshaled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Dispatcher? _dispatcher;
    private VirtualArchiveDataObject? _data;
    private IntPtr _pointer;
    private bool _dragFinished;
    private bool _stopped;
    private uint _effects;
    internal Task Completion => _completion.Task;
    internal IntPtr NativeDataPointer => _pointer;

    internal static async Task<VirtualArchiveTransfer> CreateAsync(IReadOnlyList<ArchiveExportEntry> entries, string archive, string? password, Func<ArchiveExportEntry, CancellationToken, System.IO.Stream>? open = null)
    {
        var transfer = new VirtualArchiveTransfer();
        var thread = new Thread(() => transfer.Run(entries, archive, password, open)) { IsBackground = true, Name = "Archive virtual file transfer" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var marshaled = await transfer._marshaled.Task;
        var id = new Guid("0000010E-0000-0000-C000-000000000046");
        var result = CoGetInterfaceAndReleaseStream(marshaled, ref id, out transfer._pointer);
        if (result < 0) { transfer.Cancel(); transfer.FinishDrag(0); Marshal.ThrowExceptionForHR(result); }
        return transfer;
    }

    private void Run(IReadOnlyList<ArchiveExportEntry> entries, string archive, string? password, Func<ArchiveExportEntry, CancellationToken, System.IO.Stream>? open)
    {
        var initialized = OleInitialize(IntPtr.Zero);
        try
        {
            Marshal.ThrowExceptionForHR(initialized);
            _dispatcher = Dispatcher.CurrentDispatcher;
            var engine = new SharpCompressArchiveEngine();
            _data = new VirtualArchiveDataObject(entries, entry => open is null ? engine.OpenEntryReadStream(archive, entry.ArchivePath, password, _cancel.Token) : open(entry, _cancel.Token), QueueFinish);
            var id = new Guid("0000010E-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(ref id, _data, out var marshaled));
            _marshaled.SetResult(marshaled);
            Dispatcher.Run();
        }
        catch (Exception error) { _marshaled.TrySetException(error); _completion.TrySetException(error); }
        finally { _data?.Dispose(); if (initialized >= 0) OleUninitialize(); }
    }

    internal uint Drag()
    {
        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        try
        {
            var result = DoDragDrop(_pointer, new DropSource(), 1, out var effects);
            if (result < 0) Marshal.ThrowExceptionForHR(result);
            return effects;
        }
        finally { OleUninitialize(); }
    }

    internal void Cancel() => _cancel.Cancel();
    internal void FinishDrag(uint effects)
    {
        if (_pointer != IntPtr.Zero) { Marshal.Release(_pointer); _pointer = IntPtr.Zero; }
        _dispatcher?.BeginInvoke(new Action(() => { _effects = effects; _dragFinished = true; TryFinish(); }));
    }
    private void QueueFinish() => _dispatcher?.BeginInvoke(new Action(TryFinish));
    private void TryFinish()
    {
        if (_stopped || !_dragFinished || _data is null || _data.Active) return;
        _stopped = true;
        _cancel.Cancel();
        _data.Dispose();
        if (_data.Error is { } error) _completion.TrySetException(error);
        else if (_effects == 0) _completion.TrySetCanceled();
        else _completion.TrySetResult();
        _dispatcher!.BeginInvokeShutdown(DispatcherPriority.Background);
    }

    // A raw marshaled IDataObject pointer preserves async capability and keeps callbacks off the UI object graph.
    [DllImport("ole32.dll")] private static extern int CoMarshalInterThreadInterfaceInStream(ref Guid id, [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data, out IntPtr stream);
    [DllImport("ole32.dll")] private static extern int CoGetInterfaceAndReleaseStream(IntPtr stream, ref Guid id, out IntPtr data);
    [DllImport("ole32.dll")] private static extern int DoDragDrop(IntPtr data, [MarshalAs(UnmanagedType.Interface)] IArchiveDropSource source, uint allowed, out uint effects);
    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class DropSource : IArchiveDropSource
    {
        public int QueryContinueDrag(bool escape, uint keys) => escape ? 0x00040101 : (keys & 1) == 0 ? 0x00040100 : 0;
        public int GiveFeedback(uint effects) => 0x00040102;
    }
}

[ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IArchiveDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
    [PreserveSig] int GiveFeedback(uint effects);
}
