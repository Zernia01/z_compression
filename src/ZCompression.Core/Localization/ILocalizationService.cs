using System.Globalization;

namespace ZCompression.Core.Localization;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    IReadOnlyList<CultureInfo> SupportedCultures { get; }
    string this[string key] { get; }
    event EventHandler? LanguageChanged;
    void SetCulture(string cultureName);
}
