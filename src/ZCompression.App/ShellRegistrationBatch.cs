using Microsoft.Win32;
using System.Runtime.Versioning;

namespace ZCompression.App;

// A launch may check many shell values, but only actual changes invalidate Explorer's cache.
[SupportedOSPlatform("windows")]
internal sealed class ShellRegistrationBatch(Action notify)
{
    private bool _changed;

    internal void SetDefaultValue(string path, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        SetValue(key, "", value, RegistryValueKind.String);
    }

    internal void SetValue(RegistryKey key, string name, object value, RegistryValueKind kind)
    {
        var current = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        SetValue(current, current is null ? null : key.GetValueKind(name), value, kind,
            () => key.SetValue(name, value, kind));
    }

    internal void SetValue(object? current, RegistryValueKind? currentKind, object value, RegistryValueKind kind, Action write)
    {
        var equal = current is byte[] bytes && value is byte[] desired
            ? bytes.AsSpan().SequenceEqual(desired)
            : Equals(current, value);
        if (currentKind == kind && equal) return;
        write();
        _changed = true;
    }

    internal void NotifyIfChanged()
    {
        if (!_changed) return;
        notify();
        _changed = false;
    }
}
