using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using SIinformer.Logic;
using System.Xml.Serialization;
using System.Globalization;
using System.Xml;
using SIinformer.Window;
using System.Windows;
using Db4objects.Db4o;
using Db4objects.Db4o.CS;
using Db4objects.Db4o.TA;
using System.Threading.Tasks;
using Db4objects.Db4o.Defragment;

namespace SIinformer.Utils
{
    public class DatabaseManager
    {
        public IObjectServer server = null;

        bool IsCoverting = false;
        /// <summary>
        /// Означает, что мы переключаемся с обычного xml хранилища на БД, а значит может понадобиться конвертация данных
        /// </summary>
        private bool switching = false;

        private string db_name 
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data\Authors.db4o"); }
        }
        /// <summary>
        /// Создать менеджер
        /// </summary>
        /// <param name="Switching">Переключаемся в режим БД или просто открываем менеджер. True - переключаемся(нужна конвертация)</param>
        public DatabaseManager(bool Switching)
        {
            InfoUpdater.RestoreFileFromBinFolder(db_name); // переносим бд в папку Data, если ее там еще нет, а находится она в каталоге программы
            switching = Switching;
            bool newDB = false;
            if (!File.Exists(db_name))
            {
                newDB = true;
            }
            OpenDB();
            if (switching)
            {
                Convert2DB();
            }
            else
            {
                if (newDB)
                    if (MessageBox.Show("В настройках указано использовать базу данных, она создана.\nСконвертировать данные из xml файла в базу данных?", "Сообщение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        InfoUpdater.LoadDataFromXml();
                        Convert2DB();
                    }
            }
        }
        /// <summary>
        /// Сконвертировать танные из плоского файла в БД
        /// </summary>
        private void Convert2DB()
        {
            if (server == null) return;
            IsCoverting = true;
            MainWindow.MainForm.GetLogger().Add("Конвертируются данные в БД...",true);
            // если данные были в БД, чистим ее
            ClearDB();
            // если переключаемся, значит авторы уже закружены, тогда мы их просто переносим сюда
            foreach (Author author in InfoUpdater.Authors)
            {
                SaveAuthor(author);
            }
            SaveCategories(InfoUpdater.Categories);
            MainWindow.MainForm.GetLogger().Add("Конвертация в БД завершена", true);
            IsCoverting = false;
        }

        public void Save(bool SaveAll)
        {
            if (IsCoverting) return;
            // запускаем сохранение в отдельном потоке
            Task.Factory.StartNew(() =>
                {
                    foreach (Author author in InfoUpdater.Authors)
                    {
                        if (author.Changed || SaveAll)
                        {
                            author.Changed = false;
                            SaveAuthor(author);
                        }
                    }
                    SaveCategories(InfoUpdater.Categories);
                }
            );

        }

        #region Авторы
        object _locker = new object();
        /// <summary>
        /// Сохранение автора в БД
        /// </summary>
        /// <param name="author"></param>
        public void SaveAuthor(Author author)
        {

            if (server == null) return;
            try
            {                
                using (IObjectContainer documentStore = server.OpenClient())
                {
                    var _author = documentStore.Query<AuthorDb4o>(x => x.Id == author.Id).FirstOrDefault();
                    if (_author != null)
                    {
                        _author.author = author;
                        documentStore.Delete(_author);
                        documentStore.Store(_author);
                    }else
                        documentStore.Store(new AuthorDb4o() { Id = author.Id, author = author });
                    documentStore.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения данных автора в БД." + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

         /// <summary>
        /// Сохранение автора в БД в отдельном потоке, например при удалении автора, чтобы не тормозил интерфейс
        /// </summary>
        /// <param name="author"></param>
        public void SaveAuthorThreaded(Author author)
        {
            Task.Factory.StartNew(() => SaveAuthor(author));
        }
       

        /// <summary>
        /// Загрузка авторов из БД
        /// </summary>
        /// <returns></returns>
        public AuthorList LoadAuthors()
        {
            AuthorList list = new AuthorList();
            if (server == null) return null;

            try
            {
            Task task = Task.Factory.StartNew(() =>
                {

                    using (IObjectContainer documentStore = server.OpenClient())
                    {
                        var authors = documentStore.Query<AuthorDb4o>(x => !x.author.IsDeleted);
                        foreach (var author in authors)
                        {
                            author.author.Changed = false;
                            list.Add(author.author);
                        }

                    }
                });
            task.Wait();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка выборки данных из БД." + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return list;
        }

        /// <summary>
        /// Конвертация автора из Xml в объект Author
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        private Author Xml2Author(string xml)
        {
            try
            {
                var reader = new StringReader(xml);
                var sr = new XmlSerializer(typeof(Author));
                return (Author)sr.Deserialize(reader);
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Конвертация автора в xml
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        private string Author2Xml(Author author)
        {
            var xs = new XmlSerializer(typeof(Author));
            var sb = new StringBuilder();
            var w = new StringWriter(sb, CultureInfo.InvariantCulture);
            xs.Serialize(w, author,
                         new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) }));

            return sb.ToString();
        }

        #endregion

        #region Категории

        /// <summary>
        /// Сохранить категории
        /// </summary>
        public void SaveCategories(CategoryList categories)
        {
            if (server == null) return;
            try
            {
                using (IObjectContainer documentStore = server.OpenClient())
                {

                    foreach (var category in categories)
                    {
                        var _category = documentStore.Query<Category>(x => x.Name == category.Name).FirstOrDefault();
                        if (_category != null)
                        {
                            _category.Name = category.Name;
                            documentStore.Delete(_category);
                            documentStore.Store(_category);
                        }
                        else
                            documentStore.Store(category);
                    }                                            
                    documentStore.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка записи категорий в БД." + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        /// <summary>
        /// Загрузить категории
        /// </summary>
        /// <returns></returns>
        public CategoryList LoadCategories()
        {

            CategoryList list = new CategoryList();
            if (server == null) return null;
            try
            {
                using (IObjectContainer documentStore = server.OpenClient())
                {
                    var categories = documentStore.Query<Category>();

                    //foreach (Category category in categories)
                    //    documentStore.Delete(category);
                    //documentStore.Commit();

                    if (categories.Count() > 0)
                    {
                        foreach (var category in categories)
                            list.Add(category);
                    }
                    else
                    {
                        Category c = new Category() { Name = "Default" };
                        list.Add(c);
                        documentStore.Store(c);
                        documentStore.Commit();
                    }
                    foreach (Category category in list)
                        category.SetOwner(list);
                    list.Reorder();                   
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка выборки категорий из БД." + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return list;
        }

        #endregion

        #region Работа с БД
        /// <summary>
        /// Очистка БД
        /// </summary>
        private void ClearDB()
        {
            if (server == null) return;
            try
            {
                using (IObjectContainer documentStore = server.OpenClient())
                {
                    var authors = documentStore.Query<Author>();
                    foreach (var author in authors.ToArray())                    
                        documentStore.Delete(author);

                    var categories = documentStore.Query<Category>();
                    foreach (var category in categories.ToArray())
                        documentStore.Delete(category);
                    documentStore.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка очистки БД." + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        public void DefragmentDB()
        {

            if (server != null) server.Close();
            if (!Directory.Exists(InfoUpdater.BackupsFolder)) Directory.CreateDirectory(InfoUpdater.BackupsFolder);
            string backup_file = string.Format("authors.{0}.db4o", InfoUpdater.TimeStamp); //DateTime.Now.ToString()).Replace(" ", "_").Replace(":", "_");
            backup_file = Path.Combine(InfoUpdater.BackupsFolder, backup_file);
            
            // дефрагментируем БД
            SIinformer.Window.MainWindow.MainForm.GetLogger().Add(DateTime.Now.ToString() + "  Дефрагментируется база данных...", true, false);
            Defragment.Defrag(db_name, backup_file);
            SIinformer.Window.MainWindow.MainForm.GetLogger().Add(DateTime.Now.ToString() + "  Дефрагментация выполнена.", true, false);

        }

        private void OpenDB()
        {
            if (IsCoverting) return;
            if (server != null) return;
            try
            {               
                var server_config = Db4oClientServer.NewServerConfiguration();
                server_config.Common.AllowVersionUpdates = true;
                server_config.Common.Add(new TransparentActivationSupport());
                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "do_defragment_backup")))
                {
                    DefragmentDB();
                    File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "do_defragment_backup"));
                }
                server = Db4oClientServer.OpenServer(server_config, db_name, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Ошибка открытия файла базы данных {0}.\n{1}\n ", db_name, ex.Message), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Создание БД для хранения авторов и всех их данных        
        /// </summary>
        private void PrepareDB()
        {
        }

        #endregion
    }
}
