using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Timers;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Xml.Serialization;
using System.Globalization;
//using System.Windows.Threading;
using System.Threading;


namespace SIinformer
{


    public static class InfoUpdater
    {
        // авторы
        public static MySortableBindingList<Author> authors = new MySortableBindingList<Author>();
        // информация на странице автора
        //public static SerializableDictionary<string, MySortableBindingList<AuthorText>> author_texts = new SerializableDictionary<string, MySortableBindingList<AuthorText>>();

#region внутренние переменные 
        static string config = "";
        static string author_texts_file = "";        

	#endregion        
        
        private static bool UpdateAuthor(Author author)
        {
            byte[] buffer;
            WebClient client = new WebClient();
            bool retValue = false;
            try
            {
                if (!author.URL.EndsWith("indexdate.shtml"))                
                    author.URL = (author.URL.EndsWith("/")) ? author.URL + "indexdate.shtml" : author.URL + "/indexdate.shtml";
                
                buffer = WEB.downloadPageSilent(client, author.URL);
                if (buffer != null)
                {
                    retValue = UpdateAuthorInfo(WEB.convertPage(buffer), author);
                }
            }
            catch (Exception exception)
            {
            }
            return retValue;
        }

        public static DateTime GetUpdateDate(string page)
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
        public static bool UpdateAuthorInfo(string page, Author author)
        {
            Match match = Regex.Match(page, @"Обновлялось:</font></a></b>\s*(.*?)\s*$", RegexOptions.Multiline);
            DateTime date = DateTime.MinValue;
            bool retValue = false;
            if (match.Success)
            {
                string[] newDateStr = match.Groups[1].Value.Split('/');
                date = new DateTime(int.Parse(newDateStr[2]), int.Parse(newDateStr[1]), int.Parse(newDateStr[0]));
            }

            if (author != null)
            {
                #region проанализируем данные на страничке. если раньше их не загружали, то в любом случае не показываем, что есть обновления, просто заполняем данные
                MySortableBindingList<AuthorText> texts_temp = new MySortableBindingList<AuthorText>();                
                MatchCollection matches = Regex.Matches(page, "<DL><DT><li>(?:<font.*?>.*?</font>)?\\s*(<b>(?<Authors>.*?)\\s*</b>\\s*)?<A HREF=(?<LinkToText>.*?)><b>\\s*(?<NameOfText>.*?)\\s*</b></A>.*?<b>(?<SizeOfText>\\d+)k</b>.*?<small>(?:Оценка:<b>(?<DescriptionOfRating>(?<rating>\\d+(?:\\.\\d+)?).*?)</b>.*?)?\\s*\"(?<Section>.*?)\"\\s*(?<Genres>.*?)?\\s*(?:<A HREF=\"(?<LinkToComments>.*?)\">Комментарии:\\s*(?<CommentsDescription>(?<CommentCount>\\d+).*?)</A>\\s*)?</small>.*?(?:<br><DD>(?<Description>.*?))?</DL>");                
                if (matches.Count > 0)
                {
                    int cnt = 0;
                    foreach (Match m in matches)
                    {
                        AuthorText item = new AuthorText();
                        item.Description = NormalizeHTML(m.Groups["Description"].Value).Trim();
                        item.Genres = NormalizeHTML(m.Groups["Genres"].Value);
                        item.Link = m.Groups["LinkToText"].Value;
                        item.Name = NormalizeHTML(m.Groups["NameOfText"].Value);
                        item.Order = cnt;
                        item.SectionName = NormalizeHTML(m.Groups["Section"].Value).Replace("@","");
                        item.Size = int.Parse(m.Groups["SizeOfText"].Value);
                        texts_temp.Add(item);
                        cnt++;
                    }
                }
                if (author.Texts.Count > 0) // если раньше загружали проводим стравнение
                {
                    foreach (AuthorText txt in texts_temp)
                    {
                        bool bFound = false;
                        for (int i = 0; i < author.Texts.Count; i++)
                        {
                            if (txt.Description == author.Texts[i].Description
                                && txt.Name == author.Texts[i].Name
                                && txt.Size == author.Texts[i].Size)
                            {
                                bFound = true;
                                txt.IsNew = author.Texts[i].IsNew;// переносим значение isNew в новый массив, чтобы не потерять непрочитанные новые тексты
                                break;
                            }
                        }
                        if (!bFound)
                        {
                            txt.IsNew = author.IsNew = retValue = true;// да, автор обновился                            
                            if (date <= author.UpdateDate) // поменяем дату на сегодняшнюю, если дата обновления на страничке старее, чем зарегистрированная у нас
                                author.UpdateDate = DateTime.Today;
                            else // иначе ставим дату, указанную автором
                                author.UpdateDate = date;
                        }
                    }
                    // доп проверка по количеству произведений
                    if (texts_temp.Count != author.Texts.Count)
                    {
                        retValue = true;
                        if (date <= author.UpdateDate) // поменяем дату на сегодняшнюю, если дата обновления на страничке старее, чем зарегистрированная у нас
                            author.UpdateDate = DateTime.Today;
                        else// иначе ставим дату, указанную автором
                            author.UpdateDate = date;
                    }

                    // запоминаем новые данные
                }
                author.Texts = texts_temp;
                #endregion
            }
            return retValue;
        }

        #region нормализация строк
        private static string NormalizeHTML(string s)
        {
            return NormalizeHTMLEsc(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(s, @"[\r\n\x85\f]+", ""), "<(br|li)[^>]*>", "\n", RegexOptions.IgnoreCase), "<td[^>]*>", "\t", RegexOptions.IgnoreCase), @"<script[^>]*>.*?</\s*script[^>]*>", "", RegexOptions.IgnoreCase), "<[^>]*>", ""), @"\n[\p{Z}\t]+\n", "\n\n"), @"\n\n+", "\n\n"));
        }
        private static string NormalizeHTMLEsc(string s)
        {
            return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(s, "&#([0-9]+);?", delegate(Match match)
            {
                char ch = (char)int.Parse(match.Groups[1].Value, NumberStyles.Integer);
                return ch.ToString();
            }), "&bull;?", " * ", RegexOptions.IgnoreCase), "&lsaquo;?", "<", RegexOptions.IgnoreCase), "&rsaquo;?", ">", RegexOptions.IgnoreCase), "&trade;?", "(tm)", RegexOptions.IgnoreCase), "&frasl;?", "/", RegexOptions.IgnoreCase), "&lt;?", "<", RegexOptions.IgnoreCase), "&gt;?", ">", RegexOptions.IgnoreCase), "&copy;?", "(c)", RegexOptions.IgnoreCase), "&reg;?", "(r)", RegexOptions.IgnoreCase), "&nbsp;?", " ", RegexOptions.IgnoreCase);
        }
        #endregion

        public static Author FindAuthor(string url)
        {
            foreach (Author a in authors) if (a.URL == url) return a;
            return null;
        }
        public static void AddAuthor(string url)
        {
            SetStatus("Добавление автора...");
            
            if (!url.EndsWith("indexdate.shtml"))
                url = (url.EndsWith("/")) ? url + "indexdate.shtml" : url + "/indexdate.shtml";
            byte[] buffer;
            WebClient client = new WebClient();
            string error = "Страничка не входит в круг наших интересов.";
            try
            {
                buffer = WEB.downloadPageSilent(client, url);
                string page = WEB.convertPage(buffer);
                int si = page.IndexOf('.', page.IndexOf("<title>")) + 1;
                DateTime updateDate = GetUpdateDate(page);
                if (updateDate != DateTime.MinValue)                
                {
                    string authorName = page.Substring(si, page.IndexOf('.', si) - si);
                    if (FindAuthor(url) != null)
                    {
                        error = authorName + " уже присутствует в списке";
                        throw new Exception();
                    }
                    Author author = new Author() { Name = authorName, IsNew = false, UpdateDate = updateDate, URL = url };
                    authors.Add(author);
                    // запоминаем информацию со странички автора
                    UpdateAuthorInfo(page, author);
                    // сортируем данные по дате
                    Sort();
                    // сохраняем конфиг
                    SaveConfig();
                    SetStatus("Добавлен: " + author.Name);
                }
                else                
                    SetStatus(error);
                
            }
            catch (Exception)
            {
                SetStatus(error);
            }
        }


        static void SetStatus(string msg)
        {
            Console.WriteLine(msg);            
        }

        public static void RetreiveAuthors()
        {
            config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"authorts.xml") ;
            author_texts_file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authors_data.xml");
            if (!File.Exists(config))
            {
                authors.Clear();
            }
            else
                LoadConfig();
        }

        private static void LoadConfig()
        {
            authors.Clear();
            //author_texts.Clear();
            try
            {
                if (File.Exists(config))
                using (StreamReader st = new StreamReader(config))
                {
                    XmlSerializer sr = new XmlSerializer(typeof(MySortableBindingList<Author>));
                    authors = (MySortableBindingList<Author>)sr.Deserialize(st);
                }
                //if (File.Exists(author_texts_file))
                //    using (StreamReader st = new StreamReader(author_texts_file))
                //    {
                //        SerializableDictionary<string, MySortableBindingList<AuthorText>> author_texts_temp = new SerializableDictionary<string, MySortableBindingList<AuthorText>>();
                //        XmlSerializer sr = new XmlSerializer(typeof(SerializableDictionary<string, MySortableBindingList<AuthorText>>));
                //        author_texts = (SerializableDictionary<string, MySortableBindingList<AuthorText>>)sr.Deserialize(st);

                //    }
            }
            catch(Exception){}
        }

        public static ListSortDirection SortDirection = ListSortDirection.Descending;
        public static String SortProperty = "UpdateDate";

        public static void Sort()
        {
            authors.Sort(SortProperty, SortDirection);
        }
        
        public static void SaveConfig()
        {
            if (authors.Count == 0) return;
            using (StreamWriter st = new StreamWriter(config))
            {
                XmlSerializer sr = new XmlSerializer(typeof(MySortableBindingList<Author>));
                sr.Serialize(st, authors);
            }
            //if (author_texts.Count > 0)
            //{
            //    using (StreamWriter st = new StreamWriter(author_texts_file))
            //    {
            //        XmlSerializer sr = new XmlSerializer(typeof(SerializableDictionary<string, MySortableBindingList<AuthorText>>));
            //        sr.Serialize(st, author_texts);                    
            //    }
                
            //}
        }

    }
}
