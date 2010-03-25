using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using SIinformer.Logic;

namespace SIinformer.Utils
{
    internal class RssChannel
    {
        private readonly List<RssItem> _articles = new List<RssItem>(); // создаем массив элементов item канала
        private readonly ChannelInfo _channel = new ChannelInfo(); // создаем обьект класса ChannelClass
        private readonly Dictionary<string, RssItem> _dictRssItem = new Dictionary<string, RssItem>();

        public RssChannel()
        {
        }

        public RssChannel(string text) : this()
        {
            var doc = new XmlDocument();
            doc.LoadXml(text);

            XmlNode root = doc.DocumentElement;
            if (root == null) throw new XmlException("Не найден корневой элемент в XML");
            XmlNodeList nodeList = root.ChildNodes;

            foreach (XmlNode chanel in nodeList)
            {
                foreach (XmlNode chanelItem in chanel)
                {
                    if (chanelItem.Name == "title")
                    {
                        _channel.Title = chanelItem.InnerText;
                    }
                    if (chanelItem.Name == "description")
                    {
                        _channel.Description = chanelItem.InnerText;
                    }
                    if (chanelItem.Name == "copyright")
                    {
                        _channel.Copyright = chanelItem.InnerText;
                    }
                    if (chanelItem.Name == "link")
                    {
                        _channel.Link = chanelItem.InnerText;
                    }

                    if (chanelItem.Name == "img")
                    {
                        XmlNodeList imgList = chanelItem.ChildNodes;
                        foreach (XmlNode imgItem in imgList)
                        {
                            if (imgItem.Name == "url")
                            {
                                _channel.ChannelImage.ImageURL = imgItem.InnerText;
                            }
                            if (imgItem.Name == "link")
                            {
                                _channel.ChannelImage.ImageLink = imgItem.InnerText;
                            }
                            if (imgItem.Name == "title")
                            {
                                _channel.ChannelImage.ImageTitle = imgItem.InnerText;
                            }
                        }
                    }

                    if (chanelItem.Name == "item")
                    {
                        XmlNodeList itemsList = chanelItem.ChildNodes;
                        RssItem rssItem = new RssItem();

                        foreach (XmlNode item in itemsList)
                        {
                            if (item.Name == "title")
                            {
                                rssItem.Title = ReplaceSpecialSymbolBack(item.InnerXml);
                            }
                            if (item.Name == "link")
                            {
                                rssItem.Link = item.InnerText;
                            }
                            if (item.Name == "description")
                            {
                                rssItem.Description = ReplaceSpecialSymbolBack(item.InnerXml);
                            }
                            if (item.Name == "pubDate")
                            {
                                rssItem.PubDate = DateTime.Parse(item.InnerText);
                            }
                            if (item.Name == "author")
                            {
                                rssItem.Author = ReplaceSpecialSymbolBack(item.InnerXml);
                            }
                            if (item.Name == "category")
                            {
                                rssItem.Category = ReplaceSpecialSymbolBack(item.InnerXml);
                            }
                            if (item.Name == "guid")
                            {
                                rssItem.Guid = item.InnerText;
                            }
                        }
                        _articles.Add(rssItem);
                    }
                }
            }
            foreach (RssItem rssItem in _articles)
            {
                if (!_dictRssItem.ContainsKey(rssItem.Guid))
                    _dictRssItem.Add(rssItem.Guid, rssItem);
            }
        }

        internal static string ReplaceSpecialSymbol(string text)
        {
            string result = text;
            result = result.Replace("&", "&amp;");
            string kv = '"'.ToString();
            result = result.Replace(kv, "&quot;");
            result = result.Replace(">", "&gt;");
            result = result.Replace("<", "&lt;");
            return result;
        }

        internal static string ReplaceSpecialSymbolBack(string text)
        {
            string result = text;
            result = result.Replace("&amp;", "&");
            string kv = '"'.ToString();
            result = result.Replace("&quot;", kv);
            result = result.Replace("&gt;", ">");
            result = result.Replace("&lt;", "<");
            return result;
        }

        public string GenerateRss(long count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"windows-1251\" ?>");
            sb.AppendLine("<rss version=\"2.0\">\r\n");

            sb.AppendLine("<channel>\r\n");

            sb.AppendLine("<title>Самиздат - избранное</title>");
            sb.AppendLine("<link>http://zhurnal.lib.ru</link>");
            sb.AppendLine("<description>Самиздат</description>");
            sb.AppendFormat("<lastBuildDate>{0}</lastBuildDate>", DateTime.Now.ToUniversalTime().ToString("R"));
            sb.AppendLine();
            sb.AppendLine("<copyright>Copyright 2009, Dukmp</copyright>\r\n");

            int counter = 1;
            foreach (RssItem item in _articles)
            {
                sb.AppendLine("<item>");
                sb.AppendFormat("<title>{0}</title>\r\n", ReplaceSpecialSymbol(item.Title));
                sb.AppendFormat("<link>{0}</link>\r\n", item.Link);
                sb.AppendFormat("<description>{0}</description>\r\n", ReplaceSpecialSymbol(item.Description));
                sb.AppendFormat("<pubDate>{0}</pubDate>\r\n", item.PubDate.ToUniversalTime().ToString("R"));
                sb.AppendFormat("<author>{0}</author>\r\n", ReplaceSpecialSymbol(item.Author));
                sb.AppendFormat("<category>{0}</category>\r\n", ReplaceSpecialSymbol(item.Category));
                sb.AppendFormat("<guid>{0}</guid>\r\n", item.Guid);
                sb.AppendLine("</item>\r\n");

                counter++;
                if (counter == count) break;
            }

            sb.AppendLine("</channel>\r\n");
            sb.AppendLine("</rss>\r\n");
            return sb.ToString();
        }

        /// <summary>
        /// Добавляет в RSS ленту новые произведения автора
        /// </summary>
        /// <param name="author">Автор</param>
        public void Add(Author author)
        {
            foreach (AuthorText authorText in author.Texts)
            {
                if (authorText.IsNew)
                {
                    RssItem item = new RssItem
                                       {
                                           Title = author.Name + " | " + authorText.Name,
                                           Link = authorText.GetFullLink(author),
                                           PubDate = DateTime.Now,
                                           Author = author.Name,
                                           Category = author.Category
                                       };

                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat(@"<a href=""{0}""><font size=""+1"" color=TEAL>{1}</font></a>. Категория: {2}.",
                                    author.URL,
                                    author.Name, author.Category);
                    sb.AppendLine("<br>");
                    sb.AppendFormat(@"<a href=""{0}""><font size=""+1"" color=TEAL>{1}</font></a>. Размер: {2} кб.",
                                    authorText.GetFullLink(author), authorText.Name, authorText.Size);
                    sb.AppendLine("<br>");
                    sb.AppendFormat(@"{0} -> {1}", authorText.Genres, authorText.SectionName);
                    sb.AppendLine("<br>");
                    sb.AppendFormat(@"{0}", authorText.Description);
                    item.Description = sb.ToString();

                    item.Guid =
                        (item.Title.GetHashCode() ^ item.Link.GetHashCode() ^ item.Author.GetHashCode() ^
                         item.Category.GetHashCode() ^ item.Description.GetHashCode()).ToString();

                    if (!_dictRssItem.ContainsKey(item.Guid))
                    {
                        _dictRssItem.Add(item.Guid, item);
                        _articles.Insert(0, item);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Класс статей
    /// </summary>
    public class RssItem
    {
        public RssItem()
        {
            Title = "";
            Link = "";
            Description = "";
            PubDate = new DateTime();
            Author = "";
            Category = "";
            Guid = "";
        }

        public string Description { get; set; }
        public string Link { get; set; }
        public DateTime PubDate { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public string Guid { get; set; }

        public override int GetHashCode()
        {
            return int.Parse(Guid);
        }
    }

    /// <summary>
    /// Класс который отвечает за настройки канала
    /// </summary>
    public class ChannelInfo
    {
        public ChannelInfo()
        {
            Title = "";
            Description = "";
            Link = "";
            Copyright = "";
            ChannelImage = new ChannelImage();
        }

        public string Copyright { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public string Title { get; set; }
        public ChannelImage ChannelImage { get; set; }
    }

    /// <summary>
    /// Класс рисунка канала
    /// </summary>
    public class ChannelImage
    {
        public ChannelImage()
        {
            ImageTitle = "";
            ImageLink = "";
            ImageURL = "";
        }

        public string ImageLink { get; set; }
        public string ImageTitle { get; set; }
        public string ImageURL { get; set; }
    }
}