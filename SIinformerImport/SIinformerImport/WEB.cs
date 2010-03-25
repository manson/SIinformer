using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace SIinformer
{
    static public class WEB
    {
        public static byte[] downloadPageSilent(WebClient client, string url)
        {
            byte[] buffer=null;
            int tries = 0;
            while (tries < 3)
            {
                try
                {
                    setHttpHeaders(client, null);
                    buffer = client.DownloadData(url);
                    break;
                }
                catch (Exception)
                {
                    tries++;
                }
            }
            return buffer;
        }
        public static void setHttpHeaders(WebClient client, string referer)
        {
            client.Headers.Clear();
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1;)");
            client.Headers.Add("Accept-Charset", "windows-1251");
            if (!string.IsNullOrEmpty(referer))
            {
                client.Headers.Add("Referer", referer);
            }
        }
        public static string convertPage(byte[] data)
        {
            return Encoding.GetEncoding("windows-1251").GetString(data);
        }


    }
}
