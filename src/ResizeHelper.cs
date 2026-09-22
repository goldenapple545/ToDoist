using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace ToDoist
{
    /// <summary>
    /// Ручное изменение размера окна. Окно без рамки с AllowsTransparency=True
    /// не имеет системных границ, поэтому ловим мышь по краям карточки сами.
    /// </summary>
    public class ResizeHelper
    {
        private const int Left = 1;
        private const int Right = 2;
        private const int Top = 4;
        private const int Bottom = 8;
        private const double EdgeSize = 7.0;

        /// <summary>Изменение размера закончилось — можно пересчитать размытие.</summary>
        public event EventHandler ResizeEnded;

        private readonly Window _window;
        private readonly FrameworkElement _host;

        private bool _active;
        private int _mode;
        private Point _startPoint;
        private double _startLeft;
        private double _startTop;
        private double _startWidth;
        private double _startHeight;

        public ResizeHelper(Window window, FrameworkElement host)
        {
            _window = window;
            _host = host;

            // Preview — чтобы перетаскивание окна за шапку не мешало изменению размера.
            _host.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            _host.PreviewMouseLeftButtonUp += OnPreviewMouseUp;
            _host.MouseMove += OnMouseMove;
            _host.MouseLeave += OnMouseLeave;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            int mode = HitTest(e.GetPosition(_host));
            if (mode == 0)
            {
                return;
            }

            _active = true;
            _mode = mode;
            _startPoint = e.GetPosition(_window);
            _startLeft = _window.Left;
            _startTop = _window.Top;
            _startWidth = _window.Width;
            _startHeight = _window.Height;

            _host.CaptureMouse();
            e.Handled = true;
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _mode = 0;
            _host.ReleaseMouseCapture();
            Raise(ResizeEnded);
            e.Handled = true;
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_active)
            {
                ApplyResize(e.GetPosition(_window));
                return;
            }

            Mouse.OverrideCursor = CursorFor(HitTest(e.GetPosition(_host)));
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_active)
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ApplyResize(Point current)
        {
            double dx = current.X - _startPoint.X;
            double dy = current.Y - _startPoint.Y;

            double left = _startLeft;
            double top = _startTop;
            double width = _startWidth;
            double height = _startHeight;

            if ((_mode & Right) != 0)
            {
                width = _startWidth + dx;
            }

            if ((_mode & Bottom) != 0)
            {
                height = _startHeight + dy;
            }

            if ((_mode & Left) != 0)
            {
                width = _startWidth - dx;
                left = _startLeft + dx;
            }

            if ((_mode & Top) != 0)
            {
                height = _startHeight - dy;
                top = _startTop + dy;
            }

            if (width < _window.MinWidth)
            {
                if ((_mode & Left) != 0)
                {
                    left -= _window.MinWidth - width;
                }
                width = _window.MinWidth;
            }

            if (height < _window.MinHeight)
            {
                if ((_mode & Top) != 0)
                {
                    top -= _window.MinHeight - height;
                }
                height = _window.MinHeight;
            }

            _window.Width = width;
            _window.Height = height;

            if ((_mode & Left) != 0)
            {
                _window.Left = left;
            }

            if ((_mode & Top) != 0)
            {
                _window.Top = top;
            }
        }

        private int HitTest(Point point)
        {
            int mode = 0;

            if (point.X <= EdgeSize)
            {
                mode |= Left;
            }
            else if (point.X >= _host.ActualWidth - EdgeSize)
            {
                mode |= Right;
            }

            if (point.Y <= EdgeSize)
            {
                mode |= Top;
            }
            else if (point.Y >= _host.ActualHeight - EdgeSize)
            {
                mode |= Bottom;
            }

            return mode;
        }

        private static Cursor CursorFor(int mode)
        {
            switch (mode)
            {
                case Left:
                case Right:
                    return Cursors.SizeWE;
                case Top:
                case Bottom:
                    return Cursors.SizeNS;
                case Left | Top:
                case Right | Bottom:
                    return Cursors.SizeNWSE;
                case Right | Top:
                case Left | Bottom:
                    return Cursors.SizeNESW;
                default:
                    return null;
            }
        }
    }
}
