using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Net;
using System.Threading;
#if SILVERLIGHT
using System.Windows;
#endif

namespace Laharsub.Client
{
    public class PubsubClient
    {
        HttpLongPollManager pollManager;
        Dictionary<int, List<HttpLongPollManager>> pollManagers;
        object syncRoot = new object();
        int maxBufferSize;

        int MaxBufferSize
        {
            get
            {
                return this.maxBufferSize;
            }
            set
            {
                if (value < HttpLongPollManager.MinBufferSize)
                {
                    throw new ArgumentOutOfRangeException("value", "Minimum value of MaxBufferSize is " + HttpLongPollManager.MinBufferSize.ToString() + ".");
                }
                this.maxBufferSize = value;
            }
        }

        bool MinimizeConnectionUse { get; set; }
        public Uri BaseAddress { get; private set; }

#if SILVERLIGHT
        public PubsubClient()
        {
            bool exception = false;
            try
            {
                if (Application.Current == null || Application.Current.Host == null || Application.Current.Host.Source == null)
                {
                    exception = true;
                }
                else
                {
                    this.BaseAddress = new Uri(new Uri(Application.Current.Host.Source.AbsoluteUri), "../../ps/memory/");
                }
            }
            catch (UnauthorizedAccessException)
            {
                exception = true;
            }
            finally
            {
                if (exception)
                {
                    throw new InvalidOperationException("Cannot determine the source URI of the Silverlight application. Please use the constructor that specifies the baseAddress explicitly instead.");
                }
            }
            this.MinimizeConnectionUse = true;
            this.MaxBufferSize = HttpLongPollManager.DefaultMaxBufferSize;
            this.InitializePollManagers();
        }
#endif

        public PubsubClient(string baseAddress)
            : this(baseAddress, true)
        {
            // empty
        }

        public PubsubClient(string baseAddress, bool minimizeConnectionUse)
            : this(baseAddress, minimizeConnectionUse, HttpLongPollManager.DefaultMaxBufferSize)
        {
            // empty
        }

        public PubsubClient(string baseAddress, bool minimizeConnectionUse, int maxBufferSize)
        {
            Uri uri;
            if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out uri))
            {
                throw new ArgumentException("The baseAddress parameter must be an absolute URI.");
            }
            else if (uri.AbsoluteUri.EndsWith("/"))
            {
                this.BaseAddress = uri;
            }
            else
            {
                this.BaseAddress = new Uri(uri.AbsoluteUri + "/");
            }
            this.MinimizeConnectionUse = minimizeConnectionUse;
            this.MaxBufferSize = maxBufferSize;
            this.InitializePollManagers();
        }

        void InitializePollManagers()
        {
            if (this.MinimizeConnectionUse)
            {
                this.pollManager = new HttpLongPollManager(this.BaseAddress.AbsoluteUri, this.MaxBufferSize);
            }
            else
            {
                this.pollManagers = new Dictionary<int, List<HttpLongPollManager>>();
            }
        }

        public void SubscribeAsync(Subscription subscription)
        {
            if (subscription == null)
            {
                throw new ArgumentNullException("subscription");
            }
            if (subscription.TopicId <= 0)
            {
                throw new InvalidOperationException("TopicId must be a positive integer.");
            }
            if (subscription.From < 0)
            {
                throw new InvalidOperationException("From must be a non-negative integer.");
            }

            HttpLongPollManager pollManager = null;
            if (this.MinimizeConnectionUse)
            {
                pollManager = this.pollManager;
            }
            else
            {
                lock (this.syncRoot)
                {
                    if (!this.pollManagers.ContainsKey(subscription.TopicId))
                    {
                        this.pollManagers[subscription.TopicId] = new List<HttpLongPollManager>();
                    }
                    pollManager = new HttpLongPollManager(this.BaseAddress.AbsoluteUri, this.MaxBufferSize);
                    this.pollManagers[subscription.TopicId].Add(pollManager);
                }
            }

            subscription.SynchronizationContext = SynchronizationContext.Current;
            pollManager.AddSubscription(subscription);
        }

        public void SubscribeAsync(int topicId, Action<Subscription, PubsubMessage> onMessageReceived, Action<Subscription, Exception> onError)
        {
            this.SubscribeAsync(new Subscription { TopicId = topicId, OnMessageReceived = onMessageReceived, OnError = onError });
        }
        /// <summary>
        /// реперные точки сообщений в цепочке приходящих сообщений
        /// </summary>
        Lazy<Dictionary<int,int>> topicMessagePoint = new Lazy<Dictionary<int, int>>();
        /// <summary>
        /// сделал несколько костыльно подписку к стринговому топику, так как иначе слишком много кода менять, но работать будет, так как по сути это 
        /// стандартное использование лахарсуба, только спрятанное внутрь клиента 
        /// Тут все равно происходит двойной вызов к серверу. При ошибке отписываемся, и заново пытаемся создать топик, а потом к нему подписаться, так как обрыв связи мог произойти по причине остановки сервера, а значит номер топика может измениться
        /// </summary>
        /// <param name="topicId"></param>
        /// <param name="onMessageReceived"></param>
        /// <param name="onError"></param>
        public void SubscribeAsync(string topicId, int TopicMessagePoint, Action<bool, int> onTopicCreated, Action<Subscription, PubsubMessage> onMessageReceived, Action<Subscription, Exception> onError)
        {
           

            CreateTopicAsyncAsString(topicId, (topicIdInt) =>
                                {
                                    if (!topicMessagePoint.Value.ContainsKey(topicIdInt)) // сохраняем реперную точку сообщений. Если сообщения будут приходить до нее, то будем игнорить
                                        topicMessagePoint.Value.Add(topicIdInt, TopicMessagePoint);
                                    else
                                        topicMessagePoint.Value[topicIdInt] = TopicMessagePoint;
                                    
                                    this.SubscribeAsync(new Subscription
                                    {
                                        TopicId = topicIdInt,
                                        From = TopicMessagePoint,
                                        OnMessageReceived = (sub, mes) =>
                                                                {
                                                                    //if (sub.From >= topicMessagePoint.Value[topicIdInt])// райзим событие только если сообщение реально новое, а не из пула сервера пришло
                                                                    {
                                                                        topicMessagePoint.Value[topicIdInt] = sub.From;
                                                                        onMessageReceived(sub, mes);
                                                                    }
                                                                },
                                        OnError = (s, e) =>
                                                      {
                                                          if (onError!=null)
                                                            onError(s, e);
                                                          //Unsubscribe(topicIdInt);
                                                          SubscribeAsync(topicId, topicMessagePoint.Value[topicIdInt], onTopicCreated, onMessageReceived, onError);
                                                      }
                                    });
                                    if (onTopicCreated != null) onTopicCreated(true, topicIdInt);
                                },
                                (ex)=>
                                    {
                                        if (onTopicCreated != null) onTopicCreated(false,-1);
                                        if (onError != null) onError(null, ex);
                                        SubscribeAsync(topicId,TopicMessagePoint, onTopicCreated, onMessageReceived, onError);
                                    });
            
        }


        public void TopicExistsAsyncAsString(string topicId, Action<bool> onChecked, Action<Exception> onError)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(this.BaseAddress, "topic_exists/" + topicId));
            request.Method = "GET";
            request.ContentLength = 0;
            request.BeginGetResponse(
                PubsubClient.ContinueHttpRequest,
                new CheckTopicHttpAsyncContext
                {
                    SynchronizationContext = SynchronizationContext.Current,
                    OnCheckedCore = onChecked,
                    OnErrorCore = onError,
                    Request = request,
                    Processor = PubsubClient.ProcessGetBoolResponse
                }
            );
        }

        public void CreateTopicAsync(Action<int> onSuccess, Action<Exception> onError)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(this.BaseAddress, "topics"));
            request.Method = "POST";
            request.ContentLength = 0;
            request.BeginGetResponse(
                PubsubClient.ContinueHttpRequest,
                new CreateTopicHttpAsyncContext {
                    SynchronizationContext = SynchronizationContext.Current,
                    OnSuccessCore = onSuccess, 
                    OnErrorCore = onError,
                    Request = request,
                    Processor = PubsubClient.ProcessGetResponse
                }
            );
        }

        public void CreateTopicAsyncAsString(string topicId, Action<int> onSuccess, Action<Exception> onError)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(this.BaseAddress, "createtopic/" + topicId));
            request.Method = "POST";
            request.ContentLength = 0;
            request.BeginGetResponse(
                PubsubClient.ContinueHttpRequest,
                new CreateTopicHttpAsyncContext
                {
                    SynchronizationContext = SynchronizationContext.Current,
                    OnSuccessCore = onSuccess,
                    OnErrorCore = onError,
                    Request = request,
                    Processor = PubsubClient.ProcessGetResponse
                }
            );
        }

#if SILVERLIGHT
#else
        public int CreateTopic(TimeSpan timeout)
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            int topicId = 0;
            Exception exception = null;
            this.CreateTopicAsync(
                delegate(int tid)
                {
                    topicId = tid;
                    waitHandle.Set();
                },
                delegate(Exception e)
                {
                    exception = e;
                    waitHandle.Set();
                });

            if (waitHandle.WaitOne(timeout))
            {
                if (exception != null)
                {
                    throw exception;
                }
                return topicId;
            }
            else
            {
                throw new TimeoutException("CreateTopic operation did not complete within the specified timeout.");
            }
        }
#endif


        public void PublishAsync(int topicid, string message)
        {
            this.PublishAsync(new PubsubMessage { 
                TopicId = topicid, 
                ContentType = "text/plain", 
                Body = new MemoryStream(Encoding.UTF8.GetBytes(message)) 
            });
        }

        public void PublishByStringTopicAsync(string topicid, string message)
        {
            this.PublishByStringTopicAsync(new PubsubMessage
            {
                StringTopicId = topicid,
                ContentType = "text/plain",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(message))
            });
        }

        public void PublishAsync(int topicid, string message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            this.PublishAsync(
                new PubsubMessage
                {
                    TopicId = topicid,
                    ContentType = "text/plain",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(message))
                },
                onSuccess,
                onError);
        }

        public void PublishByStringTopicAsync(string topicid, string message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            this.PublishByStringTopicAsync(
                new PubsubMessage { 
                    StringTopicId = topicid, 
                    ContentType = "text/plain", 
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(message)) 
                },
                onSuccess, 
                onError);
        }

        public void PublishAsync(int topicid, string contentType, Stream message)
        {
            this.PublishAsync(new PubsubMessage { 
                TopicId = topicid, 
                ContentType = contentType, 
                Body = message 
            });
        }

        public void PublishByStringTopicAsync(string topicid, string contentType, Stream message)
        {
            this.PublishByStringTopicAsync(new PubsubMessage
            {
                StringTopicId = topicid,
                ContentType = contentType,
                Body = message
            });
        }

        public void PublishAsync(int topicid, string contentType, Stream message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            this.PublishAsync(
                new PubsubMessage { 
                    TopicId = topicid, 
                    ContentType = contentType, 
                    Body = message 
                },
                onSuccess, 
                onError);
        }

        public void PublishByStringTopicAsync(string topicid, string contentType, Stream message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            this.PublishByStringTopicAsync(
                new PubsubMessage
                {
                    StringTopicId = topicid,
                    ContentType = contentType,
                    Body = message
                },
                onSuccess,
                onError);
        }


        public void PublishAsync(PubsubMessage message)
        {
            this.PublishAsync(message, null, null);
        }

        public void PublishByStringTopicAsync(PubsubMessage message)
        {
            this.PublishByStringTopicAsync(message, null, null);
        }

        public void PublishAsync(PubsubMessage message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            if (message == null)
            {
                throw new ArgumentException("message");
            }
            if (message.TopicId <= 0)
            {
                throw new InvalidOperationException("TopicId must be a positive integer.");
            }
            if (string.IsNullOrEmpty(message.ContentType))
            {
                throw new InvalidOperationException("ContentType must be specified.");
            }
            if (message.Body == null)
            {
                throw new InvalidOperationException("Body of the message to publish must be specified.");
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                new Uri(this.BaseAddress, string.Format(CultureInfo.InvariantCulture, "topics/{0}", message.TopicId)));
            request.Method = "POST";
            request.ContentType = message.ContentType;
            request.BeginGetRequestStream(
                PubsubClient.ContinueHttpRequest,
                new PublishHttpAsyncContext
                {
                    SynchronizationContext = SynchronizationContext.Current,
                    Message = message,
                    Request = request,
                    Processor = PubsubClient.ProcessPublishGetRequestStream,
                    OnErrorCore = onError,
                    OnSuccessCore = onSuccess
                }
            );
        }

        public void PublishByStringTopicAsync(PubsubMessage message, Action<PubsubMessage> onSuccess, Action<PubsubMessage, Exception> onError)
        {
            if (message == null)
            {
                throw new ArgumentException("message");
            }
            if (string.IsNullOrWhiteSpace(message.StringTopicId))
            {
                throw new InvalidOperationException("TopicId must be a valid string id.");
            }
            if (string.IsNullOrEmpty(message.ContentType))
            {
                throw new InvalidOperationException("ContentType must be specified.");
            }
            if (message.Body == null)
            {
                throw new InvalidOperationException("Body of the message to publish must be specified.");
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                new Uri(this.BaseAddress, string.Format(CultureInfo.InvariantCulture, "stringtopics/{0}", message.StringTopicId)));
            request.Method = "POST";
            request.ContentType = message.ContentType;
            request.BeginGetRequestStream(
                PubsubClient.ContinueHttpRequest,
                new PublishHttpAsyncContext
                {
                    Message = message,
                    Request = request,
                    Processor = PubsubClient.ProcessPublishGetRequestStream,
                    OnErrorCore = onError,
                    OnSuccessCore = onSuccess
                }
            );
        }
        static void ContinueHttpRequest(IAsyncResult result)
        {
            HttpAsyncContext context = (HttpAsyncContext)result.AsyncState;
            try
            {
                context.Processor(result, context);
            }
            catch (Exception e)
            {
                if (context != null)
                {
                    if (context.Stream != null)
                    {
                        context.Stream.Close();
                    }
                    if (context.Response != null)
                    {
                        context.Response.Close();
                    }
                    context.Exception = e;
                    try
                    {
                        context.OnError();
                    }
                    catch (Exception)
                    {
                        // empty
                    }
                }
            }
        }

        static void ProcessPublishGetRequestStream(IAsyncResult result, HttpAsyncContext context)
        {
            PublishHttpAsyncContext publishContext = (PublishHttpAsyncContext)context;
            context.Stream = context.Request.EndGetRequestStream(result);
            context.Processor = PubsubClient.ProcessPublishReadRequest;
            publishContext.Message.Body.BeginRead(context.Buffer, 0, context.Buffer.Length, PubsubClient.ContinueHttpRequest, context);
        }

        static void ProcessPublishReadRequest(IAsyncResult result, HttpAsyncContext context)
        {
            PublishHttpAsyncContext publishContext = (PublishHttpAsyncContext)context;
            int count = publishContext.Message.Body.EndRead(result);
            if (count == 0)
            {
                context.Stream.Close();
                context.Processor = PubsubClient.ProcessGetResponse;
                context.Request.BeginGetResponse(PubsubClient.ContinueHttpRequest, context);
            }
            else
            {
                context.Processor = PubsubClient.ProcessPublishWriteRequest;
                context.Stream.BeginWrite(context.Buffer, 0, count, PubsubClient.ContinueHttpRequest, context);
            }
        }

        static void ProcessPublishWriteRequest(IAsyncResult result, HttpAsyncContext context)
        {
            PublishHttpAsyncContext publishContext = (PublishHttpAsyncContext)context;
            context.Stream.EndWrite(result);
            context.Processor = PubsubClient.ProcessPublishReadRequest;
            publishContext.Message.Body.BeginRead(context.Buffer, 0, context.Buffer.Length, PubsubClient.ContinueHttpRequest, context);
        }

        #region Чтение булевого результата по GET
        static void ProcessGetBoolResponse(IAsyncResult result, HttpAsyncContext context)
        {
            context.Response = (HttpWebResponse)context.Request.EndGetResponse(result);
            if (context.Response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture,
                    "{0}. Server returned status code {1}: {2}.",
                    context.ExceptionMessagePrefix,
                    context.Response.StatusCode,
                    context.Response.StatusDescription));
            }
            context.Stream = context.Response.GetResponseStream();
            context.Processor = PubsubClient.ProcessReadBoolResponse;
            context.Stream.BeginRead(context.Buffer, 0, context.Buffer.Length, PubsubClient.ContinueHttpRequest, context);
        }

        static void ProcessReadBoolResponse(IAsyncResult result, HttpAsyncContext context)
        {
            int count = context.Stream.EndRead(result);
            context.Offset += count;
            if (count == 0)
            {
                bool BooleanResult;
                // ignore BOM if present
                int startIndex = (context.Offset >= 3 && context.Buffer[0] == 0xEF && context.Buffer[1] == 0xBB && context.Buffer[2] == 0xBF) ? 3 : 0;
                if (bool.TryParse(Encoding.UTF8.GetString(context.Buffer, startIndex, context.Offset - startIndex), out BooleanResult))
                {
                    context.Response.Close();
                    context.BooleanResult = BooleanResult;
                    context.OnSuccess();
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        "{0}. Server response is malformed.",
                        context.ExceptionMessagePrefix));
                }
            }
            else if (context.Offset == context.Buffer.Length)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "{0}. Server response exceedes the expected size.",
                    context.ExceptionMessagePrefix));
            }
            else
            {
                context.Stream.BeginRead(context.Buffer, context.Offset, context.Buffer.Length - context.Offset, PubsubClient.ContinueHttpRequest, context);
            }
        } 
        #endregion

        static void ProcessGetResponse(IAsyncResult result, HttpAsyncContext context)
        {
            context.Response = (HttpWebResponse)context.Request.EndGetResponse(result);
            if (context.Response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture,
                    "{0}. Server returned status code {1}: {2}.",
                    context.ExceptionMessagePrefix,
                    context.Response.StatusCode,
                    context.Response.StatusDescription));
            }
            context.Stream = context.Response.GetResponseStream();
            context.Processor = PubsubClient.ProcessReadResponse;
            context.Stream.BeginRead(context.Buffer, 0, context.Buffer.Length, PubsubClient.ContinueHttpRequest, context);
        }

        static void ProcessReadResponse(IAsyncResult result, HttpAsyncContext context)
        {
            int count = context.Stream.EndRead(result);
            context.Offset += count;
            if (count == 0)
            {
                int resultId;
                // ignore BOM if present
                int startIndex = (context.Offset >= 3 && context.Buffer[0] == 0xEF && context.Buffer[1] == 0xBB && context.Buffer[2] == 0xBF) ? 3 : 0;
                if (int.TryParse(Encoding.UTF8.GetString(context.Buffer, startIndex, context.Offset - startIndex), out resultId))
                {
                    context.Response.Close();
                    context.ResultId = resultId;
                    context.OnSuccess();
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        "{0}. Server response is malformed.",
                        context.ExceptionMessagePrefix));
                }
            }
            else if (context.Offset == context.Buffer.Length)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "{0}. Server response exceedes the expected size.",
                    context.ExceptionMessagePrefix));
            }
            else
            {
                context.Stream.BeginRead(context.Buffer, context.Offset, context.Buffer.Length - context.Offset, PubsubClient.ContinueHttpRequest, context);
            }
        }

        public void Unsubscribe(int topicId)
        {
            if (topicMessagePoint.Value.ContainsKey(topicId)) // удаляем реперную точку сообщений. 
                topicMessagePoint.Value.Remove(topicId);

            if (this.MinimizeConnectionUse)
            {
                this.pollManager.RemoveSubscription(topicId);
            }
            else
            {
                lock (this.syncRoot)
                {
                    List<HttpLongPollManager> pollManagerList;
                    if (this.pollManagers.TryGetValue(topicId, out pollManagerList))
                    {
                        foreach (HttpLongPollManager pm in pollManagerList)
                        {
                            pm.RemoveAllSubscriptions();
                        }
                        this.pollManagers.Remove(topicId);
                    }
                }
            }
        }

        public void UnsubscribeAll()
        {
            if (this.MinimizeConnectionUse)
            {
                this.pollManager.RemoveAllSubscriptions();
            }
            else
            {
                lock (this.syncRoot)
                {
                    foreach (List<HttpLongPollManager> pml in this.pollManagers.Values)
                    {
                        foreach (HttpLongPollManager pm in pml)
                        {
                            pm.RemoveAllSubscriptions();
                        }
                    }
                    this.pollManagers.Clear();
                }
            }
            topicMessagePoint.Value.Clear();// обнулим локальные реперные точки
        }

        abstract class HttpAsyncContext
        {
            public int Offset { get; set; }
            public byte[] Buffer = new byte[256];
            public int ResultId { get; set; }
            public bool BooleanResult { get; set; }
            public Exception Exception { get; set; }
            public HttpWebRequest Request { get; set; }
            public HttpWebResponse Response { get; set; }
            public Stream Stream { get; set; }
            public Action<IAsyncResult, HttpAsyncContext> Processor { get; set; }
            public abstract string ExceptionMessagePrefix { get; }

            public abstract void OnSuccess();
            public abstract void OnError();
        }

        class CheckTopicHttpAsyncContext : HttpAsyncContext
        {
            public SynchronizationContext SynchronizationContext { get; set; }
            public Action<bool> OnCheckedCore { get; set; }
            public Action<Exception> OnErrorCore { get; set; }
            public override string ExceptionMessagePrefix { get { return "Error while checking a topic."; } }

            public override void OnSuccess()
            {
                if (this.OnCheckedCore != null)
                {
                     if (this.SynchronizationContext != null)
                    {
                    this.SynchronizationContext.Post(
                           delegate(object state)
                           {
                               this.OnCheckedCore(this.BooleanResult);
                           },
                           null);
                    }
                     else
                     {
                         this.OnCheckedCore(this.BooleanResult);
                     }
                }
            }

            public override void OnError()
            {
                if (this.OnErrorCore != null)
                {
                    if (this.SynchronizationContext != null)
                    {
                        this.SynchronizationContext.Post(
                            delegate(object state)
                            {
                                this.OnErrorCore(this.Exception);
                            },
                            null);
                    }
                    else
                    {
                        this.OnErrorCore(this.Exception);
                    }
                }
            }
        }


       

        class PublishHttpAsyncContext : HttpAsyncContext
        {
            public SynchronizationContext SynchronizationContext { get; set; }
            public PubsubMessage Message { get; set; }
            public Action<PubsubMessage> OnSuccessCore { get; set; }
            public Action<PubsubMessage, Exception> OnErrorCore { get; set; }
            public override string ExceptionMessagePrefix { get { return "Error while publishing a message."; } }


            public override void OnSuccess()
            {
                if (this.OnSuccessCore != null)
                {
                    this.Message.MessageId = this.ResultId;
                    if (this.SynchronizationContext != null)
                    {
                        this.SynchronizationContext.Post(
                            delegate(object state)
                            {
                                this.OnSuccessCore(this.Message);
                            },
                            null);
                    }
                    else
                    {
                        this.OnSuccessCore(this.Message);
                    }
                }
            }

            public override void OnError()
            {
                if (this.OnErrorCore != null)
                {
                    if (this.SynchronizationContext != null)
                    {
                        this.SynchronizationContext.Post(
                            delegate(object state)
                            {
                                this.OnErrorCore(this.Message, this.Exception);
                            },
                            null);
                    }
                    else
                    {
                        this.OnErrorCore(this.Message, this.Exception);
                    }
                }
            }
        }

        class CreateTopicHttpAsyncContext : HttpAsyncContext
        {
            public SynchronizationContext SynchronizationContext { get; set; }
            public Action<int> OnSuccessCore { get; set; }
            public Action<Exception> OnErrorCore { get; set; }
            public override string ExceptionMessagePrefix { get { return "Error while creating a topic."; } }

            public override void OnSuccess()
            {
                if (this.OnSuccessCore != null)
                {
                    if (this.SynchronizationContext != null)
                    {
                        this.SynchronizationContext.Post(
                            delegate(object state)
                            {
                                this.OnSuccessCore(this.ResultId);
                            },
                            null);
                    }
                    else
                    {
                        this.OnSuccessCore(this.ResultId);
                    }
                }
            }

            public override void OnError()
            {
                if (this.OnErrorCore != null)
                {
                    if (this.SynchronizationContext != null)
                    {
                        this.SynchronizationContext.Post(
                            delegate(object state)
                            {
                                this.OnErrorCore(this.Exception);
                            },
                            null);
                    }
                    else
                    {
                        this.OnErrorCore(this.Exception);
                    }
                }
            }
        }
    }
}
