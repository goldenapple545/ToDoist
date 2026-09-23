using System;

namespace ToDoist.Tests
{
    /// <summary>
    /// Проверки теней текста и иконок: тень существует, чтобы отделять глиф
    /// от пёстрых обоев, а не чтобы спорить с ним. В светлой теме эффекта нет
    /// вовсе — текст сохраняет ClearType, читаемость держит тинт стекла.
    /// В тёмной теме тень смещена вниз-вправо: симметричная тень ложится прямо
    /// под букву и размывает сами штрихи.
    /// </summary>
    internal static class TextShadowTests
    {
        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("Проверка теней текста и иконок:");

            TestLightThemeHasNoShadow();
            TestOffsetInDarkTheme();
            TestDropInDarkTheme();
            TestIcons();
            TestReadability();
        }

        private static void TestLightThemeHasNoShadow()
        {
            ShadowSpec text = TextShadowSupport.Text(true);
            ShadowSpec icon = TextShadowSupport.Icon(true);

            Check("светлая тема: тень текста выключена — текст остаётся резким",
                !TextShadowSupport.IsVisible(text));
            Check("светлая тема: тень иконок тоже выключена",
                !TextShadowSupport.IsVisible(icon));
            Check("светлая тема: выключенная тень пустая — ни радиуса, ни плотности, ни смещения",
                text.BlurRadius == TextShadowSupport.NoBlur && text.Opacity == 0.0 &&
                text.ShadowDepth == TextShadowSupport.NoDepth &&
                icon.BlurRadius == TextShadowSupport.NoBlur && icon.Opacity == 0.0 &&
                icon.ShadowDepth == TextShadowSupport.NoDepth);
        }

        private static void TestOffsetInDarkTheme()
        {
            Check("тёмная тема: тень текста включена",
                TextShadowSupport.IsVisible(TextShadowSupport.Text(false)));
            Check("тёмная тема: тень смещена вниз-вправо, а не лежит под глифом",
                TextShadowSupport.IsOffset(TextShadowSupport.Text(false)));
            Check("тёмная тема: у иконок та же сторона — источник света один",
                TextShadowSupport.IsVisible(TextShadowSupport.Icon(false)) &&
                TextShadowSupport.IsOffset(TextShadowSupport.Icon(false)));
            Check("тень: видимая тень всегда смещена, а без смещения тень только выключенная",
                OffsetOrOff(TextShadowSupport.Text(true)) &&
                OffsetOrOff(TextShadowSupport.Icon(true)) &&
                OffsetOrOff(TextShadowSupport.Text(false)) &&
                OffsetOrOff(TextShadowSupport.Icon(false)));
            Check("тёмная тема: смещение заметное, но текст не «плывёт»",
                TextShadowSupport.Text(false).ShadowDepth >= TextShadowSupport.MinDepth &&
                TextShadowSupport.Text(false).ShadowDepth <= TextShadowSupport.MaxDepth &&
                TextShadowSupport.Icon(false).ShadowDepth <= TextShadowSupport.MaxDepth);
        }

        private static void TestDropInDarkTheme()
        {
            ShadowSpec dark = TextShadowSupport.Text(false);

            Check("тень: в тёмной теме текст получает чёрную тень",
                Luma(dark) < 0.05);
            Check("тень: цвет полностью непрозрачный, за прозрачность отвечает Opacity",
                dark.Alpha == 0xFF);
            Check("тень: прозрачность в разумных пределах",
                dark.Opacity > 0.0 && dark.Opacity <= TextShadowSupport.MaxOpacity);
            Check("тень: радиус маленький — штрихи не размываются",
                dark.BlurRadius >= TextShadowSupport.MinBlur &&
                dark.BlurRadius <= TextShadowSupport.MaxBlur);
        }

        private static void TestIcons()
        {
            ShadowSpec darkIcon = TextShadowSupport.Icon(false);

            Check("иконки: в тёмной теме тень есть",
                TextShadowSupport.IsVisible(darkIcon));
            Check("иконки: радиус не меньше текстового — штрих у глифов тоньше",
                darkIcon.BlurRadius >= TextShadowSupport.Text(false).BlurRadius);
            Check("иконки: тень не бледнее текстовой",
                darkIcon.Opacity >= TextShadowSupport.Text(false).Opacity);
        }

        private static void TestReadability()
        {
            Check("читаемость: чёрная тень контрастирует со светлым текстом тёмной темы",
                TextShadowSupport.ContrastsWithText(TextShadowSupport.Text(false), false));
            Check("читаемость: яркость текста и тени расходятся почти на всю шкалу",
                Contrast(TextShadowSupport.Text(false), false) > 0.8);
            Check("читаемость: у выключенной тени цвет тот же, что у текста — она ничего не подмешивает",
                SameColorAsText(TextShadowSupport.Text(true), true) &&
                SameColorAsText(TextShadowSupport.Icon(true), true));
        }

        /// <summary>Видимая тень обязана быть смещена; у выключенной смещения нет.</summary>
        private static bool OffsetOrOff(ShadowSpec spec)
        {
            if (TextShadowSupport.IsVisible(spec))
            {
                return TextShadowSupport.IsOffset(spec);
            }

            return spec.ShadowDepth <= TextShadowSupport.NoDepth;
        }

        private static bool SameColorAsText(ShadowSpec spec, bool isLight)
        {
            byte red;
            byte green;
            byte blue;
            TextShadowSupport.TextColor(isLight, out red, out green, out blue);

            return spec.Red == red && spec.Green == green && spec.Blue == blue;
        }

        private static double Luma(ShadowSpec spec)
        {
            return TextShadowSupport.Luma(spec.Red, spec.Green, spec.Blue);
        }

        private static double Contrast(ShadowSpec spec, bool isLight)
        {
            byte red;
            byte green;
            byte blue;
            TextShadowSupport.TextColor(isLight, out red, out green, out blue);

            return Math.Abs(TextShadowSupport.Luma(red, green, blue) -
                TextShadowSupport.Luma(spec.Red, spec.Green, spec.Blue));
        }

        private static void Check(string name, bool condition)
        {
            StorageTests.Check(name, condition);
        }
    }
}
