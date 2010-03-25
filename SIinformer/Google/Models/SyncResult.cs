using System;
using Google.Documents;

namespace Nocs.Models
{
    /// <summary>
    /// Represents the result for a synchronization attempt.
    /// Encapsulates the results and potential errors.
    /// </summary>
    public class SyncResult
    {
        /// <summary>
        /// The SyncJob associated with attempted sync.
        /// </summary>
        public SyncJob Job { get; set; }

        /// <summary>
        /// Has the content been updated?
        /// </summary>
        public bool ContentUpdated { get; set; }
        
        /// <summary>
        /// When was the updated content fetched?
        /// </summary>
        public DateTime TimeUpdated { get; set; }

        /// <summary>
        /// Updated Document object.
        /// </summary>
        public Document Document { get; set; }

        /// <summary>
        /// A potential error string.
        /// </summary>
        public string Error { get; set; }
    }
}
