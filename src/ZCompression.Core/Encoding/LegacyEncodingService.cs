namespace ZCompression.Core.Encoding;

public sealed record EncodingChoice(string Id, string DisplayName, System.Text.Encoding? Encoding);

public static class LegacyEncodingService
{
    public static IReadOnlyList<EncodingChoice> Choices { get; }

    static LegacyEncodingService()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Choices =
        [
            new("default", "UTF-8 (default)", null),
            new("cp949", "한국어 CP949", System.Text.Encoding.GetEncoding(949)),
            new("euc-kr", "한국어 EUC-KR", System.Text.Encoding.GetEncoding(51949)),
            new("cp932", "日本語 Shift-JIS / CP932", System.Text.Encoding.GetEncoding(932)),
            new("gbk", "简体中文 GBK", System.Text.Encoding.GetEncoding(936)),
            new("gb18030", "简体中文 GB18030", System.Text.Encoding.GetEncoding(54936)),
            new("big5", "繁體中文 Big5", System.Text.Encoding.GetEncoding(950)),
            new("cp437", "DOS CP437", System.Text.Encoding.GetEncoding(437)),
            new("cp1252", "Western European Windows-1252", System.Text.Encoding.GetEncoding(1252)),
        ];
    }
}
