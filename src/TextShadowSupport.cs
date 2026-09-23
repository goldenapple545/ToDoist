namespace ToDoist
{
    /// <summary>
    /// Параметры тени, которую получают текст и иконки поверх стекла.
    /// Без WPF: ThemeManager превращает спецификацию в DropShadowEffect,
    /// поэтому значения можно проверять тестами.
    /// </summary>
    public struct ShadowSpec
    {
        public byte Alpha;
        public byte Red;
        public byte Green;
        public byte Blue;
        public double BlurRadius;
        public double ShadowDepth;
        public double Direction;
        public double Opacity;
    }

    /// <summary>
    /// Тени — не украшение, а читаемость: обои просвечивают сквозь стекло,
    /// и на пёстрых местах мелкий текст с тонкими глифами сливается с фоном.
    /// В светлой теме тень не нужна: текст почти чёрный, а тинт стекла всегда
    /// светлый (минимум 35 % белого), поэтому буква отделена от фона сама,
    /// а любой Effect только отнял бы у текста ClearType. В тёмной теме светлый
    /// текст на тёмном стекле без тени сливается с пёстрыми обоями, поэтому там
    /// остаётся мягкая чёрная тень. Она смещена вниз-вправо: тень без смещения
    /// ложится ровно под глиф и размывает сами штрихи.
    /// </summary>
    public static class TextShadowSupport
    {
        /// <summary>
        /// «Вниз-вправо» в системе координат WPF (угол отсчитывается от востока):
        /// 315 — обычный источник света сверху-слева, 270 — строго вниз, под самый глиф.
        /// </summary>
        public const double DownRightDirection = 315.0;

        /// <summary>Разумные границы: меньше — тень снова прилипает к глифу, больше — текст «плывёт».</summary>
        public const double MinBlur = 1.0;
        public const double MaxBlur = 6.0;
        public const double MinDepth = 1.0;
        public const double MaxDepth = 3.0;
        public const double MaxOpacity = 0.9;

        /// <summary>Выключенная тень: ни радиуса, ни смещения, ни плотности.</summary>
        public const double NoBlur = 0.0;
        public const double NoDepth = 0.0;

        /// <summary>Тёмная тема: чёрная тень под светлыми буквами — фон под ними тёмный, смещение заметное.</summary>
        public const double TextDropBlur = 2.5;
        public const double TextDropDepth = 2.0;
        public const double TextDropOpacity = 0.50;

        /// <summary>Тёмная тема, иконки: штрих тоньше, поэтому тень чуть шире и плотнее текстовой.</summary>
        public const double IconDropBlur = 3.0;
        public const double IconDropDepth = 2.0;
        public const double IconDropOpacity = 0.55;

        /// <summary>Цвет текста темы — тот же, что ThemeManager кладёт в TextBrush.</summary>
        public static void TextColor(bool isLight, out byte red, out byte green, out byte blue)
        {
            if (isLight)
            {
                red = 0x1B;
                green = 0x1B;
                blue = 0x1F;
                return;
            }

            red = 0xF2;
            green = 0xF2;
            blue = 0xF5;
        }

        /// <summary>Тень мелкого текста: заголовок, подписи, метки дат, поле ввода.</summary>
        public static ShadowSpec Text(bool isLight)
        {
            if (isLight)
            {
                return None(true);
            }

            return Drop(TextDropBlur, TextDropDepth, TextDropOpacity);
        }

        /// <summary>Тень глифов и тонких обводок: кнопки шапки, «+», корзина, кружок галочки.</summary>
        public static ShadowSpec Icon(bool isLight)
        {
            if (isLight)
            {
                return None(isLight);
            }

            return Drop(IconDropBlur, IconDropDepth, IconDropOpacity);
        }

        /// <summary>
        /// Тень выключена: в светлой теме эффект не вешают вовсе, поэтому текст
        /// сохраняет ClearType. Цвет совпадает с цветом текста — «тень» ничего не подмешивает.
        /// </summary>
        public static ShadowSpec None(bool isLight)
        {
            ShadowSpec spec = new ShadowSpec();
            spec.Alpha = 0x00;
            TextColor(isLight, out spec.Red, out spec.Green, out spec.Blue);
            spec.BlurRadius = NoBlur;
            spec.ShadowDepth = NoDepth;
            spec.Direction = DownRightDirection;
            spec.Opacity = 0.0;
            return spec;
        }

        /// <summary>Тень включена: нулевой радиус или нулевая плотность — это уже не тень.</summary>
        public static bool IsVisible(ShadowSpec spec)
        {
            return spec.Alpha > 0 &&
                spec.BlurRadius >= MinBlur &&
                spec.Opacity > 0.0;
        }

        /// <summary>
        /// Тень смещена, а не размазана симметрично вокруг глифа: симметричная тень
        /// ложится прямо под букву и размывает сами штрихи.
        /// </summary>
        public static bool IsOffset(ShadowSpec spec)
        {
            return spec.ShadowDepth >= MinDepth &&
                spec.ShadowDepth <= MaxDepth &&
                System.Math.Abs(spec.Direction - DownRightDirection) < 0.5;
        }

        /// <summary>
        /// Ореол светлее текста, тень темнее — иначе край глифа не отделится от фона.
        /// </summary>
        public static bool ContrastsWithText(ShadowSpec spec, bool isLight)
        {
            byte red;
            byte green;
            byte blue;
            TextColor(isLight, out red, out green, out blue);

            return System.Math.Abs(Luma(red, green, blue) - Luma(spec.Red, spec.Green, spec.Blue))
                >= 0.5;
        }

        /// <summary>Воспринимаемая яркость цвета: 0 — чёрный, 1 — белый.</summary>
        public static double Luma(byte red, byte green, byte blue)
        {
            return (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;
        }

        /// <summary>Чёрная тень под светлым текстом тёмной темы.</summary>
        private static ShadowSpec Drop(double blur, double depth, double opacity)
        {
            ShadowSpec spec = new ShadowSpec();
            spec.Alpha = 0xFF;
            spec.Red = 0x00;
            spec.Green = 0x00;
            spec.Blue = 0x00;
            spec.BlurRadius = blur;
            spec.ShadowDepth = depth;
            spec.Direction = DownRightDirection;
            spec.Opacity = opacity;
            return spec;
        }
    }
}

