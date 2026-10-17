namespace ZCompression.Core.Archives;

internal sealed class SplitArchiveReadStream : Stream
{
    private readonly List<(string Path, long Start, long Length)> _parts = [];
    private readonly CancellationToken _token;
    private FileStream? _current;
    private int _currentIndex = -1;
    private long _position;
    private bool _disposed;

    public SplitArchiveReadStream(string path, CancellationToken token)
    {
        _token = token;
        var basePath = Path.GetFullPath(SplitArchiveService.GetBasePath(path));
        var parent = Path.GetDirectoryName(basePath)!;
        var parts = Directory.EnumerateFiles(parent)
            .Where(candidate => SplitArchiveService.IsVolumePath(candidate) && SplitArchiveService.GetBasePath(candidate).Equals(basePath, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => (Path: candidate, Number: int.Parse(Path.GetExtension(candidate)[1..], System.Globalization.CultureInfo.InvariantCulture)))
            .OrderBy(part => part.Number).ToArray();
        if (parts.Length == 0) throw new FileNotFoundException("The first split volume is missing.", basePath + ".001");
        long offset = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Number != index + 1) throw new InvalidDataException($"Missing split archive volume: {basePath}.{index + 1:D3}");
            var length = new FileInfo(parts[index].Path).Length;
            if (length == 0) throw new InvalidDataException("An archive volume is empty.");
            _parts.Add((parts[index].Path, offset, length));
            offset = checked(offset + length);
        }
        Length = offset;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _token.ThrowIfCancellationRequested();
        if (buffer.Length == 0 || _position >= Length) return 0;
        var total = 0;
        while (buffer.Length > 0 && _position < Length)
        {
            _token.ThrowIfCancellationRequested();
            var index = _parts.FindIndex(part => _position >= part.Start && _position < part.Start + part.Length);
            var part = _parts[index];
            if (index != _currentIndex)
            {
                _current?.Dispose();
                _current = null;
                _currentIndex = -1;
                _current = TransientFileAccess.Run(() => File.OpenRead(part.Path), _token);
                if (_current.Length != part.Length) { _current.Dispose(); _current = null; throw new InvalidDataException("An archive volume changed while being read."); }
                _currentIndex = index;
            }
            _current!.Position = _position - part.Start;
            var read = _current.Read(buffer[..(int)Math.Min(buffer.Length, part.Start + part.Length - _position)]);
            if (read == 0) throw new EndOfStreamException("An archive volume is incomplete.");
            _position += read;
            total += read;
            buffer = buffer[read..];
        }
        return total;
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var position = checked((origin switch { SeekOrigin.Begin => 0, SeekOrigin.Current => _position, SeekOrigin.End => Length, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }) + offset);
        if (position < 0) throw new IOException("Cannot seek before the archive.");
        return _position = position;
    }
    protected override void Dispose(bool disposing) { if (disposing) _current?.Dispose(); _disposed = true; base.Dispose(disposing); }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
