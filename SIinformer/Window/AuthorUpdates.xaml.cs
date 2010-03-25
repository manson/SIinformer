using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using SIinformer.Logic;
using SIinformer.Utils;
using KeyEventArgs=System.Windows.Input.KeyEventArgs;

namespace SIinformer.Window
{
    /// <summary>
    /// Interaction logic for AuthorUpdates.xaml
    /// </summary>
    public partial class AuthorUpdates
    {
        public static List<AuthorUpdates> OpenedWindows = new List<AuthorUpdates>();

        private readonly Setting _setting;
        private readonly Author _author;

        public AuthorUpdates(Author author, Setting setting, Logger logger)
        {
            InitializeComponent();

            _author = author;
            _setting = setting;

            AuthorWindowSetting authorWindowSetting = _setting.GetAuthorWindowSetting(author);
            Width = authorWindowSetting.Size.Width;
            Height = authorWindowSetting.Size.Height;
            Left = authorWindowSetting.Location.X;
            Top = authorWindowSetting.Location.Y;

            SizeChanged += AuthorUpdatesSizeChanged;
            LocationChanged += AuthorUpdatesLocationChanged;

            authorPanel.SetSetting(setting, logger);
            authorPanel.Build(author, authorWindowSetting.HeightComment);
            authorPanel.SplitterChanged += AuthorPanelSplitterChanged;

            Title = author.Name;
            AuthorUpdateDate.Content = "Состояние текстов автора на дату: " + author.UpdateDate.ToShortDateString();

        }

        private void AuthorUpdatesLocationChanged(object sender, EventArgs e)
        {
            _setting.SetAuthorWindowLocationSetting(_author, new Point(Left, Top));
        }

        private void AuthorUpdatesSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _setting.SetAuthorWindowSizeSetting(_author, e.NewSize);
        }

        private void AuthorPanelSplitterChanged(Author author, double heightcomment)
        {
            _setting.SetAuthorWindowHeightCommentSetting(_author, heightcomment);
        }

        public static AuthorUpdates FindWindow(Author author)
        {
            foreach (AuthorUpdates au in OpenedWindows)
                if (au.authorPanel.Author == author) return au;
            return null;
        }

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            OpenedWindows.Remove(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OpenedWindows.Add(this);
        }

        private void Minimize_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}