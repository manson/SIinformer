using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SIinformer.Readers;
using SIinformer.Utils;
using SIinformer.Logic;

namespace SIinformer.Window
{
    /// <summary>
    /// Логика взаимодействия для SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow
    {        
        public SettingWindow()
        {
            InitializeComponent();

            currentCacheSize.Text = "???/";
            BackgroundWorker worker = new BackgroundWorker();
            worker.RunWorkerCompleted += ((o, e) =>
            {
                if (e.Error==null)
                {
                    currentCacheSize.Text = e.Result.ToString()+"/";
                }
            });
            worker.DoWork += ((o, e) =>
            {
                long size = 0;
                if (Directory.Exists(DownloadTextHelper.BooksPath()))
                {
                    string[] files = Directory.GetFiles(DownloadTextHelper.BooksPath(), "*.*", SearchOption.AllDirectories);
                    foreach (string s in files)
                    {
                        size += (new FileInfo(s)).Length;
                    }
                }
                e.Result = size/1024/1024;
            });
            worker.RunWorkerAsync();
        }

        public SettingWindow(Setting setting) : this()
        {
            Original = setting;
            Result = new Setting();
            Result.PartialCopy(Original);
            DataContext = Result;            
        }

        private Setting Result { get; set; }
        private Setting Original { get; set; }

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
        }

        public new Setting ShowDialog()
        {
            if (base.ShowDialog()==true)
            {
                Original.PartialCopy(Result);
            }
            return Original;
        }

        private void ButtonSelectRss_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
                                        {
                                            Filter = "RSS и XML|*.rss;*.xml|RSS-ленты|*.rss|XML-файлы|*.xml|Все файлы|*.*",
                                            OverwritePrompt = true,
                                            ValidateNames = true,
                                            InitialDirectory =
                                                (Result.RSSFileName.Trim() != "")
                                                    ? Path.GetDirectoryName(Result.RSSFileName.Trim())
                                                    : AppDomain.CurrentDomain.BaseDirectory,
                                            Title = "Выберите имя RSS-ленты"
                                        };
            if (dialog.ShowDialog() == true)
                Result.RSSFileName = dialog.FileName;
        }

        private void AfterUpdaterButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
                                        {
                                            CheckFileExists = true,
                                            Filter =
                                                "Исполняемые файлы(*.exe;*.com;*.bat;*.cmd;*.js;*.vbs)|*.exe;*.com;*.bat;*.cmd;*.js;*.vbs|Все файлы(*.*)|*.*",
                                            ValidateNames = true,
                                            InitialDirectory =
                                                (Result.AfterUpdater.Trim() != "")
                                                    ? Path.GetDirectoryName(Result.AfterUpdater.Trim())
                                                    : AppDomain.CurrentDomain.BaseDirectory,
                                            Title = "Выберите запускаемый файл"
                                        };
            if (dialog.ShowDialog() == true)
                Result.AfterUpdater = dialog.FileName;
        }

        private void BeforeUpdaterButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
                                        {
                                            CheckFileExists = true,
                                            Filter =
                                                "Исполняемые файлы(*.exe;*.com;*.bat;*.cmd;*.js;*.vbs)|*.exe;*.com;*.bat;*.cmd;*.js;*.vbs|Все файлы(*.*)|*.*",
                                            ValidateNames = true,
                                            InitialDirectory =
                                                (Result.BeforeUpdater.Trim() != "")
                                                    ? Path.GetDirectoryName(Result.BeforeUpdater.Trim())
                                                    : AppDomain.CurrentDomain.BaseDirectory,
                                            Title = "Выберите запускаемый файл"
                                        };
            if (dialog.ShowDialog() == true)
                Result.BeforeUpdater = dialog.FileName;
        }

        private void AlternativeReaderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter =
                    "Исполняемые файлы(*.exe;*.com;*.bat;*.cmd;*.js;*.vbs)|*.exe;*.com;*.bat;*.cmd;*.js;*.vbs|Все файлы(*.*)|*.*",
                ValidateNames = true,
                InitialDirectory =
                    (Result.AlternativeReader.Trim() != "")
                        ? Path.GetDirectoryName(Result.AlternativeReader.Trim())
                        : AppDomain.CurrentDomain.BaseDirectory,
                Title = "Выберите другую читалку"
            };
            if (dialog.ShowDialog() == true)
                Result.AlternativeReader = dialog.FileName;
        }

        private void BookConverterButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter =
                    "Исполняемые файлы(*.exe;*.com;*.bat;*.cmd;*.js;*.vbs)|*.exe;*.com;*.bat;*.cmd;*.js;*.vbs|Все файлы(*.*)|*.*",
                ValidateNames = true,
                InitialDirectory =
                    (Result.BookConverter.Trim() != "")
                        ? Path.GetDirectoryName(Result.BookConverter.Trim())
                        : AppDomain.CurrentDomain.BaseDirectory,
                Title = "Выберите конвертер"
            };
            if (dialog.ShowDialog() == true)
                Result.BookConverter = dialog.FileName;
        }

        private void ButtonMarkAuthorsAsRead_Click(object sender, RoutedEventArgs e)
        {
            foreach (var author in InfoUpdater.Authors)            
                author.IsNew = false;
            
            MessageBox.Show("Все авторы помечены как прочитанные.", "Сообщение");
        }

        private void ButtonReadLinksFromExternalFile_Click(object sender, RoutedEventArgs e)
        {
            ImportWindow iw = new ImportWindow();
            iw.ShowDialog();
        }
    }
}