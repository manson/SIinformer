using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HTMLConverter;
using SIinformer.Logic;
using SIinformer.Utils;

namespace SIinformer.Readers
{
    public static class DownloadTextHelper
    {
        private static Logger _logger;
        private static ListBox _mainListBox;

        public static ObservableCollection<DownloadTextItem> DownloadTextItems =
            new ObservableCollection<DownloadTextItem>();

        /// <summary>
        /// Пытается добавить текст автора в очередь закачки.
        /// Если уже есть в кеше, то не добавляет, а возвращает из кеша.
        /// </summary>
        /// <param name="author">Автор</param>
        /// <param name="text">Текст</param>
        /// <returns>Элемент очереди, либо из кеша</returns>
        public static DownloadTextItem Add(Author author, AuthorText text)
        {
            foreach (DownloadTextItem downloadTextItem in DownloadTextItems)
            {
                if (downloadTextItem.GetHashCode() == (new DownloadTextItem(author, text)).GetHashCode())
                    return downloadTextItem;
            }
            DownloadTextItem item = new DownloadTextItem(author, text) {Logger = _logger};
            if (item.Text == null)
                DownloadTextItems.Add(item);
            ChangeVisibility();
            return item;
        }

        private static void ChangeVisibility()
        {
            _mainListBox.Visibility = DownloadTextItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public static void Remove(DownloadTextItem item)
        {
            DownloadTextItems.Remove(item);
            ChangeVisibility();
        }

        public static void Init(ListBox mainListBox, Logger logger, Setting setting)
        {
            _mainListBox = mainListBox;
            _logger = logger;
            if (Directory.Exists(BooksPath()))
            {
                Thread thread = new Thread(ClearCache) { IsBackground = true };
                thread.Start(setting.MaxCacheSize*1024*1024);
            }
        }

        private static void ClearCache(object obj)
        {
            string[] files = Directory.GetFiles(BooksPath(), "*.*",
                                                SearchOption.AllDirectories);
            List<FileComparerInfo> fileList =
                new List<FileComparerInfo>(files.Length);
            long size = 0;
            foreach (string fileName in files)
            {
                FileInfo fileInfo = new FileInfo(fileName);
                FileComparerInfo fileComparerInfo = new FileComparerInfo
                                                        {
                                                            DateTime = fileInfo.LastWriteTime,
                                                            Length = fileInfo.Length,
                                                            FileInfo = fileInfo
                                                        };
                fileList.Add(fileComparerInfo);
                size += fileComparerInfo.Length;
            }
            fileList.Sort((x, y) => Math.Sign((x.DateTime - y.DateTime).Ticks));
            long cacheSize = (long) obj;
            if (cacheSize != 0)
            {
                while ((fileList.Count > 0) && (size > cacheSize))
                {
                    size -= fileList[0].Length;
                    fileList[0].FileInfo.Delete();
                    fileList.RemoveAt(0);
                }
            }
        }

        public static string BooksPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Books");
        }

        #region Nested type: FileComparerInfo

        private class FileComparerInfo
        {
            public DateTime DateTime { get; set; }
            public long Length { get; set; }
            public FileInfo FileInfo { get; set; }
        }

        #endregion
    }

    public class DownloadTextItem : BindableObject
    {
        private readonly Author _author;
        private long _bytesReceived;
        private WebClient _client;
        private bool _stop;
        private string _xaml;

        public override int GetHashCode()
        {
            return _author.GetHashCode() ^ AuthorText.GetHashCode();
        }

        /// <summary>
        /// Конструктор (при нахождении файла в кеше устанавливается свойство Text
        /// </summary>
        /// <param name="author">Автор</param>
        /// <param name="text">Текст</param>
        public DownloadTextItem(Author author, AuthorText text)
        {
            _author = author;
            AuthorText = text;
            string fileName = GetCachedFileName();
            if (File.Exists(fileName))
                Text = File.ReadAllText(fileName, Encoding.GetEncoding(1251));
            BytesReceived = 0;
        }

        public Logger Logger { get; set; }

        public AuthorText AuthorText { get; set; }

        public long BytesReceived
        {
            get { return _bytesReceived; }
            set
            {
                if (value != _bytesReceived)
                {
                    _bytesReceived = value;
                    RaisePropertyChanged("BytesReceived");
                }
            }
        }

        public string AuthorName
        {
            get { return _author.Name; }
        }

        public string AuthorTextName
        {
            get { return AuthorText.Name; }
        }

        public string BytesFull
        {
            get { return AuthorText.Size.ToString(); }
        }

        public string Text { get; private set; }

        public string Xaml
        {
            get
            {
                long start = Environment.TickCount;

                // формируем xaml страничку для чтения
                if (_xaml == null)
                {
                    _xaml = HtmlToXamlConverter.ConvertHtmlToXaml(Text, true).
                        Replace("----------------------", "").
                        Replace("<Run Foreground=\"Black\">", "<Run>").
                        Replace("другие произведения.</Hyperlink>", "</Hyperlink>");
                }

                Debug.WriteLine("xaml " + (Environment.TickCount - start));

                return _xaml;
            }
        }

        public int? ReaderType { get; set; }

        public void Start()
        {
            if (_isDownloading) return;
            _isDownloading = true;
            _client = null;
            _stop = false;
            Logger.Add(string.Format("Запущена закачка книги '{0}'.", AuthorText.Name));
            WEB.DownloadPageSilent(AuthorText.GetFullLink(_author), Progress, Complete);
        }

        /// <summary>
        /// Выполняется загрузка
        /// </summary>
        private bool _isDownloading;

        public void Stop()
        {
            _isDownloading = false;
            _stop = true;
            Logger.Add(string.Format("Загрузка книги '{0}' прерывается...", AuthorText.Name));
            if (_client != null) _client.CancelAsync();
            DownloadTextHelper.Remove(this);
        }

        private void Complete(object sender, DownloadDataCompletedEventArgs e)
        {
            WebClient client = (WebClient) sender;
            client.DownloadDataCompleted -= Complete;
            client.DownloadProgressChanged -= Progress;

            DownloadTextHelper.Remove(this);

            if ((e != null) && e.Cancelled)
            {
                Logger.Add(string.Format("Загрузка книги '{0}' прервана.", AuthorText.Name));
            }
            else if ((e != null) && (e.Error != null))
            {
                Logger.Add(e.Error.StackTrace, false, true);
                Logger.Add(e.Error.Message, false, true);
                Logger.Add(string.Format("Ошибка при загрузке книги '{0}'.", AuthorText.Name), true, true);
            }

            if ((e != null) && (e.Error == null) && (!e.Cancelled))
            {
                Text = (e.Result != null) ? WEB.ConvertPage(e.Result) : null;
                if (Text != null)
                {
                    Text = PostProcess(Text);
                    if (!Directory.Exists(Path.GetDirectoryName(GetCachedFileName())))
                        Directory.CreateDirectory(Path.GetDirectoryName(GetCachedFileName()));
                    File.WriteAllText(GetCachedFileName(), Text, Encoding.GetEncoding(1251));
                    Logger.Add(string.Format("Книга '{0}' закачана.", AuthorText.Name));
                }
            }
            if (DownloadTextComplete != null) DownloadTextComplete(this, e);
        }

        private void Progress(object sender, DownloadProgressChangedEventArgs e)
        {
            _client = (WebClient) sender;
            if (_stop) _client.CancelAsync();
            BytesReceived = e.BytesReceived;
        }

        public event DownloadTextCompleteEventHandler DownloadTextComplete;

        public string GetCachedFileName()
        {
            return AuthorText.GetCachedFileName(_author);
        }

        internal string GetCachedConvertFileName()
        {
            string fileName = GetCachedFileName();
            int hash = fileName.GetHashCode();
            string ext1 = Path.GetExtension(fileName);
            fileName = fileName.Substring(0, fileName.LastIndexOf(ext1));
            string ext2 = Path.GetExtension(fileName);
            fileName = fileName.Substring(0, fileName.LastIndexOf(ext2));
            return fileName + "." + hash.ToString("x") + ext2 + ext1;
        }

        private static string PostProcess(string html)
        {
            int st0 = html.IndexOf("<!------- Первый блок ссылок ------------->");
            int st1 = html.IndexOf("<!----------- Собственно произведение --------------->") +
                      "<!----------- Собственно произведение --------------->".Length;
            int st2 = html.LastIndexOf("<!---- Блок описания произведения (слева внизу) ----------------------->");

            if (st0 < 0 || st1 < 0 || st2 < 0) return html;
            string retValue = html.Substring(0, st0);
            retValue = retValue + "</center>" + html.Substring(st1, st2 - st1) + "</body></html>";
            retValue = retValue.Replace("\r\n\r\n", "<br/>");
            if (!retValue.Contains("<dd>&nbsp;&nbsp;") && retValue.Contains("<dd>"))
                retValue = retValue.Replace("<dd>", "<dd>&nbsp;&nbsp;");
            return retValue;
        }
    }

    public delegate void DownloadTextCompleteEventHandler(DownloadTextItem sender, DownloadDataCompletedEventArgs args);

    [ValueConversion(typeof (long), typeof (string))]
    public class BytesReceivedConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long)
            {
                double result = ((double) ((long) value))/1024;
                return result.ToString("N1", culture);
            }
            return null;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}