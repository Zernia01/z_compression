using System.Text;
using SharpCompress.Common;
using SharpCompress.Readers;
using ZCompression.Core.Archives;

namespace ZCompression.Core.Encoding;

internal static class ZipNameEncodingDetector
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static ArchiveEncoding Create(string path, CancellationToken token)
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var sample = new MemoryStream();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var probe = new ArchiveEncoding
        {
            CustomDecoder = (bytes, offset, count, type) =>
            {
                token.ThrowIfCancellationRequested();
                var name = bytes.AsSpan(offset, count);
                if (type == EncodingType.Default && sample.Length + name.Length + 1 <= 65536 && !IsUtf8(name) && seen.Add(Convert.ToBase64String(name)))
                {
                    sample.Write(name);
                    sample.WriteByte((byte)'\n');
                }
                return System.Text.Encoding.UTF8.GetString(name);
            },
        };
        // Only ZIP headers are read; no file contents are decompressed to guess names.
        using (Stream input = SplitArchiveService.IsVolumePath(path)
            ? new SplitArchiveReadStream(path, token)
            : TransientFileAccess.Run(() => File.OpenRead(path), token))
        using (var zip = SharpCompress.Archives.Zip.ZipArchive.OpenArchive(input, new ReaderOptions { ArchiveEncoding = probe, LeaveStreamOpen = true }))
        {
            var count = 0;
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                _ = entry.Key;
                if (sample.Length >= 65536 || ++count >= 4096) break;
            }
        }
        var fallback = Detect(sample.ToArray());
        return new ArchiveEncoding
        {
            Default = fallback,
            CustomDecoder = (bytes, offset, count, type) =>
            {
                token.ThrowIfCancellationRequested();
                var name = bytes.AsSpan(offset, count);
                // A declared UTF-8 name is authoritative. Also accept valid unmarked
                // UTF-8 names in archives containing both UTF-8 and legacy entries.
                return type == EncodingType.UTF8 || IsUtf8(name)
                    ? System.Text.Encoding.UTF8.GetString(name)
                    : fallback.GetString(name);
            },
        };
    }

    private static System.Text.Encoding Detect(byte[] sample)
    {
        if (sample.Length == 0) return System.Text.Encoding.UTF8;
        var details = UtfUnknown.CharsetDetector.DetectFromBytes(sample).Details;
        var candidates = new Dictionary<int, double> { [932] = 0, [949] = 0, [936] = 0, [950] = 0, [1252] = 0 };
        foreach (var detail in details)
        {
            if (detail.Encoding is not { } encoding || encoding.CodePage is 65001 or 1200 or 1201 or 12000 or 12001) continue;
            var codePage = encoding.CodePage == 51949 ? 949 : encoding.CodePage;
            candidates[codePage] = Math.Max(candidates.GetValueOrDefault(codePage), detail.Confidence * 4);
        }
        System.Text.Encoding best = System.Text.Encoding.GetEncoding(1252);
        var bestScore = double.NegativeInfinity;
        foreach (var (codePage, confidence) in candidates)
        {
            var encoding = System.Text.Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            string decoded;
            try { decoded = encoding.GetString(sample); }
            catch (DecoderFallbackException) { continue; }
            // Very short filenames often receive a spurious Western/Thai result.
            // Supplement the statistical detector with the scripts and common
            // filename characters present in a strictly valid decoding.
            const string commonHan = "的一是不了在人有我他这這个個们們中来來上大为為和国國地到以说說时時要就出会會可也你对對生能而子那得于於着著下自之年过過发發后後作里裏用道行所然家种種事成方多经經么麼去法学學如都同现現当當没沒动動面起看定天分还還进進好小部其些主样樣理心本前开開但因只从從想实實日军軍者意无無力它与與长長把机機十民第公此已工使情明性知全三又关關点點正业業外将將两兩高间間由问問很最重并並物手应應战戰向头頭文体體政美相见見被利什二等产產或新己制身果加西斯月话話合回特代内內信表化老给給世位次度门門任常先海通教儿兒原东東声聲提立及比员員解水名真论論处處走义義各入几幾口认認条條平系气氣题題活尔爾更别別打女变變四神计計少至直程展五确確眼书書非则則听聽白却卻界达達光放强強即像难難且权權思王象完设設式色路记記转轉风風件字符資料文件説明語漢試験测试測試压縮圧缩档檔案夹夾图圖补補丁注册註冊安装裝游戏戲遊音乐樂画畫動画版存保存設置";
            var score = confidence * Math.Max(1, sample.Count(value => value >= 128) / 4d);
            foreach (var character in decoded)
            {
                if (character is >= '\uAC00' and <= '\uD7A3') score += 3;
                else if (character is >= '\u3040' and <= '\u30FF') score += 3;
                else if (character is >= '\u4E00' and <= '\u9FFF') score += commonHan.Contains(character) ? 2 : 0.1;
                else if (character is >= '\uFF61' and <= '\uFF9F') score -= 0.5;
                else if (character is >= '\u3130' and <= '\u318F') score -= 2;
                else if (character > 127 && char.IsControl(character)) score -= 8;
            }
            if (score <= bestScore) continue;
            bestScore = score;
            best = encoding;
        }
        return best;
    }

    private static bool IsUtf8(ReadOnlySpan<byte> bytes)
    {
        try { _ = StrictUtf8.GetCharCount(bytes); return true; }
        catch (DecoderFallbackException) { return false; }
    }
}
