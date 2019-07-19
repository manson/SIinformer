using System.Text;
using System.IO;

namespace Laharsub.Client
{
    public class PubsubMessage
    {
        public int TopicId { get; set; }
        public string StringTopicId { get; set; }
        public int MessageId { get; set; }
        public string ContentType { get; set; }
        public Stream Body { get; set; }

        public string GetBodyAsString()
        {
            if (this.Body == null)
            {
                return null;
            }
            else
            {
                MemoryStream ms = this.Body as MemoryStream;
                if (ms == null)
                {
                    ms = new MemoryStream();
                    this.Body.CopyTo(ms);
                }
                byte[] bytes = ms.ToArray();
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }
        }
    }
}
