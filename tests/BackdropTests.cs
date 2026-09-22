using System;

namespace ToDoist.Tests
{
    /// <summary>
    /// Проверки логики стекла: выбор режима размытия, плотность тинта,
    /// разбор ключей командной строки, область обоев и пиксельные операции.
    /// </summary>
    internal static class BackdropTests
    {
        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("Проверка стекла:");

            TestDecide();
            TestTintAlpha();
            TestArguments();
            TestWallpaperRegion();
            TestNoiseAndBlur();
        }

        private static void TestDecide()
        {
            Check("режим: Windows 11 24H2 — размытие обоев",
                BackdropSupport.Decide(26300, true) == BackdropMode.Blur);
            Check("режим: граница 17134 — размытие обоев",
                BackdropSupport.Decide(17134, true) == BackdropMode.Blur);
            Check("режим: Windows 10 1709 — без размытия",
                BackdropSupport.Decide(16299, true) == BackdropMode.Solid);
            Check("режим: старая сборка — без размытия",
                BackdropSupport.Decide(10240, true) == BackdropMode.Solid);
            Check("режим: выключены эффекты прозрачности — без размытия",
                BackdropSupport.Decide(26300, false) == BackdropMode.Solid);
            Check("режим: неизвестная сборка — без размытия",
                BackdropSupport.Decide(0, true) == BackdropMode.Solid);
        }

        private static void TestTintAlpha()
        {
            Check("тинт: минимум слайдера даёт ровно «вуаль» светлой темы",
                Near(BackdropSupport.TintAlpha(0.45, true, BackdropMode.Blur),
                    BackdropSupport.LightMinTint));
            Check("тинт: максимум слайдера даёт плотное стекло",
                Near(BackdropSupport.TintAlpha(1.0, true, BackdropMode.Blur),
                    BackdropSupport.LightMaxTint));
            Check("тинт: середина слайдера — ровно между краями",
                Near(BackdropSupport.TintAlpha(0.725, true, BackdropMode.Blur),
                    (BackdropSupport.LightMinTint + BackdropSupport.LightMaxTint) / 2.0));
            Check("тинт: растёт вместе со слайдером",
                BackdropSupport.TintAlpha(0.5, true, BackdropMode.Blur) <
                BackdropSupport.TintAlpha(0.7, true, BackdropMode.Blur) &&
                BackdropSupport.TintAlpha(0.7, true, BackdropMode.Blur) <
                BackdropSupport.TintAlpha(0.9, true, BackdropMode.Blur));
            Check("тинт: значение больше единицы зажато",
                Near(BackdropSupport.TintAlpha(5.0, true, BackdropMode.Blur),
                    BackdropSupport.LightMaxTint));
            Check("тинт: отрицательное значение зажато",
                Near(BackdropSupport.TintAlpha(-5.0, true, BackdropMode.Blur),
                    BackdropSupport.LightMinTint));
            Check("тинт: в тёмной теме дымка плотнее",
                Near(BackdropSupport.TintAlpha(0.45, false, BackdropMode.Blur),
                    BackdropSupport.DarkMinTint) &&
                Near(BackdropSupport.TintAlpha(1.0, false, BackdropMode.Blur),
                    BackdropSupport.DarkMaxTint) &&
                BackdropSupport.DarkMinTint > BackdropSupport.LightMinTint);
            Check("тинт: при любом слайдере текст остаётся читаемым",
                BackdropSupport.TintAlpha(0.45, true, BackdropMode.Blur) >= 0.35 &&
                BackdropSupport.TintAlpha(0.45, false, BackdropMode.Blur) >= 0.55);
            Check("тинт: NaN заменяется значением по умолчанию",
                Near(BackdropSupport.TintAlpha(double.NaN, true, BackdropMode.Blur),
                    BackdropSupport.TintAlpha(AppData.DefaultOpacity, true, BackdropMode.Blur)));
            Check("тинт: сплошной режим повторяет прежнее поведение",
                Near(BackdropSupport.TintAlpha(0.73, true, BackdropMode.Solid), 0.73));
            Check("тинт: сплошной режим зажат по границам слайдера",
                Near(BackdropSupport.TintAlpha(9.0, true, BackdropMode.Solid), 1.0) &&
                Near(BackdropSupport.TintAlpha(-9.0, true, BackdropMode.Solid),
                    AppData.MinOpacity));
        }

        private static void TestArguments()
        {
            Check("ключ: --backdrop=blur",
                BackdropSupport.ParseBackdropArgument(new string[] { "--backdrop=blur" }) == "blur");
            Check("ключ: --backdrop Blur",
                BackdropSupport.ParseBackdropArgument(new string[] { "--backdrop", "Blur" }) == "Blur");
            Check("ключ: чужие аргументы игнорируются",
                BackdropSupport.ParseBackdropArgument(new string[] { "-x", "--other=1" }) == null);
            Check("ключ: без аргументов",
                BackdropSupport.ParseBackdropArgument(null) == null &&
                BackdropSupport.ParseBackdropArgument(new string[0]) == null);

            BackdropMode mode;
            Check("ключ: blur — своё размытие",
                BackdropSupport.TryParseBackdropArg("blur", out mode) && mode == BackdropMode.Blur);
            Check("ключ: glass — синоним размытия",
                BackdropSupport.TryParseBackdropArg("glass", out mode) && mode == BackdropMode.Blur);
            Check("ключ: регистр и пробелы не важны",
                BackdropSupport.TryParseBackdropArg("  SOLID ", out mode) && mode == BackdropMode.Solid);
            Check("ключ: off — сплошное стекло",
                BackdropSupport.TryParseBackdropArg("off", out mode) && mode == BackdropMode.Solid);
            Check("ключ: auto — считаем автоматически",
                !BackdropSupport.TryParseBackdropArg("auto", out mode));
            Check("ключ: мусор игнорируется",
                !BackdropSupport.TryParseBackdropArg("мусор", out mode));
            Check("ключ: пустое значение игнорируется",
                !BackdropSupport.TryParseBackdropArg(string.Empty, out mode) &&
                !BackdropSupport.TryParseBackdropArg(null, out mode));
        }

        private static void TestWallpaperRegion()
        {
            WallpaperGeometry geometry = new WallpaperGeometry();
            geometry.ImageWidth = 3840;
            geometry.ImageHeight = 2160;
            geometry.Style = 10;
            geometry.ScreenX = 0;
            geometry.ScreenY = 0;
            geometry.ScreenWidth = 1920;
            geometry.ScreenHeight = 1080;
            geometry.CardX = 100;
            geometry.CardY = 200;
            geometry.CardWidth = 380;
            geometry.CardHeight = 560;

            WallpaperRegion region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: стиль «заполнить» — область под карточкой",
                Near(region.X, 200) && Near(region.Y, 400) &&
                Near(region.Width, 760) && Near(region.Height, 1120));

            geometry.ImageWidth = 1600;
            geometry.ImageHeight = 1200;
            geometry.Style = 2;
            geometry.CardX = 192;
            geometry.CardY = 108;
            geometry.CardWidth = 384;
            geometry.CardHeight = 540;
            region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: стиль «растянуть» — область под карточкой",
                Near(region.X, 160) && Near(region.Y, 120) &&
                Near(region.Width, 320) && Near(region.Height, 600));

            geometry.ImageWidth = 4000;
            geometry.ImageHeight = 1000;
            geometry.Style = 6;
            geometry.CardX = 0;
            geometry.CardY = 0;
            geometry.CardWidth = 380;
            geometry.CardHeight = 560;
            region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: стиль «вписать» — карточка выше картинки, край зажат",
                Near(region.X, 0) && Near(region.Y, 0) &&
                Near(region.Height, 1000) && Near(region.Width, 380 / 0.48));

            geometry.ImageWidth = 1000;
            geometry.ImageHeight = 800;
            geometry.Style = 0;
            geometry.CardX = 100;
            geometry.CardY = 100;
            geometry.CardWidth = 380;
            geometry.CardHeight = 560;
            region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: стиль «по центру» — карточка вне картинки зажата",
                Near(region.X, 0) && Near(region.Y, 0) &&
                Near(region.Width, 380) && Near(region.Height, 560));

            geometry.ImageWidth = 0;
            region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: без картинки область пустая",
                Near(region.X, 0) && Near(region.Y, 0) &&
                Near(region.Width, 0) && Near(region.Height, 0));

            geometry.ImageWidth = 1000;
            geometry.ImageHeight = 800;
            geometry.CardWidth = 0;
            region = BackdropSupport.ComputeWallpaperRegion(geometry);
            Check("обои: без карточки область пустая",
                Near(region.Width, 0) && Near(region.Height, 0));
        }

        private static void TestNoiseAndBlur()
        {
            byte[] tile = GlassSupport.CreateNoiseTile(8, 7);
            Check("зерно: размер плитки", tile.Length == 8 * 8 * 4);

            bool monochrome = true;
            bool alphaOk = true;
            for (int i = 0; i + 3 < tile.Length; i += 4)
            {
                if (tile[i] != tile[i + 1] || tile[i] != tile[i + 2])
                {
                    monochrome = false;
                }

                if (tile[i + 3] > GlassSupport.NoiseMaxAlpha)
                {
                    alphaOk = false;
                }
            }

            Check("зерно: монохромное", monochrome);
            Check("зерно: альфа почти прозрачная", alphaOk);

            byte[] sameTile = GlassSupport.CreateNoiseTile(8, 7);
            bool identical = sameTile.Length == tile.Length;
            for (int i = 0; identical && i < tile.Length; i++)
            {
                if (sameTile[i] != tile[i])
                {
                    identical = false;
                }
            }

            Check("зерно: воспроизводимо по seed", identical);

            byte[] otherTile = GlassSupport.CreateNoiseTile(8, 8);
            bool differs = false;
            for (int i = 0; i < tile.Length; i++)
            {
                if (otherTile[i] != tile[i])
                {
                    differs = true;
                    break;
                }
            }

            Check("зерно: другой seed — другой узор", differs);

            byte[] flat = new byte[5 * 5 * 4];
            for (int i = 0; i + 3 < flat.Length; i += 4)
            {
                flat[i] = 100;
                flat[i + 1] = 100;
                flat[i + 2] = 100;
                flat[i + 3] = 255;
            }

            byte[] untouched = (byte[])flat.Clone();
            GlassSupport.BoxBlurBgra(untouched, 5, 5, 0, 3);
            Check("размытие: нулевой радиус ничего не меняет", SameBytes(untouched, flat));

            GlassSupport.BoxBlurBgra(flat, 5, 5, 1, 2);
            Check("размытие: однотонная картинка не меняется", SameBytes(flat, untouched));

            byte[] impulse = new byte[7 * 7 * 4];
            int center = (3 * 7 + 3) * 4;
            impulse[center] = 255;
            impulse[center + 2] = 255;
            GlassSupport.BoxBlurBgra(impulse, 7, 7, 1, 1);

            Check("размытие: центр импульса размыт", impulse[center] == 28);
            Check("размытие: импульс расходится на соседей",
                impulse[(3 * 7 + 2) * 4] > 0 && impulse[(2 * 7 + 3) * 4] > 0);
            Check("размытие: дальше соседей импульс не идёт",
                impulse[(3 * 7 + 1) * 4] == 0 && impulse[(1 * 7 + 3) * 4] == 0);

            byte[] again = new byte[7 * 7 * 4];
            again[center] = 255;
            again[center + 2] = 255;
            GlassSupport.BoxBlurBgra(again, 7, 7, 1, 1);
            Check("размытие: воспроизводимо", SameBytes(again, impulse));

            byte[] saturated = new byte[] { 0, 0, 200, 255 };
            GlassSupport.BoostSaturation(saturated, 1.6);
            Check("насыщенность: цвет становится сочнее",
                saturated[0] == 0 && saturated[2] == 255);

            byte[] gray = new byte[] { 128, 128, 128, 255 };
            GlassSupport.BoostSaturation(gray, 1.6);
            Check("насыщенность: серый остаётся серым",
                gray[0] == 128 && gray[1] == 128 && gray[2] == 128);

            byte[] brightness = new byte[] { 100, 100, 100, 255 };
            GlassSupport.AdjustBrightness(brightness, 10);
            Check("яркость: сдвиг вверх", brightness[0] == 110 && brightness[2] == 110);
            GlassSupport.AdjustBrightness(brightness, 200);
            Check("яркость: сдвиг зажат белым", brightness[0] == 255);
            GlassSupport.AdjustBrightness(brightness, -400);
            Check("яркость: сдвиг зажат чёрным", brightness[0] == 0);
        }

        private static bool SameBytes(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Near(double actual, double expected)
        {
            return Math.Abs(actual - expected) < 0.000001;
        }

        private static void Check(string name, bool condition)
        {
            StorageTests.Check(name, condition);
        }
    }
}



