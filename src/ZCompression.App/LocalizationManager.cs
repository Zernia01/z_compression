using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using ZCompression.Core.Localization;

namespace ZCompression.App;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private ILocalizationService? _service;
    public static LocalizationManager Instance { get; } = new();
    public string this[string key] => _service?[key] ?? key;

    public void Initialize(ILocalizationService service)
    {
        if (_service is not null) _service.LanguageChanged -= OnLanguageChanged;
        _service = service;
        _service.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged(this, EventArgs.Empty);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    public event PropertyChangedEventHandler? PropertyChanged;
}

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key) => Key = key;
    [ConstructorArgument("key")]
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = LocalizationManager.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
