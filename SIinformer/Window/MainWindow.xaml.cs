using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SIinformer.Logic;
using SIinformer.Readers;
using SIinformer.Utils;
using Application=System.Windows.Application;
using Binding=System.Windows.Data.Binding;
using Button=System.Windows.Controls.Button;
using Clipboard=System.Windows.Clipboard;
using ContextMenu=System.Windows.Forms.ContextMenu;
using Cursors=System.Windows.Input.Cursors;
using DataFormats=System.Windows.DataFormats;
using DragDropEffects=System.Windows.DragDropEffects;
using DragEventArgs=System.Windows.DragEventArgs;
using KeyEventArgs=System.Windows.Input.KeyEventArgs;
using MenuItem=System.Windows.Controls.MenuItem;
using MessageBox=System.Windows.MessageBox;
using MouseEventArgs=System.Windows.Forms.MouseEventArgs;
using Point=System.Windows.Point;

namespace SIinformer.Window
{
    public partial class MainWindow
    {
        private Logger _logger;
        private static Setting _setting;

        public static MainWindow MainForm;

        private AuthorPanel authorPanel = null;

        private bool currentUseDatabase = false;
        private bool currentUseGoogle = false;

        public static Setting GetSettings()
        {
            return _setting;
        }

        public Logger GetLogger()
        {
            return _logger;
        }

        public DatabaseManager GetDatabaseManager()
        {
            return databaseManager;
        }

        public MainWindow()
        {
            InitializeComponent();
            MainForm = this;

            Loaded += MainWindowLoaded;

            // биндинг этих свойств вынесен в код, иначе в xaml окно схлопывается
            SetBinding(HeightProperty, new Binding {Path = new PropertyPath("Height"), Mode = BindingMode.TwoWay});
            SetBinding(WidthProperty, new Binding {Path = new PropertyPath("Width"), Mode = BindingMode.TwoWay});

            // запустить сервис обновления
            UpdateService.GetInstance().StartUpdate();
        }

        #region Инициализация

        private DatabaseManager databaseManager = null;
        // инициализировать работу с БД c возсможной конвертацией
        public void InitializeDatabase(bool swithing)
        {
            if (swithing)
            {
                //if (MessageBox.Show("Прочитать авторов из файла базы данных?\nЕсли вы ответите \"нет\", файл базы данных с авторами будет перезаписан текущим списком.", "Сообщение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                //{
                //    // реинициализируем апдейтер
                //    InfoUpdater.Init(_setting, _logger);
                //}

                if (MessageBox.Show("Сконвертировать данные из xml файла в базу данных?","Сообщение", MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.No)
                    swithing = false;
            }
            if (databaseManager==null)
                databaseManager = new DatabaseManager(swithing);
        }

        /// <summary>
        /// Процесс загрузки окна завершен
        /// </summary>
        public bool LoadedFlag { get; set; }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // создаем основную логику программы
            _setting = Setting.LoadFromXml();
            _setting.PropertyChanged += SettingPropertyChanged;

            // проконтролируем, показывать или нет панель произведений на главной странице в зависимости от настроек
            CheckForAuthorPanelAlwaysVisivility();

            _logger = new Logger();
            WEB.Init(_setting.ProxySetting, _logger);
            InfoUpdater.Init(_setting, _logger);
            InfoUpdater.InfoUpdaterRefresh += SetFocusToSelectedItem;
            InfoUpdater.Authors.ListChanged += ((o, le) =>
                                                    {
                                                        if ((le.PropertyDescriptor != null) &&
                                                            (le.PropertyDescriptor.Name == "IsNew"))
                                                        {
                                                            AuthorsListBox_SelectionChanged(o, null);
                                                            bool summaryIsNew = false;
                                                            foreach (Author author in InfoUpdater.Authors)
                                                            {
                                                                if (author.IsNew)
                                                                {
                                                                    summaryIsNew = true;
                                                                    break;
                                                                }
                                                            }
                                                            if (!summaryIsNew)
                                                                _mNotifyIcon.Icon = Properties.Resources.books;
                                                        }
                                                    });

            // инициализируем трей и сворачиваем окно
            InitTray();
#if !DEBUG
            HideWindow();
#endif

            // Создаются дополнительные кнопки в заголовке
            CreateTopButton();

            // Привязки
            DataContext = _setting;
            AuthorsListBox.ItemsSource = InfoUpdater.OutputCollection;
            StatusLabel.DataContext = _logger;
            PlayPauseButton.DataContext = _logger;
            LogListBox.DataContext = _logger;
            DownloadTextHelper.Init(downloadHelper, _logger, _setting);
            downloadHelper.ItemsSource = DownloadTextHelper.DownloadTextItems;

            AuthorsListBox.SelectedItem = InfoUpdater.Authors.FindAuthor(_setting.LastAuthorUrl);
            AuthorsListBox.Focus();
            SetFocusToSelectedItem();

            // Обновляем интерфейс (иконки на кнопках сортировки)
            SettingPropertyChanged(null, new PropertyChangedEventArgs("Init"));

            LoadedFlag = true;
        }

        /// <summary>
        /// Контроль показа панели с произведениями на главной странице
        /// </summary>
        private void CheckForAuthorPanelAlwaysVisivility()
        {
            if (_setting.AdvancedWindowVisibleStyle == AdvancedWindowVisibleStyle.AlwaysPanel)
            {
                if (authorPanel==null)
                {
                    authorPanel = new AuthorPanel();
                    AuthorPanelPlacement.Children.Clear();
                    AuthorPanelPlacement.Children.Add(authorPanel);
                }
                AuthorsListBox.SetValue(Grid.ColumnSpanProperty, 1);
                Spliter.Visibility = Visibility.Visible;                          
            }
            else
            {
                if (authorPanel!=null)
                {
                    AuthorPanelPlacement.Children.Clear();
                    AuthorsListBox.SetValue(Grid.ColumnSpanProperty, 2);
                    Spliter.Visibility = Visibility.Collapsed;
                    authorPanel = null;
                }
            }
                    
        }

        private void CreateTopButton()
        {
            StackPanel stackPanel = (StackPanel) FindControl(this, "stackPanel");
            ToggleButton topMostButton = new ToggleButton
                                             {
                                                 Width = 21,
                                                 Height = 21,
                                                 Cursor = Cursors.Hand,
                                                 ToolTip = "Поверх всех",
                                                 Content = "O",
                                                 IsChecked = _setting.Topmost
                                             };
            topMostButton.Click += ((sender, e) =>
                                        {
                                            Topmost = !Topmost;
                                            _setting.Topmost = Topmost;
                                            topMostButton.IsChecked = _setting.Topmost;
                                        });
            stackPanel.Children.Add(topMostButton);
            Topmost = _setting.Topmost;
            Button settingButton = new Button
                                       {
                                           Width = 20,
                                           Height = 20,
                                           Cursor = Cursors.Hand,
                                           ToolTip = "Настройки программы",
                                           Content = "p",
                                       };
            settingButton.Click += ((o, e) =>ShowSettingsWindow());
            stackPanel.Children.Add(settingButton);
        }

        #endregion

        /// <summary>
        /// вызов окна настроек
        /// </summary>
        public void ShowSettingsWindow()
        {
            currentUseDatabase = _setting.UseDatabase;
            SettingWindow settingWindow = new SettingWindow(_setting) { Owner = this };
            _setting = settingWindow.ShowDialog();
        }

        /// <summary>
        /// Реагирует на изменение некоторых настроек (сортировки, категории, правила для динамического окна)
        /// </summary>
        private void SettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "UseDatabase":
                    if (currentUseDatabase==_setting.UseDatabase) return;
                    if (_setting.UseDatabase)
                    {
                        InitializeDatabase(true);
                        // реинициализируем апдейтер
                        InfoUpdater.Init(_setting, _logger);
                    }else
                    {
                        if (MessageBox.Show("Прочитать авторов из файла xml?\nЕсли вы ответите \"нет\", файл xml с авторами будет перезаписан текущим списком.","Сообщение", MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
                        {
                            // реинициализируем апдейтер
                            InfoUpdater.Init(_setting, _logger);
                        }
                        if (databaseManager != null)
                            databaseManager = null;
                        InfoUpdater.Save();
                    }
                    // перепривязываем данные
                    AuthorsListBox.ItemsSource = InfoUpdater.OutputCollection;
                    break;
                case "Init":
                case "SortDirection":
                case "SortProperty":
                case "UseCategory":
                    InfoUpdater.Sort(_setting.SortProperty, _setting.SortDirection);
                    InfoUpdater.UseCategory = _setting.UseCategory;
                    switch (_setting.SortProperty)
                    {
                        case "UpdateDate":
                            SortPropertyButton.Content = "д";
                            SortPropertyButton.ToolTip = "Сортировка по дате обновления";
                            break;
                        case "Name":
                            SortPropertyButton.Content = "а";
                            SortPropertyButton.ToolTip = "Сортировка по авторам";
                            break;
                    }
                    switch (_setting.SortDirection)
                    {
                        case ListSortDirection.Ascending:
                            SortDirectButton.ToolTip = "Сортировка по возрастанию";
                            SortDirectButton.Content = FindResource("SortAscPath");
                            break;
                        case ListSortDirection.Descending:
                            SortDirectButton.ToolTip = "Сортировка по убыванию";
                            SortDirectButton.Content = FindResource("SortDescPath");
                            break;
                    }
                    AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
                    break;
                case "DesiredPositionAdvancedWindow":
                case "AdvancedWindowVisibleStyle":
                    {
                        CheckForAuthorPanelAlwaysVisivility();
                        AuthorsListBox_SelectionChanged(sender, null);
                    }
                    break;
            }
        }

        #region Drag&Drop

        private void AuthorsListBox_DragOver(object sender, DragEventArgs e)
        {
            string url = e.Data.GetData(DataFormats.Text, true) as string;
            if ((url != null) && (url.Trim() != ""))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void AuthorsListBox_Drop(object sender, DragEventArgs e)
        {
            string url = e.Data.GetData(DataFormats.Text, true) as string;
            if (url != null)
                AddAuthorPP(url);
        }

        #endregion

        #region Команды панели инструментов

        private void PlayPauseListCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthorsListBox.Focus();
            InfoUpdater.ManualProcessing();
        }

        private void SortDirectButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorsListBox.Focus();
            switch (_setting.SortDirection)
            {
                case ListSortDirection.Ascending:
                    _setting.SortDirection = ListSortDirection.Descending;
                    break;
                case ListSortDirection.Descending:
                    _setting.SortDirection = ListSortDirection.Ascending;
                    break;
            }
        }

        private void SortPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorsListBox.Focus();
            switch (_setting.SortProperty)
            {
                case "UpdateDate":
                    _setting.SortProperty = "Name";
                    break;
                case "Name":
                    _setting.SortProperty = "UpdateDate";
                    break;
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            InfoUpdater.Filter = FilterTextBox.Text;
        }

        private void ExtendedModeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthorsListBox.Focus();
            _setting.ExtendedMode = !_setting.ExtendedMode;
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            SetFocusToSelectedItem();
        }

        private void UseCategoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthorsListBox.Focus();
            _setting.UseCategory = !_setting.UseCategory;
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            SetFocusToSelectedItem();
        }

        private void FilterCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            FilterTextBox.Focus();
        }

        #endregion

        #region Действия с окном автора и страницей автора

        private void ShowAuthorPageFromSelected()
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            if (author == null) return;
            try
            {
                string url = author.URL;
                if ((!_setting.OpenAuthorPageSortingDate)&&(url.EndsWith("indexdate.shtml")))
                    url = url.Replace("indexdate.shtml", "");

                WEB.OpenURL(url);

                if (_setting.MarkAuthorIsReadWithAuthorPage)
                    author.IsNew = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть страничку автора\n\n" + ex.Message, "Ошибка", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void ShowAuthorWindowFromSelected()
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            if (author == null) return;
            AuthorUpdates au = AuthorUpdates.FindWindow(author);
            if (au != null)
            {
                au.Activate();
                if (au.WindowState == WindowState.Minimized) au.WindowState = WindowState.Normal;
            }
            else
            {
                au = new AuthorUpdates(author, _setting, _logger);
                au.Show();
            }
            if ((_advancedAuthorWindows != null) &&
                (_advancedAuthorWindows.Visibility == Visibility.Visible))
            {
                _advancedAuthorWindows.Visibility = Visibility.Hidden;
            }
        }

        private void ShowAuthorWindowOrAuthorPage()
        {
            if (_setting.DefaultActionAsAuthorPage)
                ShowAuthorPageFromSelected();
            else ShowAuthorWindowFromSelected();
        }

        #endregion

        #region Действия с категориями

        private void CategoryCollapsedCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Category category;
            if (e == null) // даблклик по категории (метод вызван из AuthorsListBox_MouseDoubleClick)
                category = AuthorsListBox.SelectedValue as Category;
            else category = ((Button) e.OriginalSource).DataContext as Category;
            if (category != null)
            {
                category.Collapsed = !category.Collapsed;
                InfoUpdater.Refresh();
            }
        }

        private void CategoryUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Category category = AuthorsListBox.SelectedValue as Category;
            if (category != null)
            {
                category.PositionUp();
                InfoUpdater.Refresh();
            }
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            SetFocusToSelectedItem();
        }

        private void CategoryDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Category category = AuthorsListBox.SelectedValue as Category;
            if (category != null)
            {
                category.PositionDown();
                InfoUpdater.Refresh();
            }
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            SetFocusToSelectedItem();
        }

        private void RenameCategoryCommandExecuted()
        {
            Category category = (Category) AuthorsListBox.SelectedValue;
            if (category == null) return;
            RenameWindow rw = new RenameWindow(category);
            if (rw.ShowDialog() == true)
            {
                InfoUpdater.BeginUpdate();
                try
                {
                    foreach (Author author in InfoUpdater.Authors)
                    {
                        if (author.Category == category.Name)
                            author.Category = rw.ResultNewName;
                    }
                    category.Name = rw.ResultNewName;
                }
                finally
                {
                    InfoUpdater.EndUpdate();
                }
            }
            InfoUpdater.Refresh();
            AuthorsListBox.ScrollIntoView(category);
            InfoUpdater.Save();
        }

        private void DeleteCategoryCommandExecuted()
        {
            int selectedIndex = AuthorsListBox.SelectedIndex;

            Category category = (Category) AuthorsListBox.SelectedValue;
            if (category == null) return;
            for (int i = 0; i < InfoUpdater.Categories.Count; i++)
            {
                if (InfoUpdater.Categories[i].Name == category.Name)
                {
                    InfoUpdater.Categories.RemoveAt(i);
                    break;
                }
            }

            InfoUpdater.Refresh();

            if (selectedIndex < AuthorsListBox.Items.Count)
                AuthorsListBox.SelectedIndex = selectedIndex;
            else AuthorsListBox.SelectedIndex = AuthorsListBox.Items.Count - 1;

            AuthorsListBox.ScrollIntoView(category);
            InfoUpdater.Save();
        }

        private void CategoryListBoxIsNotNull(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Category);
        }

        #endregion

        #region Команды автора

        private void IsReadAuthorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Author author;
            // если команда пришла от button'а (кликнули по звезде) - берем автора из кнопки
            // если от MainWindow (комбинация клавиш) - берем текущий элемент списка
            if (e.OriginalSource.GetType() == typeof (Button))
                author = ((Button) e.OriginalSource).DataContext as Author;
            else author = (Author) AuthorsListBox.SelectedValue;
            if (author == null) return;

            author.IsNew = false;

            InfoUpdater.Save();

            AuthorsListBox.SelectedItem = author;
            AuthorsListBox.ScrollIntoView(author);
        }

        private void AddAuthorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AddAuthorTextBox.Text))
            {
                AddAuthorPP(AddAuthorTextBox.Text);
                AddAuthorTextBox.Text = "";
            }
        }

        private void AddAuthorPP(string url)
        {
            Author author = InfoUpdater.AddAuthor(url);
            if (author != null)
            {
                InfoUpdater.Save();
                if (_setting.UseCategory)
                {
                    Category category = InfoUpdater.Categories.GetCategoryFromName(author.Category);
                    category.Collapsed = false;
                    InfoUpdater.Refresh();
                }
                AuthorsListBox.SelectedValue = author;
            }
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            AuthorsListBox.Focus();
            SetFocusToSelectedItem();
        }

        private void RenameAuthorCommandExecuted()
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            if (author == null) return;
            RenameWindow rw = new RenameWindow(author);
            if (rw.ShowDialog() == true)
                author.Name = rw.ResultNewName;
            AuthorsListBox.ScrollIntoView(author);
            InfoUpdater.Save();
        }

        private void RenameAuthorOrCategoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (AuthorsListBox.SelectedValue is Author)
                RenameAuthorCommandExecuted();
            if (AuthorsListBox.SelectedValue is Category)
                RenameCategoryCommandExecuted();
        }

        private void CopyAuthorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            string result = string.Format("{1}{0}{2}{0}", Environment.NewLine, author.Name, author.URL);
            if (_setting.ExtendedMode)
            {
                int i = 1;
                foreach (AuthorText text in author.Texts)
                {
                    if (text.IsNew)
                    {
                        result = result +
                                 string.Format("{0}{1}.{0}{2}-{3}{0}{4} ({5}){0}{6}{0}{7}{0}", Environment.NewLine, i,
                                               text.Genres, text.SectionName, text.Name, text.Size,
                                               author.URL.Replace("indexdate.shtml", text.Link), text.Description);
                        i++;
                    }
                }
            }
            try
            {
                Clipboard.SetText(result.Trim());
            }
            catch 
            {}
        }

        private void OpenAuthorWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowAuthorWindowFromSelected();
        }

        private void OpenAuthorPageCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowAuthorPageFromSelected();
        }

        private void DeleteAuthorCommandExecuted()
        {
            int selectedIndex = AuthorsListBox.SelectedIndex;
            Author author = (Author) AuthorsListBox.SelectedValue;
            AuthorsListBox.SelectedItem = null;

            InfoUpdater.DeleteAuthor(author);

            if (selectedIndex < AuthorsListBox.Items.Count)
                AuthorsListBox.SelectedIndex = selectedIndex;
            else AuthorsListBox.SelectedIndex = AuthorsListBox.Items.Count - 1;

            InfoUpdater.Save();
        }

        private void DeleteAuthorOrCategoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (AuthorsListBox.SelectedValue is Category)
                DeleteCategoryCommandExecuted();
            if (AuthorsListBox.SelectedValue is Author)
                DeleteAuthorCommandExecuted();
        }

        private void UpdateAuthorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            if (author.IsUpdated) return;
            _logger.Add(string.Format("'{0}' проверяется", author.Name));
            Updater updater = new Updater(_setting, _logger) {ManualUpdater = true};
            updater.UpdaterComplete += ((o, arg) =>
                                            {
                                                if (arg.Error != null)
                                                {
                                                    _logger.Add(arg.Error.StackTrace, false, true);
                                                    _logger.Add(arg.Error.Message, false, true);
                                                    _logger.Add(string.Format("'{0}' не проверен. Ошибка.",
                                                                              author.Name), true, true);
                                                }
                                                else
                                                {
                                                    _logger.Add(string.Format("'{0}' проверен",
                                                                              author.Name));
                                                    AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
                                                }
                                            });
            updater.RunWorkerAsync(new List<Author> {author});
        }

        private void IsIgnoredCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ((Author) AuthorsListBox.SelectedValue).IsIgnored = !((Author) AuthorsListBox.SelectedValue).IsIgnored;
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            InfoUpdater.Save();
        }

        private void ChangeCategoryAuthorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            if (e.OriginalSource.GetType() != typeof (Button)) return;
            Button button = (Button) e.OriginalSource;
            button.ContextMenu = new System.Windows.Controls.ContextMenu
                                     {
                                         PlacementTarget = button,
                                         Placement = PlacementMode.Bottom,
                                     };
            foreach (Category category in InfoUpdater.Categories)
            {
                if (author.Category != category.Name)
                {
                    MenuItem menuItem = new MenuItem {Header = category.Name};
                    menuItem.Click += ChangeCategoryMenuCommandClick;
                    button.ContextMenu.Items.Add(menuItem);
                }
            }
            if (button.ContextMenu.Items.Count != 0)
            {
                button.ContextMenu.Items.Add(new Separator());
            }
            MenuItem mi = new MenuItem {Header = "Новая категория"};
            mi.Click += NewCategoryMenuClick;
            button.ContextMenu.Items.Add(mi);
            button.ContextMenu.IsOpen = true;
        }

        private void NewCategoryMenuClick(object sender, RoutedEventArgs e)
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            RenameWindow rw = new RenameWindow();
            if (rw.ShowDialog() == true)
            {
                if (rw.ResultNewName.Trim() == "") return;                
                author.Category = rw.ResultNewName;
            }
            AuthorsListBox.ScrollIntoView(author);
            InfoUpdater.Save();
        }

        private void ChangeCategoryMenuCommandClick(object sender, RoutedEventArgs e)
        {
            Author author = (Author) AuthorsListBox.SelectedValue;
            MenuItem mi = (MenuItem) sender;
            author.Category = mi.Header.ToString();
            AuthorsListBox.ScrollIntoView(author);
        }

        private void AuthorsListBoxIsNotNull(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Author);
        }

        private void AuthorOrCategoryListBoxIsNotNull(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (AuthorsListBox.SelectedValue != null) &&
                           ((AuthorsListBox.SelectedValue is Author) || (AuthorsListBox.SelectedValue is Category));
        }

        private void AuthorOrCategoryListBoxIsNotNullAndCategoryIsEmpty(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (AuthorsListBox.SelectedValue != null) &&
                           ((AuthorsListBox.SelectedValue is Author) ||
                            ((AuthorsListBox.SelectedValue is Category) &&
                             (((Category) AuthorsListBox.SelectedValue).IsEmpty)));
        }

        #endregion

        #region Восстановление фокуса на элементе ListBox

        /// <summary>
        /// Устанавливает фокус на выбранном элементе списка.
        /// Фокус с элемента слетает, поскольку изменяется источник данных при изменении автора в списке.
        /// </summary>
        private void SetFocusToSelectedItem()
        {
            if (!AuthorsListBox.IsFocused) return;
            AuthorsListBox.ItemContainerGenerator.StatusChanged +=
                SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            IInputElement element =
                AuthorsListBox.ItemContainerGenerator.ContainerFromItem(AuthorsListBox.SelectedValue) as IInputElement;
            if (element != null)
            {
                Keyboard.Focus(element);
                AuthorsListBox.ItemContainerGenerator.StatusChanged -=
                    SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            }
        }

        private void SetFocusToSelectedItemItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator gen = (ItemContainerGenerator) sender;
            if (gen.Status == GeneratorStatus.ContainersGenerated)
            {
                IInputElement element =
                    AuthorsListBox.ItemContainerGenerator.ContainerFromItem(AuthorsListBox.SelectedValue) as
                    IInputElement;
                if (element != null)
                {
                    Keyboard.Focus(element);
                }
                AuthorsListBox.ItemContainerGenerator.StatusChanged -=
                    SetFocusToSelectedItemItemContainerGeneratorStatusChanged;
            }
        }

        #endregion

        #region События клавомыши

        private bool _keyEnterDown;

        private void AuthorsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if ((AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Author))
                    ShowAuthorWindowOrAuthorPage();
                if ((AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Category))
                    CategoryCollapsedCommand_Executed(sender, null);
            }
        }

        private void AuthorsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Нужно для контроля, что Enter был нажат в listBox, а не в другом окне (при переименовании?)
            // Действует в паре с AuthorsListBox_KeyUp - проверка if ((e.Key == Key.Enter) && (_keyEnterDown))
            if ((e.Key == Key.Enter) && (!e.IsRepeat))
            {
                _keyEnterDown = true;
                return;
            }
        }

        private void AuthorsListBox_KeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.I) &&
                (Keyboard.Modifiers == (ModifierKeys.Control ^ ModifierKeys.Alt ^ ModifierKeys.Shift)))
            {
                try
                {
                    WEB.OpenURL("http://zhurnal.lib.ru/comment/p/pupkin_wasja_ibragimowich/siinformer");
                }
                catch (Exception)
                {
                    MessageBox.Show("Не удалось открыть страничку отзывов", "Ошибка", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
            if ((e.Key == Key.Enter) && (_keyEnterDown))
            {
                if ((AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Author))
                    ShowAuthorWindowOrAuthorPage();
                if ((AuthorsListBox.SelectedValue != null) && (AuthorsListBox.SelectedValue is Category))
                    CategoryCollapsedCommand_Executed(sender, null);
                _keyEnterDown = false;
            }
            if ((e.Key == Key.A) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                _setting.SortProperty = "Name";
            }
            if ((e.Key == Key.D) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                _setting.SortProperty = "UpdateDate";
            }
            if ((e.Key == Key.S) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                SortPropertyButton_Click(sender, null);
            }
            if ((e.Key == Key.Up) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                _setting.SortDirection = ListSortDirection.Ascending;
            }
            if ((e.Key == Key.Down) && (Keyboard.Modifiers == ModifierKeys.Control))
            {
                _setting.SortDirection = ListSortDirection.Descending;
            }
            if (((e.Key == Key.Up) || (e.Key == Key.Down)) &&
                (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
            {
                SortDirectButton_Click(sender, null);
            }
            if ((e.Key == Key.Left) && (_setting.UseCategory))
            {
                if (AuthorsListBox.SelectedItem is Category)
                {
                    Category category = (Category) AuthorsListBox.SelectedItem;
                    category.Collapsed = true;
                    InfoUpdater.Refresh();
                }
                if (AuthorsListBox.SelectedItem is Author)
                {
                    string categoryName = ((Author) AuthorsListBox.SelectedItem).Category;
                    Category category = InfoUpdater.Categories.GetCategoryFromName(categoryName);
                    AuthorsListBox.SelectedItem = category;
                    AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
                    SetFocusToSelectedItem();
                }
            }
            if (e.Key == Key.Right)
            {
                if ((_setting.UseCategory) && (AuthorsListBox.SelectedItem is Category))
                {
                    Category category = (Category) AuthorsListBox.SelectedItem;
                    category.Collapsed = false;
                    InfoUpdater.Refresh();
                }
                if ((_advancedAuthorWindows != null) && (AuthorsListBox.SelectedItem is Author) &&
                    (_advancedAuthorWindows.Visibility != Visibility.Visible))
                {
                    AuthorsListBox_SelectionChanged(sender, null);
                }
            }
            if ((e.Key == Key.Escape) && (_advancedAuthorWindows != null) &&
                (_advancedAuthorWindows.Visibility == Visibility.Visible))
            {
                _advancedAuthorWindows.Visibility = Visibility.Hidden;
            }
        }

        private void AddAuthorTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddAuthorCommand_Executed(sender, null);
            }
        }

        private void StatusLabel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            switch (LogListBox.Visibility)
            {
                case Visibility.Collapsed:
                    LogListBox.Visibility = Visibility.Visible;
                    break;
                case Visibility.Visible:
                    _logger.IsError = false;
                    LogListBox.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        #endregion

        #region События окна

        /// <summary>
        /// Проверять перед закрытием флаг _setting.CloseHowToMinimize
        /// </summary>
        private bool _closeHowToMinimize;

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            HideWindow();
        }

        private void Maximize_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _closeHowToMinimize = true;
            Close();
        }

        private void DarkWindow_Closing(object sender, CancelEventArgs e)
        {
            // выход при возникновении ошибки без сохранения
            if (Application.Current.ShutdownMode ==ShutdownMode.OnExplicitShutdown) 
                return;
            if ((_setting.CloseHowToMinimize) && _closeHowToMinimize)
            {
                _closeHowToMinimize = false;
                e.Cancel = true;
                Minimize_Executed(e, null);
            }
            else
            {
                InfoUpdater.CancelUpdater();
                // сохранить всех авторов
                InfoUpdater.Save(false);

                if (AuthorsListBox.SelectedItem is Author)
                    _setting.LastAuthorUrl = ((Author) AuthorsListBox.SelectedItem).URL;
                _setting.SaveToXML(InfoUpdater.Authors);

                foreach (AuthorUpdates au in AuthorUpdates.OpenedWindows.ToArray())
                    au.Close();

                if (_mNotifyIcon != null)
                    _mNotifyIcon.Dispose();
                _mNotifyIcon = null;
            }
        }

        #endregion

        #region Tray

        private static NotifyIcon _mNotifyIcon;
        private static ContextMenu _trayMenu;

        public static void ShowTrayInfo(string message)
        {
            _mNotifyIcon.BalloonTipText = message;
            _mNotifyIcon.ShowBalloonTip(0);
            _mNotifyIcon.Icon = Properties.Resources.gotnews;
        }

        private void InitTray()
        {
            _mNotifyIcon = new NotifyIcon
                               {
                                   BalloonTipText = "Обновлений нет",
                                   BalloonTipTitle = "Информатор СИ",
                                   Text = "Информатор СИ",
                                   Icon = Properties.Resources.books,
                                   Visible = true
                               };
            _mNotifyIcon.Click += MNotifyIconClick;
            _mNotifyIcon.BalloonTipClicked += MNotifyIconBalloonTipClicked;

            _trayMenu = new ContextMenu();
            _trayMenu.MenuItems.Add("Показать Информатор СИ", TraymenuShowClick);
            _trayMenu.MenuItems.Add("Проверить обновления", TraymenuUpdateClick);
            _trayMenu.MenuItems.Add("-");
            _trayMenu.MenuItems.Add("Выход", TraymenuExitClick);
            _mNotifyIcon.ContextMenu = _trayMenu;
        }

        public void ShowWindow()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
            if (!IsActive) Activate();
            _trayMenu.MenuItems[0].Text = "Спрятать информатор СИ";
            _mNotifyIcon.Icon = Properties.Resources.books;

            AuthorsListBox.Focus();
            AuthorsListBox.ScrollIntoView(AuthorsListBox.SelectedValue);
            SetFocusToSelectedItem();
        }

        private void HideWindow()
        {
            Visibility = Visibility.Hidden;
            _mNotifyIcon.Icon = Properties.Resources.books;
            // гасит звезду в трее, если обновление произошло при открытом окне
            _trayMenu.MenuItems[0].Text = "Показать информатор СИ";
        }

        private void TraymenuShowClick(object sender, EventArgs e)
        {
            if (Visibility == Visibility.Hidden)
                ShowWindow();
            else HideWindow();
        }

        private static void TraymenuUpdateClick(object sender, EventArgs e)
        {
            InfoUpdater.ManualProcessing();
        }

        private void TraymenuExitClick(object sender, EventArgs e)
        {
            Close();
        }

        private void MNotifyIconBalloonTipClicked(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void MNotifyIconClick(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs) e;
            if (me.Button == MouseButtons.Left)
            {
                if (Visibility == Visibility.Visible)
                    HideWindow();
                else
                    ShowWindow();
            }
        }

        #endregion

        #region AdvancedAuthorWindows

        private AdvancedAuthorWindow _advancedAuthorWindows;

        private void AuthorsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_setting != null && _setting.AdvancedWindowVisibleStyle == AdvancedWindowVisibleStyle.AlwaysPanel)
            {
                if (AuthorsListBox.SelectedValue is Author)
                    authorPanel.Author = (Author)AuthorsListBox.SelectedValue;
                //Grid.SetColumnSpan(AuthorsListBox, 1);                    
            }


            if ((_advancedAuthorWindows != null) && (AuthorsListBox.SelectedItem == null) &&
                (_advancedAuthorWindows.Visibility == Visibility.Visible))
            {
                _advancedAuthorWindows.Visibility = Visibility.Hidden;
                return;
            }
            if (((_advancedAuthorWindows != null) && !(AuthorsListBox.SelectedValue is Author)))
            {
                _advancedAuthorWindows.Visibility = Visibility.Hidden;
                return;                
            }
            if (_advancedAuthorWindows != null)
            {
                Author au = (Author) AuthorsListBox.SelectedValue;
                AdvancedWindowVisibleStyle style = _setting.AdvancedWindowVisibleStyle;
                if (style == AdvancedWindowVisibleStyle.Never)
                {
                    _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }
                if ((((style & AdvancedWindowVisibleStyle.OnlyIsNew) == AdvancedWindowVisibleStyle.OnlyIsNew) &&
                     (!au.IsNew)) &&
                    (((style & AdvancedWindowVisibleStyle.OnlyComment) == AdvancedWindowVisibleStyle.OnlyComment) &&
                     (au.Comment.Trim() == "")))
                {
                    _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }
                if ((style == AdvancedWindowVisibleStyle.OnlyIsNew) && (!au.IsNew))
                {
                    _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }
                if ((style == AdvancedWindowVisibleStyle.OnlyComment) && (au.Comment.Trim() == ""))
                {
                    _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }
            }

            AuthorsListBox.ItemContainerGenerator.StatusChanged +=
                AuthorsListBoxSelectionChangedItemContainerGeneratorStatusChanged;
            IInputElement element =
                AuthorsListBox.ItemContainerGenerator.ContainerFromItem(AuthorsListBox.SelectedValue) as IInputElement;
            if (element != null)
            {
                ShowAdvancedAuthorWindow(element);
                AuthorsListBox.ItemContainerGenerator.StatusChanged -=
                    AuthorsListBoxSelectionChangedItemContainerGeneratorStatusChanged;
            }
        }

        private void AuthorsListBoxSelectionChangedItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator gen = (ItemContainerGenerator) sender;
            if (gen.Status == GeneratorStatus.ContainersGenerated)
            {
                IInputElement element =
                    AuthorsListBox.ItemContainerGenerator.ContainerFromItem(AuthorsListBox.SelectedValue) as
                    IInputElement;
                if (element != null)
                {
                    ShowAdvancedAuthorWindow(element);
                }
                AuthorsListBox.ItemContainerGenerator.StatusChanged -=
                    AuthorsListBoxSelectionChangedItemContainerGeneratorStatusChanged;
            }
        }

        private void ShowAdvancedAuthorWindow(IInputElement element)
        {
            try
            {
                if (_advancedAuthorWindows != null && _setting.AdvancedWindowVisibleStyle == AdvancedWindowVisibleStyle.AlwaysPanel)
                {
                    _advancedAuthorWindows.Close();
                    _advancedAuthorWindows = null;
                }

                if (_advancedAuthorWindows == null && _setting.AdvancedWindowVisibleStyle != AdvancedWindowVisibleStyle.AlwaysPanel)
                    _advancedAuthorWindows = new AdvancedAuthorWindow(_setting, _logger)
                                                 {
                                                     Visibility = Visibility.Hidden,
                                                     ShowActivated = false,
                                                     Owner = this
                                                 };

                if ((!(AuthorsListBox.SelectedValue is Author)) &&
                    (_advancedAuthorWindows.Visibility == Visibility.Visible))
                {
                    _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }

                ListBoxItem listBoxItem = element as ListBoxItem;
                if (listBoxItem == null) return;

                Rect rectListBoxItem = VisualTreeHelper.GetDescendantBounds(listBoxItem);
                if (rectListBoxItem == Rect.Empty) return;

                Point locationRelativelyScreen = listBoxItem.PointToScreen(rectListBoxItem.Location);

                Point locationRelativelyAuthorsListBox = AuthorsListBox.PointFromScreen(locationRelativelyScreen);
                if ((locationRelativelyAuthorsListBox.Y < 0) ||
                    (locationRelativelyAuthorsListBox.Y > AuthorsListBox.ActualHeight))
                {
                    if ((_advancedAuthorWindows != null) && (_advancedAuthorWindows.Visibility == Visibility.Visible))
                        _advancedAuthorWindows.Visibility = Visibility.Hidden;
                    return;
                }

                Point locationRelativelyForm = PointFromScreen(locationRelativelyScreen);
                Rect rect = new Rect(locationRelativelyForm, rectListBoxItem.Size);

                if (_advancedAuthorWindows != null)
                {
                    _advancedAuthorWindows.ReBuild((Author) AuthorsListBox.SelectedValue, rect);

                    if ((_advancedAuthorWindows.Visibility == Visibility.Hidden) && (Visibility == Visibility.Visible))
                    {
                        _advancedAuthorWindows.Visibility = Visibility.Visible;
                    }
                }
            }
            catch
            {
            }
        }

        private void AuthorsListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            AuthorsListBox_SelectionChanged(sender, null);
        }

        private void DarkWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((Visibility == Visibility.Visible) && (_advancedAuthorWindows != null) &&
                (_advancedAuthorWindows.Visibility == Visibility.Hidden))
            {
                _advancedAuthorWindows.Visibility = Visibility.Visible;
                AuthorsListBox_SelectionChanged(sender, null);
            }
            if ((Visibility == Visibility.Hidden) && (_advancedAuthorWindows != null) &&
                (_advancedAuthorWindows.Visibility == Visibility.Visible))
            {
                _advancedAuthorWindows.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region Липкие окошки

        private void DarkWindow_SourceInitialized(object sender, EventArgs e)
        {
            HwndSource hwndSource = (HwndSource) PresentationSource.FromVisual((DarkWindow) sender);
            if (hwndSource != null) hwndSource.AddHook(DragHook);
        }

        private static IntPtr DragHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handeled)
        {
            const int delta = 10;
            const int swpNoMove = 0x0002;
            const int windowPosChanging = 0x0046;
            switch (msg)
            {
                case windowPosChanging:
                    {
                        WindowPos pos = (WindowPos) Marshal.PtrToStructure(lParam, typeof (WindowPos));

                        if ((pos.Flags & swpNoMove) == 0)
                        {
                            Rectangle rect = SystemInformation.WorkingArea;

                            Point newPos = new Point(pos.X, pos.Y);

                            if ((pos.X - rect.Left < delta) && (pos.X >= 0))
                                newPos.X = 0;
                            else if ((rect.Right - (pos.X + pos.CX) < delta) && (pos.X + pos.CX <= rect.Right))
                                newPos.X = rect.Right - pos.CX;
                            if ((pos.Y - rect.Top < delta) && (pos.Y >= 0))
                                newPos.Y = 0;
                            else if ((rect.Bottom - (pos.Y + pos.CY) < delta) && (pos.Y + pos.CY <= rect.Bottom))
                                newPos.Y = rect.Bottom - pos.CY;

                            pos.X = (int) newPos.X;
                            pos.Y = (int) newPos.Y;

                            pos.Flags = 0;
                            Marshal.StructureToPtr(pos, lParam, true);
                            handeled = true;
                        }
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowPos
        {
            public IntPtr Hwnd;
            public IntPtr HwndInsertAfter;
            public int X;
            public int Y;
            public int CX;
            public int CY;
            public int Flags;
        }

        #endregion

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadTextItem downloadTextItem = ((Button)e.OriginalSource).DataContext as DownloadTextItem;
            if (downloadTextItem == null) return;
            downloadTextItem.Stop();
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            si_toolbar.Visibility = (si_toolbar.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

    }

    public static class MainCommands
    {
        public static readonly RoutedUICommand AddAuthorCommand =
            new RoutedUICommand("Добавить автора", "AddAuthorCommand", typeof (MainWindow),
                                new InputGestureCollection());

        public static readonly RoutedUICommand CategoryCollapsedCommand =
            new RoutedUICommand("Свернуть категорию", "CategoryCollapsedCommand", typeof (MainWindow),
                                new InputGestureCollection());

        public static readonly RoutedUICommand CategoryDownCommand =
            new RoutedUICommand("Категорию вниз", "CategoryDownCommand", typeof (MainWindow),
                                new InputGestureCollection());

        public static readonly RoutedUICommand CategoryUpCommand =
            new RoutedUICommand("Категорию вверх", "CategoryUpCommand", typeof (MainWindow),
                                new InputGestureCollection());

        public static readonly RoutedUICommand ChangeCategoryAuthorCommand =
            new RoutedUICommand("Переместить в категорию", "ChangeCategoryAuthorCommand", typeof (MainWindow),
                                new InputGestureCollection());

        public static readonly RoutedUICommand CopyAuthorCommand =
            new RoutedUICommand("Скопировать в буфер обмена", "CopyAuthorCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.C, ModifierKeys.Control)}));

        public static readonly RoutedUICommand DeleteAuthorOrCategoryCommand =
            new RoutedUICommand("Удалить автора или категорию", "DeleteAuthorOrCategoryCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.Delete, ModifierKeys.Control)}));

        public static readonly RoutedUICommand ExtendedModeCommand =
            new RoutedUICommand("Расширенный режим", "ExtendedModeCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.E, ModifierKeys.Control)}));

        public static readonly RoutedUICommand FilterCommand =
            new RoutedUICommand("Фильтр", "FilterCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.F, ModifierKeys.Control)}));

        public static readonly RoutedUICommand IsIgnoredCommand =
            new RoutedUICommand("Не проверять автора", "IsIgnoredCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.I, ModifierKeys.Control)}));

        public static readonly RoutedUICommand IsReadAuthorCommand =
            new RoutedUICommand("Пометить автора как прочитанного", "IsReadAuthorCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.M, ModifierKeys.Control)}));

        public static readonly RoutedUICommand OpenAuthorPageCommand =
            new RoutedUICommand("Открыть страничку автора", "OpenAuthorPageCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.F2, ModifierKeys.None)}));

        public static readonly RoutedUICommand OpenAuthorWindowCommand =
            new RoutedUICommand("Открыть информацию о произведениях", "OpenAuthorWindowCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.F1, ModifierKeys.None)}));

        public static readonly RoutedUICommand PlayPauseListCommand =
            new RoutedUICommand("Обновить данные", "PlayPauseListCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {
                                                                   new KeyGesture(Key.U,
                                                                                  ModifierKeys.Control |
                                                                                  ModifierKeys.Shift)
                                                               }));

        public static readonly RoutedUICommand RenameAuthorOrCategoryCommand =
            new RoutedUICommand("Переименовать автора или категорию", "RenameAuthorOrCategoryCommand",
                                typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.R, ModifierKeys.Control)}));

        public static readonly RoutedUICommand UpdateAuthorCommand =
            new RoutedUICommand("Обновить автора", "UpdateAuthorCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.U, ModifierKeys.Control)}));

        public static readonly RoutedUICommand UseCategoryCommand =
            new RoutedUICommand("Использовать категории", "UseCategoryCommand", typeof (MainWindow),
                                new InputGestureCollection(new InputGesture[]
                                                               {new KeyGesture(Key.W, ModifierKeys.Control)}));
    }

    public static class WindowCommands
    {
        public static readonly RoutedUICommand Close = new RoutedUICommand();
        public static readonly RoutedUICommand Maximize = new RoutedUICommand();
        public static readonly RoutedUICommand Minimize = new RoutedUICommand();
    }
}