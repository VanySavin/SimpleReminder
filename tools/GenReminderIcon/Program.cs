using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Генерирует Assets/reminder_icon.ico (несколько размеров, PNG внутри ICO) и reminder_icon_32.png
// по визуалу исходного reminder_icon.svg (без парсера SVG).

var repoRoot = FindRepoRoot();
var assetsDir = Path.Combine(repoRoot, "src", "ReminderApp", "Assets");
Directory.CreateDirectory(assetsDir);

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var pngBySize = new Dictionary<int, byte[]>();

foreach (var s in sizes)
{
    using var bmp = RenderIcon(s);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    pngBySize[s] = ms.ToArray();
}

var icoPath = Path.Combine(assetsDir, "reminder_icon.ico");
await File.WriteAllBytesAsync(icoPath, BuildIcoFromPngs(pngBySize));

var png32Path = Path.Combine(assetsDir, "reminder_icon_32.png");
await File.WriteAllBytesAsync(png32Path, pngBySize[32]);

Console.WriteLine($"Written {icoPath}");
Console.WriteLine($"Written {png32Path}");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "publish-portable-win-x64.ps1")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static Bitmap RenderIcon(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    var scale = size / 1024f;
    float M(float v) => v * scale;

    // Скруглённый прямоугольник (rx ≈ 156 на 1024)
    var pad = M(64);
    var side = M(896);
    var rx = M(156);
    using (var path = RoundedRect(pad, pad, side, side, rx))
    using (var brush = new LinearGradientBrush(
               new RectangleF(0, 0, size, size),
               Color.FromArgb(0x7F, 0xA9, 0xA4),
               Color.FromArgb(0x98, 0xC4, 0xBE),
               45f))
    {
        g.FillPath(brush, path);
    }

    using (var path = RoundedRect(pad, pad, side, side, rx))
    using (var pen = new Pen(Color.White, Math.Max(1f, M(56))))
    {
        g.DrawPath(pen, path);
    }

    // Белое кольцо (окружность)
    var cx = M(512);
    var cy = M(512);
    var r = M(256);
    using var ringPen = new Pen(Color.White, Math.Max(1f, M(56)));
    g.DrawEllipse(ringPen, cx - r, cy - r, 2 * r, 2 * r);

    // Галочка
    using var checkPen = new Pen(Color.FromArgb(0x37, 0x41, 0x53), Math.Max(1f, M(58)));
    checkPen.StartCap = LineCap.Round;
    checkPen.EndCap = LineCap.Round;
    checkPen.LineJoin = LineJoin.Round;
    g.DrawLine(checkPen, M(391), M(523), M(475), M(607));
    g.DrawLine(checkPen, M(475), M(607), M(651), M(431));

    // Точка
    var dotR = Math.Max(1f, M(36));
    g.FillEllipse(new SolidBrush(Color.FromArgb(0x37, 0x41, 0x53)), M(690) - dotR, M(334) - dotR, 2 * dotR, 2 * dotR);

    return bmp;
}

static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
{
    var d = Math.Min(r * 2, Math.Min(w, h));
    var path = new GraphicsPath();
    path.AddArc(x, y, d, d, 180, 90);
    path.AddArc(x + w - d, y, d, d, 270, 90);
    path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
    path.AddArc(x, y + h - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

/// <summary>ICO с несколькими PNG-кадрами (формат Windows Vista+).</summary>
static byte[] BuildIcoFromPngs(Dictionary<int, byte[]> pngBySize)
{
    var ordered = pngBySize.Keys.OrderBy(k => k).ToList();
    var count = (ushort)ordered.Count;
    using var ms = new MemoryStream();
    // ICONDIR
    ms.WriteByte(0);
    ms.WriteByte(0);
    ms.WriteByte(1);
    ms.WriteByte(0);
    ms.WriteByte((byte)(count & 0xff));
    ms.WriteByte((byte)(count >> 8));

    var headerSize = 6 + count * 16;
    var offset = headerSize;
    var entries = new List<(int size, int len, int off)>();

    foreach (var s in ordered)
    {
        var png = pngBySize[s];
        entries.Add((s, png.Length, offset));
        offset += png.Length;
    }

    foreach (var (s, len, off) in entries)
    {
        var dim = s >= 256 ? (byte)0 : (byte)s;
        ms.WriteByte(dim);
        ms.WriteByte(dim);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteUInt16LE(ms, 1);
        WriteUInt16LE(ms, 32);
        WriteUInt32LE(ms, (uint)len);
        WriteUInt32LE(ms, (uint)off);
    }

    foreach (var s in ordered)
    {
        ms.Write(pngBySize[s], 0, pngBySize[s].Length);
    }

    return ms.ToArray();
}

static void WriteUInt16LE(Stream s, ushort v)
{
    s.WriteByte((byte)(v & 0xff));
    s.WriteByte((byte)(v >> 8));
}

static void WriteUInt32LE(Stream s, uint v)
{
    s.WriteByte((byte)(v & 0xff));
    s.WriteByte((byte)((v >> 8) & 0xff));
    s.WriteByte((byte)((v >> 16) & 0xff));
    s.WriteByte((byte)((v >> 24) & 0xff));
}
