using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SIinformer.Window
{
    public class DarkWindow:System.Windows.Window
    {
        public static readonly DependencyProperty CloseButtonVisibilityProperty;
        public static readonly DependencyProperty MaximizeButtonVisibilityProperty;
        public static readonly DependencyProperty MinimizeButtonVisibilityProperty;
        public static readonly DependencyProperty TitleAlignmentProperty;
        private Label _titleLabel;

        static DarkWindow()
        {
            FrameworkPropertyMetadata fpm = new FrameworkPropertyMetadata(HorizontalAlignment.Left);
            TitleAlignmentProperty = DependencyProperty.Register("TitleAlignment", typeof (HorizontalAlignment),
                                                                 typeof (DarkWindow), fpm);
            fpm = new FrameworkPropertyMetadata(Visibility.Visible);
            CloseButtonVisibilityProperty = DependencyProperty.Register("CloseButtonVisibility", typeof (Visibility),
                                                                        typeof (DarkWindow), fpm);
            fpm = new FrameworkPropertyMetadata(Visibility.Visible);
            MinimizeButtonVisibilityProperty = DependencyProperty.Register("MinimizeButtonVisibility",
                                                                           typeof (Visibility), typeof (DarkWindow), fpm);
            fpm = new FrameworkPropertyMetadata(Visibility.Visible);
            MaximizeButtonVisibilityProperty = DependencyProperty.Register("MaximizeButtonVisibility",
                                                                           typeof (Visibility), typeof (DarkWindow), fpm);
        }

        public DarkWindow()
        {
            Activated += DarkWindowActivated;
            Loaded += DarkWindowLoaded;
            Deactivated += DarkWindowDeactivated;
        }

        public HorizontalAlignment TitleAlignment
        {
            get { return (HorizontalAlignment) GetValue(TitleAlignmentProperty); }
            set { SetValue(TitleAlignmentProperty, value); }
        }

        public Visibility CloseButtonVisibility
        {
            get { return (Visibility) GetValue(CloseButtonVisibilityProperty); }
            set { SetValue(CloseButtonVisibilityProperty, value); }
        }

        public Visibility MinimizeButtonVisibility
        {
            get { return (Visibility) GetValue(MinimizeButtonVisibilityProperty); }
            set { SetValue(MinimizeButtonVisibilityProperty, value); }
        }

        public Visibility MaximizeButtonVisibility
        {
            get { return (Visibility) GetValue(MaximizeButtonVisibilityProperty); }
            set { SetValue(MaximizeButtonVisibilityProperty, value); }
        }

        public static DependencyObject FindControl(DependencyObject element, string name)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject o = VisualTreeHelper.GetChild(element, i);
                FrameworkElement e = o as FrameworkElement;
                if (e != null && e.Name == name)
                {
                    return o;
                }
                o = FindControl(o, name);
                if (o != null) return o;
            }
            return null;
        }

        private void DarkWindowLoaded(object sender, RoutedEventArgs e)
        {
            _titleLabel = (Label) FindControl(this, "TitleLabel");
        }

        private void DarkWindowDeactivated(object sender, EventArgs e)
        {
            if (_titleLabel != null)
                _titleLabel.Opacity = 0.25;
        }

        private void DarkWindowActivated(object sender, EventArgs e)
        {
            if (_titleLabel != null)
            {
                _titleLabel.Opacity = 1;
            }
        }
    }
}