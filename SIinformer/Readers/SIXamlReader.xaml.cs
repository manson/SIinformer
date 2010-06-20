using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using SIinformer.Utils;
using SIinformer.Logic;
using System.Threading;
using System.Windows.Data;
using System;

namespace SIinformer.Readers
{
    /// <summary>
    /// Interaction logic for XamlReader.xaml
    /// </summary>
    public partial class SIXamlReader
    {
        /// <summary>
        /// контент книги
        /// </summary>
        FlowDocument content = null;
        /// <summary>
        /// контент различий книги
        /// </summary>
        FlowDocument diff_content = null;
        /// <summary>
        /// таймер проверки, сформировался ли файл различий, так как это процесс небыстрый
        /// </summary>
        //Timer check_timer = null;

        DownloadTextItem _downloadTextItem = null;

        bool firstPage = true;

        public SIXamlReader(Setting setting)
        {
            InitializeComponent();
            DataContext = setting;
        }

        public void ShowReader(string xamlFileName, DownloadTextItem downloadTextItem )
        {
            _downloadTextItem = downloadTextItem;
            SwitchTextsButton.DataContext = downloadTextItem.AuthorText; // для байндинга енейблинга или нет кнопки
            downloadTextItem.AuthorText.UpdateHasDiff(downloadTextItem.GetAuthor()); // проверим наличие файла различий, если он есть кнопка показа автоматом разблокируется

            string title = downloadTextItem.AuthorText.Name;
            string xamlText = File.ReadAllText(xamlFileName, Encoding.GetEncoding(1251));
            StringReader input = new StringReader(xamlText);
            XmlTextReader reader = new XmlTextReader(input);
            content = System.Windows.Markup.XamlReader.Load(reader) as FlowDocument;

            if (content != null)
            {
                XamlReaderControl.Document = content;
                XamlReaderControl.Document.ColumnWidth = 800;
            }
            reader.Close();
            Title = title;            
            Show();
            //if (XamlReaderControl.CanGoToPage(2))
            //    NavigationCommands.GoToPage.Execute(2, XamlReaderControl);
        }

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove(); 
        }

        #region События кнопок окна

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

        #endregion

        private void SwitchTextsButton_Click(object sender, RoutedEventArgs e)
        {
            if (firstPage)
            {
                if (diff_content == null)
                {
                    StringReader input = new StringReader(_downloadTextItem.DiffXaml);
                    XmlTextReader reader = new XmlTextReader(input);
                    diff_content = System.Windows.Markup.XamlReader.Load(reader) as FlowDocument;
                    XamlReaderControl.Document = diff_content;
                    XamlReaderControl.Document.ColumnWidth = 800;
                }else
                    XamlReaderControl.Document = diff_content;
                firstPage = false;
            }
            else
            {
                XamlReaderControl.Document = content;                
                firstPage = true;
            }
        }
    }


    public class VisibilityConverter : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }


        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    public class MyFlowDocumentReader:FlowDocumentReader
    {
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            switch (e.Property.Name)
            {
                case "Zoom":
                    Setting settingzoom = (Setting)DataContext;
                    settingzoom.FlowDocumentZoom = (double)e.NewValue;
                    break;
                case "ViewingMode":
                    Setting settingmode = (Setting)DataContext;
                    settingmode.FlowDocumentReaderViewingMode = (FlowDocumentReaderViewingMode) e.NewValue;
                    break;
            }
        }
    }
}