using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
                // а случае, если выставляется метка, что данные изменились - изменяем инфу и для гугл-синхронизации, иначе нет.
                if (_Changed)
                    ChangedGoogle = true;
            } }

        [XmlIgnore]
        public bool ChangedGoogle { get; set; }

        


        /// <summary>
        /// Данные, которые храняться внутри файла ссылки на Гугле
        /// </summary>
        [XmlIgnore]
        public string GoogleContent
        {
            get
            {
                //System.Text.StringBuilder sb = new StringBuilder();                
                //sb.AppendLine(URL);
                //sb.AppendLine(Name);
                //return sb.ToString();                
                var xs = new XmlSerializer(typeof(Author));
                var sb = new StringBuilder();
                var w = new StringWriter(sb, CultureInfo.InvariantCulture);
                xs.Serialize(w, this,
                             new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) }));
                return sb.ToString();

            }
        }

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
            bool summaryIsNew = false;
            foreach (AuthorText authorText in Texts)
            {
                if (authorText.IsNew)
                {
                    summaryIsNew = true;
                    break;
                }
            }
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
                Id = URL.ToLower().Replace("http://", "").Replace("indexdate.shtml", "").Replace("/", "_").Replace(".", "_").Replace("\\", "_").Trim();
                if (Id.EndsWith("_")) Id = Id.Substring(0, Id.Length - 1);
            }
        }

        // время-штамп изменения любых значений любых полей. Нужно для синхронизации с интернет-хранилищем 
        public DateTime? timeStamp { get; set; }

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
                    _isUpdated = value;
                    RaisePropertyChanged("IsUpdated");
                }
            }
        }

        /// <summary>
        /// удален. нужно для синхронизациию при загрузке из бд не показывается, не грузится
        /// </summary>
        bool _IsDeleted = false;
        public bool IsDeleted
        {
            get { return _IsDeleted; }
            set
            {
                if (_IsDeleted != value)
                {
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

        ///// <summary>
        ///// Обновляет информацию о произведениях автора.
        ///// Адрес берется из поля author.URL.
        ///// </summary>
        ///// <exception <exception cref="System.Exception">Когда страница не загружена</exception>
        ///// <returns>true-есть новые произведения, false-нет</returns>
        //public bool UpdateAuthor()
        //{
        //    bool retValue;
        //    if (!URL.EndsWith("indexdate.shtml"))
        //        URL = (URL.EndsWith("/"))
        //                  ? URL + "indexdate.shtml"
        //                  : URL + "/indexdate.shtml";

        //    byte[] buffer = WEB.DownloadPageSilent(URL);
        //    if (buffer != null)
        //    {
        //        retValue = UpdateAuthorInfo(WEB.ConvertPage(buffer));
        //    }
        //    else
        //    {
        //        throw new Exception(string.Format("Недоступна страница '{0}'", Name));
        //    }

        //    return retValue;
        //}

        public string GetAuthorPage()
        {
            string url = URL;
            if (!url.EndsWith("indexdate.shtml"))
                url = (url.EndsWith("/")) ? URL + "indexdate.shtml" : URL + "/indexdate.shtml";

            return WEB.DownloadPageSilent(url);
        }

        /// <summary>
        /// Обновляет список произведений автора
        /// </summary>
        /// <param name="page">Страница автора со списком произведений</param>
        /// <param name="context">Контекст синхронизации для обновления GUI</param>
        /// <returns>true - автор обновился</returns>
        public bool UpdateAuthorInfo(string page, SynchronizationContext context)
        {
            lock (_lockObj)
            {
                bool retValue = false;
                Author authorTemp = new Author { UpdateDate = UpdateDate };

                // проанализируем данные на страничке. если раньше их не загружали, то в любом случае не показываем, что есть обновления, просто заполняем данные
                MatchCollection matches = Regex.Matches(page,
                                                        "<DL><DT><li>(?:<font.*?>.*?</font>)?\\s*(<b>(?<Authors>.*?)\\s*</b>\\s*)?<A HREF=(?<LinkToText>.*?)><b>\\s*(?<NameOfText>.*?)\\s*</b></A>.*?<b>(?<SizeOfText>\\d+)k</b>.*?<small>(?:Оценка:<b>(?<DescriptionOfRating>(?<rating>\\d+(?:\\.\\d+)?).*?)</b>.*?)?\\s*\"(?<Section>.*?)\"\\s*(?<Genres>.*?)?\\s*(?:<A HREF=\"(?<LinkToComments>.*?)\">Комментарии:\\s*(?<CommentsDescription>(?<CommentCount>\\d+).*?)</A>\\s*)?</small>.*?(?:<br><DD>(?<Description>.*?))?</DL>");
                if (matches.Count > 0)
                {
                    int cnt = 0;
                    foreach (Match m in matches)
                    {
                        var item = new AuthorText
                                       {
                                           Description = NormalizeHTML(m.Groups["Description"].Value).Trim(),
                                           Genres = NormalizeHTML(m.Groups["Genres"].Value),
                                           Link = m.Groups["LinkToText"].Value,
                                           Name = NormalizeHTML(m.Groups["NameOfText"].Value),
                                           Order = cnt,
                                           SectionName =
                                               NormalizeHTML(m.Groups["Section"].Value).Replace("@", ""),
                                           Size = int.Parse(m.Groups["SizeOfText"].Value)
                                       };
                        authorTemp.Texts.Add(item);
                        cnt++;
                    }
                }
                if (Texts.Count > 0) // если раньше загружали автора, то проводим сравнение
                {
                    foreach (AuthorText txt in authorTemp.Texts)
                    {
                        bool bFound = false;
                        int OldSize = 0; // стрый размер текста
                        for (int i = 0; i < Texts.Count; i++)
                        {
                            if (txt.Link == Texts[i].Link)
                            {
                                txt.Cached = Texts[i].Cached;
                                OldSize = Texts[i].Size;// запоминаем старый размер, чтобы запомнить его в новом тексте
                            }
                            if (txt.Description == Texts[i].Description
                                && txt.Name == Texts[i].Name
                                && txt.Size == Texts[i].Size)
                            {
                                bFound = true;
                                // переносим значение isNew в новый массив, чтобы не потерять непрочитанные новые тексты
                                txt.IsNew = Texts[i].IsNew;
                                txt.UpdateDate = Texts[i].UpdateDate;                                
                                break;
                            }
                        }
                        if (!bFound)
                        {
                            retValue = true;
                            authorTemp.IsNew = true;
                            txt.IsNew = true;
                            txt.UpdateDate = DateTime.Now;
                            txt.SizeOld = OldSize;
                            // да, автор обновился 
                            authorTemp.UpdateDate = DateTime.Now;
                        }
                    }
                    // доп проверка по количеству произведений
                    if (authorTemp.Texts.Count != Texts.Count)
                    {
                        retValue = true;
                        authorTemp.UpdateDate = DateTime.Now;
                    }
                }

                context.Post(SyncRun, new RunContent {Renewed = this, New = authorTemp});

                return retValue; 
            } // lock
        }

        private static void SyncRun(object state)
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
                                                    Regex.Replace(Regex.Replace(s, "&#([0-9]+);?", delegate(Match match)
                                                                                                       {
                                                                                                           var ch =
                                                                                                               (char)
                                                                                                               int.Parse
                                                                                                                   (match
                                                                                                                        .
                                                                                                                        Groups
                                                                                                                        [
                                                                                                                        1
                                                                                                                        ]
                                                                                                                        .
                                                                                                                        Value,
                                                                                                                    NumberStyles
                                                                                                                        .
                                                                                                                        Integer);
                                                                                                           return
                                                                                                               ch.
                                                                                                                   ToString
                                                                                                                   ();
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


