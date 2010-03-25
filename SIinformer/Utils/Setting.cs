using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;
using SIinformer.Logic;

namespace SIinformer.Utils
{
    public class Setting : BindableObject
    {
        public Setting()
        {
            Height = 510;
            Width = 347;
            Top = SystemInformation.WorkingArea.Top + (SystemInformation.WorkingArea.Height - Height)/2;
            Left = SystemInformation.WorkingArea.Left + (SystemInformation.WorkingArea.Width - Width)/2;
            DesiredPositionAdvancedWindow = DesiredPositionAdvancedWindow.Auto;
            AdvancedWindowVisibleStyle = AdvancedWindowVisibleStyle.Always;
            AdvancedWindowSettingDictionary = new SerializableDictionary<string, AdvancedWindowSetting>
                                                  {
                                                      {
                                                          "Default", new AdvancedWindowSetting
                                                                         {
                                                                             Size =
                                                                                 new Size(
                                                                                 SystemInformation.WorkingArea.Width/2,
                                                                                 SystemInformation.WorkingArea.Height/3*
                                                                                 2),
                                                                             HeightComment = 100
                                                                         }
                                                          }
                                                  };
            AuthorWindowSettingDictionary = new SerializableDictionary<string, AuthorWindowSetting>
                                                {
                                                    {
                                                        "Default", new AuthorWindowSetting
                                                                       {
                                                                           HeightComment = 100,
                                                                           Size =
                                                                               new Size(
                                                                               SystemInformation.WorkingArea.Width/2,
                                                                               SystemInformation.WorkingArea.Height/3*2),
                                                                       }
                                                        }
                                                };
            AuthorWindowSettingDictionary["Default"].Location =
                new Point(
                    (SystemInformation.WorkingArea.Width - AuthorWindowSettingDictionary["Default"].Size.Width)/2,
                    (SystemInformation.WorkingArea.Height - AuthorWindowSettingDictionary["Default"].Size.Height)/2);

            ProxySetting = new ProxySetting();
            RSSFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "siinformer.rss");
            RSSCount = 100;
            AfterUpdater = "";
            BeforeUpdater = "";
            AfterUpdaterParam = "";
            BeforeUpdaterParam = "";
            AlternativeReader = "";
            AlternativeReaderParam = "";
            BookConverter = "";
            BookConverterParam = "";
            MaxCacheSize = 50;
        }

        public bool CloseHowToMinimize { get; set; }
        public ProxySetting ProxySetting { get; set; }

        /// <summary>
        /// Использовать синхронизацию с гуглом
        /// </summary>
        private bool _UseGoogle = false;
        public bool UseGoogle
        {
            get
            { return _UseGoogle; }
            set
            {
                _UseGoogle = value;
                RaisePropertyChanged("UseGoogle");
            }
        }
        /// <summary>
        /// Имя пользователя на гугловском аккаунте
        /// </summary>
        private string _GoogleLogin ;
        public string GoogleLogin
        {
            get
            { return _GoogleLogin; }
            set
            {
                _GoogleLogin = value;
                RaisePropertyChanged("GoogleLogin");
            }
        }

        /// <summary>
        /// Пароль пользователя на гугловском аккаунте
        /// </summary>
        private string _GooglePassword;
        public string GooglePassword
        {
            get
            { return _GooglePassword; }
            set
            {
                _GooglePassword = value;
                RaisePropertyChanged("GooglePassword");
            }
        }

        // использовать базу данных для хранения данных
        private bool _UseDatabase = false;
        public bool UseDatabase { get
            { return _UseDatabase; } 
            set
            {
                _UseDatabase = value;
                RaisePropertyChanged("UseDatabase");
            } }

        /// <summary>
        /// Период обновления в часах
        /// </summary>
        public long IntervalOfUpdate
        {
            get { return _intervalOfUpdate; }
            set
            {
                if (value != _intervalOfUpdate)
                {
                    _intervalOfUpdate = value;
                    RaisePropertyChanged("IntervalOfUpdate");
                }
            }
        }

        private long _intervalOfUpdate = 1;

        #region Читалки

        private string _alternativeReader;
        private string _alternativeReaderParam;
        private string _bookConverter;
        private string _bookConverterParam;
        private bool _cached;
        private int _defaultReader;
        private FlowDocumentReaderViewingMode _flowDocumentReaderViewingMode = FlowDocumentReaderViewingMode.Page;
        private double _flowDocumentZoom = 130;
        private long _maxCacheSize;

        public int DefaultReader
        {
            get { return _defaultReader; }
            set
            {
                if (value != _defaultReader)
                {
                    _defaultReader = value;
                    RaisePropertyChanged("DefaultReader");
                }
            }
        }

        public string AlternativeReader
        {
            get { return _alternativeReader; }
            set
            {
                if (value != _alternativeReader)
                {
                    _alternativeReader = value;
                    RaisePropertyChanged("AlternativeReader");
                }
            }
        }

        public string AlternativeReaderParam
        {
            get { return _alternativeReaderParam; }
            set
            {
                if (value != _alternativeReaderParam)
                {
                    _alternativeReaderParam = value;
                    RaisePropertyChanged("AlternativeReaderParam");
                }
            }
        }

        public string BookConverter
        {
            get { return _bookConverter; }
            set
            {
                if (value != _bookConverter)
                {
                    _bookConverter = value;
                    RaisePropertyChanged("BookConverter");
                }
            }
        }

        public string BookConverterParam
        {
            get { return _bookConverterParam; }
            set
            {
                if (value != _bookConverterParam)
                {
                    _bookConverterParam = value;
                    RaisePropertyChanged("BookConverterParam");
                }
            }
        }

        public long MaxCacheSize
        {
            get { return _maxCacheSize; }
            set
            {
                if (value != _maxCacheSize)
                {
                    _maxCacheSize = value;
                    RaisePropertyChanged("MaxCacheSize");
                }
            }
        }


        public bool Cached
        {
            get { return _cached; }
            set
            {
                if (value != _cached)
                {
                    _cached = value;
                    RaisePropertyChanged("Cached");
                }
            }
        }

        public FlowDocumentReaderViewingMode FlowDocumentReaderViewingMode
        {
            get { return _flowDocumentReaderViewingMode; }
            set
            {
                if (value != _flowDocumentReaderViewingMode)
                {
                    _flowDocumentReaderViewingMode = value;
                    RaisePropertyChanged("FlowDocumentReaderViewingMode");
                }
            }
        }


        public double FlowDocumentZoom
        {
            get { return _flowDocumentZoom; }
            set
            {
                if (value != _flowDocumentZoom)
                {
                    _flowDocumentZoom = value;
                    RaisePropertyChanged("FlowDocumentZoom");
                }
            }
        }

        #endregion

        #region Страница автора

        private bool _defaultActionAsAuthorPage;

        private bool _markAuthorIsReadWithAuthorPage;

        private bool _openAuthorPageSortingDate = true;

        public bool DefaultActionAsAuthorPage
        {
            get { return _defaultActionAsAuthorPage; }
            set
            {
                if (value != _defaultActionAsAuthorPage)
                {
                    _defaultActionAsAuthorPage = value;
                    RaisePropertyChanged("DefaultActionAsAuthorPage");
                }
            }
        }

        public bool MarkAuthorIsReadWithAuthorPage
        {
            get { return _markAuthorIsReadWithAuthorPage; }
            set
            {
                if (value != _markAuthorIsReadWithAuthorPage)
                {
                    _markAuthorIsReadWithAuthorPage = value;
                    RaisePropertyChanged("MarkAuthorIsReadWithAuthorPage");
                }
            }
        }

        public bool OpenAuthorPageSortingDate
        {
            get { return _openAuthorPageSortingDate; }
            set
            {
                if (value != _openAuthorPageSortingDate)
                {
                    _openAuthorPageSortingDate = value;
                    RaisePropertyChanged("OpenAuthorPageSortingDate");
                }
            }
        }

        #endregion

        #region Окно автора

        /// <summary>
        /// Размеры окна и разделителя. Публичен только для сериализации. 
        /// Напрямую не писать, только методом SetAuthorWindowSetting.
        /// </summary>
        public SerializableDictionary<string, AuthorWindowSetting> AuthorWindowSettingDictionary;

        public void SetAuthorWindowHeightCommentSetting(Author author, double heightComment)
        {
            if (author == null) return;
            if (heightComment == AuthorWindowSettingDictionary["Default"].HeightComment) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Shift))
            {
                foreach (AuthorWindowSetting authorWindowSetting in AuthorWindowSettingDictionary.Values)
                {
                    authorWindowSetting.HeightComment = heightComment;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AuthorWindowSettingDictionary["Default"].HeightComment = heightComment;
                return;
            }
            if (AuthorWindowSettingDictionary.ContainsKey(author.URL))
                AuthorWindowSettingDictionary[author.URL].HeightComment = heightComment;
            else
            {
                AuthorWindowSetting newSetting = new AuthorWindowSetting
                                                     {
                                                         Size = AuthorWindowSettingDictionary["Default"].Size,
                                                         Location = AuthorWindowSettingDictionary["Default"].Location,
                                                         HeightComment = heightComment
                                                     };
                AuthorWindowSettingDictionary.Add(author.URL, newSetting);
            }
        }

        public void SetAuthorWindowSizeSetting(Author author, Size size)
        {
            if (author == null) return;
            if (size == AuthorWindowSettingDictionary["Default"].Size) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Shift))
            {
                foreach (AuthorWindowSetting authorWindowSetting in AuthorWindowSettingDictionary.Values)
                {
                    authorWindowSetting.Size = size;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AuthorWindowSettingDictionary["Default"].Size = size;
                return;
            }
            if (AuthorWindowSettingDictionary.ContainsKey(author.URL))
                AuthorWindowSettingDictionary[author.URL].Size = size;
            else
            {
                AuthorWindowSetting newSetting = new AuthorWindowSetting
                                                     {
                                                         Size = size,
                                                         Location = AuthorWindowSettingDictionary["Default"].Location,
                                                         HeightComment =
                                                             AuthorWindowSettingDictionary["Default"].HeightComment
                                                     };
                AuthorWindowSettingDictionary.Add(author.URL, newSetting);
            }
        }

        public void SetAuthorWindowLocationSetting(Author author, Point location)
        {
            if (author == null) return;
            if (location == AuthorWindowSettingDictionary["Default"].Location) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Shift))
            {
                foreach (AuthorWindowSetting authorWindowSetting in AuthorWindowSettingDictionary.Values)
                {
                    authorWindowSetting.Location = location;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AuthorWindowSettingDictionary["Default"].Location = location;
                return;
            }
            if (AuthorWindowSettingDictionary.ContainsKey(author.URL))
                AuthorWindowSettingDictionary[author.URL].Location = location;
            else
            {
                AuthorWindowSetting newSetting = new AuthorWindowSetting
                                                     {
                                                         Size = AuthorWindowSettingDictionary["Default"].Size,
                                                         Location = location,
                                                         HeightComment =
                                                             AuthorWindowSettingDictionary["Default"].HeightComment
                                                     };
                AuthorWindowSettingDictionary.Add(author.URL, newSetting);
            }
        }

        public AuthorWindowSetting GetAuthorWindowSetting(Author author)
        {
            return (author != null) && (AuthorWindowSettingDictionary.ContainsKey(author.URL))
                       ? AuthorWindowSettingDictionary[author.URL]
                       : AuthorWindowSettingDictionary["Default"];
        }

        #endregion

        #region Динамическое окно автора

        private AdvancedWindowVisibleStyle _advancedWindowVisibleStyle;
        private DesiredPositionAdvancedWindow _desiredPositionAdvancedWindow;

        /// <summary>
        /// Размеры окна и разделителя. Публичен только для сериализации. 
        /// Напрямую не писать, только методом SetAdvancedWindowSetting.
        /// </summary>
        public SerializableDictionary<string, AdvancedWindowSetting> AdvancedWindowSettingDictionary;

        public DesiredPositionAdvancedWindow DesiredPositionAdvancedWindow
        {
            get { return _desiredPositionAdvancedWindow; }
            set
            {
                if (value != _desiredPositionAdvancedWindow)
                {
                    _desiredPositionAdvancedWindow = value;
                    RaisePropertyChanged("DesiredPositionAdvancedWindow");
                }
            }
        }

        public AdvancedWindowVisibleStyle AdvancedWindowVisibleStyle
        {
            get { return _advancedWindowVisibleStyle; }
            set
            {
                if (value != _advancedWindowVisibleStyle)
                {
                    _advancedWindowVisibleStyle = value;
                    RaisePropertyChanged("AdvancedWindowVisibleStyle");
                }
            }
        }

        public AdvancedWindowSetting GetAdvancedWindowSetting(Author author)
        {
            return (author != null) && (AdvancedWindowSettingDictionary.ContainsKey(author.URL))
                       ? AdvancedWindowSettingDictionary[author.URL]
                       : AdvancedWindowSettingDictionary["Default"];
        }

        public void SetAdvancedWindowSizeSetting(Author author, Size size)
        {
            if (author == null) return;
            if (size == AdvancedWindowSettingDictionary["Default"].Size) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Shift))
            {
                foreach (AdvancedWindowSetting advancedWindowSetting in AdvancedWindowSettingDictionary.Values)
                {
                    advancedWindowSetting.Size = size;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AdvancedWindowSettingDictionary["Default"].Size = size;
                return;
            }
            if (AdvancedWindowSettingDictionary.ContainsKey(author.URL))
                AdvancedWindowSettingDictionary[author.URL].Size = size;
            else
            {
                AdvancedWindowSetting newSetting = new AdvancedWindowSetting
                                                       {
                                                           Size = size,
                                                           HeightComment =
                                                               AdvancedWindowSettingDictionary["Default"].HeightComment
                                                       };
                AdvancedWindowSettingDictionary.Add(author.URL, newSetting);
            }
        }

        public void SetAdvancedWindowHeightCommentSetting(Author author, double heightComment)
        {
            if (author == null) return;
            if (heightComment == AdvancedWindowSettingDictionary["Default"].HeightComment) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Shift))
            {
                foreach (AdvancedWindowSetting advancedWindowSetting in AdvancedWindowSettingDictionary.Values)
                {
                    advancedWindowSetting.HeightComment = heightComment;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                AdvancedWindowSettingDictionary["Default"].HeightComment = heightComment;
                return;
            }
            if (AdvancedWindowSettingDictionary.ContainsKey(author.URL))
                AdvancedWindowSettingDictionary[author.URL].HeightComment = heightComment;
            else
            {
                AdvancedWindowSetting newSetting = new AdvancedWindowSetting
                                                       {
                                                           Size = AdvancedWindowSettingDictionary["Default"].Size,
                                                           HeightComment = heightComment
                                                       };
                AdvancedWindowSettingDictionary.Add(author.URL, newSetting);
            }
        }

        #endregion

        #region Поддержка для сортировки ListCollectionView

        private ListSortDirection _sortDirection = ListSortDirection.Descending;
        private string _sortProperty = "UpdateDate";

        public ListSortDirection SortDirection
        {
            get { return _sortDirection; }
            set
            {
                if (value != _sortDirection)
                {
                    _sortDirection = value;
                    RaisePropertyChanged("SortDirection");
                }
            }
        }

        public string SortProperty
        {
            get { return _sortProperty; }
            set
            {
                if (value != _sortProperty)
                {
                    _sortProperty = value;
                    RaisePropertyChanged("SortProperty");
                }
            }
        }

        #endregion

        #region Save-Load

        private static string SettingFileName
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setting.xml"); }
        }

        public bool Topmost { get; set; }

        public static Setting LoadFromXml()
        {
            try
            {
                string xml = File.ReadAllText(SettingFileName);
                var reader = new StringReader(xml);
                var sr = new XmlSerializer(typeof (Setting));
                return (Setting) sr.Deserialize(reader);
            }
            catch (Exception)
            {
                return new Setting();
            }
        }

        public void SaveToXML(AuthorList authors)
        {
            Clearning(authors);
            var xs = new XmlSerializer(typeof (Setting));
            var sb = new StringBuilder();
            var w = new StringWriter(sb, CultureInfo.InvariantCulture);
            xs.Serialize(w, this,
                         new XmlSerializerNamespaces(new[] {new XmlQualifiedName(string.Empty)}));
            File.WriteAllText(SettingFileName, sb.ToString());
        }

        /// <summary>
        /// Очистка размеров и положений окон авторов от удаленных авторов
        /// и дефолтных размеров
        /// </summary>
        /// <param name="authors"></param>
        private void Clearning(AuthorList authors)
        {
            string[] keys = new string[AdvancedWindowSettingDictionary.Keys.Count];
            AdvancedWindowSettingDictionary.Keys.CopyTo(keys, 0);
            foreach (string key in keys)
            {
                if ((key != "Default") && (authors.FindAuthor(key) == null))
                    AdvancedWindowSettingDictionary.Remove(key);
            }
            Dictionary<string, AdvancedWindowSetting> copy =
                new Dictionary<string, AdvancedWindowSetting>(AdvancedWindowSettingDictionary);
            AdvancedWindowSetting @default = AdvancedWindowSettingDictionary["Default"];
            foreach (KeyValuePair<string, AdvancedWindowSetting> pair in copy)
            {
                if ((pair.Key != "Default") && (pair.Value.Size == @default.Size) &&
                    (pair.Value.HeightComment == @default.HeightComment))
                    AdvancedWindowSettingDictionary.Remove(pair.Key);
            }

            keys = new string[AuthorWindowSettingDictionary.Keys.Count];
            AuthorWindowSettingDictionary.Keys.CopyTo(keys, 0);
            foreach (string key in keys)
            {
                if ((key != "Default") && (authors.FindAuthor(key) == null))
                    AuthorWindowSettingDictionary.Remove(key);
            }
            Dictionary<string, AuthorWindowSetting> copy1 =
                new Dictionary<string, AuthorWindowSetting>(AuthorWindowSettingDictionary);
            AuthorWindowSetting @default1 = AuthorWindowSettingDictionary["Default"];
            foreach (KeyValuePair<string, AuthorWindowSetting> pair in copy1)
            {
                if ((pair.Key != "Default") && (pair.Value.Size == @default1.Size) &&
                    (pair.Value.Location == @default1.Location) && (pair.Value.HeightComment == @default1.HeightComment))
                    AuthorWindowSettingDictionary.Remove(pair.Key);
            }
        }

        public static Setting CopyFrom(Setting setting)
        {
            var xs = new XmlSerializer(typeof (Setting));
            var sb = new StringBuilder();
            var w = new StringWriter(sb, CultureInfo.InvariantCulture);
            xs.Serialize(w, setting,
                         new XmlSerializerNamespaces(new[] {new XmlQualifiedName(string.Empty)}));
            var reader = new StringReader(sb.ToString());
            var sr = new XmlSerializer(typeof (Setting));
            return (Setting) sr.Deserialize(reader);
        }

        #endregion

        #region Основное окно

        public double Top { get; set; }
        public double Left { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }

        #endregion

        #region Панель управления

        private bool _extendedMode;
        private bool _useCategory = true;

        public bool UseCategory
        {
            get { return _useCategory; }
            set
            {
                if (value != _useCategory)
                {
                    _useCategory = value;
                    RaisePropertyChanged("UseCategory");
                }
            }
        }

        public bool ExtendedMode
        {
            get { return _extendedMode; }
            set
            {
                if (value != _extendedMode)
                {
                    _extendedMode = value;
                    RaisePropertyChanged("ExtendedMode");
                }
            }
        }

        #endregion

        #region RSS

        private long _rssCount;
        private string _rssFileName;
        private bool _useRSS;

        public bool UseRSS
        {
            get { return _useRSS; }
            set
            {
                if (value != _useRSS)
                {
                    _useRSS = value;
                    RaisePropertyChanged("UseRSS");
                }
            }
        }

        public long RSSCount
        {
            get { return _rssCount; }
            set
            {
                if (value != _rssCount)
                {
                    _rssCount = value;
                    RaisePropertyChanged("RSSCount");
                }
            }
        }

        public string RSSFileName
        {
            get { return _rssFileName; }
            set
            {
                if (value != _rssFileName)
                {
                    _rssFileName = value;
                    RaisePropertyChanged("RSSFileName");
                }
            }
        }

        #endregion

        #region Запускаемые программы

        private string _afterUpdater;
        private string _afterUpdaterParam;
        private string _beforeUpdater;
        private string _beforeUpdaterParam;

        public string AfterUpdater
        {
            get { return _afterUpdater; }
            set
            {
                if (value != _afterUpdater)
                {
                    _afterUpdater = value;
                    RaisePropertyChanged("AfterUpdater");
                }
            }
        }

        public string BeforeUpdater
        {
            get { return _beforeUpdater; }
            set
            {
                if (value != _beforeUpdater)
                {
                    _beforeUpdater = value;
                    RaisePropertyChanged("BeforeUpdater");
                }
            }
        }

        public string AfterUpdaterParam
        {
            get { return _afterUpdaterParam; }
            set
            {
                if (value != _afterUpdaterParam)
                {
                    _afterUpdaterParam = value;
                    RaisePropertyChanged("AfterUpdaterParam");
                }
            }
        }

        public string BeforeUpdaterParam
        {
            get { return _beforeUpdaterParam; }
            set
            {
                if (value != _beforeUpdaterParam)
                {
                    _beforeUpdaterParam = value;
                    RaisePropertyChanged("BeforeUpdaterParam");
                }
            }
        }

        public string LastAuthorUrl { get; set; }

        #endregion

        /// <summary>
        /// Копирует некоторые поля объекта
        /// </summary>
        public void PartialCopy(Setting original)
        {
            DesiredPositionAdvancedWindow = original.DesiredPositionAdvancedWindow;
            AdvancedWindowVisibleStyle = original.AdvancedWindowVisibleStyle;
            CloseHowToMinimize = original.CloseHowToMinimize;
            DefaultActionAsAuthorPage = original.DefaultActionAsAuthorPage;
            ProxySetting.UseProxy = original.ProxySetting.UseProxy;
            ProxySetting.Address = original.ProxySetting.Address;
            ProxySetting.Port = original.ProxySetting.Port;
            ProxySetting.UseAuthentification = original.ProxySetting.UseAuthentification;
            ProxySetting.UserName = original.ProxySetting.UserName;
            ProxySetting.Password = original.ProxySetting.Password;
            UseRSS = original.UseRSS;
            RSSFileName = original.RSSFileName;
            RSSCount = original.RSSCount;
            AfterUpdater = original.AfterUpdater;
            BeforeUpdater = original.BeforeUpdater;
            AfterUpdaterParam = original.AfterUpdaterParam;
            BeforeUpdaterParam = original.BeforeUpdaterParam;
            MarkAuthorIsReadWithAuthorPage = original.MarkAuthorIsReadWithAuthorPage;
            OpenAuthorPageSortingDate = original.OpenAuthorPageSortingDate;
            DefaultReader = original.DefaultReader;
            AlternativeReader = original.AlternativeReader;
            AlternativeReaderParam = original.AlternativeReaderParam;
            BookConverter = original.BookConverter;
            BookConverterParam = original.BookConverterParam;
            MaxCacheSize = original.MaxCacheSize;
            Cached = original.Cached;
            IntervalOfUpdate = original.IntervalOfUpdate;
            UseDatabase = original.UseDatabase;
            UseGoogle = original.UseGoogle;
            GoogleLogin = original.GoogleLogin;
            GooglePassword = original.GooglePassword;
        }

        public static string ErrorLogFileName()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        }
    }

    #region Дополнительные классы

    [Flags]
    public enum AdvancedWindowVisibleStyle
    {
        Never = 1,
        OnlyComment = 2,
        OnlyIsNew = 4,
        OnlyCommentAndOnlyIsNew = OnlyComment ^ OnlyIsNew,
        Always = 8,
        AlwaysPanel = Never ^ Always
    }

    public class ProxySetting
    {
        public ProxySetting()
        {
            UseProxy = false;
            Address = "";
            Port = 80;
            UseAuthentification = false;
            UserName = "";
            Password = "";
        }

        public bool UseProxy { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public bool UseAuthentification { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class AdvancedWindowSetting
    {
        public double HeightComment { get; set; }
        public Size Size { get; set; }
    }

    public class AuthorWindowSetting
    {
        public double HeightComment { get; set; }
        public Size Size { get; set; }
        public Point Location { get; set; }
    }

    public enum DesiredPositionAdvancedWindow
    {
        Left,
        Right,
        Auto
    }

    #endregion

    #region Конвертеры

    [ValueConversion(typeof (DesiredPositionAdvancedWindow), typeof (string))]
    public class DesiredPositionAdvancedWindowConverter : IValueConverter
    {
        private const string AUTO = "Автоматически";
        private const string LEFT = "Всегда слева";
        private const string RIGHT = "Всегда справа";

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DesiredPositionAdvancedWindow[])
            {
                DesiredPositionAdvancedWindow[] arr = (DesiredPositionAdvancedWindow[]) value;
                string[] result = new string[arr.Length];
                int index = 0;
                foreach (DesiredPositionAdvancedWindow o in arr)
                {
                    result[index] = Parse(o);
                    index++;
                }
                return result;
            }
            return Parse((DesiredPositionAdvancedWindow) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string desiredPositionAdvancedWindow = (string) value;
            switch (desiredPositionAdvancedWindow)
            {
                case LEFT:
                    return DesiredPositionAdvancedWindow.Left;
                case RIGHT:
                    return DesiredPositionAdvancedWindow.Right;
                case AUTO:
                    return DesiredPositionAdvancedWindow.Auto;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private static string Parse(DesiredPositionAdvancedWindow desiredPositionAdvancedWindow)
        {
            switch (desiredPositionAdvancedWindow)
            {
                case DesiredPositionAdvancedWindow.Left:
                    return LEFT;
                case DesiredPositionAdvancedWindow.Right:
                    return RIGHT;
                case DesiredPositionAdvancedWindow.Auto:
                    return AUTO;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    [ValueConversion(typeof (AdvancedWindowVisibleStyle), typeof (string))]
    public class AdvancedWindowVisibleStyleConverter : IValueConverter
    {
        private const string ALWAYS = "Всегда";
        private const string NEWER = "Никогда";
        private const string ONLY_COMMENT = "Только с комментарием";
        private const string ONLY_COMMENT_AND_ONLY_IS_NEW = "С комментарием или обновлением";
        private const string ONLY_IS_NEW = "Только с обновлением";
        private const string ALWAYSPanel = "Всегда панель";

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AdvancedWindowVisibleStyle[])
            {
                AdvancedWindowVisibleStyle[] arr = (AdvancedWindowVisibleStyle[]) value;
                string[] result = new string[arr.Length];
                int index = 0;
                foreach (AdvancedWindowVisibleStyle o in arr)
                {
                    result[index] = Parse(o);
                    index++;
                }
                return result;
            }
            return Parse((AdvancedWindowVisibleStyle) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string advancedWindowVisibleStyle = (string) value;
            switch (advancedWindowVisibleStyle)
            {
                case NEWER:
                    return AdvancedWindowVisibleStyle.Never;
                case ONLY_COMMENT:
                    return AdvancedWindowVisibleStyle.OnlyComment;
                case ONLY_IS_NEW:
                    return AdvancedWindowVisibleStyle.OnlyIsNew;
                case ONLY_COMMENT_AND_ONLY_IS_NEW:
                    return AdvancedWindowVisibleStyle.OnlyCommentAndOnlyIsNew;
                case ALWAYS:
                    return AdvancedWindowVisibleStyle.Always;
                case ALWAYSPanel:
                    return AdvancedWindowVisibleStyle.AlwaysPanel; 
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private static string Parse(AdvancedWindowVisibleStyle advancedWindowVisibleStyle)
        {
            switch (advancedWindowVisibleStyle)
            {
                case AdvancedWindowVisibleStyle.Never:
                    return NEWER;
                case AdvancedWindowVisibleStyle.OnlyComment:
                    return ONLY_COMMENT;
                case AdvancedWindowVisibleStyle.OnlyIsNew:
                    return ONLY_IS_NEW;
                case AdvancedWindowVisibleStyle.OnlyCommentAndOnlyIsNew:
                    return ONLY_COMMENT_AND_ONLY_IS_NEW;
                case AdvancedWindowVisibleStyle.Always:
                    return ALWAYS;
                case AdvancedWindowVisibleStyle.AlwaysPanel:
                    return ALWAYSPanel;
                default:
                    throw new ArgumentOutOfRangeException("advancedWindowVisibleStyle");
            }
        }
    }

    [ValueConversion(typeof (bool), typeof (string))]
    public class DefaultActionAsAuthorPageConverter : IValueConverter
    {
        private const string PAGE = "Страница в интернете";
        private const string WINDOW = "Окно с текстами";

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool[])
            {
                bool[] arr = (bool[]) value;
                string[] result = new string[arr.Length];
                int index = 0;
                foreach (bool o in arr)
                {
                    result[index] = Parse(o);
                    index++;
                }
                return result;
            }
            return Parse((bool) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((string) value)
            {
                case PAGE:
                    return true;
                case WINDOW:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private static string Parse(bool value)
        {
            return value ? PAGE : WINDOW;
        }

        public bool[] GetValues()
        {
            return new[] {true, false};
        }
    }

    [ValueConversion(typeof (bool), typeof (string))]
    public class DefaultReaderConverter : IValueConverter
    {
        private const string AJ = "AJ-Reader (F3)";
        private const string ALTERNATIVE = "Другая читалка (F4)";
        private const string PAGE = "Страница в интернете (F2)";
        private const string WINDOW = "Встроенная читалка (F1)";

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int[])
            {
                int[] arr = (int[]) value;
                string[] result = new string[arr.Length];
                int index = 0;
                foreach (int o in arr)
                {
                    result[index] = Parse(o);
                    index++;
                }
                return result;
            }
            return Parse((int) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((string) value)
            {
                case PAGE:
                    return 0;
                case WINDOW:
                    return 1;
                case AJ:
                    return 2;
                case ALTERNATIVE:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private static string Parse(int value)
        {
            string retValue = PAGE;
            switch (value)
            {
                case 0:
                    retValue = PAGE;
                    break;
                case 1:
                    retValue = WINDOW;
                    break;
                case 2:
                    retValue = AJ;
                    break;
                case 3:
                    retValue = ALTERNATIVE;
                    break;
                default:
                    break;
            }
            return retValue;
        }

        public int[] GetValues()
        {
            return new[] {1, 0, 2, 3};
        }
    }

    [ValueConversion(typeof(long), typeof(string))]
    public class IntervalOfUpdateConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long[])
            {
                long[] arr = (long[])value;
                string[] result = new string[arr.Length];
                int index = 0;
                foreach (long o in arr)
                {
                    result[index] = Parse(o);
                    index++;
                }
                return result;
            }
            return Parse((long)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((string)value == NEWER) return 0;
            string result = "";
            for (int i = 0; i < ((string)value).Length; i++)
            {
                if (char.IsDigit((string)value, i))
                    result = result + ((string) value)[i];
                else break;
            }
            string remain = ((string) value).Remove(0, result.Length).Trim();
            long @out;
            return (remain == "" || remain == "час" || remain == "часа" || remain == "часов") &&
                   (long.TryParse(result, out @out))
                       ? @out
                       : 1;
        }

        #endregion

        private const string NEWER = "Никогда";

        public static string Parse(long value)
        {
            if (value == 0) return NEWER;
            if ((value % 100 > 10) && (value % 100 < 20))
                return value + " часов";
            switch (value%10)
            {
                case 0:
                    return value + " часов";
                case 1:
                    return value + " час";
                case 2:
                case 3:
                case 4:
                    return value + " часа";
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return value + " часов";
            }
            return value.ToString();
        }

        public long[] GetValues()
        {
            return new long[] { 0, 1, 2, 3, 5, 7, 12, 18, 24 };
        }
    }
    #endregion
}