using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Laharsub.Client
{
    class HttpLongPollManager
    {
        public const int DefaultMaxBufferSize = 64 * 1024;
        public const int MinBufferSize = 256;

        byte[] buffer = new byte[MinBufferSize];
        int offset;
        int count;
        PollContext pollContext;
        Dictionary<int, Subscription> subscriptions;
        string baseAddress;
        object syncRoot = new object();
        AsyncCallback processNext;
        string connectionGroupName;
        int maxBufferSize;

        static Regex contentTypeRegex = new Regex(@"Content-type:\s*(.+)", RegexOptions.IgnoreCase);
        static Regex contentDescriptionRegex = new Regex(@"Content-Description:\s*(\d+)/(\d+)", RegexOptions.IgnoreCase);

        public HttpLongPollManager(string baseAddress, int maxBufferSize)
        {
            this.baseAddress = baseAddress;
            this.subscriptions = new Dictionary<int, Subscription>();
            this.processNext = new AsyncCallback(this.ProcessNext);
            this.connectionGroupName = Guid.NewGuid().ToString();
            this.maxBufferSize = maxBufferSize;
        }

        void AbortPoll()
        {
            if (this.pollContext != null)
            {
                lock (this.syncRoot)
                {
                    if (this.pollContext != null)
                    {
                        this.pollContext.PollAborted = true;
                        this.pollContext.Poll.Abort();
                        this.pollContext = null;
                    }
                }
            }
        }

        public void AddSubscription(Subscription s)
        {
            lock (this.syncRoot)
            {
                if (this.subscriptions.ContainsKey(s.TopicId))
                {
                    throw new InvalidOperationException("Cannot create subscription. Only one subscription to a given topic may be active at a time.");
                }
                this.subscriptions[s.TopicId] = s;
                this.AbortPoll();
                this.StartPoll();
            }
        }

        public void RemoveSubscription(int topicId)
        {
            lock (this.syncRoot)
            {
                if (this.subscriptions.ContainsKey(topicId))
                {
                    this.subscriptions.Remove(topicId);
                    if (this.subscriptions.Count == 0)
                    {
                        this.AbortPoll();
                    }
                }
            }
        }

        public void RemoveAllSubscriptions()
        {
            lock (this.syncRoot)
            {
                this.AbortPoll();
                this.subscriptions.Clear();
            }
        }

        void FaultAllSubscriptions(Exception e)
        {
            lock (this.syncRoot)
            {
                Exception e1 = new InvalidOperationException("HTTP long poll returned an error. Subscription is terminated.", e);
                foreach (Subscription s in this.subscriptions.Values)
                {
                    if (s.OnError != null)
                    {
                        if (s.SynchronizationContext != null)
                        {
                            s.SynchronizationContext.Post(
                                delegate(object state)
                                {
                                    this.FaultSubscriptionCore(s, e1);
                                },
                                null);
                        }
                        else
                        {
                            this.FaultSubscriptionCore(s, e1);
                        }
                    }
                }
                this.subscriptions.Clear();
                this.AbortPoll();
            }
        }

        void FaultSubscriptionCore(Subscription s, Exception e1)
        {
            try
            {
                s.OnError(s, e1);
            }
            catch (Exception)
            {
                // empty
            }
        }

        void StartPoll()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}subscriptions/volatile?", this.baseAddress);
            int i = 0;
            foreach (Subscription s in this.subscriptions.Values)
            {
                if (i > 0)
                {
                    sb.Append("&");
                }
                sb.AppendFormat(CultureInfo.InvariantCulture, "subs[{0}][topicid]={1}", i, s.TopicId);
                if (s.From > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "&subs[{0}][from]={1}", i, s.From);
                }
                i++;
            }
            this.pollContext = new PollContext
            {
                Poll = (HttpWebRequest)WebRequest.Create(sb.ToString()),
                Processor = this.ProcessPollResponse
            };
#if SILVERLIGHT
            this.pollContext.Poll.AllowReadStreamBuffering = false;
#else
            this.pollContext.Poll.Pipelined = false;
            this.pollContext.Poll.ConnectionGroupName = this.connectionGroupName;
#endif
            this.pollContext.Poll.BeginGetResponse(this.processNext, this.pollContext);
        }

        void ProcessNext(IAsyncResult result)
        {
            PollContext context = (PollContext)result.AsyncState;
            if (!context.PollAborted)
            {
                try
                {
                    context.Processor(result, context);
                }
                catch (Exception e)
                {
                    this.pollContext = null;
                    context.Close();
                    if (!context.PollAborted)
                    {
                        this.FaultAllSubscriptions(e);
                    }
                }
            }
            else
            {
                this.pollContext = null;
                context.Close();
            }
        }

        void ProcessPollResponse(IAsyncResult result, PollContext context)
        {
            context.Response = (HttpWebResponse)context.Poll.EndGetResponse(result);
            context.ResponseStream = context.Response.GetResponseStream();
            this.offset = 0;
            this.count = 0;
            context.Processor = this.ProcessPollRead;
            context.ParsingContext = new MultipartMimeParsingContex();
            context.ResponseStream.BeginRead(this.buffer, this.offset, this.buffer.Length, this.processNext, context);
        }

        void ProcessPollRead(IAsyncResult result, PollContext context)
        {
            int read = context.ResponseStream.EndRead(result);
            this.ParseMultipartMime(read, context.ParsingContext);
            if (read > 0)
            {
                this.EnsureRoomInBuffer();
                context.ResponseStream.BeginRead(this.buffer, this.offset + this.count, this.buffer.Length - this.offset - this.count, this.processNext, context);
            }
            else
            {
                context.Close();
                this.pollContext = null;
                if (this.subscriptions.Count > 0)
                {
                    this.StartPoll();
                }
            }
        }

        void EnsureRoomInBuffer()
        {
            if (this.count == this.buffer.Length)
            {
                // buffer is full with no data consumed yet; grow the buffer
                if (this.buffer.Length >= this.maxBufferSize)
                {
                    throw new InvalidOperationException("Notification exceeds the size limit. Increase MaxBufferSize to allow it.");
                }
                int newSize = buffer.Length * 2 > this.maxBufferSize ? this.maxBufferSize : this.buffer.Length * 2;
                byte[] newBuffer = new byte[newSize];
                Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.buffer.Length);
                this.buffer = newBuffer;
            }
            else if (this.count == 0)
            {
                // all data in the buffer has been consumed
                this.offset = 0;
            }
            else if ((this.offset + this.count) == this.buffer.Length)
            {
                // some unconsumed data remains at the very end of the buffer with some free space up front; 
                // move the unread data to the beginning of the buffer
                Buffer.BlockCopy(this.buffer, this.offset, this.buffer, 0, this.count);
                this.offset = 0;
            }
        }

        void ConsumeBytes(int count)
        {
            this.offset += count;
            this.count -= count;
        }

        void ParseMultipartMime(int read, MultipartMimeParsingContex context)
        {
            this.count += read;
            bool continueParsing = true;
            while (continueParsing)
            {
                switch (context.State)
                {
                    case ParsingState.FirstBoundary:
                        continueParsing = this.ParseFirstMimeBoundary(context);
                        break;
                    case ParsingState.ContentTypeHeader:
                        continueParsing = this.ParseContentTypeHeader(context);
                        break;
                    case ParsingState.ContentDescriptionHeader:
                        continueParsing = this.ParseContentDescriptionHeader(context);
                        break;
                    case ParsingState.CRLF:
                        continueParsing = this.ParseCRLF(context);
                        break;
                    case ParsingState.Body:
                        continueParsing = this.ParseBody(context);
                        if (continueParsing)
                        {
                            this.DispatchMessage(context.Message);
                            context.Message = null;
                        }
                        break;
                    case ParsingState.AfterBoundary:
                        continueParsing = this.ParseAfterBoundary(context);
                        break;
                    case ParsingState.Epilogue:
                        // ignore all data after the final MIME boundary
                        continueParsing = false;
                        this.ConsumeBytes(this.count);
                        break;
                };
            }
        }

        bool ParseFirstMimeBoundary(MultipartMimeParsingContex context)
        {
            bool result = this.FindCRLF(context);

            if (result)
            {
                if (context.ParsingIndex < 3 || this.buffer[this.offset] != 0x2D || this.buffer[this.offset + 1] != 0x2D)
                {
                    // boundary does not start with "--" or is shorter than 1 character
                    throw new InvalidOperationException("Malformed HTTP long poll response. Cannot determine multipart/mixed boundary.");
                }
                context.Boundary = new byte[2 + context.ParsingIndex];
                context.Boundary[0] = 0x0D;
                context.Boundary[1] = 0x0A;
                Buffer.BlockCopy(this.buffer, this.offset, context.Boundary, 2, context.ParsingIndex);
                this.ConsumeBytes(context.ParsingIndex + 2);
                context.ParsingIndex = 0;
                context.State = ParsingState.ContentTypeHeader;
            }

            return result;
        }

        bool ParseContentTypeHeader(MultipartMimeParsingContex context)
        {
            bool result = this.FindCRLF(context);

            if (result)
            {
                string header = Encoding.UTF8.GetString(this.buffer, this.offset, context.ParsingIndex);
                Match m = contentTypeRegex.Match(header);
                if (!m.Success)
                {
                    throw new InvalidOperationException("Malformed HTTP long poll response. Cannot determine the content type of the MIME part.");
                }
                context.Message = new PubsubMessage();
                context.Message.ContentType = m.Groups[1].Value;
                this.ConsumeBytes(context.ParsingIndex + 2);
                context.ParsingIndex = 0;
                context.State = ParsingState.ContentDescriptionHeader;
            }

            return result;
        }

        bool ParseContentDescriptionHeader(MultipartMimeParsingContex context)
        {
            bool result = this.FindCRLF(context);

            if (result)
            {
                string header = Encoding.UTF8.GetString(this.buffer, this.offset, context.ParsingIndex);
                Match m = contentDescriptionRegex.Match(header);
                if (!m.Success)
                {
                    throw new InvalidOperationException("Malformed HTTP long poll response. Connot determine topicId and messageId of the MIME part.");
                }
                context.Message.TopicId = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                context.Message.MessageId = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                this.ConsumeBytes(context.ParsingIndex + 2);
                context.ParsingIndex = 0;
                context.State = ParsingState.CRLF;
            }

            return result;
        }

        bool ParseCRLF(MultipartMimeParsingContex context)
        {
            bool result = this.FindCRLF(context);

            if (result)
            {
                if (context.ParsingIndex != 0)
                {
                    throw new InvalidOperationException("Malformed HTTP long poll response. Unexpected MIME part header.");
                }
                this.ConsumeBytes(2);
                context.ParsingIndex = 0;
                context.State = ParsingState.Body;
            }

            return result;
        }

        bool ParseBody(MultipartMimeParsingContex context)
        {
            bool result = this.FindBoundary(context);

            if (result)
            {
                byte[] body = new byte[context.ParsingIndex];
                Buffer.BlockCopy(this.buffer, this.offset, body, 0, context.ParsingIndex);
                context.Message.Body = new MemoryStream(body);
                this.ConsumeBytes(context.ParsingIndex + context.Boundary.Length);
                context.ParsingIndex = 0;
                context.State = ParsingState.AfterBoundary;
            }

            return result;
        }

        bool ParseAfterBoundary(MultipartMimeParsingContex context)
        {
            if (this.count >= 2)
            {
                if (this.buffer[this.offset] == 0x0D && this.buffer[this.offset + 1] == 0x0A)
                {
                    // another MIME part is expected
                    this.ConsumeBytes(2);
                    context.State = ParsingState.ContentTypeHeader;
                }
                else if (this.buffer[this.offset] == 0x2D && this.buffer[this.offset + 1] == 0x2D)
                {
                    // another MIME part is not expected
                    this.ConsumeBytes(2);
                    context.State = ParsingState.Epilogue;
                }
                else
                {
                    throw new InvalidOperationException("Malformed HTTP long poll response. Protocol violation after MIME boundary.");
                }
            }

            return (this.count > 0);
        }

        bool FindBoundary(MultipartMimeParsingContex context)
        {
            bool result = false;

            while (!result && context.ParsingIndex <= (this.count - context.Boundary.Length))
            {
                result = true;
                int i = 0;
                while (result && i < context.Boundary.Length)
                {
                    result = this.buffer[this.offset + context.ParsingIndex + i] == context.Boundary[i];
                    i++;
                }
                if (!result)
                {
                    context.ParsingIndex++;
                }
            }

            return result;
        }

        bool FindCRLF(MultipartMimeParsingContex context)
        {
            bool result = false;

            while (!result && context.ParsingIndex < (this.count - 1))
            {
                if (this.buffer[this.offset + context.ParsingIndex] == 0x0D
                    && this.buffer[this.offset + context.ParsingIndex + 1] == 0x0A)
                {
                    result = true;
                }
                else
                {
                    context.ParsingIndex++;
                }
            }

            return result;
        }

        void DispatchMessage(PubsubMessage message)
        {
            Subscription s = null;
            if (this.subscriptions.TryGetValue(message.TopicId, out s))
            {
                if (s.From <= message.MessageId)
                {
                    s.From = message.MessageId + 1;
                }
                try
                {
                    if (s.OnMessageReceived != null)
                    {
                        if (s.SynchronizationContext != null)
                        {
                            s.SynchronizationContext.Post(
                                delegate(object state)
                                {
                                    s.OnMessageReceived(s, message);
                                },
                                null);
                        }
                        else
                        {
                            s.OnMessageReceived(s, message);
                        }
                    }
                }
                catch (Exception)
                {
                    // empty
                }
            }
        }

        class PollContext
        {
            public bool PollAborted { get; set; }
            public HttpWebRequest Poll { get; set; }
            public HttpWebResponse Response { get; set; }
            public Stream ResponseStream { get; set; }
            public Action<IAsyncResult, PollContext> Processor { get; set; }
            public MultipartMimeParsingContex ParsingContext { get; set; }

            public void Close()
            {
                if (this.ResponseStream != null)
                {
                    this.ResponseStream.Close();
                    this.ResponseStream = null;
                }
                if (this.Response != null)
                {
                    this.Response.Close();
                    this.Response = null;
                }
            }
        }

        class MultipartMimeParsingContex
        {
            public ParsingState State { get; set; }
            public int ParsingIndex { get; set; }
            public int BoundaryIndex { get; set; }
            public byte[] Boundary { get; set; }
            public PubsubMessage Message { get; set; }
        }

        enum ParsingState
        {
            FirstBoundary,
            ContentTypeHeader,
            ContentDescriptionHeader,
            CRLF,
            Body,
            AfterBoundary,
            Epilogue
        }
    }
}
