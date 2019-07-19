using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Laharsub.Subscriptions;
using SIinformer.Logic;
using SIinformer.Logic.Transport;
using SIinformer.Utils;

namespace SIinformer.ApiStuff
{
    /// <summary>
    /// менеджер публикации обнов через JSON-RPC сервер информатора СИ
    /// </summary>
    public class ApiManager
    {
        private const string _apiUrl = "http://nickadmin.net/SIInformerStatServer/api.ashx";
        private IWebProxy _proxy;

        //private const string _apiUrl = "http://localhost:55621/api.ashx";
        /// <summary>
        /// криво конечно, но пока рефакторить весь код совсем не хочется
        /// </summary>
        /// <returns></returns>
        private FrameworkElement GetUiControl()
        {
            return SIinformer.Window.MainWindow.MainForm;
        }


        #region singleton
        private static volatile ApiManager _manager = null;
        private static object syncRoot = new Object();

        public static ApiManager GetInstance()
        {
            if (_manager == null)
            {
                lock (syncRoot)
                {
                    if (_manager == null)
                    {
                        _manager = new ApiManager();
                    }
                }
            }
            return _manager;
        }

        #endregion

        public string GetApiUrl()
        {
            return _apiUrl;
        }

        private void CheckProxy(Setting setting)
        {
            if (setting.ProxySetting != null && setting.ProxySetting.UseProxy)
            {
                 IPAddress test;
                    if (!IPAddress.TryParse(setting.ProxySetting.Address, out test))
                        throw new ArgumentException("Некорректный адрес прокси сервера");
                    _proxy = setting.ProxySetting.UseAuthentification
                                       ? new WebProxy(
                                             new Uri("http://" + setting.ProxySetting.Address + ":" + setting.ProxySetting.Port),
                                             false,
                                             new string[0],
                                             new NetworkCredential(setting.ProxySetting.UserName, setting.ProxySetting.Password))
                                       : new WebProxy(
                                             new Uri("http://" + setting.ProxySetting.Address + ":" + setting.ProxySetting.Port));
                }            
        }

        public void PublishMessageUpdatedBook(Logger logger, Setting settings, AuthorText book, string authorUrl, string authorName)
        {
             if (!settings.UseMessageBroker) return;
            CheckProxy(settings);
           
            var paramBJson = new fastBinaryJSON.BJSONParameters
                                 {
                                     UsingGlobalTypes = false,
                                     EnableAnonymousTypes = true,
                                     UseExtensions = false
                                 };


            try
            {
                
                    var transportBookInfo = new TransportBookInfo()
                    {
                        AuthorLink = authorUrl,
                        AuthorName = authorName,
                        Description = book.Description,
                        Genres = book.Genres,
                        Link = book.Link,
                        Name = book.Name,
                        SectionName = book.SectionName,
                        Size = book.Size,
                        UpdateDate = DateTime.Now.ToUniversalTime().Ticks
                    };
                    var jsonTransportBookInfo = fastBinaryJSON.BJSON.Instance.ToBJSON(transportBookInfo, paramBJson);

                    var rpc = new JsonRpcClient(_proxy) { Url = _apiUrl };
                    rpc.Invoke<BooleanOperationResult>(
                        new RpcCommand() { Method = "SetUpdateInfo", ParametersArray = new object[] { "clientId", SubscriptionManager.CurrentClientId, "jsonObjectBytes", jsonTransportBookInfo, "appId", "slkdjfhsjdfks928347832940hfjdsf982738r9" } },
                        (data) =>
                            GetUiControl().InvokeIfRequired(() =>
                                                                
                        {
                            if (data == null)
                            {
                                logger.Add("Не удалось отослать информацию об обновлении серверу статистики SIInformer", true);
                            }
                            else if (data.Result)
                            {
                                // ну, отослали успешно, чо делать-то? Да ничего. :-)
                            }
                            else
                            {
                                logger.Add("Не удалось отослать информацию об обновлении серверу статистики SIInformer: " + data.Error, true);
                            }
                        }, DispatcherPriority.Normal), (error) => GetUiControl().InvokeIfRequired(() =>logger.Add("Не удалось отослать информацию об обновлении серверу статистики SIInformer: " + error.Message, true), DispatcherPriority.Normal));  

                
            }
            catch (Exception ex)
            {
                logger.Add("Ошибка работы модуля общения с сервером статистики SIInformer: " + ex.Message, true);  
            }
        }

        public void SetBooksInfo(Logger logger, Setting settings, List<AuthorText> books, string authorUrl, string authorName)
        {
            if (!settings.UseMessageBroker) return;
            CheckProxy(settings);

            var paramBJson = new fastBinaryJSON.BJSONParameters
            {
                UsingGlobalTypes = false,
                EnableAnonymousTypes = true,
                UseExtensions = false
            };


            try
            {
                var books2Send = new List<TransportBookInfo>();
                foreach (var book in books)
                {


                    var transportBookInfo = new TransportBookInfo()
                                                {
                                                    AuthorLink = authorUrl,
                                                    AuthorName = authorName,
                                                    Description = book.Description,
                                                    Genres = book.Genres,
                                                    Link = book.Link,
                                                    Name = book.Name,
                                                    SectionName = book.SectionName,
                                                    Size = book.Size,
                                                    UpdateDate = DateTime.Now.ToUniversalTime().Ticks
                                                };
                    books2Send.Add(transportBookInfo);
                }
                var jsonTransportBookInfo = fastBinaryJSON.BJSON.Instance.ToBJSON(books2Send, paramBJson);

                    var rpc = new JsonRpcClient(_proxy) { Url = _apiUrl };
                    rpc.Invoke<BooleanOperationResult>(
                        new RpcCommand() { Method = "SetBooksInfo", ParametersArray = new object[] { "clientId", SubscriptionManager.CurrentClientId, "jsonObjectBytes", jsonTransportBookInfo, "appId", "slkdjfhsjdfks928347832940hfjdsf982738r9" } },
                        (data) => GetUiControl().InvokeIfRequired(() =>
                                                                      {
                                                                          if (data == null)
                                                                          {
                                                                              logger.Add("Не удалось отослать информацию о проверке серверу статистики SIInformer",true);
                                                                          }
                                                                          else if (data.Result)
                                                                          {
                                                                              // ну, отослали успешно, чо делать-то? Да ничего. :-)
                                                                          }
                                                                          else
                                                                          {
                                                                              logger.Add(
                                                                                  "Не удалось отослать информацию о проверке серверу статистики SIInformer: " +
                                                                                  data.Error, true);
                                                                          }
                                                                      }, DispatcherPriority.Normal), 
                                                                      (error) => GetUiControl().InvokeIfRequired(() => logger.Add("Не удалось отослать информацию о проверке серверу статистики SIInformer: " + error.Message, true), DispatcherPriority.Normal));
                

            }
            catch (Exception ex)
            {
                logger.Add("Ошибка работы модуля общения с сервером статистики SIInformer: " + ex.Message, true);
            }
        }

        public void GetAuthorUpdatesInfo(Logger logger, Setting settings, string authorId, long lastServerStamp, Action<long,List<TransportBookInfo>> success, Action<string> fail)
        {
            CheckProxy(settings);
           var rpc = new JsonRpcClient(_proxy) { Url = _apiUrl };
            rpc.Invoke<AuthorUpdatesInfoOperationResult>(
                new RpcCommand()
                    {
                        Method = "GetAuthorUpdatesInfo",
                        ParametersArray =
                            new object[]
                                {
                                    "clientId", SubscriptionManager.CurrentClientId, "appId",
                                    "slkdjfhsjdfks928347832940hfjdsf982738r9", "authorId", authorId,
                                    "lastServerStamp", lastServerStamp
                                }
                    },
                (data) =>
                    {
                        if (data == null)
                        {
                            var mes = "Не удалось запросить информацию об обновлении у сервера статистики SIInformer";
                            GetUiControl().InvokeIfRequired(() =>
                                                                {
                                                                    if (fail != null)
                                                                        fail(mes);
                                                                    else
                                                                        logger.Add(mes, true);
                                                                }, DispatcherPriority.Normal);
                        }
                        else if (!string.IsNullOrWhiteSpace(data.Error))
                        {
                            var mes = "Cервер статистики SIInformer вернул ошибку: " + data.Error;
                            GetUiControl().InvokeIfRequired(() =>
                                                                {
                                                                    if (fail != null)
                                                                        fail(mes);
                                                                    else
                                                                        logger.Add(mes, true);
                                                                }, DispatcherPriority.Normal);
                        }
                        else
                        {
                            GetUiControl().InvokeIfRequired(() =>
                                                                {
                                                                    if (success != null)
                                                                    {
                                                                        var lastStatServerStamp = data.CheckDate;
                                                                        var transportBooks = data.Result==null ? null : fastBinaryJSON.BJSON.Instance.ToObject<List<TransportBookInfo>>(data.Result);
                                                                        success(lastStatServerStamp, transportBooks);
                                                                    }
                                                                },DispatcherPriority.Normal);
                        }
                    }, (error) =>
                           {
                               var mes ="Не удалось запросить информацию об обновлении у сервера статистики SIInformer: " + error.Message;
                               GetUiControl().InvokeIfRequired(() =>
                                                                   {
                                                                       if (fail != null)
                                                                           fail(mes);
                                                                       else
                                                                           logger.Add(mes, true);
                                                                   }, DispatcherPriority.Normal);
                           });
        }
    }
}
