using System.Globalization;
using System.Text.Json;

namespace ZCompression.Core.Localization;

public sealed class JsonLocalizationService : ILocalizationService
{
    private static readonly string[] Names = ["ko-KR", "en-US", "ja-JP", "zh-CN", "zh-TW", "ko-Hanja"];
    private readonly string _directory;
    private Dictionary<string, string> _strings = [];
    private Dictionary<string, string> _fallback = [];

    public JsonLocalizationService(string directory, string cultureName = "auto")
    {
        _directory = directory;
        SupportedCultures = Names.Select(name => name == "ko-Hanja" ? CultureInfo.GetCultureInfo("ko-KR") : CultureInfo.GetCultureInfo(name)).ToArray();
        _fallback = Load("en-US");
        SetCulture(cultureName);
    }

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }
    public string this[string key] => _strings.GetValueOrDefault(key, _fallback.GetValueOrDefault(key, key));
    public event EventHandler? LanguageChanged;

    public void SetCulture(string cultureName)
    {
        var selected = cultureName == "auto" ? MatchSystemCulture() : Names.FirstOrDefault(x => string.Equals(x, cultureName, StringComparison.OrdinalIgnoreCase)) ?? "en-US";
        CurrentCulture = selected == "ko-Hanja" ? CultureInfo.GetCultureInfo("ko-KR") : CultureInfo.GetCultureInfo(selected);
        _strings = Load(selected);
        CultureInfo.CurrentUICulture = CurrentCulture;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private string MatchSystemCulture()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        if (Names.Contains(name, StringComparer.OrdinalIgnoreCase)) return name;
        return name.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "ko-KR"
            : name.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja-JP"
            : name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase) || name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ? "zh-TW"
            : name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
    }

    private Dictionary<string, string> Load(string name)
    {
        var path = Path.Combine(_directory, name + ".json");
        if (!File.Exists(path)) return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
    }
}
