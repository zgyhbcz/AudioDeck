using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AudioDeck.Audio;

namespace AudioDeck.UI
{
    internal enum DeviceIconKind
    {
        Speaker,
        Headphones,
        Display
    }

    internal static class IconPainter
    {
        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float max = Math.Min(rect.Width, rect.Height) / 2f;
            if (radius > max)
            {
                radius = max;
            }

            float d = radius * 2f;
            if (d <= 0f)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static DeviceIconKind KindFor(AudioDevice device)
        {
            if (device == null)
            {
                return DeviceIconKind.Speaker;
            }

            string name = (device.Name ?? string.Empty) + " " + (device.Description ?? string.Empty);
            string lower = name.ToLowerInvariant();

            if (device.FormFactor == 4 || device.FormFactor == 5 || device.FormFactor == 6 ||
                lower.Contains("耳机") || lower.Contains("耳麦") || lower.Contains("headphone") ||
                lower.Contains("headset") || lower.Contains("earbud") || lower.Contains("airpods") ||
                lower.Contains("buds"))
            {
                return DeviceIconKind.Headphones;
            }

            if (device.FormFactor == 9 || device.FormFactor == 10 ||
                lower.Contains("显示器") || lower.Contains("电视") || lower.Contains("monitor") ||
                lower.Contains("display") || lower.Contains("hdmi") || lower.Contains("tv"))
            {
                return DeviceIconKind.Display;
            }

            return DeviceIconKind.Speaker;
        }

        public static void DrawDeviceIcon(Graphics g, RectangleF rect, DeviceIconKind kind, Color color)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                if (kind == DeviceIconKind.Headphones)
                {
                    DrawHeadphones(g, rect, color);
                }
                else if (kind == DeviceIconKind.Display)
                {
                    DrawDisplay(g, rect, color);
                }
                else
                {
                    DrawSpeaker(g, rect, color, false, true);
                }
            }
            finally
            {
                g.SmoothingMode = old;
            }
        }

        public static void DrawSpeaker(Graphics g, RectangleF rect, Color color, bool muted, bool waves)
        {
            float w = rect.Width;
            float h = rect.Height;
            float x = rect.X;
            float y = rect.Y;
            float unit = Math.Min(w, h);

            PointF[] points = new PointF[6];
            points[0] = new PointF(x + 0.10f * w, y + 0.38f * h);
            points[1] = new PointF(x + 0.30f * w, y + 0.38f * h);
            points[2] = new PointF(x + 0.50f * w, y + 0.20f * h);
            points[3] = new PointF(x + 0.50f * w, y + 0.80f * h);
            points[4] = new PointF(x + 0.30f * w, y + 0.62f * h);
            points[5] = new PointF(x + 0.10f * w, y + 0.62f * h);

            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, points);
            }

            if (muted)
            {
                using (Pen pen = new Pen(color, Math.Max(1.4f, unit * 0.075f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, x + 0.63f * w, y + 0.36f * h, x + 0.86f * w, y + 0.64f * h);
                    g.DrawLine(pen, x + 0.86f * w, y + 0.36f * h, x + 0.63f * w, y + 0.64f * h);
                }

                return;
            }

            if (!waves)
            {
                return;
            }

            float cx = x + 0.51f * w;
            float cy = y + 0.50f * h;
            using (Pen pen = new Pen(color, Math.Max(1.2f, unit * 0.065f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float r1 = 0.22f * w;
                float r2 = 0.37f * w;
                g.DrawArc(pen, cx - r1, cy - r1, r1 * 2f, r1 * 2f, -52f, 104f);
                g.DrawArc(pen, cx - r2, cy - r2, r2 * 2f, r2 * 2f, -52f, 104f);
            }
        }

        public static void DrawHeadphones(Graphics g, RectangleF rect, Color color)
        {
            float w = rect.Width;
            float h = rect.Height;
            float x = rect.X;
            float y = rect.Y;
            float unit = Math.Min(w, h);
            float penWidth = Math.Max(1.8f, unit * 0.085f);

            using (Pen pen = new Pen(color, penWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                RectangleF band = new RectangleF(x + 0.17f * w, y + 0.12f * h, 0.66f * w, 0.66f * h);
                g.DrawArc(pen, band, 180f, 180f);
            }

            using (SolidBrush brush = new SolidBrush(color))
            {
                RectangleF left = new RectangleF(x + 0.10f * w, y + 0.47f * h, 0.19f * w, 0.37f * h);
                RectangleF right = new RectangleF(x + 0.71f * w, y + 0.47f * h, 0.19f * w, 0.37f * h);
                using (GraphicsPath p1 = RoundedRect(left, 0.09f * w))
                using (GraphicsPath p2 = RoundedRect(right, 0.09f * w))
                {
                    g.FillPath(brush, p1);
                    g.FillPath(brush, p2);
                }
            }
        }

        public static void DrawDisplay(Graphics g, RectangleF rect, Color color)
        {
            float w = rect.Width;
            float h = rect.Height;
            float x = rect.X;
            float y = rect.Y;
            float unit = Math.Min(w, h);
            float penWidth = Math.Max(1.6f, unit * 0.075f);

            using (Pen pen = new Pen(color, penWidth))
            {
                pen.LineJoin = LineJoin.Round;
                RectangleF screen = new RectangleF(x + 0.09f * w, y + 0.14f * h, 0.82f * w, 0.56f * h);
                using (GraphicsPath path = RoundedRect(screen, 0.07f * w))
                {
                    g.DrawPath(pen, path);
                }

                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, x + 0.50f * w, y + 0.70f * h, x + 0.50f * w, y + 0.83f * h);
                g.DrawLine(pen, x + 0.32f * w, y + 0.85f * h, x + 0.68f * w, y + 0.85f * h);
            }
        }

        public static void DrawClose(Graphics g, RectangleF rect, Color color)
        {
            using (Pen pen = new Pen(color, Math.Max(1.5f, rect.Width * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, rect.Left + rect.Width * 0.31f, rect.Top + rect.Height * 0.31f,
                    rect.Right - rect.Width * 0.31f, rect.Bottom - rect.Height * 0.31f);
                g.DrawLine(pen, rect.Right - rect.Width * 0.31f, rect.Top + rect.Height * 0.31f,
                    rect.Left + rect.Width * 0.31f, rect.Bottom - rect.Height * 0.31f);
            }
        }

        public static Icon CreateTrayIcon(int size)
        {
            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    RectangleF outer = new RectangleF(0.5f, 0.5f, size - 1f, size - 1f);
                    using (GraphicsPath path = RoundedRect(outer, size * 0.23f))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(15, 108, 189)))
                    {
                        g.FillPath(brush, path);
                    }

                    RectangleF glyph = new RectangleF(size * 0.17f, size * 0.19f, size * 0.66f, size * 0.62f);
                    DrawSpeaker(g, glyph, Color.White, false, true);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    Interop.DestroyIcon(handle);
                }
            }
        }

        public static void DrawLogo(Graphics g, RectangleF rect, Color accent, Color glyphColor)
        {
            using (GraphicsPath path = RoundedRect(rect, rect.Width * 0.28f))
            using (SolidBrush brush = new SolidBrush(accent))
            {
                g.FillPath(brush, path);
            }

            RectangleF glyph = new RectangleF(rect.X + rect.Width * 0.18f, rect.Y + rect.Height * 0.22f,
                rect.Width * 0.64f, rect.Height * 0.56f);
            DrawSpeaker(g, glyph, glyphColor, false, true);
        }
    }
}
