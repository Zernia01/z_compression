using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ZCompression.App;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ArchiveContentStream(string name, long length, Func<Stream> open, Action<Exception> failed, Action<ArchiveContentStream> register) : IStream, IDisposable
{
    private Stream? _input;
    private long _position;
    private long _readPosition;
    private bool _disposed;
    private bool _verifiedEnd;
    private readonly object _gate = new();

    public void Read(byte[] buffer, int count, IntPtr readCount)
    {
        lock (_gate) ReadCore(buffer, count, readCount);
    }

    private void ReadCore(byte[] buffer, int count, IntPtr readCount)
    {
        var read = 0;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            var desired = (int)Math.Min(count, length - _position);
            if (desired > 0)
            {
                _input ??= open();
                var skip = new byte[64 * 1024];
                while (_readPosition < _position)
                {
                    var skipped = _input.Read(skip, 0, (int)Math.Min(skip.Length, _position - _readPosition));
                    if (skipped == 0) throw new EndOfStreamException();
                    _readPosition += skipped;
                }
                while (read < desired)
                {
                    var received = _input.Read(buffer, read, Math.Min(64 * 1024, desired - read));
                    if (received == 0) throw new EndOfStreamException("Archive content ended before its declared size.");
                    read += received;
                    _position += received;
                    _readPosition += received;
                }
            }
            if (count > 0 && _position == length && _readPosition == length && !_verifiedEnd && (_input is not null || length == 0))
            {
                _input ??= open();
                if (_input.Read(new byte[1], 0, 1) != 0) throw new InvalidDataException("Archive content exceeded its declared size.");
                _verifiedEnd = true;
            }
        }
        catch (Exception error) { failed(error); throw; }
        finally { if (readCount != IntPtr.Zero) Marshal.WriteInt32(readCount, read); }
    }

    public void Seek(long offset, int origin, IntPtr position)
    {
        lock (_gate) SeekCore(offset, origin, position);
    }

    private void SeekCore(long offset, int origin, IntPtr position)
    {
        var next = checked((origin switch { 0 => 0, 1 => _position, 2 => length, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }) + offset);
        if (next < 0 || next > length) throw new IOException("Stream seek is outside the archive entry.");
        if (next < _readPosition) { _input?.Dispose(); _input = null; _readPosition = 0; _verifiedEnd = false; }
        _position = next;
        if (position != IntPtr.Zero) Marshal.WriteInt64(position, next);
    }

    public void CopyTo(IStream target, long count, IntPtr readCount, IntPtr writtenCount)
    {
        lock (_gate) CopyCore(target, count, readCount, writtenCount);
    }

    private void CopyCore(IStream target, long count, IntPtr readCount, IntPtr writtenCount)
    {
        var copied = 0L;
        var buffer = new byte[64 * 1024];
        var bytes = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            var remaining = Math.Min(count, length - _position);
            while (remaining > 0)
            {
                var wanted = (int)Math.Min(buffer.Length, remaining);
                Read(buffer, wanted, bytes);
                var received = Marshal.ReadInt32(bytes);
                target.Write(buffer, received, bytes);
                if (Marshal.ReadInt32(bytes) != received) throw new IOException("The destination did not accept the full stream block.");
                copied += received;
                remaining -= received;
            }
        }
        catch (Exception error) { failed(error); throw; }
        finally
        {
            Marshal.FreeCoTaskMem(bytes);
            if (readCount != IntPtr.Zero) Marshal.WriteInt64(readCount, copied);
            if (writtenCount != IntPtr.Zero) Marshal.WriteInt64(writtenCount, copied);
        }
    }

    public void Stat(out STATSTG stat, int flags) => stat = new STATSTG { type = 2, cbSize = length, grfMode = 0, pwcsName = (flags & 1) == 0 ? name : "" };
    public void Clone(out IStream stream)
    {
        lock (_gate)
        {
        var clone = new ArchiveContentStream(name, length, open, failed, register);
        clone._position = _position;
        register(clone);
        stream = clone;
        }
    }
    public void Commit(int flags) { }
    public void LockRegion(long offset, long count, int type) { }
    public void UnlockRegion(long offset, long count, int type) { }
    public void Revert() => throw new COMException("Read-only archive stream.", unchecked((int)0x80030005));
    public void SetSize(long size) => throw new COMException("Read-only archive stream.", unchecked((int)0x80030005));
    public void Write(byte[] buffer, int count, IntPtr written) => throw new COMException("Read-only archive stream.", unchecked((int)0x80030005));
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _input?.Dispose(); } }
}
