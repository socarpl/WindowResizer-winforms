using System.Drawing.Drawing2D;

namespace WindowResizer;

internal enum AppIcon { Clear, Refresh, Add, Edit, Delete, Apply }

internal static class IconFactory
{
    public static Bitmap Create(AppIcon icon, int size = 32)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 32f, size / 32f);
        using var pen = new Pen(Color.FromArgb(45, 55, 65), 2.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (icon)
        {
            case AppIcon.Add:
                graphics.DrawLine(pen, 16, 5, 16, 27);
                graphics.DrawLine(pen, 5, 16, 27, 16);
                break;
            case AppIcon.Edit:
                graphics.DrawLines(pen, new Point[] { new(5, 21), new(21, 5), new(27, 11), new(11, 27), new(5, 27), new(5, 21) });
                graphics.DrawLine(pen, 17, 9, 23, 15);
                break;
            case AppIcon.Delete:
                graphics.DrawLine(pen, 6, 8, 26, 8);
                graphics.DrawLine(pen, 13, 5, 19, 5);
                graphics.DrawLines(pen, new Point[] { new(8, 11), new(10, 27), new(22, 27), new(24, 11) });
                graphics.DrawLine(pen, 14, 13, 14, 23);
                graphics.DrawLine(pen, 18, 13, 18, 23);
                break;
            case AppIcon.Clear:
                graphics.DrawLine(pen, 23, 4, 14, 17);
                graphics.DrawLine(pen, 26, 6, 17, 19);
                graphics.FillPolygon(Brushes.SteelBlue, new Point[] { new(12, 16), new(20, 22), new(15, 28), new(4, 27), new(7, 21) });
                graphics.DrawLine(pen, 5, 29, 27, 29);
                break;
            case AppIcon.Refresh:
                graphics.DrawArc(pen, 4, 4, 24, 24, 205, 260);
                graphics.DrawLines(pen, new Point[] { new(25, 5), new(28, 13), new(20, 12) });
                break;
            case AppIcon.Apply:
                var points = new PointF[24];
                for (var i = 0; i < points.Length; i++)
                {
                    var angle = (float)(i * Math.PI / 12 - Math.PI / 2);
                    var radius = i % 2 == 0 ? 14f : 11f;
                    points[i] = new PointF(16 + radius * MathF.Cos(angle), 16 + radius * MathF.Sin(angle));
                }
                graphics.FillPolygon(Brushes.SteelBlue, points);
                graphics.FillEllipse(Brushes.White, 11, 11, 10, 10);
                graphics.DrawEllipse(pen, 11, 11, 10, 10);
                break;
        }

        return bitmap;
    }
}
