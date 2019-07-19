using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SIinformer.ApiStuff;
using SIinformer.Window;

namespace SIinformer.Controls
{
    /// <summary>
    /// Interaction logic for ToolBar.xaml
    /// </summary>
    public partial class ToolBar : UserControl
    {
        public ToolBar()
        {
            InitializeComponent();
        }


        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainForm.ShowSettingsWindow();
        }


       
        private void FindSimilarInformersButton_Click(object sender, RoutedEventArgs e)
        {
            var apiUrl = ApiManager.GetInstance().GetApiUrl();
            var similarInformers = apiUrl.Replace("api.ashx", string.Format("Home/SimilarInformers?informerId={0}", MainWindow.GetSettings().ClientId));
            OpenURL(similarInformers);
        }

        private void FindAuthorsButton_Click(object sender, RoutedEventArgs e)
        {
             var apiUrl = ApiManager.GetInstance().GetApiUrl();
             var similarAuthors = apiUrl.Replace("api.ashx", string.Format("Home/SearchAuthorsByInterest?informerId={0}&informerSimilarityThreshold=50&filterByPopularity=true", MainWindow.GetSettings().ClientId));
            OpenURL(similarAuthors);
            
        }

        private void FindBooksButton_Click(object sender, RoutedEventArgs e)
        {
            var apiUrl = ApiManager.GetInstance().GetApiUrl();
            var findBooks = apiUrl.Replace("api.ashx", string.Format("Home/SearchBooks"));
            OpenURL(findBooks);

        }

        public static void OpenURL(string url)
        {
            var pts = new ParameterizedThreadStart(_OpenUrl);           
            var thread = new Thread(pts) { IsBackground = true };
            thread.Start(url);
        }
        private static void _OpenUrl(object obj)
        {
            try
            {
                Process.Start((string)obj);
            }
            catch
            { }
        }

    }
}
