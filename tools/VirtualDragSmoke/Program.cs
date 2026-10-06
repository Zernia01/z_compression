using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Threading;
using ZCompression.App;
using ZCompression.Core.Archives;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exit = 1;
        _ = RunAsync().ContinueWith(task => dispatcher.BeginInvoke(new Action(() =>
        {
            if (task.IsCompletedSuccessfully) exit = 0;
            else Console.Error.WriteLine(task.Exception?.GetBaseException());
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        })), TaskScheduler.Default);
        Dispatcher.Run();
        return exit;
    }

    private static async Task RunAsync()
    {
        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        var root = Path.Combine(Path.GetTempPath(), "z-compression-virtual-drag-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var heartbeat = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += (_, _) => heartbeat++;
        VirtualArchiveTransfer? transfer = null;
        try
        {
            var uiThread = Environment.CurrentManagedThreadId;
            var original = RandomNumberGenerator.GetBytes(256 * 1024);
            var source = Path.Combine(root, "payload.bin");
            await File.WriteAllBytesAsync(source, original);
            var archive = Path.Combine(root, "payload.zip");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Zip));
            var entries = new ArchiveExportEntry[] { new("folder", "", true, 0, null), new("folder\\empty", "", true, 0, null), new("folder\\한글.bin", "payload.bin", false, original.Length, null) };
            var opens = 0;
            transfer = await VirtualArchiveTransfer.CreateAsync(entries, archive, null, (entry, token) =>
            {
                if (Environment.CurrentManagedThreadId == uiThread) throw new InvalidOperationException("Decompression ran on the UI thread.");
                Interlocked.Increment(ref opens);
                return new SlowReadStream(engine.OpenEntryReadStream(archive, entry.ArchivePath, cancellationToken: token));
            });
            var dataId = new Guid("0000010E-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(ref dataId, transfer.NativeDataPointer, out var marshaled));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var copied = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = new Thread(() => ReadOnTargetThread(marshaled, started, copied, () => Volatile.Read(ref opens))) { IsBackground = true };
            worker.SetApartmentState(ApartmentState.MTA);
            timer.Start();
            worker.Start();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            transfer.FinishDrag(1); // Simulate DoDragDrop returning while Explorer's worker reads.
            var received = await copied.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await transfer.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            if (!SHA256.HashData(original).SequenceEqual(SHA256.HashData(received))) throw new InvalidOperationException("Content was truncated or changed.");
            if (heartbeat < 5) throw new InvalidOperationException("UI message processing stopped during slow native stream reads.");
            if (opens != 1 || Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length != 2) throw new InvalidOperationException("Export used eager or temporary extraction.");
            Console.WriteLine($"PASS: native async QI, folder/Unicode descriptors, deferred stream reads, exact bytes, no extracted temporary files, UI heartbeats={heartbeat}.");
        }
        finally { timer.Stop(); transfer?.Cancel(); transfer?.FinishDrag(0); Directory.Delete(root, true); OleUninitialize(); }
    }

    private static void ReadOnTargetThread(IntPtr marshaled, TaskCompletionSource started, TaskCompletionSource<byte[]> copied, Func<int> opens)
    {
        var data = IntPtr.Zero;
        var async = IntPtr.Zero;
        var initialized = CoInitializeEx(IntPtr.Zero, 0);
        var result = unchecked((int)0x80004004);
        try
        {
            Marshal.ThrowExceptionForHR(initialized);
            var dataId = new Guid("0000010E-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(marshaled, ref dataId, out data));
            var asyncId = new Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4");
            Marshal.ThrowExceptionForHR(Method<QueryInterface>(data, 0)(data, ref asyncId, out async));
            Marshal.ThrowExceptionForHR(Method<GetAsyncMode>(async, 4)(async, out var enabled));
            if (!enabled) throw new InvalidOperationException("Async capability was lost during marshaling.");
            var descriptor = Format("FileGroupDescriptorW", -1, TYMED.TYMED_HGLOBAL);
            Marshal.ThrowExceptionForHR(Method<GetData>(data, 3)(data, ref descriptor, out var group));
            try
            {
                var pointer = VirtualArchiveDataObject.GlobalLock(group.unionmember);
                try
                {
                    if (Marshal.ReadInt32(pointer) != 3 || Marshal.PtrToStringUni(IntPtr.Add(pointer, 4 + 2 * 592 + 72)) != "folder\\한글.bin") throw new InvalidOperationException("File descriptors have the wrong layout.");
                }
                finally { VirtualArchiveDataObject.GlobalUnlock(group.unionmember); }
            }
            finally { VirtualArchiveDataObject.ReleaseStgMedium(ref group); }
            if (opens() != 0) throw new InvalidOperationException("Descriptor retrieval decompressed contents.");
            Marshal.ThrowExceptionForHR(Method<StartOperation>(async, 5)(async, IntPtr.Zero));
            started.SetResult();
            var contents = Format("FileContents", 2, TYMED.TYMED_ISTREAM);
            Marshal.ThrowExceptionForHR(Method<GetData>(data, 3)(data, ref contents, out var medium));
            try
            {
                if (opens() != 0) throw new InvalidOperationException("GetData eagerly read archive contents.");
                using var output = new MemoryStream();
                var buffer = new byte[64 * 1024];
                var count = Marshal.AllocCoTaskMem(4);
                try
                {
                    while (true)
                    {
                        Marshal.ThrowExceptionForHR(Method<Read>(medium.unionmember, 3)(medium.unionmember, buffer, buffer.Length, count));
                        var bytes = Marshal.ReadInt32(count);
                        if (bytes == 0) break;
                        output.Write(buffer, 0, bytes);
                    }
                }
                finally { Marshal.FreeCoTaskMem(count); }
                copied.SetResult(output.ToArray());
                result = 0;
            }
            finally { VirtualArchiveDataObject.ReleaseStgMedium(ref medium); }
        }
        catch (Exception error) { started.TrySetException(error); copied.TrySetException(error); }
        finally
        {
            if (async != IntPtr.Zero) { Method<EndOperation>(async, 7)(async, result, IntPtr.Zero, result == 0 ? 1u : 0u); Marshal.Release(async); }
            if (data != IntPtr.Zero) Marshal.Release(data);
            if (initialized >= 0) CoUninitialize();
        }
    }

    private static FORMATETC Format(string name, int index, TYMED medium) => new() { cfFormat = (short)VirtualArchiveDataObject.RegisterClipboardFormat(name), dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = medium };
    private static T Method<T>(IntPtr instance, int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterface(IntPtr self, ref Guid id, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetData(IntPtr self, ref FORMATETC format, out STGMEDIUM medium);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAsyncMode(IntPtr self, [MarshalAs(UnmanagedType.Bool)] out bool enabled);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StartOperation(IntPtr self, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EndOperation(IntPtr self, int result, IntPtr context, uint effects);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Read(IntPtr self, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int count, IntPtr read);
    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoMarshalInterThreadInterfaceInStream(ref Guid id, IntPtr data, out IntPtr stream);
    [DllImport("ole32.dll")] private static extern int CoGetInterfaceAndReleaseStream(IntPtr stream, ref Guid id, out IntPtr data);

    private sealed class SlowReadStream(Stream input) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { Thread.Sleep(150); return input.Read(buffer, offset, count); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
    }
}
