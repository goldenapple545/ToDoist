using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ToDoist
{
    /// <summary>
    /// Тема оформления: авто (как в Windows), светлая, тёмная.
    /// Кисти кладутся в ресурсы окна, XAML подтягивает их через DynamicResource.
    /// </summary>
    public static class ThemeManager
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string DwmKey = @"Software\Microsoft\Windows\DWM";
        /// <summary>Плитка зерна строится один раз на всё приложение.</summary>
        private static ImageBrush _noiseBrush;



        public static readonly Color FallbackAccent = Color.FromRgb(0x4F, 0x8C, 0xC9);

        /// <summary>Светлая ли тема приложений в Windows.</summary>
        public static bool IsSystemLight()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key != null)
                    {
                        object raw = key.GetValue("AppsUseLightTheme");
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

        public static bool ResolveIsLight(string themeMode)
        {
            if (themeMode == AppData.ThemeLight)
            {
                return true;
            }

            if (themeMode == AppData.ThemeDark)
            {
                return false;
            }

            return IsSystemLight();
        }

        /// <summary>Акцентный цвет Windows (в реестре лежит в формате ABGR).</summary>
        public static Color ReadAccentColor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(DwmKey))
                {
                    if (key != null)
                    {
                        object raw = key.GetValue("AccentColor");
                        if (raw is int)
                        {
                            int value = (int)raw;
                            byte red = (byte)(value & 0xFF);
                            byte green = (byte)((value >> 8) & 0xFF);
                            byte blue = (byte)((value >> 16) & 0xFF);
                            return Color.FromRgb(red, green, blue);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return FallbackAccent;
        }

        /// <summary>auto -> light -> dark -> auto</summary>
        public static string NextThemeMode(string themeMode)
        {
            if (themeMode == AppData.ThemeAuto)
            {
                return AppData.ThemeLight;
            }

            if (themeMode == AppData.ThemeLight)
            {
                return AppData.ThemeDark;
            }

            return AppData.ThemeAuto;
        }

        public static string DescribeTheme(string themeMode)
        {
            if (themeMode == AppData.ThemeLight)
            {
                return "Тема: светлая";
            }

            if (themeMode == AppData.ThemeDark)
            {
                return "Тема: тёмная";
            }

            return string.Format("Тема: как в Windows ({0})",
                IsSystemLight() ? "светлая" : "тёмная");
        }

        /// <summary>
        /// Перекрашивает окно. opacity — прозрачность «стекла» (0.45 .. 1.0):
        /// меняется только альфа фона, текст остаётся полностью непрозрачным.
        /// </summary>
        public static void Apply(FrameworkElement root, bool isLight, double opacity, Color accent)
        {
            if (root == null)
            {
                return;
            }

            if (opacity < AppData.MinOpacity)
            {
                opacity = AppData.MinOpacity;
            }

            if (opacity > AppData.MaxOpacity)
            {
                opacity = AppData.MaxOpacity;
            }

            byte alpha = (byte)Math.Round(opacity * 255.0);

            Color glassBase;
            Color text;
            Color subtle;
            Color hover;
            Color divider;
            Color input;
            byte textRed;
            byte textGreen;
            byte textBlue;

            // Цвет текста берём из TextShadowSupport: там же подобрана пара «текст — тень».
            TextShadowSupport.TextColor(isLight, out textRed, out textGreen, out textBlue);
            text = Color.FromRgb(textRed, textGreen, textBlue);

            if (isLight)
            {
                glassBase = Color.FromRgb(0xFB, 0xFB, 0xFC);
                subtle = Color.FromRgb(0x74, 0x74, 0x7C);
                hover = Color.FromArgb(0x14, 0x10, 0x10, 0x18);
                divider = Color.FromArgb(0x24, 0x00, 0x00, 0x00);
                input = Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF);
            }
            else
            {
                glassBase = Color.FromRgb(0x17, 0x17, 0x1B);
                subtle = Color.FromRgb(0x9E, 0x9E, 0xA6);
                hover = Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF);
                divider = Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF);
                input = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
            }

            root.Resources["GlassBrush"] = Frozen(new SolidColorBrush(
                Color.FromArgb(alpha, glassBase.R, glassBase.G, glassBase.B)));
            root.Resources["TextBrush"] = Frozen(new SolidColorBrush(text));
            root.Resources["SubtleBrush"] = Frozen(new SolidColorBrush(subtle));
            root.Resources["HoverBrush"] = Frozen(new SolidColorBrush(hover));
            root.Resources["DividerBrush"] = Frozen(new SolidColorBrush(divider));
            root.Resources["InputBrush"] = Frozen(new SolidColorBrush(input));
            root.Resources["AccentBrush"] = Frozen(new SolidColorBrush(accent));
            root.Resources["OnAccentBrush"] = Frozen(new SolidColorBrush(ContrastText(accent)));

            // Выбранная строка списка (её удалит Delete) — лёгкая заливка акцентом.
            root.Resources["RowSelectedBrush"] = Frozen(new SolidColorBrush(
                Color.FromArgb(0x2E, accent.R, accent.G, accent.B)));

            // Тени: в светлой теме их нет вовсе (текст держит тинт и сохраняет ClearType),
            // в тёмной — чёрная тень, смещённая вниз-вправо.
            ApplyShadow(root, "TextShadow", TextShadowSupport.Text(isLight));
            ApplyShadow(root, "IconShadow", TextShadowSupport.Icon(isLight));
        }

        /// <summary>
        /// Кладёт эффект в ресурсы окна. Если тень выключена, ключ убирается:
        /// DynamicResource вернёт Effect к значению по умолчанию (null), и WPF
        /// снова включит ClearType — с любым Effect текст рисуется без него.
        /// </summary>
        private static void ApplyShadow(FrameworkElement root, string key, ShadowSpec spec)
        {
            if (!TextShadowSupport.IsVisible(spec))
            {
                root.Resources.Remove(key);
                return;
            }

            root.Resources[key] = Shadow(spec);
        }

        /// <summary>
        /// Кисти «жидкого стекла»: тинт (его плотность задаёт слайдер), верхний блик,
        /// кромка с преломлением и очень мелкое зерно. Слои лежат в XAML.
        /// </summary>
        public static void ApplyGlass(FrameworkElement host, bool isLight, double opacity, BackdropMode mode)
        {
            if (host == null)
            {
                return;
            }

            byte alpha = (byte)Math.Round(
                BackdropSupport.TintAlpha(opacity, isLight, mode) * 255.0);

            Color top;
            Color bottom;

            if (isLight)
            {
                top = Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF);
                bottom = Color.FromArgb(alpha, 0xED, 0xF1, 0xF7);
            }
            else
            {
                top = Color.FromArgb(alpha, 0x1B, 0x1B, 0x21);
                bottom = Color.FromArgb(alpha, 0x0C, 0x0C, 0x10);
            }

            host.Resources["GlassBrush"] = TintGradient(top, bottom);

            Color sheen = isLight
                ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
            host.Resources["GlassSheenBrush"] = VerticalGradient(
                sheen, Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.55);

            Color edgeFrom = isLight
                ? Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF);
            Color edgeTo = isLight
                ? Color.FromArgb(0x3D, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
            host.Resources["GlassEdgeBrush"] = DiagonalGradient(edgeFrom, edgeTo);

            host.Resources["GlassNoiseBrush"] = NoiseBrush();
        }

        /// <summary>Тинт «молочного» стекла: сверху светлее, у нижней кромки — преломление.</summary>
        private static LinearGradientBrush TintGradient(Color top, Color bottom)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(top, 0.0));
            brush.GradientStops.Add(new GradientStop(bottom, 0.62));
            brush.GradientStops.Add(new GradientStop(Lift(bottom), 1.0));
            brush.Freeze();
            return brush;
        }

        private static LinearGradientBrush VerticalGradient(Color from, Color to, double endOffset)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(from, 0.0));
            brush.GradientStops.Add(new GradientStop(to, endOffset));
            brush.Freeze();
            return brush;
        }

        private static LinearGradientBrush DiagonalGradient(Color from, Color to)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(from, 0.0));
            brush.GradientStops.Add(new GradientStop(to, 1.0));
            brush.Freeze();
            return brush;
        }

        private static Color Lift(Color color)
        {
            int red = color.R + 7;
            int green = color.G + 7;
            int blue = color.B + 8;
            return Color.FromArgb(color.A,
                (byte)(red > 255 ? 255 : red),
                (byte)(green > 255 ? 255 : green),
                (byte)(blue > 255 ? 255 : blue));
        }

        /// <summary>Зерно строится один раз: мелкий шум убирает полосы на градиентах.</summary>
        private static ImageBrush NoiseBrush()
        {
            if (_noiseBrush != null)
            {
                return _noiseBrush;
            }

            int size = GlassSupport.NoiseTileSize;
            byte[] pixels = GlassSupport.CreateNoiseTile(size, 2026);
            BitmapSource tile = BitmapSource.Create(size, size, 96.0, 96.0,
                PixelFormats.Bgra32, null, pixels, size * 4);
            tile.Freeze();

            ImageBrush brush = new ImageBrush(tile);
            brush.Stretch = Stretch.Fill;
            brush.TileMode = TileMode.Tile;
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Viewport = new Rect(0, 0, size, size);
            brush.ViewboxUnits = BrushMappingMode.Absolute;
            brush.Viewbox = new Rect(0, 0, size, size);
            brush.Freeze();

            _noiseBrush = brush;
            return brush;
        }

        /// <summary>Читаемый цвет текста поверх указанного фона.</summary>
        public static Color ContrastText(Color background)
        {
            double luma = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
            return luma > 0.66 ? Color.FromRgb(0x1B, 0x1B, 0x1F) : Colors.White;
        }

        /// <summary>
        /// Эффект из спецификации. Фрозенный экземпляр переиспользуется всеми элементами:
        /// иначе WPF держал бы отдельную промежуточную поверхность на каждую копию.
        /// </summary>
        private static DropShadowEffect Shadow(ShadowSpec spec)
        {
            DropShadowEffect effect = new DropShadowEffect();
            effect.Color = Color.FromArgb(spec.Alpha, spec.Red, spec.Green, spec.Blue);
            effect.BlurRadius = spec.BlurRadius;
            effect.ShadowDepth = spec.ShadowDepth;
            effect.Direction = spec.Direction;
            effect.Opacity = spec.Opacity;
            effect.Freeze();
            return effect;
        }

        private static SolidColorBrush Frozen(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
