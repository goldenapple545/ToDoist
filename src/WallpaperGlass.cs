using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ToDoist
{
    /// <summary>
    /// Своё стекло: берём обои Windows, поднимаем насыщенность и размываем
    /// слой с ними (BlurEffect в XAML), вырезая ровно ту область обоев,
    /// которая лежит под карточкой. Окно при этом остаётся прозрачным,
    /// поэтому скругления и тень — как раньше.
    /// </summary>
    public class WallpaperGlass
    {
        private const string DesktopKey = @"Control Panel\Desktop";
        private const string ColorsKey = @"Control Panel\Colors";

        /// <summary>Обои больше 1920 по ширине декодируем уменьшенными — размытие всё скроет.</summary>
        private const int MaxDecodeWidth = 1920;
        private const double Saturation = 1.45;
        private const int Brightness = 6;

        private readonly Window _window;
        private readonly FrameworkElement _sample;
        private readonly Rectangle _layer;
        private readonly DispatcherTimer _debounce;

        private BitmapSource _wallpaper;
        private Color _solidColor = Colors.Transparent;
        private bool _solid;
        private string _sourceKey = string.Empty;
        private DateTime _stamp = DateTime.MinValue;
        private bool _enabled;
        private string _status = "выключено";

        public WallpaperGlass(Window window, FrameworkElement sample, Rectangle layer)
        {
            _window = window;
            _sample = sample;
            _layer = layer;

            _debounce = new DispatcherTimer(DispatcherPriority.Background);
            _debounce.Interval = TimeSpan.FromMilliseconds(90);
            _debounce.Tick += OnDebounceTick;

            _window.LocationChanged += OnWindowChanged;
            _window.SizeChanged += OnWindowChanged;
        }

        /// <summary>Включено ли размытие обоев (в остальных режимах слой просто скрыт).</summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { SetEnabled(value); }
        }

        /// <summary>Что взято за основу стекла — для журнала и тултипа.</summary>
        public string Status
        {
            get { return _status; }
        }

        public void Dispose()
        {
            _debounce.Stop();
            _debounce.Tick -= OnDebounceTick;
            _window.LocationChanged -= OnWindowChanged;
            _window.SizeChanged -= OnWindowChanged;
        }

        /// <summary>Пересчитать стекло: при force — обязательно, иначе только если сменились обои.</summary>
        public void Refresh(bool force)
        {
            if (!_enabled || !_window.IsLoaded)
            {
                return;
            }

            try
            {
                string path = ReadWallpaperPath();
                int style = ReadWallpaperStyle();
                bool tile = ReadTile();

                DateTime stamp = DateTime.MinValue;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        stamp = File.GetLastWriteTimeUtc(path);
                    }
                    catch (Exception)
                    {
                    }
                }

                string key = path + "|" + style + "|" + (tile ? "1" : "0");
                if (force || key != _sourceKey || stamp != _stamp)
                {
                    LoadWallpaper(path);
                    _sourceKey = key;
                    _stamp = stamp;
                }

                ApplyBrush(style, tile);
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось построить стекло из обоев", ex);
            }
        }

        /// <summary>Отложенный пересчёт — при перетаскивании и изменении размера.</summary>
        public void Invalidate()
        {
            if (!_enabled)
            {
                return;
            }

            _debounce.Stop();
            _debounce.Start();
        }

        private void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _layer.Fill = null;
                _layer.Visibility = Visibility.Collapsed;
                _status = "выключено";
                return;
            }

            _layer.Visibility = Visibility.Visible;
            Refresh(true);
        }

        private void OnWindowChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounce.Stop();
            Refresh(false);
        }

        /// <summary>Готовит картинку обоев: уменьшает при необходимости и делает цвет сочнее.</summary>
        private void LoadWallpaper(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _wallpaper = null;
                _solid = true;
                _solidColor = ReadBackgroundColor();
                _status = "однотонный фон Windows";
                return;
            }

            Uri uri = new Uri(path, UriKind.Absolute);

            int pixelWidth = 0;
            int pixelHeight = 0;
            try
            {
                BitmapDecoder decoder = BitmapDecoder.Create(uri,
                    BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                pixelWidth = decoder.Frames[0].PixelWidth;
                pixelHeight = decoder.Frames[0].PixelHeight;
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось прочитать обои " + path, ex);
                _wallpaper = null;
                _solid = true;
                _solidColor = ReadBackgroundColor();
                _status = "однотонный фон Windows";
                return;
            }

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (pixelWidth > MaxDecodeWidth)
            {
                image.DecodePixelWidth = MaxDecodeWidth;
            }

            image.EndInit();
            image.Freeze();

            BitmapSource source = image;
            if (source.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = image;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                converted.Freeze();
                source = converted;
            }

            int stride = source.PixelWidth * 4;
            byte[] pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            GlassSupport.BoostSaturation(pixels, Saturation);
            GlassSupport.AdjustBrightness(pixels, Brightness);

            BitmapSource boosted = BitmapSource.Create(source.PixelWidth, source.PixelHeight,
                96.0, 96.0, PixelFormats.Bgra32, null, pixels, stride);
            boosted.Freeze();

            _wallpaper = boosted;
            _solid = false;
            _status = string.Format("обои {0}×{1} (прочитано {2}×{3})",
                pixelWidth, pixelHeight, source.PixelWidth, source.PixelHeight);
        }

        private void ApplyBrush(int style, bool tile)
        {
            if (_solid)
            {
                SolidColorBrush solid = new SolidColorBrush(_solidColor);
                solid.Freeze();
                _layer.Fill = solid;
                return;
            }

            if (_wallpaper == null)
            {
                _layer.Fill = null;
                return;
            }

            WallpaperRegion region = BackdropSupport.ComputeWallpaperRegion(
                CurrentGeometry(style, tile));

            if (region.Width < 1.0 || region.Height < 1.0)
            {
                _layer.Fill = null;
                return;
            }

            ImageBrush brush = new ImageBrush(_wallpaper);
            brush.Stretch = Stretch.Fill;
            brush.AlignmentX = AlignmentX.Left;
            brush.AlignmentY = AlignmentY.Top;
            brush.TileMode = TileMode.None;
            brush.ViewboxUnits = BrushMappingMode.Absolute;
            brush.Viewbox = new Rect(region.X, region.Y, region.Width, region.Height);
            brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            brush.Viewport = new Rect(0, 0, 1, 1);
            brush.Freeze();

            _layer.Fill = brush;
        }

        /// <summary>Геометрия: где карточка на экране и как Windows растянула обои.</summary>
        private WallpaperGeometry CurrentGeometry(int style, bool tile)
        {
            WallpaperGeometry geometry = new WallpaperGeometry();

            if (_wallpaper != null)
            {
                geometry.ImageWidth = _wallpaper.PixelWidth;
                geometry.ImageHeight = _wallpaper.PixelHeight;
            }

            geometry.Style = style;
            geometry.Tile = tile;
            geometry.ScreenX = SystemParameters.VirtualScreenLeft;
            geometry.ScreenY = SystemParameters.VirtualScreenTop;
            geometry.ScreenWidth = SystemParameters.VirtualScreenWidth;
            geometry.ScreenHeight = SystemParameters.VirtualScreenHeight;

            Matrix fromDevice = FromDevice();
            Point topLeft = fromDevice.Transform(_sample.PointToScreen(new Point(0, 0)));

            geometry.CardX = topLeft.X;
            geometry.CardY = topLeft.Y;
            geometry.CardWidth = _sample.ActualWidth;
            geometry.CardHeight = _sample.ActualHeight;

            return geometry;
        }

        private Matrix FromDevice()
        {
            PresentationSource source = PresentationSource.FromVisual(_sample);
            if (source == null || source.CompositionTarget == null)
            {
                return Matrix.Identity;
            }

            return source.CompositionTarget.TransformFromDevice;
        }

        private static string ReadWallpaperPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(DesktopKey))
                {
                    if (key != null)
                    {
                        string raw = key.GetValue("WallPaper") as string;
                        if (!string.IsNullOrEmpty(raw))
                        {
                            return Environment.ExpandEnvironmentVariables(raw);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>0 — по центру, 2 — растянуть, 6 — вписать, 10 — заполнить, 22 — на все мониторы.</summary>
        private static int ReadWallpaperStyle()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(DesktopKey))
                {
                    if (key != null)
                    {
                        string raw = key.GetValue("WallpaperStyle") as string;
                        int style;
                        if (raw != null && int.TryParse(raw, out style))
                        {
                            return style;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return 10;
        }

        private static bool ReadTile()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(DesktopKey))
                {
                    if (key != null)
                    {
                        string raw = key.GetValue("TileWallpaper") as string;
                        return raw == "1";
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        /// <summary>Цвет фона рабочего стола, когда обоев нет: «58 110 165» в реестре.</summary>
        private static Color ReadBackgroundColor()
        {
            Color fallback = Color.FromRgb(0x1F, 0x3A, 0x5F);

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ColorsKey))
                {
                    if (key == null)
                    {
                        return fallback;
                    }

                    string raw = key.GetValue("Background") as string;
                    if (string.IsNullOrEmpty(raw))
                    {
                        return fallback;
                    }

                    string[] parts = raw.Split(' ');
                    if (parts.Length < 3)
                    {
                        return fallback;
                    }

                    byte red;
                    byte green;
                    byte blue;
                    if (byte.TryParse(parts[0].Trim(), out red) &&
                        byte.TryParse(parts[1].Trim(), out green) &&
                        byte.TryParse(parts[2].Trim(), out blue))
                    {
                        return Color.FromRgb(red, green, blue);
                    }
                }
            }
            catch (Exception)
            {
            }

            return fallback;
        }
    }
}
