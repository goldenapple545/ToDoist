using System;

namespace ToDoist
{
    /// <summary>
    /// Пиксельные операции для «стекла» без WPF и GDI: зерно, размытие, насыщенность.
    /// Работают с массивами BGRA (по 4 байта на пиксель) — так их можно проверять тестами.
    /// </summary>
    public static class GlassSupport
    {
        /// <summary>Размер плитки зерна: 64 × 64 — не видно повтор, дешёво рисовать.</summary>
        public const int NoiseTileSize = 64;

        /// <summary>Максимальная альфа зерна: выше 8 из 255 зерно становится грязью.</summary>
        public const byte NoiseMaxAlpha = 8;

        /// <summary>
        /// Детерминированная плитка зерна (монохромный шум с очень низкой альфой).
        /// Нужна, чтобы градиенты стекла не «полосили».
        /// </summary>
        public static byte[] CreateNoiseTile(int size, int seed)
        {
            if (size <= 0)
            {
                size = NoiseTileSize;
            }

            byte[] pixels = new byte[size * size * 4];
            Random random = new Random(seed);

            for (int i = 0; i < size * size; i++)
            {
                int value = 128 + random.Next(-16, 17);
                if (value < 0)
                {
                    value = 0;
                }

                if (value > 255)
                {
                    value = 255;
                }

                byte gray = (byte)value;
                int offset = i * 4;
                pixels[offset] = gray;
                pixels[offset + 1] = gray;
                pixels[offset + 2] = gray;
                pixels[offset + 3] = (byte)random.Next(0, NoiseMaxAlpha + 1);
            }

            return pixels;
        }

        /// <summary>Три прохода усреднения по квадрату дают почти гауссово размытие.</summary>
        public const int DefaultBlurPasses = 3;

        /// <summary>
        /// Box-blur (скользящее среднее) по каналам BGRA. Фиксированный радиус
        /// и несколько проходов приближают гауссово размытие и работают одинаково на любом железе.
        /// </summary>
        public static void BoxBlurBgra(byte[] pixels, int width, int height, int radius, int passes)
        {
            if (pixels == null || width <= 0 || height <= 0 || radius <= 0 || passes <= 0)
            {
                return;
            }

            int expected = width * height * 4;
            if (pixels.Length < expected)
            {
                return;
            }

            byte[] buffer = new byte[expected];

            for (int pass = 0; pass < passes; pass++)
            {
                BlurHorizontal(pixels, buffer, width, height, radius);
                BlurVertical(buffer, pixels, width, height, radius);
            }
        }

        /// <summary>Умножает насыщенность: 1.0 — без изменений, 1.6 — «сочное» стекло.</summary>
        public static void BoostSaturation(byte[] pixels, double saturation)
        {
            if (pixels == null || pixels.Length < 4)
            {
                return;
            }

            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                double blue = pixels[i];
                double green = pixels[i + 1];
                double red = pixels[i + 2];
                double luma = 0.114 * blue + 0.587 * green + 0.299 * red;

                pixels[i] = ClampByte(luma + (blue - luma) * saturation);
                pixels[i + 1] = ClampByte(luma + (green - luma) * saturation);
                pixels[i + 2] = ClampByte(luma + (red - luma) * saturation);
            }
        }

        /// <summary>Сдвигает яркость на delta (например, +8 — стекло смотрится светлее).</summary>
        public static void AdjustBrightness(byte[] pixels, int delta)
        {
            if (pixels == null || delta == 0)
            {
                return;
            }

            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                pixels[i] = ClampByte(pixels[i] + delta);
                pixels[i + 1] = ClampByte(pixels[i + 1] + delta);
                pixels[i + 2] = ClampByte(pixels[i + 2] + delta);
            }
        }

        private static void BlurHorizontal(byte[] source, byte[] target, int width, int height, int radius)
        {
            int stride = width * 4;
            int diameter = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;

                for (int channel = 0; channel < 4; channel++)
                {
                    long sum = 0;

                    for (int k = -radius; k <= radius; k++)
                    {
                        int x = k < 0 ? 0 : (k >= width ? width - 1 : k);
                        sum += source[row + x * 4 + channel];
                    }

                    for (int x = 0; x < width; x++)
                    {
                        target[row + x * 4 + channel] = (byte)(sum / diameter);

                        int remove = x - radius;
                        int add = x + radius + 1;

                        if (remove < 0)
                        {
                            remove = 0;
                        }

                        if (add >= width)
                        {
                            add = width - 1;
                        }

                        sum += source[row + add * 4 + channel] - source[row + remove * 4 + channel];
                    }
                }
            }
        }

        private static void BlurVertical(byte[] source, byte[] target, int width, int height, int radius)
        {
            int stride = width * 4;
            int diameter = radius * 2 + 1;

            for (int x = 0; x < width; x++)
            {
                int column = x * 4;

                for (int channel = 0; channel < 4; channel++)
                {
                    long sum = 0;

                    for (int k = -radius; k <= radius; k++)
                    {
                        int y = k < 0 ? 0 : (k >= height ? height - 1 : k);
                        sum += source[y * stride + column + channel];
                    }

                    for (int y = 0; y < height; y++)
                    {
                        target[y * stride + column + channel] = (byte)(sum / diameter);

                        int remove = y - radius;
                        int add = y + radius + 1;

                        if (remove < 0)
                        {
                            remove = 0;
                        }

                        if (add >= height)
                        {
                            add = height - 1;
                        }

                        sum += source[add * stride + column + channel] -
                               source[remove * stride + column + channel];
                    }
                }
            }
        }

        private static byte ClampByte(double value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return (byte)(value + 0.5);
        }
    }
}