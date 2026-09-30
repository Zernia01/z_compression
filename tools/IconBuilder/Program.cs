using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: IconBuilder <source.png> <destination.ico>");
    return 2;
}

var sourcePath = Path.GetFullPath(args[0]);
var destinationPath = Path.GetFullPath(args[1]);
var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var source = new BitmapImage();
source.BeginInit();
source.CacheOption = BitmapCacheOption.OnLoad;
source.UriSource = new Uri(sourcePath);
source.EndInit();
source.Freeze();

var frames = sizes.Select(size => EncodePng(source, size)).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
using var writer = new BinaryWriter(stream);
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)frames.Length);
var offset = 6 + frames.Length * 16;
for (var index = 0; index < frames.Length; index++)
{
    var size = sizes[index];
    writer.Write((byte)(size == 256 ? 0 : size));
    writer.Write((byte)(size == 256 ? 0 : size));
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(frames[index].Length);
    writer.Write(offset);
    offset += frames[index].Length;
}
foreach (var frame in frames) writer.Write(frame);
return 0;

static byte[] EncodePng(BitmapSource source, int size)
{
    var scale = Math.Min((double)size / source.PixelWidth, (double)size / source.PixelHeight);
    var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
    transformed.Freeze();
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(transformed));
    using var memory = new MemoryStream();
    encoder.Save(memory);
    return memory.ToArray();
}
