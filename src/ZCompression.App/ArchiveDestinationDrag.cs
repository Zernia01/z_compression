using System.Runtime.InteropServices;
using ZCompression.Core.Archives;

namespace ZCompression.App;

internal static class ArchiveDestinationDrag
{
    internal static ShellDropLocation? Drag(IReadOnlyList<ArchiveExportEntry> entries)
    {
        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        try
        {
            using var data = new VirtualArchiveDataObject(entries, _ => throw new InvalidOperationException("Destination picking never exports content."), () => { });
            var source = new DestinationDropSource(ShellDropDestination.Capture);
            // Expose names for Explorer's drag feedback, but cancel the shell drop before
            // it can start copying. The app extracts directly to the captured folder.
            var result = DoDragDrop(data, source, 1, out _);
            Marshal.ThrowExceptionForHR(result);
            return source.Location;
        }
        finally { OleUninitialize(); }
    }

    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data, [MarshalAs(UnmanagedType.Interface)] IArchiveDropSource source, uint effects, out uint result);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class DestinationDropSource(Func<ShellDropLocation?> capture) : IArchiveDropSource
{
    internal ShellDropLocation? Location { get; private set; }
    public int QueryContinueDrag(bool escape, uint keys)
    {
        if (escape) return 0x00040101;
        if ((keys & 1) != 0) return 0;
        Location = capture();
        return 0x00040101; // DRAGDROP_S_CANCEL: never let Explorer invoke its Drop/copy UI.
    }
    public int GiveFeedback(uint effects) => 0x00040102;
}
