using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using SIinformer.Logic;
using System.Xml.Serialization;
using System.Globalization;
using System.Xml;
using SIinformer.Window;
using System.Windows;

namespace SIinformer.Utils
{
    public class DatabaseManager
    {
        SQLiteConnection conn = null;
        DataSet ds = new DataSet();
        SQLiteDataAdapter da;

        /// <summary>
        /// Означает, что мы переключаемся с обычного xml хранилища на БД, а значит может понадобиться конвертация данных
        /// </summary>
        private bool switching = false;

        private string db_name 
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Authors.sqlite"); }
        }
        /// <summary>
        /// Создать менеджер
        /// </summary>
        /// <param name="Switching">Переключаемся в режим БД или просто открываем менеджер. True - переключаемся(нужна конвертация)</param>
        public DatabaseManager(bool Switching)
        {
            switching = Switching;
            bool newDB = false;
            if (!File.Exists(db_name))
            {
                PrepareDB();
                newDB = true;
            }
            else
            {
                OpenDB();
            }
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
        }

        public void Save(bool SaveAll)
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



        #region Авторы
        object _locker = new object();
        /// <summary>
        /// Сохранение автора в БД
        /// </summary>
        /// <param name="author"></param>
        public void SaveAuthor(Author author)
        {

            lock (_locker)
            {


                if (string.IsNullOrEmpty(author.Id))
                    //author.Id = Guid.NewGuid().ToString();
                    author.CheckID();
                //author.timeStamp = DateTime.Now.ToUniversalTime();
                string author_xml = Author2Xml(author);
                if (!string.IsNullOrEmpty(author_xml))
                {

                    // проверим, есть ли такой автор в БД
                    using (SQLiteCommand cmd = new SQLiteCommand("", conn))
                    {
                        string sql = "";
                        sql = string.Format("select * from authors where id='{0}'", author.Id);
                        cmd.CommandText = sql;
                        int cnt = cmd.ExecuteNonQuery();
                        //long timeStamp = author.timeStamp.ToBinary();

                        if (ds != null) ds.Dispose();
                        ds = new DataSet();
                        da = new SQLiteDataAdapter();
                        da.SelectCommand = cmd;
                        var cb = new System.Data.SQLite.SQLiteCommandBuilder(da);
                        da.Fill(ds, "author");
                        if (ds.Tables["author"].Rows.Count == 0)
                        {
                            DataRow dr = ds.Tables["author"].NewRow();
                            dr["id"] = author.Id;
                            ds.Tables["author"].Rows.Add(dr);
                        }
                        ds.Tables["author"].Rows[0]["author_xml"] = author_xml;
                        //ds.Tables["author"].Rows[0]["stamp"] = timeStamp;
                        da.Update(ds.Tables["author"]);
                        //if (cnt > 0)
                        //    sql = string.Format("update authors set author_xml='{0}', stamp={1} where id='{1}'", author_xml, timeStamp, author.Id);
                        //else
                        //    sql = string.Format("insert into authors (id, author_xml, stamp) ('{0}','{1}',{2})", author.Id, author_xml, timeStamp);
                        //cmd.CommandText = sql;
                        //cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Загрузка авторов из БД
        /// </summary>
        /// <returns></returns>
        public AuthorList LoadAuthors()
        {
            if (ds != null) ds.Dispose();
            ds = new DataSet();
            AuthorList list = new AuthorList();
            using (SQLiteCommand mycommand = new SQLiteCommand(conn))
            {
                mycommand.CommandText = "select * from authors";
                da = new SQLiteDataAdapter();
                da.SelectCommand = mycommand;
                System.Data.SQLite.SQLiteCommandBuilder cb = new System.Data.SQLite.SQLiteCommandBuilder(da);
                da.Fill(ds, "authors");
                foreach (DataRow row in ds.Tables["authors"].Rows)
                {
                    var author = Xml2Author(row["author_xml"].ToString());
                    if (author != null)
                    {
                        author.Changed = false;
                        list.Add(author);
                    }
                }
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
            long timeStamp = DateTime.Now.ToUniversalTime().ToBinary();

            var xs = new XmlSerializer(typeof(CategoryList));
            var sb = new StringBuilder();
            var w = new StringWriter(sb, CultureInfo.InvariantCulture);
            xs.Serialize(w, categories,
                         new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) }));

            string categories_xml = sb.ToString();

            if (!string.IsNullOrEmpty(categories_xml))
            {
                lock (_locker)
                {


                    using (SQLiteCommand cmd = new SQLiteCommand("", conn))
                    {
                        try
                        {
                            if (ds != null) ds.Dispose();
                            ds = new DataSet();
                            da = new SQLiteDataAdapter();
                            string sql = string.Format("select * from categories");
                            cmd.CommandText = sql;
                            da.SelectCommand = cmd;
                            var cb = new System.Data.SQLite.SQLiteCommandBuilder(da);
                            da.Fill(ds, "categories");

                            // исправляем свой косяк, надо было изначально ID сделать, иначе SQLite без праймари ки ругается на датасет
                            // это чтобы не перегенерировать БД
                            if (!ds.Tables["categories"].Columns.Contains("id"))
                            {
                                // убьем таблицу категорий
                                cmd.CommandText = "DROP TABLE categories";
                                cmd.ExecuteNonQuery();
                                // создаем таблицу категорий. Данных там мало и чтобы не мудохаться все храним в одной записи-строке
                                sql = "create table categories(" +
                                      "id varchar PRIMARY KEY ,categories_xml text, stamp bigint)";
                                cmd.CommandText = sql;
                                cmd.ExecuteNonQuery();
                                // формируем датасет и дальше уже пишем данные
                                ds = new DataSet();
                                da = new SQLiteDataAdapter();
                                sql = string.Format("select * from categories");
                                cmd.CommandText = sql;
                                da.SelectCommand = cmd;
                                cb = new System.Data.SQLite.SQLiteCommandBuilder(da);
                                da.Fill(ds, "categories");
                                MainWindow.MainForm.GetLogger().Add(
                                    "Перегенерировали таблицу категорий (исправление некорректности).");
                            }

                            if (ds.Tables["categories"].Rows.Count == 0)
                            {
                                DataRow dr = ds.Tables["categories"].NewRow();
                                dr["id"] = 1;
                                dr["categories_xml"] = categories_xml;
                                dr["stamp"] = timeStamp;
                                ds.Tables["categories"].Rows.Add(dr);
                            }
                            else
                            {
                                ds.Tables["categories"].Rows[0]["categories_xml"] = categories_xml;
                                ds.Tables["categories"].Rows[0]["stamp"] = timeStamp;
                            }
                            da.Update(ds.Tables["categories"]);

                        }
                        catch (Exception ex)
                        {
                            MainWindow.MainForm.GetLogger().Add("Ошибка записи категорий в БД: " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Загрузить категории
        /// </summary>
        /// <returns></returns>
        public CategoryList LoadCategories()
        {
            if (ds != null) ds.Dispose();
            ds = new DataSet();
            CategoryList list = null;
            using (SQLiteCommand mycommand = new SQLiteCommand(conn))
            {
                mycommand.CommandText = "select * from categories";
                da = new SQLiteDataAdapter();
                da.SelectCommand = mycommand;
                System.Data.SQLite.SQLiteCommandBuilder cb = new System.Data.SQLite.SQLiteCommandBuilder(da);
                da.Fill(ds, "categories");
                foreach (DataRow row in ds.Tables["categories"].Rows)
                {
                    string categories_xml = row["categories_xml"].ToString();
                    if (!string.IsNullOrEmpty(categories_xml))
                    {
                        try
                        {
                            var reader = new StringReader(categories_xml);
                            var sr = new XmlSerializer(typeof(CategoryList));
                            list = (CategoryList)sr.Deserialize(reader);
                            foreach (Category category in list)
                                category.SetOwner(list);
                            list.Reorder();
                            return list;//(CategoryList)sr.Deserialize(reader);
                        }
                        catch
                        { }
                        break;
                    }
                }
            }
            if (list == null)
            {
                list = new CategoryList();
                list.Add(new Category() { Name = "Default" });
            }
            foreach (Category category in list)            
                category.SetOwner(list);            
            list.Reorder();

            return list;
        }


        #endregion

#region Работа с БД
		        /// <summary>
        /// Очистка БД
        /// </summary>
        private void ClearDB()
        {
            using (SQLiteCommand cmd = new SQLiteCommand("", conn))
            {
                string sql = "";
                sql = "delete from authors";
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                sql = "delete from categories";
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void OpenDB()
        {
            try
            {
                conn = new SQLiteConnection("Data Source=" + db_name);
                conn.Open();

            }
            catch (Exception ex)
            {                
                if (MessageBox.Show(string.Format("Ошибка открытия файла базы данных {0}.\n{1}\n Сгенерировать его заново?", db_name, ex.Message),"Ошибка", MessageBoxButton.YesNo, MessageBoxImage.Error)==MessageBoxResult.Yes)
                {
                    PrepareDB();
                    OpenDB();
                }
            }
        }
        /// <summary>
        /// Создание БД для хранения авторов и всех их данных        
        /// </summary>
        private void PrepareDB()
        {
            try { conn.Close(); }
            catch { }
            SQLiteConnection.CreateFile(db_name);
            conn = new SQLiteConnection("Data Source=" + db_name);
            conn.Open();
            SQLiteCommand cmd = new SQLiteCommand("", conn);
            using (cmd)
            {
                string sql = "";
                sql = "create table authors(id varchar PRIMARY KEY ,  " +
                      "author_xml text, stamp bigint)";
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                cmd.CommandText = "CREATE INDEX id_authors ON authors (id)";
                cmd.ExecuteNonQuery();
                // индекс по штампу, чтобы искать быстрее при синхронизации с инетом
                cmd.CommandText = "CREATE INDEX stamp_authors ON authors (stamp)";
                cmd.ExecuteNonQuery();
                // создаем таблицу категорий. Данных там мало и чтобы не мудохаться все храним в одной записи-строке
                sql = "create table categories(" +
                      "id varchar PRIMARY KEY ,categories_xml text, stamp bigint)";
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

	#endregion    
    }
}
