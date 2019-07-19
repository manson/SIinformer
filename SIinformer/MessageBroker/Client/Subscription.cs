using System;
using System.Threading;

namespace Laharsub.Client
{
    public class Subscription
    {
        public int TopicId { get; set; }
        public string StringTopicId { get; set; }
        public int From { get; set; }
        public Action<Subscription, Exception> OnError { get; set; }
        public Action<Subscription, PubsubMessage> OnMessageReceived { get; set; }
        internal SynchronizationContext SynchronizationContext { get; set; }
    }
}
