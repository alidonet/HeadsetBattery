using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HeadsetBat
{
    internal enum TrayTheme
    {
        Auto,
        Light,
        Dark
    }
    internal static class TrayIconFactory
    {
        private const string HeadphonesGlyph = "headphones";
        private const string HeadsetGlyph = "headset";
        private const string SpeakerGlyph = "volume-2";
        private const string HeadphoneOffGlyph = "headphone-off";
        private const int HeadphonesBottom = 15;
        private const int HeadsetBottom = 15;
        private const float GlyphStrokeWidth = 1.4f;
        private static readonly IconColors DarkTrayColors = new IconColors(
            Color.FromArgb(248, 241, 227), Color.FromArgb(248, 241, 227), Color.FromArgb(90, 200, 250),
            Color.FromArgb(0, 230, 118), Color.FromArgb(255, 59, 48), Color.FromArgb(0, 120, 212), true);
        private static readonly IconColors LightTrayColors = new IconColors(
            Color.FromArgb(43, 43, 43), Color.FromArgb(96, 96, 96), Color.FromArgb(0, 91, 166),
            Color.FromArgb(0, 128, 67), Color.FromArgb(184, 34, 34), Color.FromArgb(0, 82, 153), false);

        public static TrayTheme Theme { get; set; }

        public static Icon CreateSpeaker()
        {
            var colors = GetColors();
            return CreateIcon(graphics =>
            {
                graphics.Clear(Color.Transparent);
                DrawGlyph(graphics, SpeakerGlyph, colors.Speaker);
            }, colors.StrengthenEdges);
        }

        public static Icon CreateHeadphones(byte? batteryPercent, bool showPercent, byte lowBatteryThreshold = 30, bool isCharging = false) => CreateGlyph(HeadphonesGlyph, batteryPercent, showPercent, lowBatteryThreshold, isCharging);
        public static Icon CreateHeadset(byte? batteryPercent, bool showPercent, byte lowBatteryThreshold = 30, bool isCharging = false) => CreateGlyph(HeadsetGlyph, batteryPercent, showPercent, lowBatteryThreshold, isCharging);

        public static Icon CreateNoAudioOutput()
        {
            var colors = GetColors();
            return CreateIcon(graphics =>
            {
                graphics.Clear(Color.Transparent);
                DrawGlyph(graphics, HeadphoneOffGlyph, colors.LowBattery);
            }, colors.StrengthenEdges);
        }

        private static Icon CreateGlyph(string glyph, byte? batteryPercent = null, bool showPercent = false, byte lowBatteryThreshold = 30, bool isCharging = false)
        {
            var colors = GetColors();
            return CreateIcon(graphics =>
            {
                graphics.Clear(Color.Transparent);
                if (!batteryPercent.HasValue || showPercent)
                {
                    DrawGlyph(graphics, glyph, showPercent && batteryPercent.HasValue ? colors.GlyphWithPercent : colors.Glyph);
                    if (batteryPercent.HasValue)
                        DrawPercentBadge(graphics, batteryPercent.Value, lowBatteryThreshold, isCharging, colors);
                }
                else if (!isCharging && batteryPercent.Value <= lowBatteryThreshold)
                {
                    DrawGlyph(graphics, glyph, colors.LowBattery);
                }
                else
                {
                    DrawGlyph(graphics, glyph, colors.Glyph);
                    var state = graphics.Save();
                    try
                    {
                        var glyphBottom = glyph == HeadphonesGlyph ? HeadphonesBottom : HeadsetBottom;
                        var chargedTop = GetChargedTop(glyphBottom, batteryPercent.Value);
                        graphics.SetClip(new Rectangle(0, chargedTop, 16, glyphBottom - chargedTop + 1));
                        DrawGlyph(graphics, glyph, colors.Charged);
                    }
                    finally
                    {
                        graphics.Restore(state);
                    }
                }
            }, colors.StrengthenEdges);
        }

        private static void DrawGlyph(Graphics graphics, string glyph, Color color)
        {
            var state = graphics.Save();
            try
            {
                // Rendering is supersampled; final downscaling provides the only antialiasing pass.
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var pen = new Pen(color, GlyphStrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (glyph == HeadsetGlyph)
                        DrawHeadsetGlyph(graphics, pen);
                    else if (glyph == HeadphonesGlyph)
                        DrawHeadphonesGlyph(graphics, pen);
                    else if (glyph == HeadphoneOffGlyph)
                        DrawHeadphoneOffGlyph(graphics, pen);
                    else
                        DrawSpeakerGlyph(graphics, pen);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawHeadsetGlyph(Graphics graphics, Pen pen)
        {
            using (var path = new GraphicsPath())
            {
                path.StartFigure();
                path.AddLine(Point(3, 11), Point(6, 11));
                path.AddArc(Rect(4, 11, 4, 4), 270, 90);
                path.AddLine(Point(8, 13), Point(8, 16));
                path.AddArc(Rect(4, 14, 4, 4), 0, 90);
                path.AddLine(Point(6, 18), Point(5, 18));
                path.AddArc(Rect(3, 14, 4, 4), 90, 90);
                path.AddLine(Point(3, 16), Point(3, 11));
                path.AddArc(Rect(3, 2, 18, 18), 180, 180);
                path.AddLine(Point(21, 11), Point(21, 16));
                path.AddArc(Rect(17, 14, 4, 4), 0, 90);
                path.AddLine(Point(19, 18), Point(18, 18));
                path.AddArc(Rect(16, 14, 4, 4), 90, 90);
                path.AddLine(Point(16, 16), Point(16, 13));
                path.AddArc(Rect(16, 11, 4, 4), 180, 90);
                path.AddLine(Point(18, 11), Point(21, 11));
                graphics.DrawPath(pen, path);

                path.Reset();
                path.StartFigure();
                path.AddLine(Point(21, 16), Point(21, 18));
                path.AddArc(Rect(13, 14, 8, 8), 0, 90);
                path.AddLine(Point(17, 22), Point(12, 22));
                graphics.DrawPath(pen, path);
            }
        }

        private static void DrawHeadphoneOffGlyph(Graphics graphics, Pen pen)
        {
            using (var path = new GraphicsPath())
            {
                path.AddLine(Point(21, 14), Point(19.657f, 14));
                graphics.DrawPath(pen, path);

                path.Reset();
                path.AddArc(Rect(3, 3, 18, 18), 251.4f, 108.6f);
                path.AddLine(Point(21, 12), Point(21, 15.343f));
                graphics.DrawPath(pen, path);

                path.Reset();
                path.AddLine(Point(2, 2), Point(22, 22));
                graphics.DrawPath(pen, path);

                path.Reset();
                path.AddLine(Point(20.414f, 20.414f), Point(19, 21));
                path.AddLine(Point(19, 21), Point(18, 21));
                path.AddArc(Rect(16, 17, 4, 4), 90, 90);
                path.AddLine(Point(16, 19), Point(16, 16));
                graphics.DrawPath(pen, path);

                path.Reset();
                path.AddLine(Point(3, 14), Point(6, 14));
                path.AddArc(Rect(4, 14, 4, 4), 270, 90);
                path.AddLine(Point(8, 16), Point(8, 19));
                path.AddArc(Rect(4, 17, 4, 4), 0, 90);
                path.AddLine(Point(6, 21), Point(5, 21));
                path.AddArc(Rect(3, 17, 4, 4), 90, 90);
                path.AddLine(Point(3, 19), Point(3, 12));
                path.AddArc(Rect(3, 3, 18, 18), 180, 45);
                graphics.DrawPath(pen, path);
            }
        }

        private static void DrawHeadphonesGlyph(Graphics graphics, Pen pen)
        {
            using (var path = new GraphicsPath())
            {
                path.StartFigure();
                path.AddLine(Point(3, 14), Point(6, 14));
                path.AddArc(Rect(4, 14, 4, 4), 270, 90);
                path.AddLine(Point(8, 16), Point(8, 19));
                path.AddArc(Rect(4, 17, 4, 4), 0, 90);
                path.AddLine(Point(6, 21), Point(5, 21));
                path.AddArc(Rect(3, 17, 4, 4), 90, 90);
                path.AddLine(Point(3, 19), Point(3, 12));
                path.AddArc(Rect(3, 3, 18, 18), 180, 180);
                path.AddLine(Point(21, 12), Point(21, 19));
                path.AddArc(Rect(17, 17, 4, 4), 0, 90);
                path.AddLine(Point(19, 21), Point(18, 21));
                path.AddArc(Rect(16, 17, 4, 4), 90, 90);
                path.AddLine(Point(16, 19), Point(16, 16));
                path.AddArc(Rect(16, 14, 4, 4), 180, 90);
                path.AddLine(Point(18, 14), Point(21, 14));
                graphics.DrawPath(pen, path);
            }
        }

        private static void DrawSpeakerGlyph(Graphics graphics, Pen pen)
        {
            using (var path = new GraphicsPath())
            {
                path.AddLines(new[]
                {
                    Point(11, 4.702f), Point(9.797f, 4.204f), Point(6.413f, 7.587f), Point(5.416f, 8),
                    Point(3, 8), Point(2, 9), Point(2, 15), Point(3, 16), Point(5.416f, 16),
                    Point(6.413f, 16.413f), Point(9.796f, 19.797f), Point(11, 19.298f), Point(11, 4.702f)
                });
                graphics.DrawPath(pen, path);
            }

            graphics.DrawArc(pen, Rect(7, 7, 10, 10), -37, 74);
            graphics.DrawArc(pen, Rect(4, 3, 18, 18), -45, 90);
        }

        private static PointF Point(float x, float y) => new PointF(x * 0.625f + 0.5f, y * 0.625f + 0.5f);
        private static RectangleF Rect(float x, float y, float width, float height) => new RectangleF(x * 0.625f + 0.5f, y * 0.625f + 0.5f, width * 0.625f, height * 0.625f);

        private static IconColors GetColors()
        {
            if (Theme == TrayTheme.Light)
                return LightTrayColors;
            if (Theme == TrayTheme.Dark)
                return DarkTrayColors;

            try
            {
                using (var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var systemUsesLightTheme = personalize?.GetValue("SystemUsesLightTheme") as int?;
                    return systemUsesLightTheme.GetValueOrDefault() != 0 ? LightTrayColors : DarkTrayColors;
                }
            }
            catch
            {
                return DarkTrayColors;
            }
        }

        private readonly struct IconColors
        {
            public IconColors(Color glyph, Color glyphWithPercent, Color speaker, Color charged, Color lowBattery, Color badge, bool strengthenEdges)
            {
                Glyph = glyph;
                GlyphWithPercent = glyphWithPercent;
                Speaker = speaker;
                Charged = charged;
                LowBattery = lowBattery;
                Badge = badge;
                StrengthenEdges = strengthenEdges;
            }

            public Color Glyph { get; }
            public Color GlyphWithPercent { get; }
            public Color Speaker { get; }
            public Color Charged { get; }
            public Color LowBattery { get; }
            public Color Badge { get; }
            public bool StrengthenEdges { get; }
        }

        private static int GetChargedTop(int glyphBottom, byte percent)
        {
            var rows = glyphBottom + 1;
            var chargedRows = (int)Math.Ceiling(percent * rows / 100.0);
            return glyphBottom - chargedRows + 1;
        }

        private static void DrawPercentBadge(Graphics graphics, byte percent, byte lowBatteryThreshold, bool isCharging, IconColors colors)
        {
            using (var brush = new SolidBrush(!isCharging && percent <= lowBatteryThreshold ? colors.LowBattery : colors.Badge))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 5.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var bounds = new Rectangle(4, 8, 12, 8);
                graphics.FillRectangle(brush, bounds);
                graphics.DrawString(percent.ToString(), font, textBrush, bounds, format);
            }
        }

        private static Icon CreateIcon(Action<Graphics> draw, bool strengthenEdges)
        {
            const int scale = 4;
            using (var source = new Bitmap(16 * scale, 16 * scale, PixelFormat.Format32bppArgb))
            using (var sourceGraphics = Graphics.FromImage(source))
            using (var bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                sourceGraphics.ScaleTransform(scale, scale);
                draw(sourceGraphics);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 16, 16));
                if (strengthenEdges)
                    StrengthenTransparentEdges(bitmap);
                var handle = bitmap.GetHicon();
                try
                {
                    using (var icon = Icon.FromHandle(handle))
                        return (Icon)icon.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static void StrengthenTransparentEdges(Bitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 0 && pixel.A < 255)
                {
                    var alpha = (int)Math.Round(255 * Math.Pow(pixel.A / 255.0, 0.75));
                    bitmap.SetPixel(x, y, Color.FromArgb(alpha, pixel.R, pixel.G, pixel.B));
                }
            }
        }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
