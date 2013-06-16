using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SIinformer.Utils;

namespace SIinformer.Logic.Sites
{
    public class Samlib:ISite
    {
        private readonly object _lockObj = new object();
        public string GetAuthorPage(string url)
        {
            var _url = url;
            if (!_url.EndsWith("indexdate.shtml") && !_url.EndsWith("indextitle.shtml"))
                _url = (_url.EndsWith("/")) ? url + "indextitle.shtml" : url + "/indextitle.shtml";

            return WEB.DownloadPageSilent(_url);
        }

        public bool UpdateAuthorInfo(string page, Author author, SynchronizationContext context, bool skipBookDescriptionChecking = false)
        {
            lock (_lockObj)
            {
                bool retValue = false;
                var authorTemp = new Author { UpdateDate = author.UpdateDate };

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


                                bFound = skipBookDescriptionChecking
                                             ? txt.Name == t.Name && txt.Size == t.Size
                                             : txt.Name == t.Name && txt.Size == t.Size & txt.Description == t.Description;
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
                        }
                    }
                    // доп проверка по количеству произведений
                    if (authorTemp.Texts.Count != author.Texts.Count)
                    {
                        retValue = true;
                        authorTemp.UpdateDate = DateTime.Now;
                    }
                }

                context.Post(Author.SyncRun, new Author.RunContent { Renewed = author, New = authorTemp });

                return retValue;
            } // lock
        }

        public void GetAuthorCredentials(string page, out string AuthorName, out DateTime AuthorUpdateDate)
        {
            int index = page.IndexOf('.', page.IndexOf("<title>")) + 1;
            string authorName = page.Substring(index, page.IndexOf('.', index) - index);
            DateTime updateDate = GetUpdateDate(page);
            AuthorName = authorName;
            AuthorUpdateDate = updateDate;
        }

        public string PrepareAuthorUrlBeforeOppening(string url, Setting setting)
        {
            if ((!setting.OpenAuthorPageSortingDate) && (url.EndsWith("indexdate.shtml")))
                url = url.Replace("indexdate.shtml", "");
            if ((!setting.OpenAuthorPageSortingDate) && (url.EndsWith("indextitle.shtml")))
                url = url.Replace("indextitle.shtml", "");
            return url;
        }

        public string PrepareAuthorUrlOnAdding(string url)
        {
            if (url.EndsWith("index.shtml"))
                url = url.Replace("index.shtml", "indextitle.shtml");

            if (url.EndsWith("indexvote.shtml"))
                url = url.Replace("indexvote.shtml", "indextitle.shtml");

            if (!url.EndsWith("indextitle.shtml"))
                url = (url.EndsWith("/")) ? url + "indextitle.shtml" : url + "/indextitle.shtml";
            return url;
        }

        public string PrepareTextUrlBeforeOpenning(string authorUrl, string textUrl)
        {
            var url = authorUrl;
            if (url.EndsWith("indexdate.shtml"))
                url = url.Replace("indexdate.shtml", textUrl);
            else if (url.EndsWith("indextitle.shtml"))
                url = url.Replace("indextitle.shtml", textUrl);
            else
                url = (url.EndsWith("/")) ? url + textUrl : url + "/" + textUrl;
            return url;
        }

        private static DateTime GetUpdateDate(string page)
        {
            Match match = Regex.Match(page, @"Обновлялось:</font></a></b>\s*(.*?)\s*$", RegexOptions.Multiline);
            DateTime date = DateTime.MinValue;
            if (match.Success)
            {
                string[] newDateStr = match.Groups[1].Value.Split('/');
                date = new DateTime(int.Parse(newDateStr[2]), int.Parse(newDateStr[1]), int.Parse(newDateStr[0]));
            }
            return date;
        }

        public string GetFileExtention(AuthorText authorText)
        {
            return Path.GetExtension(authorText.Link);
        }
        public string GetFileName(AuthorText authorText)
        {
            return authorText.Link;
        }

        public string RootBooksFolder { get { return ""; } }
        public string GetUserBooksFolder(Author author, AuthorText authorText)
        {
            string urlWithoutHTTP = author.URL.Replace(@"http://", "");

            if (urlWithoutHTTP.EndsWith("/indexdate.shtml"))
                urlWithoutHTTP = urlWithoutHTTP.Replace("/indexdate.shtml", "");
            if (urlWithoutHTTP.EndsWith("/indextitle.shtml"))
                urlWithoutHTTP = urlWithoutHTTP.Replace("/indextitle.shtml", "");

            string endPath = urlWithoutHTTP.Substring(urlWithoutHTTP.IndexOf("/") + 1).Replace("/", @"\");



            if (!string.IsNullOrWhiteSpace(RootBooksFolder))
                endPath = Path.Combine(RootBooksFolder, endPath);
            
            string sectionName = authorText.SectionName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sectionName = sectionName.Replace(c, '_');
            }
            return endPath;
        }

        public int GetSupportedReaderNumber(int suggestedNumber)
        {
            return suggestedNumber;
        }

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
                                                    Regex.Replace(
                                                         Regex.Replace(s, "&#([0-9]+);?",
                                                                 delegate(Match match)
                                                                 {
                                                                     var ch = (char)int.Parse(match.Groups[1].Value, NumberStyles.Integer);
                                                                     return ch.ToString();
                                                                 }), "&bull;?",
                                                                  " * ", RegexOptions.IgnoreCase), "&lsaquo;?", "<",
                                                    RegexOptions.IgnoreCase), "&rsaquo;?", ">", RegexOptions.IgnoreCase),
                                            "&trade;?", "(tm)", RegexOptions.IgnoreCase), "&frasl;?", "/",
                                        RegexOptions.IgnoreCase), "&lt;?", "<", RegexOptions.IgnoreCase), "&gt;?", ">",
                                RegexOptions.IgnoreCase), "&copy;?", "(c)", RegexOptions.IgnoreCase), "&reg;?", "(r)",
                        RegexOptions.IgnoreCase), "&nbsp;?", " ", RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
