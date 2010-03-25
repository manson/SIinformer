using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SIinformer.Logic
{
    public class AuthorList : BindingList<Author>
    {
        private bool _isDefault;

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
            _isDefault = true;
        }

        public static AuthorList Load(string authorsFileName)
        {
            return Load(authorsFileName, false);
        }

        public static AuthorList Load(string authorsFileName, bool isBackupLoad)
        {
            AuthorList result;
            bool isCorrect = false;
            try
            {
                // перегоняем файл в память (быстро)
                FileStream fstream = new FileStream(authorsFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[fstream.Length];
                fstream.Read(buffer, 0, (int)fstream.Length);
                MemoryStream mstream = new MemoryStream(buffer);
                // десериализируем (медленно)
                using (var st = new StreamReader(mstream))
                {
                    var sr = new XmlSerializer(typeof(AuthorList));
                    result = (AuthorList)sr.Deserialize(st);
                }
                isCorrect = true;
            }
            catch
            {
                if (!isBackupLoad && MessageBox.Show("Произошла ошибка при загрузке списка авторов.\r\n" +
                                                     "Попытаться восстановить из резервной копии?\r\n", "ВНИМАНИЕ",
                                                     MessageBoxButton.YesNo,
                                                     MessageBoxImage.Warning, MessageBoxResult.Yes) ==
                    MessageBoxResult.Yes)
                {
                    result = Load(authorsFileName + ".bak", true);
                }
                else
                {
                    result = new AuthorList();
                    result.Retreive();
                }
            }

            // создаем резервную копию
            try
            {
                if (!isBackupLoad && isCorrect) result.Save(authorsFileName + ".bak");
            }
            catch
            {
            }

            return result;
        }

        public void Save(string authorsFileName)
        {
            if (_isDefault)
            {
                if (
                    MessageBox.Show("Производится автоматическое сохранение списка авторов\r\n\r\n" +
                                    "В результате последней загрузки был сформирован список авторов ПО УМОЛЧАНИЮ.\r\n" +
                                    "В случае его сохранения Ваш список авторов будет утерян (если он есть).\r\n" +
                                    "Вы уверены, что хотите сохранить?", "ВНИМАНИЕ", MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    return;
                }
            }
            // сериализует в память
            MemoryStream mstream = new MemoryStream();
            using (var st = new StreamWriter(mstream))
            {
                var sr = new XmlSerializer(typeof(AuthorList));
                sr.Serialize(st, this);
            }
            // пишет в файл из памяти
            File.WriteAllBytes(authorsFileName, mstream.GetBuffer());
            _isDefault = false;
        }

        public Author FindAuthor(string url)
        {
            foreach (Author a in this)
                if (a.URL == url) return a;
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