using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using System.Data.SqlServerCe;   


namespace SIinformer.Logic
{
    public class AuthorList : BindingList<Author>
    {
        #region Retreive
        private void Retreive()
        {
            Add(new Author
                    {
                        Name = "Чужин",
                        IsNew = false,
                        UpdateDate = DateTime.Now,
                        URL = "http://zhurnal.lib.ru/c/chushin_i_a/"
                    });
            Add(new Author
                    {
                        Name = "Хазарх",
                        IsNew = false,
                        UpdateDate = DateTime.Now,
                        URL = "http://zhurnal.lib.ru/h/harzah_w/"
                    });
            Add(new Author
                    {
                        Name = "Конюшевский В.Н.",
                        IsNew = false,
                        UpdateDate = DateTime.Now,
                        URL = "http://zhurnal.lib.ru/k/kotow_w_n/"
                    });
        } 
        #endregion

        public static AuthorList Load()
        {
            var result = new AuthorList();
            
            #region Чтение из БД
            using (var con = new SqlCeConnection("Data Source=" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Author.sdf")))
            {
                con.Open();
                using (var com = new SqlCeCommand("Select [Id],[Name],[Url],[DateLine],[isIgnored], [Category] from [Author]", con))
                {
                    var dr = com.ExecuteReader();
                    while (dr.Read())
                        result.Add(new Author { Id = dr.GetInt32(0), Name = dr.GetString(1), URL = dr.GetString(2), UpdateDate = dr.GetDateTime(3), IsIgnored = dr.GetBoolean(4), Category = dr.GetString(5)});
                    
                    var aTexts = AuthorText.Read();

                    foreach (var aText in aTexts)
                    {
                        var a = result.Find(aText.IdAuthor);
                        if (a != null) a.Texts.Add(aText);
                    }

                    foreach (var a in result) a.PropertyChanged += AuthorPropertyChanged;                        
                }
            }
            result.ListChanged += AuthorListChanged;
            #endregion
            
            if (result.Count > 0) return result;

            #region Если есть данные в старом файле БД (пусть будет версии 0 с БД), то перегоняем оттуда
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Author.sdf_0")))
            {
                using (var con = new SqlCeConnection("Data Source=" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Author.sdf_0")))
                {
                    con.Open();
                    using (
                        var com = new SqlCeCommand("Select [Id],[Name],[Url],[DateLine],[isIgnored] from [Author]", con)
                        )
                    {
                        var dr = com.ExecuteReader();
                        while (dr.Read())
                        {
                            Author a = new Author
                                           {
                                               Id = dr.GetInt32(0),
                                               Name = dr.GetString(1),
                                               URL = dr.GetString(2),
                                               UpdateDate = dr.GetDateTime(3),
                                               IsIgnored = dr.GetBoolean(4)
                                           };
                            result.Add(a);

                            
                            a.Texts = new BindingList<AuthorText>();
                            using (
                                var _com =
                                    new SqlCeCommand(
                                        "Select [Id],[Name],[Url],[SectionName],[DateLine],[Genres],[Description],[Size],[LastSize],[IsNew],[IdAuthor] From [AuthorText] Where IdAuthor=" +
                                        a.Id.ToString(), con))
                            {

                                var _dr = _com.ExecuteReader();
                                while (_dr.Read())
                                {
                                    var aText = new AuthorText();
                                    aText.Read(_dr);
                                    aText.Id = 0;// перегенерим айдишку, чтобы произошло добавление в новую БД
                                    a.Texts.Add(aText);
                                    aText.Save();                                    
                                }
                            }                            
                        }
                    }

                    foreach (var a in result)
                    {
                        a.Id = 0;// перегенерим айдишку, чтобы произошло добавление в новую БД
                        a.Save();
                        a.PropertyChanged += AuthorPropertyChanged;
                    }

                }

                return result;
            }
            #endregion

            #region Чтение из XML
            try
            {
                using (var st = new StreamReader(InfoUpdater.AuthorsFileName))
                    result = (AuthorList)(new XmlSerializer(typeof(AuthorList))).Deserialize(st);               
            }
            catch
            {                
                result = new AuthorList();
                result.Retreive();                
            }
            result.ListChanged += new ListChangedEventHandler(AuthorListChanged);
                        
            foreach (var a in result)
            {
                
                a.Save();
                a.PropertyChanged += AuthorPropertyChanged;                
            }
            #endregion
            return result;
        }
               

        #region AuthorListChanged
        static void AuthorListChanged(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemDeleted:                    
                    
                    break;
                case ListChangedType.ItemAdded:                    
                    ((AuthorList)sender)[e.NewIndex].Save();
                    ((AuthorList)sender)[e.NewIndex].PropertyChanged += AuthorPropertyChanged;
                    break; 
            }


        } 
        #endregion       
        #region AuthorPropertyChanged
        static void AuthorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var a = (Author)sender;
            a.Save();
        }        
        #endregion
        
        public Author Find(int Id)
        {
            foreach (Author a in this) if (a.Id == Id) return a;
            return null;
        }

        public Author Find(string url)
        {
            foreach (Author a in this) if (a.URL == url) return a;
            return null;
        }

        public string[] GetCategoryNames()
        {
            List<string> result = new List<string>();
            foreach (Author author in this)
            {
                if (!result.Contains(author.Category))
                    result.Add(author.Category);
            }
            return result.ToArray();
        }
    }
}