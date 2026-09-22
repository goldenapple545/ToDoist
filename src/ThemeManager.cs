using System;
using System.Windows;
using System.Windows.Media;
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
            Color border;

            if (isLight)
            {
                glassBase = Color.FromRgb(0xFB, 0xFB, 0xFC);
                text = Color.FromRgb(0x1B, 0x1B, 0x1F);
                subtle = Color.FromRgb(0x74, 0x74, 0x7C);
                hover = Color.FromArgb(0x14, 0x10, 0x10, 0x18);
                divider = Color.FromArgb(0x24, 0x00, 0x00, 0x00);
                input = Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF);
                border = Color.FromArgb(0x2E, 0x00, 0x00, 0x00);
            }
            else
            {
                glassBase = Color.FromRgb(0x17, 0x17, 0x1B);
                text = Color.FromRgb(0xF2, 0xF2, 0xF5);
                subtle = Color.FromRgb(0x9E, 0x9E, 0xA6);
                hover = Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF);
                divider = Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF);
                input = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
                border = Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF);
            }

            root.Resources["GlassBrush"] = Frozen(new SolidColorBrush(
                Color.FromArgb(alpha, glassBase.R, glassBase.G, glassBase.B)));
            root.Resources["CardBorderBrush"] = Frozen(new SolidColorBrush(border));
            root.Resources["TextBrush"] = Frozen(new SolidColorBrush(text));
            root.Resources["SubtleBrush"] = Frozen(new SolidColorBrush(subtle));
            root.Resources["HoverBrush"] = Frozen(new SolidColorBrush(hover));
            root.Resources["DividerBrush"] = Frozen(new SolidColorBrush(divider));
            root.Resources["InputBrush"] = Frozen(new SolidColorBrush(input));
            root.Resources["AccentBrush"] = Frozen(new SolidColorBrush(accent));
            root.Resources["OnAccentBrush"] = Frozen(new SolidColorBrush(ContrastText(accent)));
        }

        /// <summary>Читаемый цвет текста поверх указанного фона.</summary>
        public static Color ContrastText(Color background)
        {
            double luma = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
            return luma > 0.66 ? Color.FromRgb(0x1B, 0x1B, 0x1F) : Colors.White;
        }

        private static SolidColorBrush Frozen(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
