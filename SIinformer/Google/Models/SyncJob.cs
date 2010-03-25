using Google.Documents;

namespace Nocs.Models
{
    public class SyncJob
    {
        public string Id { get; set; }
        public SyncJobType Type { get; set; }
        public Document SyncDocument { get; set; }
        public string Title { get; set; }
        public int ErrorsOccurred { get; set; }
        public bool Working { get; set; }
    }
}
