using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Laharsub.Client;
using System.Linq;
using System.Threading.Tasks;

namespace Laharsub.Subscriptions
{
    /// <summary>
    /// менеджер подписок, не знаю буду ли использовать или подписываться напрямую в контролах
    /// </summary>
    public class SubscriptionManager
    {
        private static string _baseAddres = "http://localhost/sireader/memory";//"http://localhost/ps/memory";
        public static string BaseAddres
        {
            get { return _baseAddres; }
            set { _baseAddres = value; }
        }

        private static string _currentClientId = Guid.NewGuid().ToString();
        public static string CurrentClientId
        {
            get { return _currentClientId; }
            set { _currentClientId = value; }
        }
        /// <summary>
        /// диспетчер UI
        /// </summary>
        public Dispatcher UIDispatcher { get; set; }
        /// <summary>
        /// функция получения текущего диспетчера UI
        /// </summary>
        public  Func<Dispatcher> GetDispatcher { get; set; }

        private bool _minimizeConnectionUse = false;
        /// <summary>
        /// Инициализация менеджера подписок
        /// </summary>
        /// <param name="uiDispatcher">диспетчер UI</param>
        /// <param name="getDispatcher">функция получения диспетчера UI, если во время инициализации он неизвестен</param>
        /// <param name="dispatcherURL">УРЛ брокера сообщений</param>
        public void Initialize(Dispatcher uiDispatcher = null, Func<Dispatcher> getDispatcher = null, string dispatcherURL = "", bool minimizeConnectionUse = false)
        {
            UIDispatcher = uiDispatcher;
            BaseAddres = !string.IsNullOrWhiteSpace(dispatcherURL) ? dispatcherURL : BaseAddres;
            GetDispatcher = getDispatcher;
            //TaskScheduler.Current.ProcessorCount = Environment.ProcessorCount; // настройка библиотеки параллельной работы
           
        }

        private PubsubClient _client = null;
        public PubsubClient Client { get
        {
            if (_client==null) _client = new PubsubClient(BaseAddres, _minimizeConnectionUse);
            return _client;
        } }

        #region проверка наличия брокера сообщений

        public  RoutedEventHandler BrokerAvailabilityChecked;

        private  DispatcherTimer _brokerChecker = null; // таймер проверки наличия брокера сообщений. ПРосто сделал так, чтобы в интерфейсе показывать актуальное состояние этого сервиса

        private  bool CheckingBroker = false;// проверяется ли в текущий момент брокер или нет

        public  void CheckBrokerAvailability()
        {
            if (CheckingBroker) return;
            CheckingBroker = true;

            var client = new PubsubClient(BaseAddres, true);

            client.SubscribeAsync("broker",0,
                 (created, topicId) =>
                 {
                     BrokerAvailable = true;
                     if (GetDispatcher != null) UIDispatcher = GetDispatcher();// получаем текущий диспетчер UI
                     if (BrokerAvailabilityChecked != null && UIDispatcher != null) 
                         UIDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>BrokerAvailabilityChecked(null,null)));
                     CheckingBroker = false;
                 },
                                  delegate(Subscription s, PubsubMessage m)
                                  { },
                                   delegate(Subscription s, Exception ex)
                                   {
                                       BrokerAvailable = false;
                                       if (GetDispatcher != null) UIDispatcher = GetDispatcher();// получаем текущий диспетчер UI
                                       if (BrokerAvailabilityChecked != null && UIDispatcher != null) UIDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => BrokerAvailabilityChecked(null, null)));
                                       CheckingBroker = false;
                                   }
                );



            //client.CreateTopicAsyncAsString("broker",
            //    delegate(int topicId)
            //    {
            //        BrokerAvailable = true;
            //        if (GetDispatcher != null) UIDispatcher = GetDispatcher();// получаем текущий диспетчер UI
            //        if (BrokerAvailabilityChecked != null && UIDispatcher != null) UIDispatcher.BeginInvoke(() => BrokerAvailabilityChecked(null, null));
            //        CheckingBroker = false;
            //    },
            //    delegate(Exception ex)
            //    {
            //        BrokerAvailable = false;
            //        if (GetDispatcher != null) UIDispatcher = GetDispatcher();// получаем текущий диспетчер UI
            //        if (BrokerAvailabilityChecked != null && UIDispatcher != null) UIDispatcher.BeginInvoke(() => BrokerAvailabilityChecked(null, null));
            //        CheckingBroker = false;
            //    }
            //);

        }
        public bool BrokerAvailable { get; set; }
        #endregion


        #region работа с отсылкой сообщений
        /// <summary>
        /// опубликовать сообщение/объект
        /// </summary>
        /// <param name="topic">топик</param>
        /// <param name="command">команда</param>
        /// <param name="jsonObject">сериализованный в Json формат объект</param>
        public  void Publish(string topic, string command, string jsonObject)
        {
            var message = new SubscriptionMessageCommand { Command = command, JsonObject = jsonObject };

            Client.PublishByStringTopicAsync(topic, fastJSON.JSON.Instance.ToJSON(message));//JsonConvert.SerializeObject(message));
        }

        private bool publishing = false; // идет процесс публикования или нет
        Lazy<List<Tuple<string,string,object>>> outQueue = new Lazy<List<Tuple<string, string, object>>>();// поток исходящих событий
        private Task sender;
        /// <summary>
        /// опубликовать сообщение/объект
        /// </summary>
        /// <param name="topic">топик</param>
        /// <param name="command">команда</param>
        /// <param name="publishingObject">объект, который надо передать в сообщении</param>
        public SubscriptionMessageCommand Publish<T>(string topic, string command, T publishingObject, long maxMessageId=0)
        {

            
            //if (publisher_timer == null)
            //{
            //    publisher_timer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 0, 500) };
            //    publisher_timer.Tick += (s, e) => processOutQueue();
            //}
            //publisher_timer.Stop();
            //outQueue.Value.Clear();
            var obj = new SubscriptionMessageCommand()
                       {
                           Command = command,
                           JsonObject = fastJSON.JSON.Instance.ToJSON(publishingObject),
                           MaxMessageId = maxMessageId
                       };
            outQueue.Value.Add(new Tuple<string, string, object>(topic, command, obj));
            //if (outQueue.Value.Count>0)
            //    processOutQueue();
            //else
            //    publisher_timer.Start();
            if (!publishing)
                DoSendOneEvent();

            return obj;
        }

        public Action LockEditor = null;

        private DispatcherTimer publisher_timer = null;

       void processOutQueue()
       {
           if (sender == null || (sender != null && (sender.IsCompleted || sender.IsCanceled || sender.IsFaulted)))
           {
               sender = new Task(OutQueueWorker);
               sender.Start();
           }
           else
           {
               publisher_timer.Start();
           }

       }
       void OutQueueWorker()
       {
           UIDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => publisher_timer.Stop()));
           // если идет публикация или количество комманд в исходящем потоке равно нулю, выходим
           if (publishing || outQueue.Value.Count == 0)
           {
               UIDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => publisher_timer.Start()));
               publishing = true;



               // берем все накопленные в потоке данные и отсылаем скопом
               List<SubscriptionMessageCommand> messages = new List<SubscriptionMessageCommand>();
               var comm = outQueue.Value.ToArray();
               var topic = ""; // подразумеваем, что топик у всех один
               int i = 0;
               //SubscriptionMessageCommand.ClearCounter();// сбросим накопительную добавку к штампу команды
               int OrderInPackage = 0;
               foreach (var tuple in comm)
               {
                   topic = tuple.Item1;
                   var command = tuple.Item2;
                   var publishingObject = tuple.Item3 as SubscriptionMessageCommand;
                   publishingObject.OrderInPackage = OrderInPackage++;
                   //string jsonObject = fastJSON.JSON.Instance.ToJSON(publishingObject);
                   //var message = new SubscriptionMessageCommand { Command = command, JsonObject = jsonObject };
                   messages.Add(publishingObject);
               }
               var jsonmessages = fastJSON.JSON.Instance.ToJSON(messages.ToArray());
               Client.PublishByStringTopicAsync(topic, jsonmessages,
                                                (pm) =>
                                                    {
                                                        string ok = "published";
                                                        // убираем отработанную команду из потока
                                                        //outQueue.Value.Remove(outQueue.Value.First());
                                                        outQueue.Value.RemoveRange(0, comm.Length);
                                                        publishing = false;
                                                        // повторно запускаем процедуру, чтобы отработать оставшиеся команды
                                                        //OutQueueWorker();
                                                        UIDispatcher.BeginInvoke(DispatcherPriority.Background,
                                                                                 new Action(
                                                                                     () => publisher_timer.Start()));
                                                    },
                                                (pm, ex) =>
                                                    {
                                                        string er = "NOT PUBLISHED";
                                                        publishing = false;
                                                        // повторно запускаем процедуру, чтобы отработать оставшиеся команды. по сути циклимся на последней не отосланной
                                                        //OutQueueWorker();
                                                        UIDispatcher.BeginInvoke(DispatcherPriority.Background,
                                                                                 new Action(
                                                                                     () => publisher_timer.Start()));
                                                    }
                   );
           }
       }

        void DoSendOneEvent()
        {
            if (outQueue.Value.Count == 0) { publishing = false; return; }
            publishing = true;



            // берем все накопленные в потоке данные и отсылаем скопом
            List<SubscriptionMessageCommand> messages = new List<SubscriptionMessageCommand>();
            var comm = outQueue.Value.ToArray();
            var topic = "";// подразумеваем, что топик у всех один
            int i = 0;
            //SubscriptionMessageCommand.ClearCounter();// сбросим накопительную добавку к штампу команды
            int OrderInPackage = 0;
            foreach (var tuple in comm)
            {
                topic = tuple.Item1;
                var command = tuple.Item2;
                var publishingObject = tuple.Item3 as SubscriptionMessageCommand;
                publishingObject.OrderInPackage = OrderInPackage++;
                //string jsonObject = fastJSON.JSON.Instance.ToJSON(publishingObject);
                //var message = new SubscriptionMessageCommand { Command = command, JsonObject = jsonObject };
                messages.Add(publishingObject);
            }
            var jsonmessages = fastJSON.JSON.Instance.ToJSON(messages.ToArray());
            Client.PublishByStringTopicAsync(topic, jsonmessages,
                                             (pm) =>
                                             {
                                                 string ok = "published";
                                                 // убираем отработанную команду из потока                                                
                                                 outQueue.Value.RemoveRange(0, comm.Length);
                                                 //publishing = false;
                                                 DoSendOneEvent();
                                             },
                                             (pm, ex) =>
                                             {
                                                 string er = "NOT PUBLISHED";
                                                 //publishing = false;
                                                 DoSendOneEvent();
                                             }
                );
        }

        /// <summary>
        /// десериализовать пришедшее сообщение подписки в объект SubscriptionMessageCommand
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public SubscriptionMessageCommand DeserializeMessageCommand(string message)
        {
            var result = fastJSON.JSON.Instance.ToObject<SubscriptionMessageCommand>(message);
            return result;
        }
        /// <summary>
        /// десериализовать пришедшие сообщения подписки в объект SubscriptionMessageCommand
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public List<SubscriptionMessageCommand> DeserializeMessagesCommand(string message)
        {
            var result = fastJSON.JSON.Instance.ToObject<List<SubscriptionMessageCommand>>(message);//Newtonsoft.Json.JsonConvert.DeserializeObject<SubscriptionMessageCommand[]>(message); 
            return result;
        }
        /// <summary>
        /// десериализовать объект, пришедший внутри сообщения подписки 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        public T DeserializeMessage<T>(string jsonObject)
        {
            var result = fastJSON.JSON.Instance.ToObject<T>(jsonObject);
            return result;
        }
        #endregion
    }
}
