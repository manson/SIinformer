using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using SIinformer.Utils;
using SIinformer.Window;

namespace SIinformer.Logic.Sites
{
    public class FanFiction:ISite
    {
        private readonly object _lockObj = new object();
        public string GetAuthorPage(string url)
        {
            var _url = url;            
            _url = (_url.EndsWith("/")) ? _url  : _url + "/";
            return WEB.DownloadPageSilent(_url);
        }

        public bool UpdateAuthorInfo(string page, Author author, SynchronizationContext context,
                                     bool skipBookDescriptionChecking = false)
        {
            try
            {
                lock (_lockObj)
                {
                    bool retValue = false;
                    if (string.IsNullOrWhiteSpace(author.AlternateURL) && author.URL.Contains("http://www.fanfiction.net/atom/u/"))
                        author.AlternateURL = author.URL.Replace("http://www.fanfiction.net/atom/u/",
                                                                 "http://www.fanfiction.net/u/");
                    if (string.IsNullOrWhiteSpace(author.AlternateURL) && author.URL.Contains("https://www.fanfiction.net/atom/u/"))
                        author.AlternateURL = author.URL.Replace("https://www.fanfiction.net/atom/u/",
                                                                 "https://www.fanfiction.net/u/");


                    var authorTemp = new Author { UpdateDate = author.UpdateDate };

                    using (XmlReader reader = XmlReader.Create(new StringReader(page)))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        int cnt = 0;
                        foreach (var item in feed.Items)
                        {

                            var text = new AuthorText
                            {
                                Description =  CleanSummary(TextCleaner.Html2Text(item.Summary.Text)),
                                Genres = item.Authors.Count > 0 ? item.Authors[0].Name : "",
                                Link = item.Links.Count > 0 ?
                                    //item.Links[0].GetAbsoluteUri()==null ? "" : item.Links[0].GetAbsoluteUri().AbsolutePath 
                                    item.Links[0].Uri.ToString()
                                    : "",
                                Name = item.Title.Text,
                                Order = cnt,
                                SectionName = item.Authors.Count > 0 ? item.Authors[0].Name : "",
                                Size = -1,
                                UpdateDate = item.LastUpdatedTime.LocalDateTime
                            };
                            authorTemp.Texts.Add(text);
                            cnt++;
                        }

                        if (author.Texts.Count > 0) // если раньше загружали автора, то проводим сравнение
                        {
                            foreach (AuthorText txt in authorTemp.Texts)
                            {
                                bool bFound = false;
                                int OldSize = 0; // стрый размер текста
                                foreach (AuthorText t in author.Texts)
                                {
                                    if (txt.Link == t.Link)
                                    {
                                        txt.Cached = t.Cached;
                                        if (t.IsNew)
                                            // если книгу не читали до этой проверки, не меняем старое значение, чтобы видеть кумулятивное изменение размера
                                            OldSize = t.SizeOld;// запоминаем позапрошлый размер, чтобы запомнить изменения в новом тексте кумулятивно
                                        else
                                            OldSize = t.Size; // запоминаем старый размер, чтобы запомнить его в новом тексте


                                        bFound = txt.Name == t.Name && txt.Description == t.Description;
                                        // && txt.UpdateDate == t.UpdateDate
                                        if (bFound)
                                        {
                                            // переносим значение isNew в новый массив, чтобы не потерять непрочитанные новые тексты
                                            txt.IsNew = t.IsNew;
                                            txt.UpdateDate = t.UpdateDate;
                                            txt.SizeOld = t.SizeOld; // переносим, чтобы при отсутствии изменений не скидывалась информация об изменениях
                                            break;
                                        }
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
                                    
                                    //#region Отсылка информации об обновлении в шину брокера сообщений
                                    //MessageBroker.HiLevelManager.Manager.GetInstance().PublishMessageUpdatedBook(txt, author.URL, author.Name);
                                    //#endregion
                                    // отсылка инормации об обновлении на сервер статистики
                                    SIinformer.ApiStuff.ApiManager.GetInstance().PublishMessageUpdatedBook(MainWindow.MainForm.GetLogger(), MainWindow.GetSettings(), txt, author.URL, author.Name);
                                }
                            }
                            // доп проверка по количеству произведений
                            if (authorTemp.Texts.Count != author.Texts.Count)
                            {
                                retValue = true;
                                authorTemp.UpdateDate = feed.LastUpdatedTime.LocalDateTime;
                            }
                        }

                        // отсылка информации о книгах (не обновленные. обновленные идут отдельным вызовом) на сервер статистики
                        SIinformer.ApiStuff.ApiManager.GetInstance().SetBooksInfo(MainWindow.MainForm.GetLogger(), MainWindow.GetSettings(), authorTemp.Texts.Where(b=>!b.IsNew).ToList(), author.URL, author.Name);

                        context.Post(Author.SyncRun, new Author.RunContent { Renewed = author, New = authorTemp });


                        reader.Close();
                    }

                    return retValue;
                }//lock
            }
            catch (Exception ex)
            {
                MainWindow.MainForm.GetLogger().Add("Ошибка парсинка странички  " + author.URL + "     " + ex.Message);
            }
            return false;
        }

        public void GetAuthorCredentials(string page, out string AuthorName, out DateTime AuthorUpdateDate)
        {
            AuthorName = "";
            AuthorUpdateDate = DateTime.Now;
            try
            {
                using (XmlReader reader = XmlReader.Create(new StringReader(page)))
                {
                    SyndicationFeed feed = SyndicationFeed.Load(reader);
                    AuthorName = feed.Title.Text;
                    AuthorUpdateDate = feed.LastUpdatedTime.LocalDateTime;
                }
            }
            catch (Exception ex)
            {
                 MainWindow.MainForm.GetLogger().Add("Ошибка парсинка странички. " + ex.Message);
            }
        }

        public string PrepareAuthorUrlBeforeOppening(string url, Setting setting)
        {
            return url;
        }

        public string PrepareAuthorUrlOnAdding(string url)
        {
            return url;
        }

        public string PrepareTextUrlBeforeOpenning(string authorUrl, string textUrl)
        {
            return textUrl;
        }

        public string GetFileExtention(AuthorText authorText)
        {
            return ".html";
        }

        public string GetFileName(AuthorText authorText)
        {
            var parts = authorText.Link.Split("/".ToCharArray());
            var path = parts[parts.Length-1];
            return TextCleaner.MakeFileAcceptableName(path);
        }

        public string RootBooksFolder { get { return "fanfiction"; } }

        public string GetUserBooksFolder(Author author, AuthorText authorText)
        {
            var path = Path.Combine(RootBooksFolder, authorText.SectionName);
            return path;
        }
        public int GetSupportedReaderNumber(int suggestedNumber)
        {
            switch (suggestedNumber)
            {
                case 0:
                case 1:
                case 2:
                    return 0;
                default:
                    return suggestedNumber;
            }
        }

        public List<string> GetKnownDomens()
        {
            var domens = new List<string>
                             {
                                 "http://www.fanfiction.net",
                                 "https://www.fanfiction.net"
                             };
            return domens;
        }

        public List<string> GetUrlVariants(string url)
        {
            return new List<string>{url};
        }

        #region Очистка текста о книге от лишних данных

        private static Regex _regRemove_1 = new Regex("Reviews: \\d+", RegexOptions.IgnoreCase
                                                               | RegexOptions.CultureInvariant
                                                               | RegexOptions.Compiled
                                                               );
        private static Regex _regRemove_2 = new Regex("Favs: \\d+", RegexOptions.IgnoreCase
                                                               | RegexOptions.CultureInvariant
                                                               | RegexOptions.Compiled
                                                               );
        private static Regex _regRemove_3 = new Regex("Follows: \\d+", RegexOptions.IgnoreCase
                                                               | RegexOptions.CultureInvariant
                                                               | RegexOptions.Compiled
                                                               );
        string CleanSummary(string text)
        {
            text = _regRemove_1.Replace(text, "");
            text = _regRemove_2.Replace(text, "");
            text = _regRemove_3.Replace(text, "");
            text = text.Replace(" ,", "");
            return text;
        }

        #endregion

    }
}
