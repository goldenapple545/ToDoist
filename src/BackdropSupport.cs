using System;
using System.Globalization;
using Microsoft.Win32;

namespace ToDoist
{
    /// <summary>Способ, которым рисуется фон за окном.</summary>
    public enum BackdropMode
    {
        /// <summary>Сплошное полупрозрачное стекло — поведение без размытия.</summary>
        Solid = 0,

        /// <summary>Размытие обоев средствами WPF: окно остаётся прозрачным.</summary>
        Blur = 1
    }

    /// <summary>Область картинки обоев, которая попадает под карточку (в пикселях картинки).</summary>
    public struct WallpaperRegion
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    /// <summary>Геометрия обоев и карточки для расчёта области выборки.</summary>
    public struct WallpaperGeometry
    {
        public double ImageWidth;
        public double ImageHeight;
        public int Style;
        public bool Tile;
        public double ScreenX;
        public double ScreenY;
        public double ScreenWidth;
        public double ScreenHeight;
        public double CardX;
        public double CardY;
        public double CardWidth;
        public double CardHeight;
    }

    /// <summary>
    /// Логика «стекла» без WPF: выбор режима размытия, плотность тинта,
    /// разбор параметров командной строки и расчёт области обоев под карточкой.
    /// </summary>
    public static class BackdropSupport
    {
        /// <summary>Windows 10 1803 — самая старая сборка, где имеет смысл своё размытие.</summary>
        public const int BlurMinBuild = 17134;

        public const double LightMinTint = 0.35;
        public const double LightMaxTint = 0.65;
        public const double DarkMinTint = 0.55;
        public const double DarkMaxTint = 0.80;

        /// <summary>Значение ключа --backdrop, разобранное до старта окна.</summary>
        public static string CommandLineValue;

        /// <summary>
        /// Размываем обои там, где это может выглядеть хорошо, и откатываемся
        /// к сплошному стеклу, если окно не сможет быть прозрачным
        /// (выключены эффекты прозрачности Windows или система слишком старая).
        /// </summary>
        public static BackdropMode Decide(int build, bool transparencyEnabled)
        {
            if (!transparencyEnabled || build < BlurMinBuild)
            {
                return BackdropMode.Solid;
            }

            return BackdropMode.Blur;
        }

        /// <summary>
        /// Плотность тинта по положению слайдера прозрачности.
        /// Стекло никогда не становится совсем прозрачным: ниже «вуали» (35 % в светлой
        /// теме, 55 % в тёмной) текст на пёстрых обоях было бы не прочитать.
        /// Сплошной режим работает как раньше.
        /// </summary>
        public static double TintAlpha(double opacity, bool isLight, BackdropMode mode)
        {
            double value = ClampOpacity(opacity);

            if (mode == BackdropMode.Solid)
            {
                return value;
            }

            double range = AppData.MaxOpacity - AppData.MinOpacity;
            double position = range <= 0.0 ? 1.0 : (value - AppData.MinOpacity) / range;
            double from = isLight ? LightMinTint : DarkMinTint;
            double to = isLight ? LightMaxTint : DarkMaxTint;

            return from + position * (to - from);
        }

        private static double ClampOpacity(double opacity)
        {
            if (double.IsNaN(opacity) || double.IsInfinity(opacity))
            {
                opacity = AppData.DefaultOpacity;
            }

            if (opacity < AppData.MinOpacity)
            {
                return AppData.MinOpacity;
            }

            if (opacity > AppData.MaxOpacity)
            {
                return AppData.MaxOpacity;
            }

            return opacity;
        }

        /// <summary>Сборка Windows из реестра (0, если прочитать не удалось).</summary>
        public static int ReadWindowsBuild()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        object raw = key.GetValue("CurrentBuildNumber");
                        int build;
                        if (raw != null && int.TryParse(
                                Convert.ToString(raw, CultureInfo.InvariantCulture),
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out build))
                        {
                            return build;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return 0;
        }

        /// <summary>Включены ли в Windows «эффекты прозрачности».</summary>
        public static bool ReadTransparencyEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object raw = key.GetValue("EnableTransparency");
                        if (raw is int)
                        {
                            return ((int)raw) != 0;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return true;
        }

        /// <summary>
        /// Значение ключа --backdrop из командной строки («blur», «solid»),
        /// либо null, если ключа нет. Поддерживаются формы «--backdrop=value» и «--backdrop value».
        /// </summary>
        public static string ParseBackdropArgument(string[] args)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                string value = arg.Trim().Trim('"');
                if (value.StartsWith("--backdrop=", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring("--backdrop=".Length).Trim().Trim('"');
                }

                if (string.Equals(value, "--backdrop", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                {
                    string next = args[i + 1] == null ? string.Empty : args[i + 1];
                    return next.Trim().Trim('"');
                }
            }

            return null;
        }

        /// <summary>
        /// Разбирает значение ключа в режим. false означает «нет принудительного режима,
        /// считай автоматически».
        /// </summary>
        public static bool TryParseBackdropArg(string text, out BackdropMode mode)
        {
            mode = BackdropMode.Solid;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string value = text.Trim().ToLowerInvariant();

            if (value == "auto")
            {
                return false;
            }

            if (value == "solid" || value == "off" || value == "none")
            {
                mode = BackdropMode.Solid;
                return true;
            }

            if (value == "blur" || value == "glass")
            {
                mode = BackdropMode.Blur;
                return true;
            }

            return false;
        }

        /// <summary>Подпись активного режима для тултипа и журнала.</summary>
        public static string Describe(BackdropMode mode)
        {
            if (mode == BackdropMode.Blur)
            {
                return "Стекло: размытие обоев";
            }

            return "Стекло: без размытия";
        }

        /// <summary>
        /// Какая часть картинки обоев видна под карточкой. Учитывает стиль обоев
        /// (по центру, растянуть, вписать, заполнить, замостить) и положение окна.
        /// Область обрезается по границам картинки, чтобы не тянуть пустые края.
        /// </summary>
        public static WallpaperRegion ComputeWallpaperRegion(WallpaperGeometry geometry)
        {
            WallpaperRegion region = new WallpaperRegion();

            if (geometry.ImageWidth <= 0 || geometry.ImageHeight <= 0 ||
                geometry.CardWidth <= 0 || geometry.CardHeight <= 0)
            {
                return region;
            }

            double screenWidth = geometry.ScreenWidth > 0 ? geometry.ScreenWidth : geometry.ImageWidth;
            double screenHeight = geometry.ScreenHeight > 0 ? geometry.ScreenHeight : geometry.ImageHeight;

            double scaleX = screenWidth / geometry.ImageWidth;
            double scaleY = screenHeight / geometry.ImageHeight;

            if (geometry.Style == 2)
            {
                // «Растянуть»: пропорции картинки не сохраняются.
            }
            else if (geometry.Style == 6)
            {
                double uniform = Math.Min(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else if (geometry.Tile || geometry.Style == 0 || geometry.Style == 20)
            {
                // «По центру» и «Замостить» показывают картинку без масштаба.
                scaleX = 1.0;
                scaleY = 1.0;
            }
            else
            {
                // «Заполнить» (10) и «Развернуть на все мониторы» (22).
                double cover = Math.Max(scaleX, scaleY);
                scaleX = cover;
                scaleY = cover;
            }

            double drawnWidth = geometry.ImageWidth * scaleX;
            double drawnHeight = geometry.ImageHeight * scaleY;
            double drawnX = geometry.ScreenX + (screenWidth - drawnWidth) / 2.0;
            double drawnY = geometry.ScreenY + (screenHeight - drawnHeight) / 2.0;

            double x = (geometry.CardX - drawnX) / scaleX;
            double y = (geometry.CardY - drawnY) / scaleY;
            double width = geometry.CardWidth / scaleX;
            double height = geometry.CardHeight / scaleY;

            region.Width = Math.Min(width, geometry.ImageWidth);
            region.Height = Math.Min(height, geometry.ImageHeight);
            region.X = Clamp(x, 0.0, geometry.ImageWidth - region.Width);
            region.Y = Clamp(y, 0.0, geometry.ImageHeight - region.Height);

            return region;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
