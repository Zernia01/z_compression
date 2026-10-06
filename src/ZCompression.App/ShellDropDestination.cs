using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ZCompression.App;

internal sealed record ShellDropLocation(int X, int Y, IntPtr View, IntPtr Root, bool Desktop);

internal static class ShellDropDestination
{
    internal static ShellDropLocation? Capture()
    {
        if (!GetCursorPos(out var point)) return null;
        var hit = WindowFromPoint(point);
        var root = GetAncestor(hit, 2);
        var view = hit;
        while (view != IntPtr.Zero && ClassName(view) != "SHELLDLL_DefView") view = GetParent(view);
        if (view == IntPtr.Zero) return null;
        var rootClass = ClassName(root);
        if (rootClass is not ("Progman" or "WorkerW" or "CabinetWClass" or "ExploreWClass")) return null;
        return new(point.X, point.Y, view, root, rootClass is "Progman" or "WorkerW");
    }

    internal static async Task<string?> ResolveAsync(ShellDropLocation location, CancellationToken token)
    {
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var initialized = OleInitialize(IntPtr.Zero);
            try { Marshal.ThrowExceptionForHR(initialized); result.TrySetResult(Resolve(location)); }
            catch (Exception error) { result.TrySetException(error); }
            finally { if (initialized >= 0) OleUninitialize(); }
        }) { IsBackground = true, Name = "Archive drop folder resolver" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await result.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    private static string? Resolve(ShellDropLocation location)
    {
        // A closed/navigated-away view must never be guessed as another destination.
        if (!IsWindow(location.View)) return null;
        var hit = HitItem(location);
        if (hit is null) return null;
        object? shell = null, windows = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("13709620-C279-11CE-A49E-444553540000"), throwOnError: true)!);
            if (location.Desktop)
            {
                object? folder = null;
                try
                {
                    folder = ((dynamic)shell!).NameSpace(0);
                    return ResolveFolder(folder, hit.Value, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                }
                finally { Release(folder); }
            }
            windows = ((dynamic)shell!).Windows();
            var count = (int)((dynamic)windows).Count;
            for (var i = 0; i < count; i++)
            {
                object? window = null, document = null, folder = null, self = null;
                try
                {
                    window = ((dynamic)windows).Item(i);
                    if (window is null || new IntPtr((long)((dynamic)window).HWND) != location.Root || !MatchesView(window, location.View)) continue;
                    document = ((dynamic)window).Document;
                    folder = ((dynamic)document).Folder;
                    self = ((dynamic)folder).Self;
                    return ResolveFolder(folder, hit.Value, (string)((dynamic)self).Path);
                }
                catch (COMException) { }
                finally { Release(self); Release(folder); Release(document); Release(window); }
            }
            return null;
        }
        finally { Release(windows); Release(shell); }
    }

    private static string? ResolveFolder(object folder, (bool Item, string? Name) hit, string background)
    {
        if (!hit.Item) return Directory.Exists(background) ? Path.GetFullPath(background) : null;
        if (string.IsNullOrEmpty(hit.Name)) return null;
        object? items = null;
        string? result = null;
        try
        {
            items = ((dynamic)folder).Items();
            var count = (int)((dynamic)items).Count;
            for (var i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)items).Item(i);
                    if (!string.Equals((string)((dynamic)item).Name, hit.Name, StringComparison.CurrentCultureIgnoreCase)) continue;
                    var path = (string)((dynamic)item).Path;
                    if (!Directory.Exists(path)) return null;
                    if (result is not null && !result.Equals(path, StringComparison.OrdinalIgnoreCase)) return null;
                    result = Path.GetFullPath(path);
                }
                finally { Release(item); }
            }
            return result;
        }
        finally { Release(items); }
    }

    private static (bool Item, string? Name)? HitItem(ShellDropLocation location)
    {
        IAccessibleHit? accessible = null;
        try
        {
            if (AccessibleObjectFromPoint(new Point(location.X, location.Y), out accessible, out var child) < 0 || accessible is null) return null;
            for (var depth = 0; depth < 6; depth++)
            {
                var role = accessible.GetRole(child);
                if (role is int value)
                {
                    if (value is 0x22 or 0x24) return (true, accessible.GetName(child));
                    if (value is 0x21 or 0x0A or 0x09 or 0x10) return (false, null);
                }
                var parent = accessible.GetParent();
                Release(accessible);
                accessible = parent as IAccessibleHit;
                if (accessible is null) { Release(parent); return null; }
                child = 0;
            }
            return null;
        }
        catch (COMException) { return null; }
        finally { Release(accessible); }
    }

    private static bool MatchesView(object window, IntPtr expected) => GetViewWindow(window) == expected;

    internal static int ProbeShellViews()
    {
        object? shell = null, windows = null;
        var found = 0;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("13709620-C279-11CE-A49E-444553540000"), throwOnError: true)!);
            windows = ((dynamic)shell!).Windows();
            for (var i = 0; i < (int)((dynamic)windows).Count; i++)
            {
                object? window = null;
                try
                {
                    window = ((dynamic)windows).Item(i);
                    if (window is null || ClassName(new IntPtr((long)((dynamic)window).HWND)) is not ("CabinetWClass" or "ExploreWClass")) continue;
                    if (GetViewWindow(window) == IntPtr.Zero) throw new InvalidOperationException("Explorer did not expose its active folder view.");
                    found++;
                }
                finally { Release(window); }
            }
            return found;
        }
        finally { Release(windows); Release(shell); }
    }

    private static IntPtr GetViewWindow(object window)
    {
        var unknown = Marshal.GetIUnknownForObject(window);
        var provider = IntPtr.Zero; var browser = IntPtr.Zero; var view = IntPtr.Zero;
        try
        {
            var providerId = new Guid("6D5140C1-7436-11CE-8034-00AA006009FA");
            if (Marshal.QueryInterface(unknown, in providerId, out provider) < 0) return IntPtr.Zero;
            var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
            var browserId = new Guid("000214E2-0000-0000-C000-000000000046");
            if (Method<QueryService>(provider, 3)(provider, ref service, ref browserId, out browser) < 0)
            {
                service = browserId;
                if (Method<QueryService>(provider, 3)(provider, ref service, ref browserId, out browser) < 0) return IntPtr.Zero;
            }
            if (Method<QueryView>(browser, 15)(browser, out view) < 0) return IntPtr.Zero;
            return Method<GetWindow>(view, 3)(view, out var handle) >= 0 ? handle : IntPtr.Zero;
        }
        finally { if (view != IntPtr.Zero) Marshal.Release(view); if (browser != IntPtr.Zero) Marshal.Release(browser); if (provider != IntPtr.Zero) Marshal.Release(provider); Marshal.Release(unknown); }
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private static string ClassName(IntPtr handle) { var name = new StringBuilder(256); GetClassName(handle, name, name.Capacity); return name.ToString(); }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Point(int X, int Y);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryService(IntPtr self, ref Guid service, ref Guid id, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryView(IntPtr self, out IntPtr view);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetWindow(IntPtr self, out IntPtr handle);
    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder name, int size);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromPoint(Point point, [MarshalAs(UnmanagedType.Interface)] out IAccessibleHit? accessible, [MarshalAs(UnmanagedType.Struct)] out object child);
}

// The first seven IAccessible methods, in native dual-interface vtable order.
[ComImport, Guid("618736E0-3C3D-11CF-810C-00AA00389B71"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IAccessibleHit
{
    [return: MarshalAs(UnmanagedType.IDispatch)] object GetParent();
    int GetChildCount();
    [return: MarshalAs(UnmanagedType.IDispatch)] object GetChild([MarshalAs(UnmanagedType.Struct)] object child);
    [return: MarshalAs(UnmanagedType.BStr)] string? GetName([MarshalAs(UnmanagedType.Struct)] object child);
    [return: MarshalAs(UnmanagedType.BStr)] string? GetValue([MarshalAs(UnmanagedType.Struct)] object child);
    [return: MarshalAs(UnmanagedType.BStr)] string? GetDescription([MarshalAs(UnmanagedType.Struct)] object child);
    [return: MarshalAs(UnmanagedType.Struct)] object GetRole([MarshalAs(UnmanagedType.Struct)] object child);
}
