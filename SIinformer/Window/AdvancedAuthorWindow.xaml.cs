using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using SIinformer.Logic;
using SIinformer.Utils;
using Cursor=System.Windows.Input.Cursor;
using Cursors=System.Windows.Input.Cursors;
using MouseEventArgs=System.Windows.Input.MouseEventArgs;

namespace SIinformer.Window
{
    /// <summary>
    /// Логика взаимодействия для AdvancedAuthorWindow.xaml
    /// </summary>
    public partial class AdvancedAuthorWindow
    {
        #region Private Fields

        /// <summary>
        /// Положение относительно формы
        /// </summary>
        private Rect _desiredRect;
        private readonly Setting _setting;
        private Author _author;

        #endregion

        public AdvancedAuthorWindow(Setting setting, Logger logger)
        {
            InitializeComponent();
            authorPanel.SetSetting(setting, logger);
            authorPanel.SplitterChanged += AuthorPanelSplitterChanged;
            _setting = setting;
        }

        #region Загрузка, выгрузка, привязки к Owner

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Owner.SizeChanged += WindowSizeChanged;
            Owner.LocationChanged += WindowLocationChanged;
            Resize();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Owner.SizeChanged -= WindowSizeChanged;
            Owner.LocationChanged -= WindowLocationChanged;
        }

        private void WindowLocationChanged(object sender, EventArgs e)
        {
            Resize();
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Resize();
        }

        #endregion

        #region Логика подгонки размера

        private AdvancedAuthorWindowPosition _advancedAuthorWindowPosition = AdvancedAuthorWindowPosition.Right;
        private bool _manualChange;

        private void Resize()
        {
            if (_author==null) return;
            Rect rectListBox = VisualTreeHelper.GetDescendantBounds(((MainWindow)Owner).AuthorsListBox);
            if (rectListBox == Rect.Empty) return;
            double rectListBoxLeft = Owner.PointFromScreen(((MainWindow)Owner).AuthorsListBox.PointToScreen(rectListBox.Location)).X;
            // уточнить ширину по размеру listbox, может быть полоса прокрутки
            var correct = new Rect(rectListBoxLeft, _desiredRect.Location.Y,
                                   ((MainWindow) Owner).AuthorsListBox.ActualWidth, _desiredRect.Height);
            // пересчитать в экранный прямоугольник относительно экрана
            var rect = new Rect(Owner.PointToScreen(correct.Location), correct.Size);
            var working = SystemInformation.WorkingArea;
            // расстояние слева и справа
            var deltaRigth = working.Right - rect.Right;
            var deltaLeft = rect.Left - working.Left;
            // в автоматическом режиме пересчитывается сторона окна
            switch (_advancedAuthorWindowPosition)
            {
                case AdvancedAuthorWindowPosition.Left:
                    if ((deltaLeft < _setting.GetAdvancedWindowSetting(_author).Size.Width) && (deltaRigth > deltaLeft))
                        _advancedAuthorWindowPosition = AdvancedAuthorWindowPosition.Right;
                    break;
                case AdvancedAuthorWindowPosition.Right:
                    if ((deltaRigth < _setting.GetAdvancedWindowSetting(_author).Size.Width) && (deltaLeft > deltaRigth))
                        _advancedAuthorWindowPosition = AdvancedAuthorWindowPosition.Left;
                    break;
            }

            if (_setting.DesiredPositionAdvancedWindow == DesiredPositionAdvancedWindow.Right)
                _advancedAuthorWindowPosition = AdvancedAuthorWindowPosition.Right;
            if (_setting.DesiredPositionAdvancedWindow == DesiredPositionAdvancedWindow.Left)
                _advancedAuthorWindowPosition = AdvancedAuthorWindowPosition.Left;

            double left = Left;
            double height = Height;
            double width = Width;

            // пересчитываем положение и размеры
            if (_setting.DesiredPositionAdvancedWindow == DesiredPositionAdvancedWindow.Auto)
            {
                // по ширине
                switch (_advancedAuthorWindowPosition)
                {
                    case AdvancedAuthorWindowPosition.Left:
                        left = Math.Max(0, rect.Left - _setting.GetAdvancedWindowSetting(_author).Size.Width);
                        width = rect.Left - left;
                        break;
                    case AdvancedAuthorWindowPosition.Right:
                        left = rect.Right;
                        width = Math.Min(_setting.GetAdvancedWindowSetting(_author).Size.Width, working.Right - left);
                        break;
                }
            }
            else
            {
                // по ширине
                switch (_advancedAuthorWindowPosition)
                {
                    case AdvancedAuthorWindowPosition.Left:
                        left = rect.Left - _setting.GetAdvancedWindowSetting(_author).Size.Width;
                        break;
                    case AdvancedAuthorWindowPosition.Right:
                        left = rect.Right;
                        width = _setting.GetAdvancedWindowSetting(_author).Size.Width;
                        break;
                }               
            }

            // по высоте
            double top = rect.Top;
            if (top + _setting.GetAdvancedWindowSetting(_author).Size.Height > working.Height) top = working.Height - height;
            if (top + height < rect.Bottom) top = rect.Bottom - height;

            // присваиваем вручную, _manualChange нужно, чтобы Window_SizeChanged
            // не изменил желаемые размеры окна, установленные мышью
            _manualChange = true;
            Top = top;
            Height = height;
            Width = width;
            Left = left;
            _manualChange = false;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_manualChange)
            {
                _setting.SetAdvancedWindowSizeSetting(_author,new Size(e.NewSize.Width, e.NewSize.Height));
            }
        }

        private void AuthorPanelSplitterChanged(Author author, double heightComment)
        {
            if (!_manualChange)
            {
                _setting.SetAdvancedWindowHeightCommentSetting(_author, heightComment);
            }
        }

        #endregion

        #region Управление размером

        private CursorPoint _captureScreenCursorPos;
        private Point _captureWindowPosition;
        private Size _captureWindowSize;
        private bool _isResize;

        private void DarkWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((Cursor == Cursors.SizeNESW) || (Cursor == Cursors.SizeNWSE))
            {
                _isResize = true;
                GetCursorPos(out _captureScreenCursorPos);
                _captureWindowSize = new Size(Width, Height);
                _captureWindowPosition = new Point(Left, Top);
                Mouse.Capture(this);
            }
        }

        private void DarkWindow_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isResize = false;
            if (Mouse.Captured == this) Mouse.Capture(null);
        }

        private void DarkWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            const int delta = 10;
            if (_isResize)
            {
                CursorPoint currentScreenCursorPos;
                GetCursorPos(out currentScreenCursorPos);
                if (Cursor == Cursors.SizeNWSE)
                {
                    Width = Math.Max(100,
                                     _captureWindowSize.Width + (currentScreenCursorPos.X - _captureScreenCursorPos.X));
                    Height = Math.Max(100,
                                      _captureWindowSize.Height + (currentScreenCursorPos.Y - _captureScreenCursorPos.Y));
                }
                if (Cursor == Cursors.SizeNESW)
                {
                    Rect rectListBox = VisualTreeHelper.GetDescendantBounds(((MainWindow) Owner).AuthorsListBox);
                    double rectListBoxLeft = ((MainWindow) Owner).AuthorsListBox.PointToScreen(rectListBox.Location).X;
                    Width = Math.Max(100,
                                     _captureWindowSize.Width + (_captureScreenCursorPos.X - currentScreenCursorPos.X));
                    Left = Math.Min(rectListBoxLeft - Width,
                                    _captureWindowPosition.X - (_captureScreenCursorPos.X - currentScreenCursorPos.X));
                    Height = Math.Max(100,
                                      _captureWindowSize.Height + (currentScreenCursorPos.Y - _captureScreenCursorPos.Y));
                }
            }
            else
            {
                Point currentCursorPos = e.GetPosition(this);
                Cursor tempCursor = null;
                if ((currentCursorPos.X < delta) && (currentCursorPos.Y > ActualHeight - delta) &&
                    (_advancedAuthorWindowPosition == AdvancedAuthorWindowPosition.Left))
                    tempCursor = Cursors.SizeNESW;
                if ((currentCursorPos.X > ActualWidth - delta) && (currentCursorPos.Y > ActualHeight - delta) &&
                    (_advancedAuthorWindowPosition == AdvancedAuthorWindowPosition.Right))
                    tempCursor = Cursors.SizeNWSE;
                if (tempCursor != null)
                    Cursor = tempCursor;
                else ClearValue(CursorProperty);
            }
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Перестраивает окно
        /// </summary>
        /// <param name="author">Автор</param>
        /// <param name="rect">Положение относительно формы</param>
        public void ReBuild(Author author, Rect rect)
        {
            _author = author;
            authorPanel.Build(author, _setting.GetAdvancedWindowSetting(author).HeightComment);
            _desiredRect = rect;
            // присваиваем вручную, _manualChange нужно, чтобы Window_SizeChanged
            // не изменил желаемые размеры окна, установленные мышью
            _manualChange = true;
            Width = _setting.GetAdvancedWindowSetting(author).Size.Width;
            Height = _setting.GetAdvancedWindowSetting(author).Size.Height;
            _manualChange = false;
            Resize();
        }

        #endregion

        #region Win32

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out CursorPoint lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct CursorPoint
        {
            public int X;
            public int Y;

            public CursorPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static implicit operator System.Drawing.Point(CursorPoint p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }

            public static implicit operator CursorPoint(System.Drawing.Point p)
            {
                return new CursorPoint(p.X, p.Y);
            }
        }

        #endregion

        #region Nested type: AdvancedAuthorWindowPosition

        internal enum AdvancedAuthorWindowPosition
        {
            Left,
            Right
        }

        #endregion

        #region Обработка событий формы
        
        private void DarkWindow_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key==Key.Escape)
            {
                Visibility = Visibility.Hidden;
                Owner.Activate();
            }
        }

        #endregion
    }
}