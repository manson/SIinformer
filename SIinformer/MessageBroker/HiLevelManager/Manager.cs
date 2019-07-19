using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using Laharsub.Subscriptions;
using SIinformer.Logic;
using SIinformer.Logic.Transport;
using SIinformer.Utils;

namespace SIinformer.MessageBroker.HiLevelManager
{
    public class Manager
    {

        #region singleton
        private static volatile Manager _manager = null;
        private static object syncRoot = new Object();

        public static Manager GetInstance()
        {
            if (_manager == null)
            {
                lock (syncRoot)
                {
                    if (_manager == null)
                    {
                        _manager = new Manager();
                    }
                }
            }
            return _manager;
        }

        #endregion


        // менеджер подписки
        private SubscriptionManager _subscriptionManager = null;
        // имя канала подписки
        private const string SubscriptionTopicId = "SIInformerUpdatesChannel_v1";
        // логгер
        private Logger _logger = null;
        //настройки
        private Setting _settings = null;
        // вызывается при получении информации
        private Action<string> _bookUpdateArrived = null;

        /// <summary>
        /// Инициализация менеджера подписок
        /// </summary>
        /// <param name="logger">логгер, куда пишутся ошибки</param>
        /// <param name="settings">настройки приложения</param>
        /// <param name="bookUpdateArrived">вызывается при получении информации по каналу подписки. В качестве параметра идет JSON-значение объекта книги</param>
        public void InitSubscriptionManager(Logger logger, Setting settings, Action<string> bookUpdateArrived)
        {
            try
            {
                _logger = logger;
                _settings = settings;
                _bookUpdateArrived = bookUpdateArrived;
                // отписываемся от старых подписок, если были
                StopSubscriptions();
                // инициализируем диспетчер задач
                _subscriptionManager = new SubscriptionManager();                
                _subscriptionManager.Initialize(Dispatcher.CurrentDispatcher, null, "http://client.sireader.ru/sireader/memory");
                // подписываемся на сообщения по каналу обновлений
                _subscriptionManager.Client.SubscribeAsync(SubscriptionTopicId, 0, (ok, topicId) => { },
                                                           (subscription, message) =>
                                                           {

                                                               if (_bookUpdateArrived != null)
                                                               {
                                                                   var mess = message.GetBodyAsString();
                                                                   _bookUpdateArrived(mess);
                                                               }

                                                           }, (subscription, error) => { });//_logger.Add(string.Format("Ошибка по каналу подписки {0}: {1}", SubscriptionTopicId, error.Message), true,true)});

                _logger.Add("Запущен сервис подписки на push-уведомления об обновлениях.");
            }
            catch (Exception ex)
            {
            }
     
        }
        /// <summary>
        /// отписаться от всех подписок
        /// </summary>
        public void StopSubscriptions()
        {
            if (_subscriptionManager != null && _subscriptionManager.Client != null)
            {
                _subscriptionManager.Client.UnsubscribeAll();

                if (_logger != null)
                    _logger.Add("Остановлен сервис подписки на push-уведомления об обновлениях.");
            }
        }

        /// <summary>
        /// опубликовать информация об обновлении книги
        /// </summary>
        /// <param name="message">JSON значение объекта книги</param>
        public void PublishMessageUpdatedBook(string message)
        {
            if (_settings.UseMessageBroker)
            if (_subscriptionManager != null && _subscriptionManager.Client!=null)
                _subscriptionManager.Client.PublishByStringTopicAsync(SubscriptionTopicId, message);
        }

        /// <summary>
        /// опубликовать информация об обновлении книги
        /// </summary>
        /// <param name="authorUrl">урл автора</param>
        /// <param name="book">книга</param>
        /// <param name="authorName">имя автора</param>
        public void PublishMessageUpdatedBook(AuthorText book, string authorUrl, string authorName)
        {
            var paramJson = new fastJSON.JSONParameters
                                {
                                    UsingGlobalTypes = false,
                                    EnableAnonymousTypes = true,
                                    UseExtensions = false
                                };


            var paramBJson = new fastBinaryJSON.BJSONParameters
                                 {
                                     UsingGlobalTypes = false,
                                     EnableAnonymousTypes = true,
                                     UseExtensions = false
                                 };


            try
            {
                if (_settings.UseMessageBroker)
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
                    var jsonTransportBookInfo = fastBinaryJSON.BJSON.Instance.ToBJSON(transportBookInfo,paramBJson);// fastJSON.JSON.Instance.ToJSON(transportBookInfo);
                    var command = new SubscriptionMessageCommand()
                                      {
                                          JsonObjectBytes = jsonTransportBookInfo
                                      };
                    PublishMessageUpdatedBook(fastJSON.JSON.Instance.ToJSON(command, paramJson));
                }
            }
            catch (Exception ex)
            {
                //_logger.Add("Остановлен сервис подписки на push-уведомления об обновлениях.");
            }
        }

     
    }
}
