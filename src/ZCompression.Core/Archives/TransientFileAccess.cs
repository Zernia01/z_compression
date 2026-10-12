namespace ZCompression.Core.Archives;

internal static class TransientFileAccess
{
    // Retry only Windows sharing/lock violations, never permissions, full disks,
    // invalid paths, or a partially completed read/write operation.
    internal static T Run<T>(Func<T> action, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return action(); }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33 && attempt < 20)
            {
                if (token.WaitHandle.WaitOne(100)) token.ThrowIfCancellationRequested();
            }
        }
    }
}
