using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using SIinformer.Logic;
using SIinformer.Readers;
using SIinformer.Utils;
using SIinformer.Window;

namespace SIinformer
{
    /// <summary>
    /// Логика взаимодействия для AuthorPanel.xaml
    /// </summary>
    public partial class AuthorPanel
    {
        #region Private Fields

        /// <summary>
        /// Категории текстов с состояниями свернутости
        /// </summary>
        private readonly List<CategoryText> _category = new List<CategoryText>();

        /// <summary>
        /// Выходной список текстов с категориями
        /// </summary>
        private readonly ObservableCollection<object> _outputCollection = new ObservableCollection<object>();

        private Author _author;
        private Logger _logger;
        //private Setting MainWindow.GetSettings();

        /// <summary>
        /// Промежуточное отсортированное, группированное представление текстов
        /// </summary>
        private ICollectionView _textView;

        #endregion

        public AuthorPanel()
        {
            InitializeComponent();
            authorTextsListBox.ItemsSource = _outputCollection;
            Loaded += ((o, e) => UpdateView(false));
            Unloaded += ((o, e) => { MainWindow.GetSettings().PropertyChanged -= SettingPropertyChanged; });
            _author = null;            
        }

        public Author Author
        {
            get { return _author; }
            set
            {
                if (_author != value)
                {
                    if (_author != null)
                    {
                        _author.PropertyChanged -= AuthorPropertyChanged;
                    }
                    _author = value;
                    _author.PropertyChanged += AuthorPropertyChanged;
                    _category.Clear();
                    textbox.DataContext = _author;
                    UpdateView(true);
                }
            }
        }

        public void SetSetting(Setting value, Logger logger)
        {
            _logger = logger;
            MainWindow.GetSettings().PropertyChanged += SettingPropertyChanged; // отписываемся в Unloaded
            //if (MainWindow.GetSettings() == null)
            //{
            //    MainWindow.GetSettings() = value;
            //    MainWindow.GetSettings().PropertyChanged += SettingPropertyChanged; // отписываемся в Unloaded
            //}
            //else
            //{
            //    throw new Exception("Повторное присвоение setting для AuthorPanel");
            //}
        }

        public void Build(Author author, double heightComment)
        {
            Author = author;
            grid.RowDefinitions[2].Height = new GridLength(heightComment);
        }

        private void SettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateView(false);
        }

        private void authorTextsListBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.S) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) SaveButton_Click(text, null);
            }
            if ((e.Key == Key.R) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) ReadTextButton_Click(text, null);
            }
            if (e.Key == Key.F1)
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) OpenReader(text, 1);
            }
            if (e.Key == Key.F2)
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) OpenReader(text, 0);
            }
            if (e.Key == Key.F3)
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) OpenReader(text, 2);
            }
            if (e.Key == Key.F4)
            {
                AuthorText text = authorTextsListBox.SelectedValue as AuthorText;
                if (text != null) OpenReader(text, 3);
            }
        }

        #region Генерация дерева текстов

        private void AuthorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == "Texts") || (e.PropertyName == "IsNew"))
                UpdateView(false);
        }

        private CategoryText GetCategoryFromName(string name)
        {
            foreach (CategoryText categoryText in _category)
            {
                if (categoryText.Name == name)
                    return categoryText;
            }
            return null;
        }

        private void UpdateView(bool scrollToFirstItem)
        {
            if (Author == null)
                return;

            var selItem = authorTextsListBox.SelectedItem;
            authorTextsListBox.Visibility = Visibility.Hidden;

            ListCollectionView view = new ListCollectionView(Author.Texts);

            // Вынесено в отдельный поток, потому что медленно сортируются и группируются 
            // авторы с большим количеством текстов
            BackgroundWorker worker = new BackgroundWorker();
            worker.RunWorkerCompleted += ((o, e) =>
                                              {
                                                  if ((e.Error != null) && (_logger != null))
                                                  {
                                                      _logger.Add(e.Error.StackTrace, false, true);
                                                      _logger.Add(e.Error.Message, false, true);
                                                      _logger.Add("Ошибка при создании окна с текстами", false, true);
                                                  }
                                                  // за время сортировки автор может и смениться -> выходим, ничего не делая
                                                  if (e.Result != Author) return;
                                                  _textView = view;
                                                  ShowText(view, selItem);
                                                  if ((scrollToFirstItem) && (authorTextsListBox.Items.Count > 0))
                                                  {
                                                      authorTextsListBox.SelectedItem = authorTextsListBox.Items[0];
                                                      authorTextsListBox.ScrollIntoView(authorTextsListBox.SelectedItem);
                                                      SetFocusToSelectedItem();
                                                  }
                                              });
            worker.DoWork += ((o, e) =>
                                  {
                                      try
                                      {
                                          // обновляем инфу о кешировании текстов
                                          foreach (AuthorText authorText in ((Author)e.Argument).Texts)
                                          {
                                              authorText.UpdateIsCached((Author)e.Argument);
                                          }
                                          // отсортируем
                                          switch (MainWindow.GetSettings().SortProperty)
                                          {
                                              case "UpdateDate":
                                                  view.SortDescriptions.Add(new SortDescription("IsNew",
                                                                                                ListSortDirection.Descending));
                                                  view.SortDescriptions.Add(new SortDescription("UpdateDate",
                                                                                                MainWindow.GetSettings().SortDirection));
                                                  view.SortDescriptions.Add(new SortDescription("SectionName",
                                                                                                ListSortDirection.Ascending));
                                                  view.SortDescriptions.Add(new SortDescription("Name",
                                                                                                ListSortDirection.Ascending));
                                                  break;
                                              case "Name":
                                                  view.SortDescriptions.Add(new SortDescription("SectionName",
                                                                                                MainWindow.GetSettings().SortDirection));
                                                  view.SortDescriptions.Add(new SortDescription("Name",
                                                                                                MainWindow.GetSettings().SortDirection));
                                                  break;
                                          }
                                          // сгруппируем тексты по секции
                                          view.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = "SectionName" });

                                      }
                                      catch 
                                      {} 
                                      e.Result = e.Argument;
                                  });
            worker.RunWorkerAsync(Author);
        }

        private void ShowText(ICollectionView view, object selItem)
        {
            if (view == null) return;
            int startsCategoryCount = _category.Count;
            _outputCollection.Clear();
            if (view.Groups!=null)
            {
                foreach (CollectionViewGroup @group in view.Groups)            
                ShowTextByGroup(@group, startsCategoryCount);
            }else
            {
                ShowTextByGroup(null, startsCategoryCount);
            }
            
            authorTextsListBox.Visibility = Visibility.Visible;

            authorTextsListBox.SelectedItem = selItem;
            SetFocusToSelectedItem();
        }

        private void ShowTextByGroup(CollectionViewGroup @group, int startsCategoryCount)
        {
            if (@group==null) return;
            CategoryText categoryText =
                GetCategoryFromName(@group==null ? "" : @group.Name.ToString());
            if (categoryText == null)
            {
                categoryText = new CategoryText
                    {
                        Name = @group == null ? "" : @group.Name.ToString(),
                        Collapsed = true
                    };
                _category.Add(categoryText);
            }
            categoryText.SetVisualNameAndIsNew(@group.Items);
            // если список категорий был пуст (т.е. тексты автора обновились полностью)
            // и в категории есть новые произведение, то раскрыть
            if ((startsCategoryCount == 0) && (categoryText.IsNew))
                categoryText.Collapsed = false;
            _outputCollection.Add(categoryText);
            if (!categoryText.Collapsed)
            {
                foreach (object item in @group.Items)
                {
                    _outputCollection.Add(item);
                }
            }
        }

        #endregion

        #region Обработчики событий

        private void ListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if ((authorTextsListBox.SelectedValue != null) && (authorTextsListBox.SelectedValue is AuthorText))
                    ShowSelectedURL(sender as ListBox);
                if ((authorTextsListBox.SelectedValue != null) && (authorTextsListBox.SelectedValue is CategoryText))
                    CategoryCollapsed_Click(sender, null);
            }
        }

        private void ShowSelectedURL(Selector listBox)
        {
            try
            {
                AuthorText authorText = (AuthorText) listBox.SelectedValue;
                if (authorText == null) return;
                OpenReader(authorText);
                authorText.IsNew = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть страничку\n\n" + ex.Message, "Ошибка", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof (Button))
            {
                AuthorText text = ((Button) e.OriginalSource).DataContext as AuthorText;
                if (text != null) text.IsNew = false;
            }
        }

        private void CategoryCollapsed_Click(object sender, RoutedEventArgs e)
        {
            CategoryText category;
            if (e == null) // даблклик по категории (метод вызван из ListMouseDoubleClick)
                category = authorTextsListBox.SelectedValue as CategoryText;
            else // клик по кнопке сворачивания
            {
                category = ((Button) e.OriginalSource).DataContext as CategoryText;
            }
            if (category != null)
            {
                category.Collapsed = !category.Collapsed;
                authorTextsListBox.SelectedItem = category;
                ShowText(_textView, authorTextsListBox.SelectedItem);
            }
        }

        private void authorTextsListBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if ((authorTextsListBox.SelectedValue != null) && (authorTextsListBox.SelectedValue is AuthorText))
                    ShowSelectedURL(sender as ListBox);
                if ((authorTextsListBox.SelectedValue != null) && (authorTextsListBox.SelectedValue is CategoryText))
                    CategoryCollapsed_Click(sender, null);
            }
            if (e.Key == Key.Left)
            {
                if (authorTextsListBox.SelectedItem is CategoryText)
                {
                    CategoryText category = (CategoryText) authorTextsListBox.SelectedItem;
                    category.Collapsed = true;
                    ShowText(_textView, authorTextsListBox.SelectedItem);
                }
                if (authorTextsListBox.SelectedItem is AuthorText)
                {
                    string sectionName = ((AuthorText) authorTextsListBox.SelectedItem).SectionName;
                    CategoryText category = GetCategoryFromName(sectionName);
                    authorTextsListBox.SelectedItem = category;
                    authorTextsListBox.ScrollIntoView(authorTextsListBox.SelectedValue);
                }
            }
            if (e.Key == Key.Right)
            {
                if (authorTextsListBox.SelectedItem is CategoryText)
                {
                    CategoryText category = (CategoryText) authorTextsListBox.SelectedItem;
                    category.Collapsed = false;
                    ShowText(_textView, authorTextsListBox.SelectedItem);
                }
            }
        }

        #endregion

        #region OpenText

        private void OpenReader(AuthorText authorText)
        {
            OpenReader(authorText, null);
        }

        private void OpenReader(AuthorText authorText, int? readerType)
        {
            if (readerType == null) readerType = MainWindow.GetSettings().DefaultReader;
            readerType = readerType ?? 0;

            var site = Logic.Sites.SitesDetector.GetSite(_author.URL);
            if (site != null)
                readerType = site.GetSupportedReaderNumber((int)readerType);
            
            string url = authorText.GetFullLink(_author);
            switch (readerType)
            {
                case 0: // веб-страничка
                    WEB.OpenURL(url.Trim());
                    break;
                case 1: // внутренняя читалка
                case 3: // другая читалка
                    DownloadTextItem item = DownloadTextHelper.Add(_author, authorText);
                    item.ReaderType = readerType;
                    item.DownloadTextComplete += ItemDownloadTextComplete;
                    if (item.Text == null)
                    {
                        item.Start();
                    }
                    else ItemDownloadTextComplete(item, null);
                    break;
                case 2: // Aj-reader
                    string aj = "http://samlib.ru/img/m/mertwyj_o_a/aj.shtml?" +
                                url.Replace("http://samlib.ru/", "");
                    WEB.OpenURL(aj.Trim());
                    break;
                default:
                    break;
            }
        }

        private void ItemDownloadTextComplete(DownloadTextItem sender, DownloadDataCompletedEventArgs e)
        {
            if (sender.ReaderType == null) sender.ReaderType = MainWindow.GetSettings().DefaultReader;
            sender.DownloadTextComplete -= ItemDownloadTextComplete;
            if ((e == null) || ((e.Error == null) && (!e.Cancelled)))
            {
                switch (sender.ReaderType)
                {
                    case 1: // внутренняя читалка
                        sender.Logger.Add(string.Format("Открывается книга '{0}'.", sender.AuthorText.Name));
                        SIXamlReader reader = new SIXamlReader(MainWindow.GetSettings());
                        if (!File.Exists(sender.GetCachedFileName()+".xaml"))
                        {
                            File.WriteAllText(sender.GetCachedFileName() + ".xaml", sender.Xaml, Encoding.GetEncoding(1251));
                        }
                        UpdateView(false);
                        reader.ShowReader(sender.GetCachedFileName() + ".xaml", sender);
                        break;
                    case 3: // другая читалка
                        if (MainWindow.GetSettings().AlternativeReader.Trim().Length == 0)
                        {
                            sender.Logger.Add("Не задана внешняя читалка");
                            break;
                        }
                        if (!File.Exists(MainWindow.GetSettings().AlternativeReader.Trim()))
                        {
                            sender.Logger.Add(string.Format("Не найдена внешняя читалка '{0}'",
                                                            MainWindow.GetSettings().AlternativeReader.Trim()));
                            break;
                        }
                        if ((MainWindow.GetSettings().BookConverter.Trim().Length != 0) && (!File.Exists(MainWindow.GetSettings().BookConverter.Trim())))
                        {
                            sender.Logger.Add(string.Format("Не найден конвертер '{0}'", MainWindow.GetSettings().BookConverter.Trim()));
                            break;
                        }
                        UpdateView(false);
                        StartReader(MainWindow.GetSettings(), sender);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void StartReader(Setting setting, DownloadTextItem sender)
        {
            Thread thread = new Thread(StartReaderPP) { IsBackground = true };
            thread.Start(new StartReaderThreadParam
                             {Context = SynchronizationContext.Current, Setting = setting, DownloadTextItem = sender});
        }

        private static void StartReaderPP(object obj)
        {
            StartReaderThreadParam param = (StartReaderThreadParam) obj;
            string removedFilename = "";

            string convertFileName = param.DownloadTextItem.GetCachedFileName();
            if (param.Setting.BookConverter.Trim().Length != 0)
            {
                string converterParam = param.Setting.BookConverterParam.Trim().Length == 0
                                            ? "{0} {1}"
                                            : param.Setting.BookConverterParam.Trim();
                converterParam = converterParam.Replace("{0}", '"' + param.DownloadTextItem.GetCachedFileName() + '"');
                if (converterParam.IndexOf("{1}") >= 0)
                {
                    convertFileName = param.DownloadTextItem.GetCachedConvertFileName();
                    removedFilename = convertFileName;
                    converterParam = converterParam.Replace("{1}", '"' + convertFileName + '"');
                }
                else
                {
                    int pos = converterParam.IndexOf("{1|");
                    if (pos >= 0)
                    {
                        converterParam = converterParam.Remove(pos, 3);
                        int pos1 = converterParam.IndexOf("}");
                        string ext = "";
                        if (pos1 >= 0)
                        {
                            ext = converterParam.Substring(pos, pos1 - pos);
                            converterParam = converterParam.Remove(pos, pos1 - pos + 1);
                        }
                        if (ext != "")
                        {
                            convertFileName = Path.ChangeExtension(param.DownloadTextItem.GetCachedConvertFileName(),
                                                                   "." + ext);
                            removedFilename = convertFileName;
                        }
                        converterParam = converterParam.Insert(pos, '"' + convertFileName + '"');
                    }
                }
                Process process = new Process
                                      {
                                          StartInfo =
                                              {
                                                  FileName = param.Setting.BookConverter,
                                                  Arguments = converterParam,
                                                  UseShellExecute = false
                                              }
                                      };
                process.Start();
                process.WaitForExit();
                process.Close();
            }
            string readerParam = param.Setting.AlternativeReaderParam.Trim().Length == 0
                                     ? convertFileName
                                     : param.Setting.AlternativeReaderParam.Replace("{0}", '"' + convertFileName + '"');
            Process processAlternativeReader = new Process
                                                   {
                                                       StartInfo =
                                                           {
                                                               FileName = param.Setting.AlternativeReader,
                                                               Arguments = readerParam,
                                                               UseShellExecute = false
                                                           }
                                                   };
            processAlternativeReader.Start();
            processAlternativeReader.WaitForExit();
            processAlternativeReader.Close();
            if (File.Exists(removedFilename)) File.Delete(removedFilename);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorText text = (e != null) && (e.OriginalSource.GetType() == typeof (Button))
                                  ? ((Button) e.OriginalSource).DataContext as AuthorText
                                  : sender as AuthorText;
            if (text != null)
            {
                DownloadTextItem item = DownloadTextHelper.Add(_author, text);
                item.DownloadTextComplete += ItemDownloadSaveComplete;
                if (item.Text == null)
                {
                    item.Start();
                }
                else ItemDownloadSaveComplete(item, null);
            }
        }

        private void ItemDownloadSaveComplete(DownloadTextItem sender, DownloadDataCompletedEventArgs args)
        {
            sender.DownloadTextComplete -= ItemDownloadTextComplete;
            if ((args == null) || ((args.Error == null) && (!args.Cancelled)))
            {
                if (sender.Text != null)
                {
                    var site = Logic.Sites.SitesDetector.GetSite(sender.GetAuthor().URL);

                    var dialog = new SaveFileDialog
                                                {
                                                    AddExtension = true,
                                                    Filter = "HTML-файлы|*.html|Все файлы|*.*",
                                                    ValidateNames = true,
                                                    OverwritePrompt = true,
                                                    FileName = site !=null ? site.GetFileName(sender.AuthorText) : sender.AuthorText.Link
                                                };
                    if (dialog.ShowDialog() == true)
                    {
                        File.WriteAllText(dialog.FileName, sender.Text, Encoding.GetEncoding(1251));
                    }
                }
            }
        }


        private void ReadTextButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorText text = (e != null) && (e.OriginalSource.GetType() == typeof (Button))
                                  ? ((Button) e.OriginalSource).DataContext as AuthorText
                                  : sender as AuthorText;
            if (text != null)
            {
                OpenReader(text);
            }
        }

        internal class StartReaderThreadParam
        {
            internal SynchronizationContext Context { get; set; }
            internal Setting Setting { get; set; }
            internal DownloadTextItem DownloadTextItem { get; set; }
        }

        #endregion

        #region Восстановление фокуса на элементе ListBox

        /// <summary>
        /// Устанавливает фокус на выбранном элементе списка.
        /// Фокус с элемента слетает, поскольку изменяется источник данных при изменении автора в списке.
        /// </summary>
        private void SetFocusToSelectedItem()
        {
            if (!authorTextsListBox.IsFocused) return;
            authorTextsListBox.ItemContainerGenerator.StatusChanged +=
                SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            IInputElement element =
                authorTextsListBox.ItemContainerGenerator.ContainerFromItem(authorTextsListBox.SelectedValue) as
                IInputElement;
            if (element != null)
            {
                Keyboard.Focus(element);
                authorTextsListBox.ItemContainerGenerator.StatusChanged -=
                    SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            }
        }

        private void SetFocusToSelectedItemItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator gen = (ItemContainerGenerator) sender;
            if (gen.Status == GeneratorStatus.ContainersGenerated)
            {
                IInputElement element =
                    authorTextsListBox.ItemContainerGenerator.ContainerFromItem(authorTextsListBox.SelectedValue) as
                    IInputElement;
                if (element != null)
                {
                    Keyboard.Focus(element);
                }
                authorTextsListBox.ItemContainerGenerator.StatusChanged -=
                    SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            }
        }

        #endregion

        #region Splitter

        public double HeightComment
        {
            get
            {
                double pos = grid.RowDefinitions[2].ActualHeight;
                return pos;
            }
        }

        private void authorTextsListBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if ((e.HeightChanged) && (MainWindow.GetSettings() != null))
            {
                if (SplitterChanged != null)
                    SplitterChanged(Author, HeightComment);
            }
        }

        public event SplitterChangedEventHandler SplitterChanged;

        #endregion

        #region Применить Binding к комментарию

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            BindingExpression be = textbox.GetBindingExpression(TextBox.TextProperty);
            if (be != null) be.UpdateSource();
        }

        private void textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            BindingExpression be = textbox.GetBindingExpression(TextBox.TextProperty);
            if (be != null) be.UpdateSource();
        }

        private void textbox_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            BindingExpression be = textbox.GetBindingExpression(TextBox.TextProperty);
            if (be != null) be.UpdateSource();
        }

        #endregion
    }

    public delegate void SplitterChangedEventHandler(Author author, double heightComment);

    public class CategoryText : BindableObject
    {
        #region Private Fields

        private bool _isCollapsed;
        private bool _isNew;
        private string _name;
        private string _visualName;

        #endregion

        #region Public Property

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged("Name");
                }
            }
        }

        public bool Collapsed
        {
            get { return _isCollapsed; }
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    RaisePropertyChanged("Collapsed");
                }
            }
        }

        public bool IsNew
        {
            get { return _isNew; }
            set
            {
                if (_isNew != value)
                {
                    _isNew = value;
                    RaisePropertyChanged("IsNew");
                }
            }
        }

        public string VisualName
        {
            get { return _visualName; }
            set
            {
                if (_visualName != value)
                {
                    _visualName = value;
                    RaisePropertyChanged("VisualName");
                }
            }
        }

        #endregion

        #region Public Method

        public void SetVisualNameAndIsNew(ReadOnlyObservableCollection<object> texts)
        {
            int counter = 0;
            int counterIsNew = 0;
            foreach (AuthorText author in texts)
            {
                if (author.SectionName == Name) counter++;
                if ((author.SectionName == Name) && (author.IsNew)) counterIsNew++;
            }
            VisualName = counterIsNew == 0
                             ? string.Format("{0} ({1})", Name, counter)
                             : string.Format("{0} ({1}/{2})", Name, counter, counterIsNew);
            IsNew = counterIsNew != 0;
        }

        #endregion
    }


    public class SizeVisibilityConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            var size = (int) value;
            return size == -1 ? Visibility.Collapsed : Visibility.Visible;
            //var authorText = (AuthorText) value;
            //if (authorText == null) return Visibility.Visible;

            //return authorText.Size==-1 ? Visibility.Collapsed : Visibility.Visible;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class AuthorListBoxDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            ContentPresenter pres = (ContentPresenter) container;
            return item != null && item is CategoryText
                       ? pres.FindResource("ListItemsTemplate_AuthorTextCategory") as DataTemplate
                       : pres.FindResource("ListItemsTemplate_AuthorText") as DataTemplate;
        }
    }
}