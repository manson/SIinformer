using System;

namespace Laharsub.Subscriptions
{
    public class SubscriptionMessageCommand
    {
        public string Id { get; set; }
        public static long SubCounter = 0;
        public string ClientId { get; set; }
        public string Command { get; set; }
        public string JsonObject { get; set; }
        public byte[] JsonObjectBytes { get; set; }
        public long Timestamp { get; set; }
        public int OrderInPackage { get; set; }
        /// <summary>
        /// текущий максимальный обработанный id
        /// </summary>
        public long MaxMessageId { get; set; }

        public SubscriptionMessageCommand()
        {
            ClientId = SubscriptionManager.CurrentClientId;
            Timestamp =-1;
            OrderInPackage = 0;
            Id = Guid.NewGuid().ToString();
        }

      
    }
}
