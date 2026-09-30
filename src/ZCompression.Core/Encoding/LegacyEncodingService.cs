namespace ZCompression.Core.Encoding;

public sealed record EncodingChoice(string Id, string DisplayName, System.Text.Encoding? Encoding);

public static class LegacyEncodingService
{
    static LegacyEncodingService() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    public static IReadOnlyList<EncodingChoice> Choices { get; } =
    [
        new("auto", "Auto detect", null),
        new("utf-8", "UTF-8", System.Text.Encoding.UTF8),
        new("cp949", "한국어 CP949", System.Text.Encoding.GetEncoding(949)),
        new("euc-kr", "한국어 EUC-KR", System.Text.Encoding.GetEncoding(51949)),
        new("cp932", "日本語 Shift-JIS / CP932", System.Text.Encoding.GetEncoding(932)),
        new("gbk", "简体中文 GBK", System.Text.Encoding.GetEncoding(936)),
        new("gb18030", "简体中文 GB18030", System.Text.Encoding.GetEncoding(54936)),
        new("big5", "繁體中文 Big5", System.Text.Encoding.GetEncoding(950)),
    ];
}
