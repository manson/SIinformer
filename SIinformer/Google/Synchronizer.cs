using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.ComponentModel;

using Google.Documents;
using Nocs.Models;


namespace Nocs
{
    public class Synchronizer
    {
        public delegate void SyncEventHandler(SyncResult result);

        public event SyncEventHandler ContentUpdated;
        public event SyncEventHandler AutoFetchAllEntriesFinished;
        public event SyncEventHandler ErrorWhileSyncing;

        public readonly Queue<SyncJob> JobQueue = new Queue<SyncJob>();
        public readonly Queue<SyncJob> ErrorQueue = new Queue<SyncJob>();

        public bool SyncStopped { get; private set; }

        private Timer _jobTimer;
        private BackgroundWorker _bgWorker;


        public void InitializeSynchronizer()
        {
            // we'll use 10sec intervals for checks
            _jobTimer = new Timer { Interval = 10000 };
            _jobTimer.Elapsed += JobTimer_Elapsed;

            // let's initialize the background worker
            _bgWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            _bgWorker.DoWork += BgWorkerDoWork;
            _bgWorker.RunWorkerCompleted += BgWorkerRunWorkerCompleted;
        }


        /// <summary>
        /// Will start the timer for the synchronizer.
        /// </summary>
        public void Start()
        {
            SyncStopped = false;
            _jobTimer.Start();
        }


        /// <summary>
        /// Will stop the timer for the synchronizer.
        /// </summary>
        public void Stop()
        {
            SyncStopped = true;
            _jobTimer.Stop();
        }


        /// <summary>
        /// Will check whether a job with a given id exists in the JobQueue.
        /// </summary>
        /// <param name="id">The id to be checked.</param>
        /// <returns>
        /// true = a job for the given id is already in sync queue
        /// false = a job for the given id is not queued
        /// </returns>
        public bool IsJobAlreadyQueued(string id)
        {
            return JobQueue.Any(job => job.Id == id);
        }


        public void AddJobToQueue(SyncJob job)
        {
            JobQueue.Enqueue(job);

            // no reason to wait if queue was empty
            if (JobQueue.Count == 1)
            {
                JobTimer_Elapsed(null, null);
            }
            else
            {
                if (job.SyncDocument != null)
                {
                    Debug.WriteLine(string.Format("Added {0} to job queue", job.SyncDocument.Title));
                }
                else
                {
                    Debug.WriteLine(string.Format("Added {0} to job queue", job.Id));
                }
            }
        }

        void JobTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // let's first check the error queue
            if (ErrorQueue.Count > 0 && !SyncStopped && !_bgWorker.IsBusy)
            {
                // let's fetch an error job and execute it
                Debug.WriteLine(DateTime.Now + " - " + "Synchronizer: executing an error job");
                var job = ErrorQueue.Dequeue();
                ExecuteJob(job);
            }

            // let's make sure there's jobs to be executed and sync isn't halted or worker isn't running
            else if (JobQueue.Count > 0 && !SyncStopped && !_bgWorker.IsBusy)
            {
                // let's fetch a regular job and execute it
                var job = JobQueue.Dequeue();
                ExecuteJob(job);
            }
        }

        /// <summary>
        /// Will execute any given SyncJob.
        /// </summary>
        /// <param name="job">A SyncJob object that includes all the sync-related info.</param>
        private void ExecuteJob(SyncJob job)
        {
            // let's pause the timer first
            Stop();
            _bgWorker.RunWorkerAsync(job);
        }


        #region BackgroundWorker

        private static void BgWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            if (e.Argument == null)
            {
                Debug.WriteLine("Synchronizer: BgWorkerDoWork - given syncjob (e.Argument) is null");
                return;
            }

            SyncResult result = null;
            var job = (SyncJob)e.Argument;

            try
            {
                switch (job.Type)
                {
                    case SyncJobType.CheckForChanges:
                    {
                        var updatedDocument = NocsService.GetUpdatedDocument(job.SyncDocument);
                    
                        // GetUpdatedDocument won't return null if the document has been updated since last check
                        if (updatedDocument != null)
                        {
                            // the document has been updated, let's fetch the updated content
                            result = new SyncResult
                            {
                                ContentUpdated = true,
                                TimeUpdated = DateTime.Now,
                                Job = job,
                                Document = NocsService.GetDocumentContent(updatedDocument)
                            };
                        }
                        else
                        {
                            // the document hasn't been updated, let's generate a null result
                            result = new SyncResult
                            {
                                ContentUpdated = false,
                                Job = job
                            };
                        }
                        break;
                    }

                    case SyncJobType.UpdateAllEntries:
                    {
                        NocsService.UpdateAllEntries();
                        result = new SyncResult
                        {
                            ContentUpdated = false,
                            // this is only to be used with single documents
                            Job = job,
                        };
                        break;
                    }
                }

                e.Result = result;
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;

                if (job.Type == SyncJobType.CheckForChanges)
                {
                    Trace.WriteLine(string.Format("{0} - Synchronizer: error while checking if {1} is different in Google Docs: {2}", DateTime.Now, job.SyncDocument.Title, ex.Message));
                }
                else if (job.Type == SyncJobType.UpdateAllEntries)
                {
                    Trace.WriteLine(string.Format("{0} - Synchronizer: error while fetching all documents: {1}", DateTime.Now, ex.Message));
                }

                result = new SyncResult
                {
                    ContentUpdated = false,
                    Error = errorMessage,
                    Job = job
                };

                e.Result = result;
            }
        }

        private void BgWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var result = (SyncResult)e.Result;

            // let's then check for an error
            if (!string.IsNullOrEmpty(result.Error))
            {
                // error occurred, let's add the job to error queue
                var errorJob = result.Job;

                if (errorJob.ErrorsOccurred > 2)
                {
                    // this is the third time the job fails, let's notify listeners
                    ErrorWhileSyncing(result);
                }

                errorJob.ErrorsOccurred++;
                ErrorQueue.Enqueue(errorJob);
            }
            else
            {
                // no error, let's see if we have an update
                if (result.Job.Type == SyncJobType.CheckForChanges && result.ContentUpdated)
                {
                    ContentUpdated(result);
                }
                else if (result.Job.Type == SyncJobType.UpdateAllEntries)
                {
                    // if AutoFetchAllFinished is hooked, it means the Browse-form is open and we have to notify it
                    if (AutoFetchAllEntriesFinished != null)
                    {
                        AutoFetchAllEntriesFinished(result);
                    }
                }
            }

            // regardless of what happens, let's resume the syncTimer and check for new jobs immediately
            Start();
            JobTimer_Elapsed(null, null);
        }

        #endregion

        //public Document CreateNewDocument(string folderId, string title, string content, bool createDefaultDirectory)
        //{
        //    try
        //    {
        //        return NocsService.CreateNewDocument(folderId, title, content, createDefaultDirectory);
        //    }
        //    catch (Exception ex)
        //    {
        //        var result = new SyncResult
        //        {
        //            Job = new SyncJob { Title = title, Type = SyncJobType.Save },
        //            ContentUpdated = false,
        //            Error = ex.Message,
        //            Document = new Document { Content = content, Title = title }
        //        };

        //        // let's notify listeners
        //        ErrorWhileSyncing(result);
        //        return null;
        //    }
        //}

        //public Document SaveDocument(Document document)
        //{
        //    try
        //    {
        //        return NocsService.SaveDocument(document);
        //    }
        //    catch (Exception ex)
        //    {
        //        var result = new SyncResult
        //        {
        //            Job = new SyncJob { Title = document.Title, Type = SyncJobType.Save },
        //            ContentUpdated = false,
        //            Error = ex.Message,
        //            Document = document
        //        };

        //        // let's notify listeners
        //        ErrorWhileSyncing(result);
        //        return null;
        //    }
        //}
    }
}
