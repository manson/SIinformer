using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

namespace SIinformer.Utils
{
    public static class WEB
    {
        private static Logger _logger;
        private static ProxySetting _proxySetting;

        public static void Init(ProxySetting proxySetting, Logger logger)
        {
            _proxySetting = proxySetting;
            _logger = logger;
        }

        /// <summary>
        /// Синхронная или асинхронная загрузка данных
        /// </summary>
        /// <param name="url">Адрес</param>
        /// <param name="progress">Обработчик события прогресса загрузки для асинхронной загрузки (null для синхронной</param>
        /// <param name="complete">Обработчик события завершения загрузки</param>
        /// <returns>Результат синхронной загрузки или null при неудаче, всегда null при асинхронной</returns>
        public static string DownloadPageSilent(string url, DownloadProgressChangedEventHandler progress,
                                                DownloadDataCompletedEventHandler complete)
        {
            byte[] buffer = null;
            int tries = 0;

            var client = new WebClient();
            try
            {
                if (_proxySetting.UseProxy)
                {
                    IPAddress test;
                    if (!IPAddress.TryParse(_proxySetting.Address, out test))
                        throw new ArgumentException("Некорректный адрес прокси сервера");
                    client.Proxy = _proxySetting.UseAuthentification
                                       ? new WebProxy(
                                             new Uri("http://" + _proxySetting.Address + ":" + _proxySetting.Port),
                                             false,
                                             new string[0],
                                             new NetworkCredential(_proxySetting.UserName, _proxySetting.Password))
                                       : new WebProxy(
                                             new Uri("http://" + _proxySetting.Address + ":" + _proxySetting.Port));
                }
            }
            catch (Exception ex)
            {
                _logger.Add(ex.StackTrace, false, true);
                _logger.Add(ex.Message, false, true);
                _logger.Add("Ошибка конструктора прокси", false, true);
            }

            while (tries < 3)
            {
                try
                {
                    SetHttpHeaders(client, null);
                    if (progress == null)
                        buffer = client.DownloadData(url);
                    else
                    {
                        client.DownloadProgressChanged += progress;
                        client.DownloadDataCompleted += complete;
                        client.DownloadDataAsync(new Uri(url));
                        return null;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Add(ex.StackTrace, false, true);
                    _logger.Add(ex.Message, false, true);
                    _logger.Add("Ошибка загрузки страницы", false, true);
                    tries++;
                }
            }

            return (buffer != null) ? ConvertPage(buffer) : null;
        }

        /// <summary>
        /// Синхронная загрузка данных
        /// </summary>
        /// <param name="url">Адрес</param>
        /// <returns>Результат загрузки или null при неудаче</returns>
        public static string DownloadPageSilent(string url)
        {
            return DownloadPageSilent(url, null, null);
        }

        private static void SetHttpHeaders(WebClient client, string referer)
        {
            client.Headers.Clear();
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1;)");
            client.Headers.Add("Accept-Charset", "windows-1251");
            if (!string.IsNullOrEmpty(referer))
            {
                client.Headers.Add("Referer", referer);
            }
        }

        public static string ConvertPage(byte[] data)
        {
            return Encoding.GetEncoding("windows-1251").GetString(data);
        }

        private static void _OpenUrl(object obj)
        {
            try
            {
                Process.Start((string)obj);
            }
            catch 
            {}
        }
        public static void OpenURL(string url)
        {
                ParameterizedThreadStart pts = new ParameterizedThreadStart(_OpenUrl);
                //Thread thread = new Thread(obj => Process.Start((string)obj)) {  IsBackground = true };
                Thread thread = new Thread(pts) { IsBackground = true };
                thread.Start(url);

        }
    }
}