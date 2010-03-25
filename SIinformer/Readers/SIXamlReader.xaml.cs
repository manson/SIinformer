using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using SIinformer.Utils;

namespace SIinformer.Readers
{
    /// <summary>
    /// Interaction logic for XamlReader.xaml
    /// </summary>
    public partial class SIXamlReader
    {
        public SIXamlReader(Setting setting)
        {
            InitializeComponent();
            DataContext = setting;
        }

        public void ShowReader(string xamlFileName, string title)
        {
            string xamlText = File.ReadAllText(xamlFileName, Encoding.GetEncoding(1251));
            StringReader input = new StringReader(xamlText);
            XmlTextReader reader = new XmlTextReader(input);
            FlowDocument content = System.Windows.Markup.XamlReader.Load(reader) as FlowDocument;
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