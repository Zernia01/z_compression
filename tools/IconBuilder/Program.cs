using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--trim-transparent"))
{
    Console.Error.WriteLine("Usage: IconBuilder <source.png> <destination.ico> [--trim-transparent]");
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

BitmapSource artwork = args.Length == 3 ? TrimTransparent(source) : source;
var frames = sizes.Select(size => EncodePng(artwork, size)).ToArray();
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
    var width = source.PixelWidth * scale;
    var height = source.PixelHeight * scale;
    var visual = new DrawingVisual();
    RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
    using (var drawing = visual.RenderOpen())
        drawing.DrawImage(source, new Rect((size - width) / 2, (size - height) / 2, width, height));
    var transformed = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    transformed.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(transformed));
    using var memory = new MemoryStream();
    encoder.Save(memory);
    return memory.ToArray();
}

static BitmapSource TrimTransparent(BitmapSource source)
{
    var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
    var stride = bitmap.PixelWidth * 4;
    var pixels = new byte[stride * bitmap.PixelHeight];
    bitmap.CopyPixels(pixels, stride, 0);
    var left = bitmap.PixelWidth;
    var top = bitmap.PixelHeight;
    var right = -1;
    var bottom = -1;
    for (var y = 0; y < bitmap.PixelHeight; y++)
        for (var x = 0; x < bitmap.PixelWidth; x++)
            if (pixels[y * stride + x * 4 + 3] != 0)
            {
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
    return right < left ? source : new CroppedBitmap(bitmap, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
}
