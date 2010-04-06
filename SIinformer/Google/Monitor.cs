using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml.Serialization;
using Google.Documents;
using Google.GData.Documents;
using Nocs;
using Nocs.Helpers;
using Nocs.Models;
using Timer=System.Threading.Timer;
using SIinformer.Logic;
using SIinformer.Utils;

namespace SISyncronizer
{
    public class Monitor
    {
        // получатель структуры файлов и каталогов
        BackgroundWorker BgWorkerGetAllItems = new BackgroundWorker();

        // a common synchronizer that will be referenced throughout
        private Synchronizer _synchronizer;
        // a delegate for thread-safe control-manipulation
        private delegate void MainFormThreadSafeDelegate(object value);

        // because the loading of new documents is done asynchronously, we'll use a threadLock object as help
        private readonly object _threadLock = new object();
        private int _contentUpdaterWorkers;


        // we'll also use a timer in the main form for queuing up retrievals for all entries (documents and folders)
        private const int AutoFetchAllEntriesInterval = 4;
        private readonly Timer _autoFetchAllEntriesTimer;
        private const string AutoFetchId = "autoFetchId";

        private string txtGUser = "";
        private string txtGPassword = "";

        private BackgroundWorker bgWorker = null;

        // первая синхронизациия
        public bool FirstRun{ get; set; }

        private string _Status = "";
        public string Status
        {
            get { return _Status; }
            set
            {
                _Status = value;
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(
                value, false, false);
            }
        }


        public Monitor(string UserName, string Password)
        {
            txtGUser = UserName;
            txtGPassword = Password;
            FirstRun = true;

        }


        public void Stop()
        {
            if (BgWorkerGetAllItems != null)
            {
                while (BgWorkerGetAllItems.IsBusy) System.Threading.Thread.Sleep(1) ;
                //BgWorkerGetAllItems.CancelAsync();
                //while (BgWorkerGetAllItems.IsBusy) System.Threading.Thread.Sleep(1);
                BgWorkerGetAllItems = null;
            }
            if (bgWorker != null)
            {
                while (bgWorker.IsBusy) System.Threading.Thread.Sleep(1);
                //bgWorker.CancelAsync();
                //while (bgWorker.IsBusy) System.Threading.Thread.Sleep(1);
                bgWorker = null;
            }
            if (_synchronizer!=null)
            {
                _synchronizer.Stop();
                _synchronizer = null;
            }
            if (_autoFetchAllEntriesTimer != null)
                _autoFetchAllEntriesTimer.Dispose();
        }

        /// <summary>
        /// Тестовый конструктор, потом сюда надо будет привязывать список авторов. Вернее синхронизатор будет обращаться к нему
        /// </summary>
        /// <param name="UserName"></param>
        /// <param name="Password"></param>
        /// <param name="data"></param>
        //private Dictionary<string, string> _data = null;
        //public Monitor(string UserName, string Password, Dictionary<string, string> data)
        //{
        //    txtGUser = UserName;
        //    txtGPassword = Password;
        //    _data = data;
        //}
        
        
        
        public void Start()
        {
            if (string.IsNullOrEmpty(txtGUser) || string.IsNullOrEmpty(txtGPassword))
            {
                Status = "Google - имя и пароль не должны быть пустыми.";
                return;
            }
            // настроим прокси
            NocsService.UseProxy = SIinformer.Window.MainWindow.GetSettings().ProxySetting.UseProxy;
            if(NocsService.UseProxy)
            {
                NocsService.AutomaticProxyDetection = false;
                NocsService.ProxyHost = SIinformer.Window.MainWindow.GetSettings().ProxySetting.Address;
                NocsService.ProxyPort = SIinformer.Window.MainWindow.GetSettings().ProxySetting.Port.ToString();
                NocsService.ProxyUsername = SIinformer.Window.MainWindow.GetSettings().ProxySetting.UseAuthentification
                                                ?
                                                    SIinformer.Window.MainWindow.GetSettings().ProxySetting.UserName
                                                : "";
                NocsService.ProxyPassword = SIinformer.Window.MainWindow.GetSettings().ProxySetting.UseAuthentification
                                                ?
                                                    SIinformer.Window.MainWindow.GetSettings().ProxySetting.Password
                                                : "";
            }
            else            
                NocsService.AutomaticProxyDetection = true;
            
            AuthorDocumentLink = new Dictionary<string, Document>();
            // let's instantiate our Synchronizer
            _synchronizer = new Synchronizer();
            _synchronizer.ErrorWhileSyncing += SynchronizerErrorWhileSyncing;
            _synchronizer.AutoFetchAllEntriesFinished += AutoFetchAllEntriesFinished;
            _synchronizer.InitializeSynchronizer();
            _synchronizer.Start();
            bgWorker = new BackgroundWorker();
            bgWorker.DoWork += new DoWorkEventHandler(bgWorker_Validate_DoWork);
            bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorker_Validate_RunWorkerCompleted);

            BgWorkerGetAllItems = new BackgroundWorker();

            BgWorkerGetAllItems.DoWork += new DoWorkEventHandler(BgWorkerGetAllItems_DoWork);
            BgWorkerGetAllItems.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BgWorkerGetAllItems_RunWorkerCompleted);

            FirstRun = true;// это первый запуск, а значит будем тянуть инфу с гугла
            // login
            bgWorker.RunWorkerAsync();
            
        }

        /// <summary>
        /// сохранение всех данных на гугл. Делается один раз. Инициализация
        /// </summary>
        private void SaveAllItemsToGoogle()
        {          
            var bg = new BackgroundWorker();
            bg.DoWork += (s, e) =>
                             {
                                 foreach (Author author in InfoUpdater.Authors)
                                 {
                                     // если синхронизацию остановили - выходим
                                     if (!TimerBasedAuthorsSaver.GetInstance().SynchroGoogle)
                                     {
                                         Status = "Google: Синхронизация ссылок остановлена по запросу.";
                                         return;
                                     }
                                     SaveNewDocument(author.Id, author.GoogleContent, false, author);
                                 }
                                 
                             };
            bg.RunWorkerAsync();
        }


        /// <summary>
        /// Сохранить новый документ/ссылку
        /// </summary>
        /// <param name="file">имя</param>
        /// <param name="content">содержимое</param>
        /// <param name="async">асинхронно или нет</param>
        public void SaveNewDocument(string file, string content, bool async, Author author)
        {
            string folderId = NocsService.GetFolderId(NocsService.SInformerFolder);
            if (string.IsNullOrEmpty(folderId))
            {
                Status = "Google: Невозможно сохранить ссылку, так как отсутствует папка на Гугле. Проведите полную синхронизацию.";
                return;
            }
            string authorName = author.Name;//content.Split(Environment.NewLine.ToCharArray(),StringSplitOptions.RemoveEmptyEntries)[1];
            if (async)
            {
                var bg = new BackgroundWorker();
                bg.DoWork += (s, e) =>
                                 {
                                     _SyncingItems++;
                                     Document d = NocsService.CreateNewDocument(folderId, file, content, false);
                                     if (d != null) // если успешно сохранили
                                     {
                                         author.timeStamp = d.Updated; // синхронизируем даты на гугле и локально
                                         author.ChangedGoogle = false;
                                         // сохраняем связь автора и гугловский документ
                                         if (!AuthorDocumentLink.ContainsKey(author.Id))
                                             AuthorDocumentLink.Add(author.Id, d);
                                         else
                                             AuthorDocumentLink[author.Id] = d;

                                         SIinformer.Window.MainWindow.MainForm.GetLogger().Add(
                                             "Google: сохранена ссылка на " + authorName, false, false);
                                     }
                                     _SyncingItems--;
                                 };
            
            bg.RunWorkerAsync();
            }
            else
            {
                _SyncingItems++;
                Document d = NocsService.CreateNewDocument(folderId, file, content, false);
                if (d != null) // если успешно сохранили
                {
                    author.timeStamp = d.Updated; // синхронизируем даты на гугле и локально
                    author.ChangedGoogle = false;
                    // сохраняем связь автора и гугловский документ
                    if (!AuthorDocumentLink.ContainsKey(author.Id))
                        AuthorDocumentLink.Add(author.Id, d);
                    else
                        AuthorDocumentLink[author.Id] = d;

                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(
                        "Google: сохранена ссылка на " + authorName, false, false);
                }
                _SyncingItems--;
            }
            
        }

        public void SaveDocument(Author author)
        {
            if (!AuthorDocumentLink.ContainsKey(author.Id)) return;
            var bg = new BackgroundWorker();
            bg.DoWork += (s, e) =>
            {
                _SyncingItems++;
                Document d = AuthorDocumentLink[author.Id];
                // берем обновленные данные автора
                d.Content = InfoUpdater.GetAuthorById(author.Id).GoogleContent;
                d = NocsService.SaveDocument(d);
                if (d != null)// если сохранение прошло успешлно
                {
                    AuthorDocumentLink[author.Id] = d;
                    author.timeStamp = d.Updated; // синхронизируем даты на гугле и локально
                    author.Changed = true;// для сохранения в БД изменившихся данных
                    author.ChangedGoogle = false;
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(
                        "Google: обновлена ссылка на гугле для " + author.Name, false, false);
                }
                _SyncingItems--;
            };

            bg.RunWorkerAsync();

        }

        #region Загрузка контента документа/ссылки на гугле
        private void LoadDocument(Document document)
        {
            // 1. let's create a new bgWorker
            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += BgWorkerLoadDocumentContent_DoWork;
            worker.RunWorkerCompleted += BgWorkerLoadDocumentContent_Completed;

            // 2. let's increase the number of current workers for syncing purposes
            lock (_threadLock)
            {
                _contentUpdaterWorkers++;
            }

            // 3. let's update status and run our worker
            //Status="Загрузка данных ссылки " + document.Title;
            worker.RunWorkerAsync(document);

        }
        private static void BgWorkerLoadDocumentContent_DoWork(object sender, DoWorkEventArgs e)
        {
            var document = e.Argument as Document;
            // if an error will occur, we'll keep the document as result so we can retry
            e.Result = document;
            e.Result = NocsService.GetDocumentContent(document);
        }

        private void BgWorkerLoadDocumentContent_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            var updatedDocument = e.Result as Document;

            // let's first check for an error
            if (e.Error != null)
            {
                if (updatedDocument == null)
                {
                    Trace.WriteLine(DateTime.Now + " - Main: error while loading document content");
                }
                else
                {
                    Trace.WriteLine(DateTime.Now + " - Main: error while loading document content: " + updatedDocument.Title);
                }

                Status = "Google: Ошибка чтения содержимого ссылки: " + e.Error.Message;
                return;
            }

            if (updatedDocument != null)
            {
                // if for some reason the document wasn't found or file was corrupt, we'll remove that tab
                /*                   
                          where we will inform the user to either reload the document or save it inside docs.google.com
                */
                if (updatedDocument.Summary != null &&
                    (updatedDocument.Summary.ToLowerInvariant().Contains("document not found") ||
                     updatedDocument.Summary.ToLowerInvariant().Contains("file is corrupt, or an unknown format")))
                {
                    Status = "Google: Ошибка чтения содержимого ссылки: " + updatedDocument.Summary;
                    updatedDocument.Summary = null;
                }
                else
                {
                    // а здесь добавим урл в си-информер!
                    Author author = null;
                    try
                    {
                        var reader = new StringReader(Tools.DecodeFrom64(updatedDocument.Content));
                        var sr = new XmlSerializer(typeof (Author));
                        author = (Author) sr.Deserialize(reader);
                    }
                    catch
                    {
                        Status = "Google: Ошибка при десериализации ссылки на " + updatedDocument.Title;
                    }

                    //string[] lines = updatedDocument.Content.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (author != null)
                    {
                        SIinformer.Window.MainWindow.MainForm.InvokeIfRequired(() =>
                        {
                            // если такой автор есть - обновляем его, иначе добавляем
                            if (InfoUpdater.ContainsAuthor(author.Id))
                            {
                                // дополнительно проверяем, если локальная ссылка успела обновиться до синхронизации, то ничего не делаем
                                // эта ссылка в следующем цикле пойдет на гугл
                                if (InfoUpdater.GetAuthorById(author.Id).UpdateDate <= author.UpdateDate)
                                {
                                    author.timeStamp = updatedDocument.Updated; // на всякий случай выравниваем время обновления
                                    author.Changed = true; // для сохранения в БД
                                    author.ChangedGoogle = false;// чтобы не попала в цикл обновления                                    
                                    InfoUpdater.UpdateAuthorContent(author);
                                }
                            }
                            else
                                InfoUpdater.Authors.Add(author);
                        }, DispatcherPriority.Background);

                        // сохраняем связь автора и документа гугла
                        if (!AuthorDocumentLink.ContainsKey(author.Id))
                            AuthorDocumentLink.Add(author.Id, updatedDocument);
                        else
                            AuthorDocumentLink[author.Id] = updatedDocument;

                        Status = "Google: Скачана ссылка на " + author.Name;
                        //InfoUpdater.AddAuthor(lines[0].Trim(), false);
                        
                    }
                }
            }

            // let's handle the loader icon with a thread lock
            lock (_threadLock)
            {
                // let's first decrement the current number of workers
                // (should always be over 0 at this point
                if (_contentUpdaterWorkers > 0)
                {
                    _contentUpdaterWorkers--;
                }

                if (_contentUpdaterWorkers == 0)
                {
                    // there are no more updaters active, we can disable loader/clear status
                    Status="Google: Синхронизация на локальную машину завершена.";
                    FirstRun = false;
                }
            }
        }

        #endregion

        private int _SyncingItems = 0;
        public bool IsSyncing()
        {
            bool result = true;
            lock (_threadLock)
            {
                result =(_contentUpdaterWorkers > 0 | _SyncingItems>0);
            }
            return result;
        }

        /// <summary>
        /// Реальная синхронизация ссылок с гуглом
        /// </summary>        
        public void SyncWithGoogle()
        {            
            string folderId = NocsService.GetFolderId(NocsService.SInformerFolder);
            // let's make sure there is a folder with the given folderId
            var foundFoldersForFolderId = NocsService.AllFolders.Values.Any(d => d.Self == folderId) ||
                                          NocsService.AllFolders.Values.Any(d => d.ResourceId == folderId);
            if (!foundFoldersForFolderId)
            {
                // no folders for this user with given folderId found, let's reset the folderId to avoid problems
                folderId = null;
                Status = "Google: Проблема с папкой на гугле. Кто-то удалил его там...";                
                // остановим текущую синхронизацию. По таймеру она запустится снова
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            // получим список всех файлов в нашей папке
            var folderIdUrl = folderId.Contains("http:") ? folderId : DocumentsListQuery.documentsBaseUri + "/" + folderId.Replace(":", "%3A");
            IEnumerable<Google.Documents.Document> listOfDocuments = NocsService.AllDocuments.Values.Where(d => d.ParentFolders.Contains(folderIdUrl)).ToList();

            foreach (Google.Documents.Document document in listOfDocuments)
            {                
                // если в локальной базе отсутствует ссылка, то добавляем
                if (!InfoUpdater.ContainsAuthor(document.Title))
                {
                    // если синхронизацию остановили - выходим
                    if (!TimerBasedAuthorsSaver.GetInstance().SynchroGoogle)
                    {
                        Status = "Google: Синхронизация ссылок остановлена по запросу.";
                        _SyncingItems = 0;
                        // остановим текущую синхронизацию. По таймеру она запустится снова
                        TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                        return;
                    }
                    LoadDocument(document);     
                }
                // если в локально кеше нет данных об авторе, запоминаем
                if (!AuthorDocumentLink.ContainsKey(document.Title))
                    AuthorDocumentLink.Add(document.Title, document);
            }
            // для каждой локальной ссылки проверяем, есть ли она на гугле, если нет - добавлем
            foreach (Author author in InfoUpdater.Authors)
            {
                // если синхронизацию остановили - выходим
                if (!TimerBasedAuthorsSaver.GetInstance().SynchroGoogle)
                {
                    Status = "Google: Синхронизация ссылок остановлена по запросу.";
                    _SyncingItems = 0;
                    // остановим текущую синхронизацию. По таймеру она запустится снова
                    TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                    return;
                }
                bool bFound = false;
                Google.Documents.Document foundDocument = null;
                foreach (Google.Documents.Document document in listOfDocuments)
                {
                    if (document.Title == author.Id)
                    {
                        bFound = true;
                        foundDocument = document;
                        break;
                    }
                }
                if (!bFound)
                {
                    SaveNewDocument(author.Id, author.GoogleContent, true, author);
                }
                else // если есть такой автор, смотрим, синхронизирован или нет
                {
                    // если нет штампа, это означает, что данная копия автора никогда не синхронизировалас
                    // поэтому просто выставим ей дату с Гугла, дабы избежать полного скачивания
                    // данных с гугла
                    if (foundDocument != null && author.timeStamp == null)
                    {
                        author.timeStamp = foundDocument.Updated;
                        author.Changed = true;
                        author.ChangedGoogle = false;
                    }
                    else // иначе синхронизируем
                        if (foundDocument != null && author.timeStamp < foundDocument.Updated)
                        {
                            if (FirstRun) // первая синхронизация
                                LoadDocument(foundDocument);
                            else // иначе считаем, что просто у нас косяк, но все-таки наши данные верные, так как уже одна синхронизация с гуглам была
                            {
                                author.timeStamp = foundDocument.Updated;
                                author.Changed = true;
                                author.ChangedGoogle = false;                                
                            }
                        }
                        else// иначе, если автор измнился локально, закачиваем его на гугл
                            if (foundDocument != null && author.ChangedGoogle)
                                SaveDocument(author);
                }
                
            }            
        }


        #region Get All Items

        private Dictionary<string, Document> AuthorDocumentLink = null;

        public void GetAllItems()
        {
            _SyncingItems++;
            Status = "Google: Получение списка ссылок...";
            BgWorkerGetAllItems.RunWorkerAsync();
        }

        private void BgWorkerGetAllItems_DoWork(object sender, DoWorkEventArgs e)
        {
            NocsService.UpdateAllEntries();
        }

        private void BgWorkerGetAllItems_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Status = "Google: ссылки из интернета загружены.";
            // functionality after GetAllDocuments-worker has finished
            if (e.Error != null)
            {
                Trace.WriteLine(DateTime.Now + " - Main: error while retrieving all documents: " + e.Error.Message);
                // there was an error during the operation
                Status = "Google: Ошибка - " + e.Error.Message + ".";
                // остановим текущую синхронизацию. По таймеру она запустится снова
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
            }
            else
            {
                _SyncingItems--;
                // проверим, существует ли наша папка
                bool HasBookmarks = false;
                foreach (var folder in NocsService.AllFolders.Values)
                {
                    // Наша папка лежит в корне
                    if (folder.ParentFolders.Count == 0 && folder.Title == NocsService.SInformerFolder)
                    {
                        HasBookmarks = true;
                        break;
                    }
                }
                // если есть ссылки, то синхронизируем их на локальную машину
                if (HasBookmarks)
                {
                    SyncWithGoogle();   
                }
                else //иначе запускаем процесс записи всех наших ссылок в гугл
                {
                    // создадим нашу папку
                    NocsService.CreateNewFolder(NocsService.SInformerFolder);
                    if (NocsService.GetFolderId(NocsService.SInformerFolder)=="")
                    {
                        Status = "Google: Не удалось создать папку SInformer bookmarks!";
                        // остановим текущую синхронизацию. По таймеру она запустится снова
                        TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                        return;
                    }
                    SaveAllItemsToGoogle();
                }

            }
        }

        #endregion





        #region Background Workers

        private void bgWorker_Validate_DoWork(object sender, DoWorkEventArgs e)
        {
            if (Tools.IsConnected())
            {
                // try to start the NocsServiceervice with given credentials
                NocsService.AuthenticateUser(txtGUser, txtGPassword, true);
            }
            else
            {
                Status = "Google: Нет соединения с интернетом.";
                // остановим текущую синхронизацию. По таймеру она запустится снова
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
            }
        }

        private void bgWorker_Validate_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                // there was an error during the operation, show it to user
                if (e.Error.Message.Length > 27)
                {
                    Status = String.Format("Google: {0}..", e.Error.Message.Substring(0, 26));
                }
                else
                {
                    Status = "Google: " +  e.Error.Message;
                }
                // остановим текущую синхронизацию. По таймеру она запустится снова
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
            }
            else
            {
                // the operation succeeded, save settings
                NocsService.AccountChanged = true;
                NocsService.Username = txtGUser;
                NocsService.Password = txtGPassword;
                GetAllItems();

            }
        }

        #endregion


        private static void SynchronizerErrorWhileSyncing(SyncResult result)
        {
            if (!string.IsNullOrEmpty(result.Error))
            {
                Trace.WriteLine(string.Format("{0} - Main: error while syncing - type: {1} - message: {2}", DateTime.Now, result.Job.Type, result.Error));

                var lowerCaseError = result.Error.ToLowerInvariant();

                if (lowerCaseError.Contains("internet down") ||
                    lowerCaseError.Contains("connection timed out") ||
                    lowerCaseError.Contains("remote name could not be resolved") ||
                    lowerCaseError.Contains("unable to connect to the remote server"))
                {
                    MessageBox.Show(new Form { TopMost = true }, "Can't connect to internet. Make sure you're online and try again.", "Can't connect to internet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (lowerCaseError.Contains("resource not found"))
                {
                    MessageBox.Show(new Form { TopMost = true }, "A resource couldn't be found while attempting an update. Please investigate nocs.log and report any errors at http://nocs.googlecode.com/. Thanks!", "Resource not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show(new Form { TopMost = true }, "Error occurred while syncing, please close Nocs, inspect nocs.log and report found errors to http://nocs.googlecode.com/", "Error while syncing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            Trace.Flush();
        }
        /// <summary>
        /// Will be called whenever an autofetch for all documents finishes.
        /// </summary>
        private void AutoFetchAllEntriesFinished(SyncResult result)
        {
            //MessageBox.Show("Synced!");
        }


    }
}
