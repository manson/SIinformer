using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;

namespace SIinformer.Logic
{
    public class AuthorList : BindingList<Author>
    {
        private bool _isDefault;

        private void Retreive()
        {
           // Дефолтный список авторов должен заканчиваться на indexdate.shtml, чтобы избежать дублирования при добавлении
            Add(new Author
                    {
                        Name = "Конторович Александр Сергеевич",
                        IsNew = false,
                        UpdateDate = DateTime.Now,
                        URL = "http://samlib.ru/k/kontorowich_a_s/indexdate.shtml"
                    });
            Add(new Author
                    {
                        Name = "Конюшевский В.Н.",
                        IsNew = false,
                        UpdateDate = DateTime.Now,
                        URL = "http://samlib.ru/k/kotow_w_n/indexdate.shtml"
                    });
            Add(new Author
            {
                Name = "Ясинский Анджей",
                IsNew = false,
                UpdateDate = DateTime.Now,
                URL = "http://samlib.ru/p/pupkin_wasja_ibragimowich/indexdate.shtml"
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
            if (!File.Exists(authorsFileName))
            {
                result = new AuthorList();
                result.Retreive();
                result.Save(authorsFileName);
                return result;
            }

            // .bak файл сохраняем там же, где и основное приложение
            var authorsBakFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                Path.GetFileName(authorsFileName) + ".bak");
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
                    result = Load(authorsBakFileName, true);
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
                if (!isBackupLoad && isCorrect) result.Save(authorsBakFileName);
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
                if (MessageBox.Show("Производится автоматическое сохранение списка авторов\r\n\r\n" +
                                    "В результате последней загрузки был сформирован список авторов ПО УМОЛЧАНИЮ.\r\n" +
                                    "В случае его сохранения Ваш список авторов будет утерян (если он есть).\r\n" +
                                    "Вы уверены, что хотите сохранить?", "ВНИМАНИЕ", MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    return;
                }
            }
            // сериализует в память
            var mstream = new MemoryStream();
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
            // Если в базе все еще хранится zhurnal.lib.ru, делаем поиск по измененному урлу.
            return this.FirstOrDefault(a => a.URL.Replace("zhurnal.lib.ru", "samlib.ru") == url);
        }

        public string[] GetCategoryNames()
        {
            return this
                .Where(_ => !_.IsDeleted)
                .Select(_ => _.Category)
                .Distinct()
                .ToArray();
        }
    }
}