using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using SIinformer.Utils;

namespace SIinformer.Logic
{
    /// <summary>
    /// класс автора для хранения в бд, нужно из-за того, что при обновлении автора его объект Author полностью перестраивается
    /// </summary>
    public class AuthorDb4o
    {
        public string Id { get; set; }
        public Author author { get; set; }
    }

    public class Author : BindableObject
    {
        #region Private Fields

        private int _maxDaysInaccessibility = 10; // через столько дней отключать пользователя от проверок, если он все это время недоступен
        private string _category = "Default";
        private bool _isIgnored;
        private bool _isNew;
        private bool _isUpdated;
        private string _name;
        private BindingList<AuthorText> _texts;
        private DateTime _updateDate = DateTime.Now;
        private string _comment = "";
        private readonly object _lockObj = new object();
        // айди для хранения в БД
        private string _id;
        // время-штамп изменения любых значений любых полей. Нужно для синхронизации с интернет-хранилищем
        //private long _timeStamp;
        #endregion
        /// <summary>
        /// Данные автора изменились
        /// </summary>
        private bool _Changed = false;
        [XmlIgnore]
        public bool Changed
        {
            get { return _Changed; }
            set
            {
                _Changed = value;
            } }

        public long ServerStamp { get; set; }// серверный штамп

        /// <summary>
        /// время последней проверки
        /// </summary>
        public DateTime LastCheckDate { get; set; }
        /// <summary>
        /// время следующей проверки
        /// </summary>
        public DateTime NextCheckDate { get; set; }

     
        public Author()
        {
            _texts = new BindingList<AuthorText>();
            _texts.ListChanged += TextsListChanged;
            Cached = null; 
        }

        /// <summary>
        /// Отслеживает авторские тексты для коррекции IsNew автора
        /// </summary>
        /// <param name="sender">игнорируется</param>
        /// <param name="e">игнорируется</param>
        private void TextsListChanged(object sender, ListChangedEventArgs e)
        {
            bool summaryIsNew = Texts.Any(authorText => authorText.IsNew);
            IsNew = summaryIsNew;
        }

        #region Public Property
        // айди для хранения в БД
        public string Id { get { return _id; } set{ _id = value;} }
        /// <summary>
        /// Если id НЕТ,то создаем. По этому ID идет сохранение на Гугл и в БД
        /// </summary>
        public void CheckID()
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = URL.ToLower().Replace("http://", "").Replace("indexdate.shtml", "").Replace("indextitle.shtml", "").Replace("/", "_").Replace(".", "_").Replace("\\", "_").Trim();
                if (Id.EndsWith("_")) Id = Id.Substring(0, Id.Length - 1);
            }
        }

        // время-штамп изменения любых значений любых полей. Нужно для синхронизации с интернет-хранилищем 
        public DateTime? timeStamp { get; set; }

        // кол-во дней недоступности автора. Нужно для того, чтобы при длительной недоступности отключать проверку автора. Должно сбрасываться, если страничка вдруг стала доступной
        public int DaysInaccessible
        {
            get { return _daysInaccessible; }
            set
            {
                if (_daysInaccessible != value)
                {
                    _daysInaccessible = value;
                    if (_daysInaccessible > _maxDaysInaccessibility) // если недоступность автора свыше константы дней, то отключаем его от проверок
                        IsIgnored = true;
                    Changed = true;
                }
            }
        }


        public BindingList<AuthorText> Texts
        {
            get { return _texts; }
            set
            {
                // отвязываемся от оповещения, чтоб _texts собрал GC
                _texts.ListChanged -= TextsListChanged;
                _texts = value;
                // привязываем новое оповещение
                _texts.ListChanged += TextsListChanged;
                // оповещаем о перезагрузке авторских текстов
                TextsListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, null));
                RaisePropertyChanged("Texts");
            }
        }

        /// <summary>
        /// Не обновлять автора
        /// </summary>
        public bool IsIgnored
        {
            get { return _isIgnored; }
            set
            {
                if (value != _isIgnored)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _isIgnored = value;
                    RaisePropertyChanged("IsIgnored");
                }
            }
        }

        /// <summary>
        /// Комментарий автора
        /// </summary>
        public string Comment
        {
            get { return _comment; }
            set
            {
                value = value.Trim();
                if (value != _comment)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _comment = value;
                    RaisePropertyChanged("Comment");
                }
            }
        }

        /// <summary>
        /// Категория автора
        /// </summary>
        public string Category
        {
            get { return _category; }
            set
            {
                if (value != _category)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _category = value.Trim();
                    RaisePropertyChanged("Category");
                }
            }
        }

        /// <summary>
        /// Автор находится в процессе обновления
        /// </summary>
        [XmlIgnore]
        public bool IsUpdated
        {
            get { return _isUpdated; }
            set
            {
                if (_isUpdated != value)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _isUpdated = value;
                    RaisePropertyChanged("IsUpdated");
                }
            }
        }

        /// <summary>
        /// удален. нужно для синхронизациию при загрузке из бд не показывается, не грузится
        /// </summary>
        bool _IsDeleted = false;

        private int _daysInaccessible;

        public bool IsDeleted
        {
            get { return _IsDeleted; }
            set
            {
                if (_IsDeleted != value)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _IsDeleted = value;
                    RaisePropertyChanged("IsDeleted");
                }
            }
        }

        /// <summary>
        /// Имя автора
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                if (value != _name)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _name = value;
                    RaisePropertyChanged("Name");
                }
            }
        }

        /// <summary>
        /// Группирует авторов (1-isNew, 2-normal, 3-ignored)
        /// </summary>
        [XmlIgnore]
        public int Group
        {
            get
            {
                if (IsNew) return 1;
                if (IsIgnored) return 3;
                return 2;
            }
        }

        /// <summary>
        /// Адрес автора
        /// </summary>
        public string URL { get; set; }  
        
        /// <summary>
        /// Фльтернативный адрес автора
        /// </summary>
        public string AlternateURL { get; set; }

        /// <summary>
        /// Дата/время обновления
        /// </summary>
        public DateTime UpdateDate
        {
            get { return _updateDate; }
            set
            {
                if (value != _updateDate)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _updateDate = value;
                    RaisePropertyChanged("UpdateDate");
                    RaisePropertyChanged("UpdateDateVisual");
                }
            }
        }

        /// <summary>
        /// Дата/время обновления в виде строки для binding'а
        /// </summary>
        public string UpdateDateVisual
        {
            get { return "Обновлено: " + UpdateDate.ToShortDateString() + " " + _updateDate.ToShortTimeString(); }
        }

        /// <summary>
        /// Автоматически кешировать книги при каждом обновлении
        /// </summary>
        public bool? Cached { get; set; }

        /// <summary>
        /// У автора есть новые произведения
        /// </summary>
        public bool IsNew
        {
            get { return _isNew; }
            set
            {
                if (value != _isNew)
                {
                    Changed = true;// данные изменены (для записи в БД)
                    _isNew = value;
                    if (value == false)
                    {
                        foreach (AuthorText txt in Texts)
                            txt.IsNew = false;
                    }
                    RaisePropertyChanged("IsNew");
                    RaisePropertyChanged("Star");
                }
            }
        }

        /// <summary>
        /// Ресурс (звезда) автора, для binding'а
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

        #endregion

        #region Обновление автора

        

        public string GetAuthorPage()
        {
            Sites.ISite _site = null;
            _site = Sites.SitesDetector.GetSite(URL);
            if (_site == null) return "";
            return _site.GetAuthorPage(URL);
            //string url = URL;
            //if (!url.EndsWith("indexdate.shtml") && !url.EndsWith("indextitle.shtml"))
            //    url = (url.EndsWith("/")) ? URL + "indextitle.shtml" : URL + "/indextitle.shtml";

            //return WEB.DownloadPageSilent(url);
        }

        /// <summary>
        /// Обновляет список произведений автора
        /// </summary>
        /// <param name="page">Страница автора со списком произведений</param>
        /// <param name="context">Контекст синхронизации для обновления GUI</param>
        /// <returns>true - автор обновился</returns>
        public bool UpdateAuthorInfo(string page, SynchronizationContext context, bool skipBookDescriptionChecking=false)
        {
            Sites.ISite _site = null;
            _site = Sites.SitesDetector.GetSite(URL);
            if (_site == null) return false;
            return _site.UpdateAuthorInfo(page, this, context, skipBookDescriptionChecking);            
        }

        public static void SyncRun(object state)
        {
            Author renewed = ((RunContent) state).Renewed;
            Author @new = ((RunContent)state).New;
            renewed.Texts = @new.Texts;
            renewed.IsNew = @new.IsNew;
            renewed.Cached = @new.Cached;
            renewed.UpdateDate = @new.UpdateDate;
        }

        internal class RunContent
        {
            /// <summary>
            /// Обновляемый автор
            /// </summary>
            internal Author Renewed { get; set; }

            /// <summary>
            /// Автор с новй инфой
            /// </summary>
            internal Author New { get; set; }
        }

        #endregion

        #region Нормализация строк

        private static string NormalizeHTML(string s)
        {
            return
                NormalizeHTMLEsc(
                    Regex.Replace(
                        Regex.Replace(
                            Regex.Replace(
                                Regex.Replace(
                                    Regex.Replace(
                                        Regex.Replace(Regex.Replace(s, @"[\r\n\x85\f]+", ""), "<(br|li)[^>]*>", "\n",
                                                      RegexOptions.IgnoreCase), "<td[^>]*>", "\t",
                                        RegexOptions.IgnoreCase), @"<script[^>]*>.*?</\s*script[^>]*>", "",
                                    RegexOptions.IgnoreCase), "<[^>]*>", ""), @"\n[\p{Z}\t]+\n", "\n\n"), @"\n\n+",
                        "\n\n"));
        }

        private static string NormalizeHTMLEsc(string s)
        {
            return
                Regex.Replace(
                    Regex.Replace(
                        Regex.Replace(
                            Regex.Replace(
                                Regex.Replace(
                                    Regex.Replace(
                                        Regex.Replace(
                                            Regex.Replace(
                                                Regex.Replace(
                                                    Regex.Replace(
                                                         Regex.Replace(s, "&#([0-9]+);?",
                                                                 delegate(Match match)
                                                                 {
                                                                     var ch = (char) int.Parse(match.Groups[1].Value, NumberStyles.Integer);
                                                                     return ch.ToString();
         }), "&bull;?",
                                                                  " * ", RegexOptions.IgnoreCase), "&lsaquo;?", "<",
                                                    RegexOptions.IgnoreCase), "&rsaquo;?", ">", RegexOptions.IgnoreCase),
                                            "&trade;?", "(tm)", RegexOptions.IgnoreCase), "&frasl;?", "/",
                                        RegexOptions.IgnoreCase), "&lt;?", "<", RegexOptions.IgnoreCase), "&gt;?", ">",
                                RegexOptions.IgnoreCase), "&copy;?", "(c)", RegexOptions.IgnoreCase), "&reg;?", "(r)",
                        RegexOptions.IgnoreCase), "&nbsp;?", " ", RegexOptions.IgnoreCase);
        }

        #endregion

        #region Override

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}


