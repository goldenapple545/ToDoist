using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ToDoist.Tools
{
    /// <summary>
    /// Рисует иконку приложения (скруглённый квадрат с галочкой) и сохраняет её в .ico.
    /// Запуск: bin\IconMaker.exe <путь к ico>
    /// </summary>
    internal static class IconMaker
    {
        private static readonly int[] Sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        private static int Main(string[] args)
        {
            string target = args != null && args.Length > 0 ? args[0] : "ToDoist.ico";

            try
            {
                List<byte[]> images = new List<byte[]>();
                foreach (int size in Sizes)
                {
                    using (Bitmap bitmap = Draw(size))
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        bitmap.Save(buffer, ImageFormat.Png);
                        images.Add(buffer.ToArray());
                    }
                }

                WriteIcon(target, images);
                Console.WriteLine("Иконка создана: " + Path.GetFullPath(target) +
                    " (" + images.Count + " размеров)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Не удалось создать иконку: " + ex);
                return 1;
            }
        }

        private static Bitmap Draw(int size)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);

                float inset = size * 0.045f;
                RectangleF bounds = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
                float radius = size * 0.24f;

                using (GraphicsPath path = RoundedRect(bounds, radius))
                {
                    using (LinearGradientBrush fill = new LinearGradientBrush(
                        new PointF(bounds.Left, bounds.Top),
                        new PointF(bounds.Right, bounds.Bottom),
                        Color.FromArgb(255, 96, 168, 232),
                        Color.FromArgb(255, 56, 106, 176)))
                    {
                        graphics.FillPath(fill, path);
                    }

                    // Лёгкий блик сверху — иконка выглядит объёмнее.
                    using (GraphicsPath inner = RoundedRect(
                        RectangleF.Inflate(bounds, -size * 0.02f, -size * 0.02f), radius * 0.9f))
                    using (LinearGradientBrush glow = new LinearGradientBrush(
                        new PointF(bounds.Left, bounds.Top),
                        new PointF(bounds.Left, bounds.Top + bounds.Height * 0.55f),
                        Color.FromArgb(70, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)))
                    {
                        graphics.SetClip(inner);
                        graphics.FillRectangle(glow, inner.GetBounds());
                        graphics.ResetClip();
                    }

                    using (Pen edge = new Pen(Color.FromArgb(45, 0, 0, 0), Math.Max(1f, size * 0.012f)))
                    {
                        graphics.DrawPath(edge, path);
                    }
                }

                using (Pen pen = new Pen(Color.White, Math.Max(1.4f, size * 0.115f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    PointF[] check = new PointF[]
                    {
                        new PointF(size * 0.27f, size * 0.52f),
                        new PointF(size * 0.43f, size * 0.69f),
                        new PointF(size * 0.74f, size * 0.33f)
                    };
                    graphics.DrawLines(pen, check);
                }
            }

            return bitmap;
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float diameter = radius * 2f;
            GraphicsPath path = new GraphicsPath();

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static void WriteIcon(string path, List<byte[]> images)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = File.Create(path))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)images.Count);

                int offset = 6 + images.Count * 16;
                for (int i = 0; i < images.Count; i++)
                {
                    int size = Sizes[i];
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }

                foreach (byte[] image in images)
                {
                    writer.Write(image);
                }
            }
        }
    }
}
