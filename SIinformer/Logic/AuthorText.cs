using System;
using System.IO;
using System.Xml.Serialization;
using SIinformer.Readers;
using SIinformer.Utils;

namespace SIinformer.Logic
{
    /// <summary>
    /// Детальная информация на страничке автора по произведению
    /// </summary>
    public class AuthorText : BindableObject
    {
        private bool _isNew;
        public int Order { get; set; }
        public string SectionName { get; set; }
        public string Description { get; set; }
        public string Genres { get; set; }
        public string Link { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Размер книги
        /// </summary>
        public int Size { get; set; }
        /// <summary>
        /// Предыдущий размер книги
        /// </summary>
        public int SizeOld { get; set; }

        /// <summary>
        /// Размер книги для вывода на экран
        /// </summary>
        [XmlIgnore]
        public string SizeVisual { 
            get
            {
                if (!IsNew || SizeOld==0) return Size.ToString();
                // если новый размер больше, то есть текста добавилось, показываем на сколько
                if (Size > SizeOld) return SizeOld.ToString() + "+" + (Size - SizeOld).ToString() + "=" + Size.ToString();
                // просто показываем старый размер текста и новый
                return SizeOld.ToString() + " > " + Size.ToString();
            }
        }

        /// <summary>
        /// Дата обновления книги
        /// </summary>
        public DateTime UpdateDate { get; set; }
        
        /// <summary>
        /// Конструирует правильный url книги из абсолютного адреса автора и относительного книги
        /// </summary>
        /// <param name="author">Автор</param>
        /// <returns>URL</returns>
        public string GetFullLink(Author author)
        {
            string url = author.URL;
            if (url.EndsWith("indexdate.shtml"))
                url = url.Replace("indexdate.shtml", Link);
            else
                url = (url.EndsWith("/")) ? url + Link : url + "/" + Link;
            return url;
        }

        /// <summary>
        /// Получает путь к файлу кеша для книги
        /// </summary>
        /// <param name="author">Автор книги</param>
        /// <returns>Путь на диске</returns>
        public string GetCachedFileName(Author author)
        {
            string urlWithoutHTTP = author.URL.Replace(@"http://", "");
            if (urlWithoutHTTP.EndsWith("/indexdate.shtml"))
                urlWithoutHTTP = urlWithoutHTTP.Replace("/indexdate.shtml", "");

            string endPath = urlWithoutHTTP.Substring(urlWithoutHTTP.IndexOf("/") + 1).Replace("/", @"\");

            string booksCachedPath = DownloadTextHelper.BooksPath();
            booksCachedPath = Path.Combine(booksCachedPath, endPath);
            string sectionName = SectionName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sectionName = sectionName.Replace(c, '_');
            }
            booksCachedPath = Path.Combine(booksCachedPath, sectionName);
            string fileName = Path.Combine(booksCachedPath, Link);
            string ext = Path.GetExtension(fileName);
            fileName = fileName.Substring(0, fileName.LastIndexOf(ext)) + "." + UpdateDate.Ticks + ext;
            return fileName;
        }

        /// <summary>
        /// Получает путь к файлу различий с предыдущей версией для книги
        /// </summary>
        /// <param name="author">Автор книги</param>
        /// <returns>Путь на диске</returns>
        public string GetDiffFileName(Author author)
        {
            return GetCachedFileName(author).Replace(".shtml", "_diff.shtml");
        }


        /// <summary>
        /// Обновляет признак кешированности книги
        /// </summary>
        /// <param name="author">Автор - для создания правильного пути к файлу в кеше</param>
        public void UpdateIsCached(Author author)
        {
            IsCached = File.Exists(GetCachedFileName(author));
        }

        /// <summary>
        /// Книга имеется в кеше?
        /// </summary>
        [XmlIgnore]
        public bool IsCached { get; set; }


        bool _HasDiff = false;
        /// <summary>
        /// Вычисленная разница книги имеется в кеше?
        /// </summary>
        [XmlIgnore]
        public bool HasDiff
        {
            get { return _HasDiff; }
            set
            {
                if (_HasDiff != value)
                {
                    _HasDiff = value;
                    RaisePropertyChanged("HasDiff");
                }
            }
        }

        /// <summary>
        /// Обновляет признак наличия файла отличий книги
        /// </summary>
        /// <param name="author">Автор - для создания правильного пути к файлу в кеше</param>
        public void UpdateHasDiff(Author author)
        {
            HasDiff = File.Exists(GetDiffFileName(author));
        }


        /// <summary>
        /// Отображаемая дата обновления книги
        /// </summary>
        [XmlIgnore]
        public string UpdateDateVisual
        {
            get { return UpdateDate.ToShortDateString() + " " + UpdateDate.ToShortTimeString(); }
        }

        /// <summary>
        /// Автоматически кешировать книгу при каждом обновлении
        /// </summary>
        public bool? Cached { get; set; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public AuthorText()
        {
            UpdateDate = DateTime.Now;
            Cached = null;
        }

        /// <summary>
        /// Признак обновления книги
        /// </summary>
        public bool IsNew
        {
            get { return _isNew; }
            set
            {
                if (value == _isNew)
                    return;

                _isNew = value;

                RaisePropertyChanged("IsNew");
                RaisePropertyChanged("Star");
            }
        }

        /// <summary>
        /// Звезда, отображаемая жля книги (желтая - для новой, серая - для старой)
        /// </summary>
        public string Star
        {
            get
            {
                return IsNew
                           ? "pack://application:,,,/Resources/star_yellow_new16.png"
                           : "pack://application:,,,/Resources/star_grey16.png";
            }
        }

        public override string ToString()
        {
            return Name + " " + Size + "кб " + Description.Length;
        }
    }
}