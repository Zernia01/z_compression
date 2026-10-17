using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class ZipPhoneCompatibilityTests
{
    [TestMethod]
    public async Task ZipDeclaresUtf8NamesForIndependentLegacyEncodingReaders()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-phone-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var names = new[] { "日本語の名前.txt", "简体中文文件.txt", "繁體中文檔案.txt", "한글 😀.txt" };
            foreach (var name in names) await File.WriteAllTextAsync(Path.Combine(root, name), "content " + name);
            var destination = Path.Combine(root, "phone.zip");
            await new SharpCompressArchiveEngine().CompressAsync(new CompressionRequest(names.Select(name => Path.Combine(root, name)).ToArray(), destination, ArchiveFormat.Zip));
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using (var file = File.OpenRead(destination))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read, false, Encoding.GetEncoding(932)))
                CollectionAssert.AreEqual(names, zip.Entries.Select(entry => entry.FullName).ToArray());
            var bytes = await File.ReadAllBytesAsync(destination);
            var local = 0; var central = 0;
            for (var offset = 0; offset + 10 < bytes.Length; offset++)
            {
                var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
                if (signature is not (0x04034b50 or 0x02014b50)) continue;
                var flagOffset = signature == 0x04034b50 ? 6 : 8;
                Assert.AreNotEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + flagOffset)) & 0x800, "ZIP UTF-8 flag is missing.");
                if (signature == 0x04034b50) local++; else central++;
            }
            Assert.AreEqual(names.Length, local); Assert.AreEqual(names.Length, central);
        }
        finally { Directory.Delete(root, true); }
    }
}
