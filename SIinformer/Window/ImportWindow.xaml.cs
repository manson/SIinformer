using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using SIinformer.Logic;

namespace SIinformer.Window
{
    /// <summary>
    /// Interaction logic for ImportWindow.xaml
    /// </summary>
    public partial class ImportWindow
    {
        private List<EncodingItem> EncodingItems = null;
        //Regex regex = new Regex(
        //  "(?<Protocol>\\w+):\\/\\/(?<Domain>[\\w@][\\w.:@]+)\\/?[\\w\\." +
        //  "?=%&=\\-@/$,]*",
        //RegexOptions.IgnoreCase
        //| RegexOptions.CultureInvariant
        //| RegexOptions.IgnorePatternWhitespace
        //| RegexOptions.Compiled
        //);

        Regex regex = new Regex(
      "http://([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*\\(\\)_\\-\\=\\+\\\\\\/\\?\\.\\:\\;\\'\\,]*)?",
      RegexOptions.IgnoreCase
      | RegexOptions.CultureInvariant
      | RegexOptions.IgnorePatternWhitespace
      | RegexOptions.Compiled
      );
                

      //  Regex regex = new Regex(
      //      @"(?\w+):\/\/(?[\w@][\w.:@]+)\/?[\w\.?=%&=\-@/$,]*|(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=]*)?",
      //RegexOptions.IgnoreCase
      //| RegexOptions.CultureInvariant
      //| RegexOptions.IgnorePatternWhitespace
      //| RegexOptions.Compiled
      //);
        

        public ImportWindow()
        {
            InitializeComponent();
            EncodingItems = new List<EncodingItem>();
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.Default, Name = "Default" });
            EncodingItems.Add(new EncodingItem(){ Encoding = Encoding.Unicode, Name = "Unicode"});                        
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.UTF8, Name = "UTF8" });
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.UTF32, Name = "UTF32" });
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.UTF7, Name = "UTF7" });
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.BigEndianUnicode, Name = "BigEndianUnicode" });            
            EncodingItems.Add(new EncodingItem() { Encoding = Encoding.ASCII, Name = "ASCII" });
            comboEncoding.ItemsSource = EncodingItems;
            comboEncoding.SelectedIndex = 0;
        }

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in listURLs.Items)
            {
                string url = item.ToString();
                lblStatus.Text = "Импорт " + url + "...";
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
                InfoUpdater.AddAuthor(url);
            }
            lblStatus.Text = "Импорт завершен.";
        }


        private void ButtonSelectFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = "";
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ""; // Default file extension
            dlg.Filter = ""; // Filter files by extension
            Nullable<bool> result = dlg.ShowDialog();
            // Process open file dialog box results
            if (result == true)
            {
                lblFile.Text = dlg.FileName;
            }

        }

        private void ButtonRead_Click(object sender, RoutedEventArgs e)
        {
            listURLs.Items.Clear();
            if (lblFile.Text.Trim()=="" || !File.Exists(lblFile.Text.Trim())) return;
            lblStatus.Text = "Чтение файла и анализ...";
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
            string data = "";

            try
            {
                data = File.ReadAllText(lblFile.Text.Trim(), ((EncodingItem)comboEncoding.SelectedItem).Encoding);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка чтения: " + ex.Message;
                return;
            }
            MatchCollection ms = regex.Matches(data);

            foreach (Match match in ms)
            {
                string url = match.Value.ToLower();
                if (url.Contains("?"))                
                    url = url.Substring(0, url.IndexOf("?"));
                if (url.Contains("&"))
                    url = url.Substring(0, url.IndexOf("&"));
                if (url.Contains("indexdate.shtml"))
                    url = url.Substring(0, url.IndexOf("indexdate.shtml"));

                listURLs.Items.Add(url);
            }
            lblStatus.Text = "Анализ окончен.";
        }


        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (listURLs.Items.Count==0 || listURLs.SelectedIndex==-1) return;
            listURLs.Items.Remove(listURLs.SelectedItem);
        }

    }

    public class EncodingItem
    {
        public string Name { get; set; }
        public Encoding Encoding { get; set; }
    }
}
