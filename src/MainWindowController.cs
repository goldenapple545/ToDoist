using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ToDoist
{
    /// <summary>
    /// Логика окна. XAML лежит во встроенном ресурсе и загружается через XamlReader,
    /// поэтому для сборки не нужен MSBuild; элементы находятся по именам.
    /// </summary>
    public class MainWindowController
    {
        private const double EdgePadding = 14.0;

        /// <summary>Радиус стеклянной карточки.</summary>
        private const double GlassRadius = 20.0;

        private readonly CultureInfo _ru = CultureInfo.GetCultureInfo("ru-RU");
        private readonly Storage _storage;
        private readonly Window _window;
        private readonly ResizeHelper _resize;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _watchTimer;

        private readonly Border _card;
        private readonly Grid _headerBar;
        private readonly TextBlock _title;
        private readonly TextBlock _subtitle;
        private readonly TextBlock _footer;
        private readonly TextBox _input;
        private readonly TextBlock _placeholder;
        private readonly StackPanel _taskPanel;
        private readonly ScrollViewer _listScroll;
        private readonly TextBlock _emptyText;
        private readonly ProgressBar _progress;
        private readonly Slider _opacitySlider;
        private readonly Button _pinButton;
        private readonly Button _themeButton;
        private readonly Button _addButton;
        private readonly Button _minimizeButton;
        private readonly Button _closeButton;

        private readonly Rectangle _backdropLayer;
        private readonly Rectangle _noiseLayer;
        private readonly Border _glassEdge;
        private readonly WallpaperGlass _glass;
        private readonly BackdropMode _plan;

        private AppData _data;
        private bool _suppressCheckEvents;
        private bool _suppressSliderEvents;
        private bool _isClosing;
        private TaskItem _selectedTask;
        private Border _selectedRow;
        private bool _lastSystemLight;
        private string _lastDay;

        public MainWindowController()
        {
            _window = (Window)LoadComponent("MainWindow.xaml");
            _storage = new Storage(Storage.DefaultDirectory);
            _data = _storage.Load();

            _card = (Border)Find("Card");
            _headerBar = (Grid)Find("HeaderBar");
            _title = (TextBlock)Find("TitleText");
            _subtitle = (TextBlock)Find("SubtitleText");
            _footer = (TextBlock)Find("FooterText");
            _input = (TextBox)Find("TaskInput");
            _placeholder = (TextBlock)Find("InputPlaceholder");
            _taskPanel = (StackPanel)Find("TaskPanel");
            _listScroll = (ScrollViewer)Find("ListScroll");
            _emptyText = (TextBlock)Find("EmptyText");
            _progress = (ProgressBar)Find("Progress");
            _opacitySlider = (Slider)Find("OpacitySlider");
            _pinButton = (Button)Find("PinButton");
            _themeButton = (Button)Find("ThemeButton");
            _addButton = (Button)Find("AddButton");
            _minimizeButton = (Button)Find("MinimizeButton");
            _closeButton = (Button)Find("CloseButton");

            _backdropLayer = (Rectangle)Find("BackdropLayer");
            _noiseLayer = (Rectangle)Find("NoiseLayer");
            _glassEdge = (Border)Find("GlassEdge");

            _plan = ResolveBackdropPlan();
            ConfigureGlass();

            _glass = new WallpaperGlass(_window, _card, _backdropLayer);
            _glass.Enabled = _plan == BackdropMode.Blur;

            _resize = new ResizeHelper(_window, _card);
            _saveTimer = new DispatcherTimer(DispatcherPriority.Background);
            _saveTimer.Interval = TimeSpan.FromMilliseconds(700);
            _saveTimer.Tick += OnSaveTimerTick;

            _watchTimer = new DispatcherTimer(DispatcherPriority.Background);
            _watchTimer.Interval = TimeSpan.FromSeconds(3);
            _watchTimer.Tick += OnWatchTimerTick;

            _lastSystemLight = ThemeManager.IsSystemLight();
            _lastDay = Today();

            ApplyWindowState();
            ApplyTheme();
            RenderTasks();
            WireEvents();

            _saveTimer.Start();
            _watchTimer.Start();
            Log.Info("Окно создано. Данные: " + _storage.FilePath);
        }

        public Window Window
        {
            get { return _window; }
        }

        private static string Today()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        private static object LoadComponent(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Не найден встроенный ресурс " + resourceName);
                }

                return XamlReader.Load(stream);
            }
        }

        private object Find(string name)
        {
            object element = _window.FindName(name);
            if (element == null)
            {
                throw new InvalidOperationException("В XAML нет элемента с именем " + name);
            }

            return element;
        }

        private void ApplyWindowState()
        {
            _window.Width = _data.Width;
            _window.Height = _data.Height;
            _window.Topmost = _data.Topmost;

            _suppressSliderEvents = true;
            _opacitySlider.Value = _data.Opacity;
            _suppressSliderEvents = false;

            if (_data.HasBounds && IsReachable(_data.Left, _data.Top, _data.Width, _data.Height))
            {
                _window.WindowStartupLocation = WindowStartupLocation.Manual;
                _window.Left = _data.Left;
                _window.Top = _data.Top;
            }
            else
            {
                _window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>Проверяет, что сохранённая позиция всё ещё видна на экране.</summary>
        private static bool IsReachable(double left, double top, double width, double height)
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            return left + width > virtualLeft + 60 &&
                   left < virtualRight - 60 &&
                   top + 30 > virtualTop &&
                   top < virtualBottom - 20;
        }

        private void ApplyTheme()
        {
            bool isLight = ThemeManager.ResolveIsLight(_data.Theme);
            ThemeManager.Apply(_window, isLight, _opacitySlider.Value, ThemeManager.ReadAccentColor());
            ApplyGlassBrushes(isLight);
            UpdateToggles();
        }

        private void ApplyGlassBrushes(bool isLight)
        {
            ThemeManager.ApplyGlass(_window, isLight, _opacitySlider.Value, _plan);
            _noiseLayer.Opacity = isLight ? 0.03 : 0.045;
        }

        /// <summary>
        /// Что и как размывать: свои обои, если окно может быть прозрачным,
        /// иначе прежнее сплошное стекло.
        /// </summary>
        private static BackdropMode ResolveBackdropPlan()
        {
            BackdropMode forced;
            if (BackdropSupport.TryParseBackdropArg(Program.BackdropArgument, out forced))
            {
                Log.Info("Стекло: режим задан ключом — " + BackdropSupport.Describe(forced));
                return forced;
            }

            int build = BackdropSupport.ReadWindowsBuild();
            bool transparency = BackdropSupport.ReadTransparencyEnabled();
            BackdropMode plan = BackdropSupport.Decide(build, transparency);

            Log.Info(string.Format("Стекло: сборка {0}, эффекты прозрачности {1} — {2}",
                build, transparency ? "включены" : "выключены", BackdropSupport.Describe(plan)));
            return plan;
        }

        /// <summary>
        /// Размытие рисует сама карточка, поэтому окно остаётся прозрачным:
        /// так сохраняются мягкая тень и крупные скругления.
        /// </summary>
        private void ConfigureGlass()
        {
            _window.AllowsTransparency = true;
            _window.Background = Brushes.Transparent;

            _card.CornerRadius = new CornerRadius(GlassRadius);
            _glassEdge.CornerRadius = new CornerRadius(GlassRadius);
            _backdropLayer.Visibility = _plan == BackdropMode.Blur
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateGlassClip();
        }

        /// <summary>
        /// Стеклянные слои обрезаются по скруглению карточки.
        /// Клип ставим на саму карточку: у элемента с тенью и клипом-потомком
        /// WPF может вовсе не нарисовать содержимое.
        /// </summary>
        private void UpdateGlassClip()
        {
            double width = _card.ActualWidth;
            double height = _card.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                _card.Clip = null;
                return;
            }

            RectangleGeometry clip = new RectangleGeometry(
                new Rect(0, 0, width, height), GlassRadius, GlassRadius);
            clip.Freeze();
            _card.Clip = clip;
        }

        private void OnCardSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGlassClip();
        }

        private void OnResizeEnded(object sender, EventArgs e)
        {
            _glass.Invalidate();
        }

        /// <summary>Ссылки на элементы одной строки списка — чтобы не искать их по дереву.</summary>
        private class RowVisuals
        {
            public TaskItem Task;
            public Border Row;
            public CheckBox Check;
            public TextBlock Text;
            public Button Delete;
        }

        private void RenderTasks()
        {
            _taskPanel.Children.Clear();
            _selectedTask = null;
            _selectedRow = null;

            foreach (TaskItem task in _data.Tasks)
            {
                if (IsVisible(task))
                {
                    _taskPanel.Children.Add(CreateRow(task));
                }
            }

            UpdateCounters();
        }

        /// <summary>
        /// Показываем все невыполненные задачи и выполненные за сегодня:
        /// вчерашние дела не пропадают, но и не мусорят список дня.
        /// </summary>
        private bool IsVisible(TaskItem task)
        {
            if (!task.Done)
            {
                return true;
            }

            return task.Date == Today();
        }

        private FrameworkElement CreateRow(TaskItem task)
        {
            RowVisuals visuals = new RowVisuals();
            visuals.Task = task;

            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Padding = new Thickness(7, 5, 5, 5);
            row.Margin = new Thickness(0, 1, 0, 1);
            // Строка всегда видна: фейд-ин есть только у только что добавленной задачи
            // (см. AnimateAppear), иначе строки пропадали бы до наведения курсора.
            row.SetResourceReference(Border.BackgroundProperty, "HoverBrush");
            visuals.Row = row;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Auto);
            grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Auto);

            CheckBox check = new CheckBox();
            check.Style = (Style)_window.FindResource("TaskCheck");
            check.IsChecked = task.Done;
            check.ToolTip = "Отметить выполненной";
            check.VerticalAlignment = VerticalAlignment.Top;
            check.Margin = new Thickness(0, 2, 0, 0);
            Grid.SetColumn(check, 0);
            visuals.Check = check;
            grid.Children.Add(check);

            Button delete = new Button();
            delete.Style = (Style)_window.FindResource("RowDeleteButton");
            delete.Content = "\uE74D";
            delete.Opacity = 0;
            delete.ToolTip = "Удалить (Delete)";
            Grid.SetColumn(delete, 2);
            visuals.Delete = delete;
            grid.Children.Add(delete);

            Grid textHost = new Grid();
            textHost.RowDefinitions.Add(new RowDefinition());
            textHost.RowDefinitions.Add(new RowDefinition());
            textHost.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Auto);
            textHost.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Auto);
            textHost.Margin = new Thickness(10, 0, 4, 0);
            Grid.SetColumn(textHost, 1);

            TextBlock text = new TextBlock();
            text.Style = (Style)_window.FindResource("TaskTitle");
            text.Text = task.Title;
            text.ToolTip = "Нажмите, чтобы отметить";
            Grid.SetRow(text, 0);
            visuals.Text = text;
            textHost.Children.Add(text);

            string badgeText = DescribeDate(task.Date);
            if (badgeText.Length > 0)
            {
                TextBlock badge = new TextBlock();
                badge.Style = (Style)_window.FindResource("TaskBadge");
                badge.Text = badgeText;
                badge.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetRow(badge, 1);
                textHost.Children.Add(badge);
            }

            grid.Children.Add(textHost);
            row.Child = grid;

            // Одна ссылка на все элементы строки — дальше обработчики берут её из Tag.
            row.Tag = visuals;
            check.Tag = visuals;
            text.Tag = visuals;
            delete.Tag = visuals;

            ApplyTaskVisual(visuals);

            check.Checked += OnTaskChecked;
            check.Unchecked += OnTaskUnchecked;
            text.MouseLeftButtonUp += OnTaskTextClick;
            delete.Click += OnDeleteClick;
            row.MouseEnter += OnRowMouseEnter;
            row.MouseLeave += OnRowMouseLeave;
            row.MouseLeftButtonDown += OnRowMouseDown;

            return row;
        }

        private static void ApplyTaskVisual(RowVisuals visuals)
        {
            if (visuals.Task.Done)
            {
                visuals.Text.TextDecorations = TextDecorations.Strikethrough;
                visuals.Text.Opacity = 0.45;
            }
            else
            {
                visuals.Text.TextDecorations = null;
                visuals.Text.Opacity = 1.0;
            }
        }

        private void AddTaskFromInput()
        {
            string title = _input.Text == null ? string.Empty : _input.Text.Trim();
            if (title.Length == 0)
            {
                return;
            }

            TaskItem task = TaskItem.Create(title, DateTime.Now);
            _data.Tasks.Insert(0, task);

            FrameworkElement row = CreateRow(task);
            _taskPanel.Children.Insert(0, row);

            _input.Clear();
            AnimateAppear(row);
            _listScroll.ScrollToTop();

            UpdateCounters();
            SaveSoon();
        }

        private void OnTaskChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressCheckEvents)
            {
                return;
            }

            CheckBox check = sender as CheckBox;
            if (check == null)
            {
                return;
            }

            SetDone(check, true);
        }

        private void OnTaskUnchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressCheckEvents)
            {
                return;
            }

            CheckBox check = sender as CheckBox;
            if (check == null)
            {
                return;
            }

            SetDone(check, false);
        }

        private void SetDone(CheckBox check, bool done)
        {
            RowVisuals visuals = check.Tag as RowVisuals;
            if (visuals == null)
            {
                return;
            }

            visuals.Task.Done = done;
            if (done)
            {
                visuals.Task.Date = Today();
            }

            ApplyTaskVisual(visuals);
            UpdateCounters();
            SaveSoon();
        }

        private void OnTaskTextClick(object sender, MouseButtonEventArgs e)
        {
            RowVisuals visuals = (sender as FrameworkElement) == null
                ? null
                : (sender as FrameworkElement).Tag as RowVisuals;
            if (visuals == null)
            {
                return;
            }

            bool target = !visuals.Task.Done;

            _suppressCheckEvents = true;
            visuals.Check.IsChecked = target;
            _suppressCheckEvents = false;

            SetDone(visuals.Check, target);
            e.Handled = true;
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            RowVisuals visuals = (sender as FrameworkElement) == null
                ? null
                : (sender as FrameworkElement).Tag as RowVisuals;
            if (visuals == null)
            {
                return;
            }

            DoubleAnimation fade = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140));
            fade.Completed += delegate
            {
                RemoveTask(visuals.Task);
            };
            visuals.Row.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void RemoveTask(TaskItem task)
        {
            _data.Tasks.Remove(task);
            if (_selectedTask == task)
            {
                _selectedTask = null;
                _selectedRow = null;
            }

            RenderTasks();
            SaveSoon();
        }

        private void WireEvents()
        {
            _addButton.Click += OnAddClick;
            _input.KeyDown += OnInputKeyDown;
            _input.TextChanged += OnInputTextChanged;
            _pinButton.Click += OnPinClick;
            _themeButton.Click += OnThemeClick;
            _minimizeButton.Click += OnMinimizeClick;
            _closeButton.Click += OnCloseClick;
            _opacitySlider.ValueChanged += OnOpacityChanged;
            _headerBar.MouseLeftButtonDown += OnHeaderMouseDown;
            _card.SizeChanged += OnCardSizeChanged;
            _resize.ResizeEnded += OnResizeEnded;
            _window.PreviewKeyDown += OnWindowKeyDown;
            _window.Closing += OnWindowClosing;
            _window.Activated += OnWindowActivated;
            _window.Loaded += OnWindowLoaded;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            UpdateToggles();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _input.Focus();
            Keyboard.Focus(_input);
            _glass.Refresh(true);
            Log.Info("Стекло: " + BackdropSupport.Describe(_plan) +
                (_plan == BackdropMode.Blur ? " (" + _glass.Status + ")" : string.Empty));
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            AddTaskFromInput();
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder();
        }

        private void UpdatePlaceholder()
        {
            _placeholder.Visibility = string.IsNullOrEmpty(_input.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTaskFromInput();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(_input.Text))
                {
                    _input.Clear();
                }
                else
                {
                    _window.Hide();
                }

                e.Handled = true;
            }
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.N && ctrl)
            {
                _input.Focus();
                Keyboard.Focus(_input);
                e.Handled = true;
                return;
            }

            if (_input.IsKeyboardFocusWithin)
            {
                return;
            }

            if (e.Key == Key.Delete && _selectedTask != null)
            {
                RemoveTask(_selectedTask);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                _window.Hide();
                e.Handled = true;
            }
        }

        private void OnRowMouseEnter(object sender, MouseEventArgs e)
        {
            RowVisuals visuals = VisualsOf(sender);
            if (visuals == null)
            {
                return;
            }

            Animate(visuals.Delete, 1.0, 120);
        }

        private void OnRowMouseLeave(object sender, MouseEventArgs e)
        {
            RowVisuals visuals = VisualsOf(sender);
            if (visuals == null)
            {
                return;
            }

            Animate(visuals.Delete, 0.0, 160);
        }

        private void OnRowMouseDown(object sender, MouseButtonEventArgs e)
        {
            RowVisuals visuals = VisualsOf(sender);
            if (visuals == null)
            {
                return;
            }

            if (_selectedRow != null && _selectedRow != visuals.Row)
            {
                ApplyRowSelection(_selectedRow.Tag as RowVisuals, false);
            }

            _selectedRow = visuals.Row;
            _selectedTask = visuals.Task;
            ApplyRowSelection(visuals, true);
        }

        private static RowVisuals VisualsOf(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;
            return element == null ? null : element.Tag as RowVisuals;
        }

        private static void Animate(UIElement element, double opacity, int milliseconds)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(milliseconds)));
        }

        /// <summary>
        /// Подсветка выбранной строки — той, которую удалит клавиша Delete.
        /// Прозрачность строки не трогаем: она отвечает только за фейд-ин новой задачи.
        /// </summary>
        private static void ApplyRowSelection(RowVisuals visuals, bool selected)
        {
            if (visuals == null || visuals.Row == null)
            {
                return;
            }

            visuals.Row.SetResourceReference(Border.BackgroundProperty,
                selected ? "RowSelectedBrush" : "HoverBrush");
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed || e.ClickCount != 1)
            {
                return;
            }

            if (IsInsideButton(e.OriginalSource) || _window.WindowState == WindowState.Minimized)
            {
                return;
            }

            try
            {
                _window.DragMove();
            }
            catch (InvalidOperationException)
            {
                // Окно уже перетаскивается — ничего страшного.
            }
        }

        private static bool IsInsideButton(object source)
        {
            DependencyObject current = source as DependencyObject;
            while (current != null)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase)
                {
                    return true;
                }

                if (!(current is Visual))
                {
                    return false;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void OnPinClick(object sender, RoutedEventArgs e)
        {
            _data.Topmost = !_data.Topmost;
            _window.Topmost = _data.Topmost;
            UpdateToggles();
            SaveSoon();
        }

        private void OnThemeClick(object sender, RoutedEventArgs e)
        {
            _data.Theme = ThemeManager.NextThemeMode(_data.Theme);
            ApplyTheme();
            SaveSoon();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            _window.WindowState = WindowState.Minimized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            _window.Close();
        }

        private void UpdateToggles()
        {
            _pinButton.Foreground = (Brush)_window.Resources[
                _data.Topmost ? "AccentBrush" : "SubtleBrush"];
            _pinButton.ToolTip = _data.Topmost
                ? "Поверх всех окон: включено"
                : "Поверх всех окон: выключено";
            _themeButton.ToolTip = ThemeManager.DescribeTheme(_data.Theme) +
                " (нажмите, чтобы сменить)" + Environment.NewLine + DescribeGlass();
        }

        /// <summary>Подпись активного стекла — в тултипе кнопки темы (и в журнале).</summary>
        private string DescribeGlass()
        {
            string glass = BackdropSupport.Describe(_plan);

            if (_plan == BackdropMode.Blur && _glass.Enabled)
            {
                glass += " (" + _glass.Status + ")";
            }

            return glass;
        }

        private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents)
            {
                return;
            }

            ApplyGlassBrushes(ThemeManager.ResolveIsLight(_data.Theme));
            SaveSoon();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!_window.Dispatcher.CheckAccess())
            {
                _window.Dispatcher.BeginInvoke(new Action(RefreshSystemTheme));
                return;
            }

            RefreshSystemTheme();
        }

        private void RefreshSystemTheme()
        {
            if (_data.Theme != AppData.ThemeAuto)
            {
                return;
            }

            _lastSystemLight = ThemeManager.IsSystemLight();
            ApplyTheme();
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            RefreshSystemTheme();
        }

        private void OnWatchTimerTick(object sender, EventArgs e)
        {
            string today = Today();
            if (today != _lastDay)
            {
                _lastDay = today;
                RenderTasks();
                SaveSoon();
            }

            bool light = ThemeManager.IsSystemLight();
            if (light != _lastSystemLight)
            {
                _lastSystemLight = light;
                RefreshSystemTheme();
            }

            _glass.Refresh(false);
        }

        private void OnSaveTimerTick(object sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveState();
        }

        private void SaveSoon()
        {
            if (_isClosing)
            {
                return;
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveState()
        {
            try
            {
                if (_window.WindowState == WindowState.Normal)
                {
                    _data.HasBounds = true;
                    _data.Left = _window.Left;
                    _data.Top = _window.Top;
                    _data.Width = _window.Width;
                    _data.Height = _window.Height;
                }

                _data.Topmost = _window.Topmost;
                _data.Opacity = _opacitySlider.Value;
                _storage.Save(_data);
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось сохранить данные", ex);
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            _saveTimer.Stop();
            _watchTimer.Stop();
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _glass.Dispose();
            SaveState();
            Log.Info("Сохранено. Задач: " + _data.Tasks.Count);
        }

        private void UpdateCounters()
        {
            _title.Text = string.Format(_ru, "Сегодня, {0:d MMMM}", DateTime.Now);

            int total = 0;
            int done = 0;
            foreach (TaskItem task in _data.Tasks)
            {
                if (!IsVisible(task))
                {
                    continue;
                }

                total++;
                if (task.Done)
                {
                    done++;
                }
            }

            int remaining = total - done;

            if (total == 0)
            {
                _subtitle.Text = "пока пусто";
            }
            else if (remaining == 0)
            {
                _subtitle.Text = "всё выполнено";
            }
            else
            {
                _subtitle.Text = string.Format("осталось {0} {1}",
                    remaining, Plural(remaining, "задача", "задачи", "задач"));
            }

            _progress.Maximum = total == 0 ? 1 : total;
            _progress.Value = done;

            _footer.Text = total == 0
                ? string.Empty
                : string.Format("выполнено {0} из {1}", done, total);

            ShowEmptyState();
            UpdatePlaceholder();
        }

        private void ShowEmptyState()
        {
            bool empty = _taskPanel.Children.Count == 0;

            if (!empty)
            {
                _emptyText.Visibility = Visibility.Collapsed;
                return;
            }

            _emptyText.Visibility = Visibility.Visible;
            _emptyText.Text = _data.Tasks.Count == 0
                ? "Пока пусто — напишите первую задачу выше ✨"
                : "На сегодня всё чисто 🎉";
        }

        /// <summary>Подпись к невыполненной задаче прошлых дней: «вчера», «3 дня назад».</summary>
        private string DescribeDate(string dateText)
        {
            DateTime date;
            if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
            {
                return string.Empty;
            }

            int days = (int)(DateTime.Today - date.Date).TotalDays;
            if (days <= 0)
            {
                return string.Empty;
            }

            if (days == 1)
            {
                return "вчера";
            }

            if (days <= 30)
            {
                return string.Format("{0} {1} назад", days, Plural(days, "день", "дня", "дней"));
            }

            return date.ToString("d MMMM", _ru);
        }

        private static string Plural(int number, string one, string few, string many)
        {
            int mod100 = number % 100;
            if (mod100 >= 11 && mod100 <= 14)
            {
                return many;
            }

            switch (number % 10)
            {
                case 1:
                    return one;
                case 2:
                case 3:
                case 4:
                    return few;
                default:
                    return many;
            }
        }

        private static void AnimateAppear(FrameworkElement element)
        {
            CubicEase easing = new CubicEase();
            easing.EasingMode = EasingMode.EaseOut;

            DoubleAnimation fade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(170));
            // Базовая прозрачность строки — 1, поэтому старт фейда задаём явно.
            fade.From = 0.0;
            fade.EasingFunction = easing;
            element.BeginAnimation(UIElement.OpacityProperty, fade);

            TranslateTransform offset = new TranslateTransform(0, -8);
            element.RenderTransform = offset;

            DoubleAnimation slide = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            slide.EasingFunction = easing;
            offset.BeginAnimation(TranslateTransform.YProperty, slide);
        }
    }
}
