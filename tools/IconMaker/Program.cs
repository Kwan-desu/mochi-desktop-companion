using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var mochPath = Path.Combine(root, "assets", "mochi", "happy.png");
var azkiPath = Path.Combine(root, "assets", "azki", "normal.png");
var outDir = Path.Combine(root, "assets", "icon");
Directory.CreateDirectory(outDir);
using var moch = new Bitmap(mochPath);
using var azki = new Bitmap(azkiPath);
var frames = new List<byte[]>();
foreach (var size in new[] { 16, 24, 32, 48, 64, 96, 128, 256 }) {
    using var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(canvas)) {
        g.SmoothingMode = SmoothingMode.HighQuality; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var brush = new LinearGradientBrush(new Rectangle(0, 0, size, size), Color.FromArgb(255, 255, 213, 232), Color.FromArgb(255, 214, 202, 242), 45f); g.FillRectangle(brush, 0, 0, size, size);
        using var outline = new Pen(Color.FromArgb(255, 95, 59, 104), Math.Max(1, size / 28f)); g.DrawRectangle(outline, 1, 1, size - 3, size - 3);
        // Upper-body crops keep both faces readable at Windows shell icon sizes.
        g.DrawImage(moch, new Rectangle(-size / 18, size / 18, size * 17 / 24, size * 17 / 20), new Rectangle(160, 0, 704, 720), GraphicsUnit.Pixel);
        g.DrawImage(azki, new Rectangle(size * 7 / 24, size / 18, size * 17 / 24, size * 17 / 20), new Rectangle(115, 0, 720, 720), GraphicsUnit.Pixel);
        using var gloss = new LinearGradientBrush(new Rectangle(0, 0, size, Math.Max(1, size / 3)), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f); g.FillRectangle(gloss, 0, 0, size, Math.Max(1, size / 3));
    }
    using var ms = new MemoryStream(); canvas.Save(ms, ImageFormat.Png); frames.Add(ms.ToArray());
    if (size == 256) File.WriteAllBytes(Path.Combine(outDir, "MochiDuo-icon.png"), frames[^1]);
}
using var ico = new FileStream(Path.Combine(outDir, "MochiDuo.ico"), FileMode.Create, FileAccess.Write);
using var writer = new BinaryWriter(ico);
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)frames.Count);
var offset = 6 + 16 * frames.Count;
for (var i = 0; i < frames.Count; i++) { var size = new[] { 16, 24, 32, 48, 64, 96, 128, 256 }[i]; writer.Write((byte)(size >= 256 ? 0 : size)); writer.Write((byte)(size >= 256 ? 0 : size)); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length; }
foreach (var frame in frames) writer.Write(frame);
Console.WriteLine(Path.Combine(outDir, "MochiDuo.ico"));
