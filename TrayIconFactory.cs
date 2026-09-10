using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace HeadsetBat
{
    internal static class TrayIconFactory
    {
        private const string HeadphonesGlyph = "headphones";
        private const string HeadsetGlyph = "headset";
        private const string SpeakerGlyph = "volume-2";
        private const string HeadphoneOffGlyph = "headphone-off";
        private const int HeadphonesBottom = 14;
        private const int HeadsetBottom = 14;
        private static readonly Color Background = Color.FromArgb(74, 74, 74);
        private static readonly Color Glyph = Color.FromArgb(248, 241, 227);
        private static readonly Color ChargedGlyph = Color.FromArgb(0, 230, 118);
        private static readonly Color Badge = Color.FromArgb(0, 120, 212);
        private static readonly Color LowBattery = Color.FromArgb(255, 59, 48);

        public static Icon CreateSpeaker() => CreateGlyph(SpeakerGlyph);
        public static Icon CreateHeadphones(byte? batteryPercent, bool showPercent) => CreateGlyph(HeadphonesGlyph, batteryPercent, showPercent);
        public static Icon CreateHeadset(byte? batteryPercent, bool showPercent) => CreateGlyph(HeadsetGlyph, batteryPercent, showPercent);
        public static Icon CreateNoAudioOutput() => CreateIcon(graphics =>
        {
            graphics.Clear(Background);
            DrawGlyph(graphics, HeadphoneOffGlyph, LowBattery);
        });

        private static Icon CreateGlyph(string glyph, byte? batteryPercent = null, bool showPercent = false)
        {
            return CreateIcon(graphics =>
            {
                graphics.Clear(Background);
                if (!batteryPercent.HasValue || showPercent)
                {
                    DrawGlyph(graphics, glyph, Glyph);
                    if (batteryPercent.HasValue)
                        DrawPercentBadge(graphics, batteryPercent.Value);
                }
                else if (batteryPercent.Value <= 30)
                {
                    DrawGlyph(graphics, glyph, LowBattery);
                }
                else
                {
                    DrawGlyph(graphics, glyph, Glyph);
                    var state = graphics.Save();
                    try
                    {
                        var glyphBottom = glyph == HeadphonesGlyph ? HeadphonesBottom : HeadsetBottom;
                        var chargedTop = GetChargedTop(glyphBottom, batteryPercent.Value);
                        graphics.SetClip(new Rectangle(0, chargedTop, 16, glyphBottom - chargedTop + 1));
                        DrawGlyph(graphics, glyph, ChargedGlyph);
                    }
                    finally
                    {
                        graphics.Restore(state);
                    }
                }
            });
        }

        private static void DrawGlyph(Graphics graphics, string glyph, Color color)
        {
            var state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var pen = new Pen(color, 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
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

        private static PointF Point(float x, float y) => new PointF(0.8f + x * 0.6f, 0.8f + y * 0.6f);
        private static RectangleF Rect(float x, float y, float width, float height) => new RectangleF(0.8f + x * 0.6f, 0.8f + y * 0.6f, width * 0.6f, height * 0.6f);

        private static int GetChargedTop(int glyphBottom, byte percent)
        {
            var rows = glyphBottom + 1;
            var chargedRows = (int)Math.Ceiling(percent * rows / 100.0);
            return glyphBottom - chargedRows + 1;
        }

        private static void DrawPercentBadge(Graphics graphics, byte percent)
        {
            using (var brush = new SolidBrush(percent <= 30 ? LowBattery : Badge))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 5.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var bounds = new Rectangle(4, 8, 12, 8);
                graphics.FillRectangle(brush, bounds);
                graphics.DrawString(percent.ToString(), font, textBrush, bounds, format);
            }
        }

        private static Icon CreateIcon(Action<Graphics> draw)
        {
            const int scale = 4;
            using (var source = new Bitmap(16 * scale, 16 * scale))
            using (var sourceGraphics = Graphics.FromImage(source))
            using (var bitmap = new Bitmap(16, 16))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                sourceGraphics.ScaleTransform(scale, scale);
                draw(sourceGraphics);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 16, 16));
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
