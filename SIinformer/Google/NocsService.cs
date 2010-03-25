using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Google.GData.Documents;
using Google.GData.Client;
using Google.Documents;

using Nocs.Helpers;
using Nocs.Properties;
using Nocs.Models;
using SIinformer.Utils;


namespace Nocs
{
    /// <summary>
    /// Handles the communication with Google Docs through Google Data APIs.
    /// </summary>
    public static class NocsService
    {
        // all documents will be edited as html
        private const string DocumentContentType = "text/plain";//"application/atom+xml; charset=UTF-8; type=entry";//"text/html"; //application/zip

        // services-related properties
        private static DocumentsService _documentService;
        private static RequestSettings _settings;

        // properties for authentication
        public static string Username { get; set; }
        public static string Password { get; set; }
        public static bool AccountChanged { get; set; }

        public static bool Working { get; private set; }
        public static Dictionary<string, Document> AllDocuments { get; private set; }
        public static Dictionary<string, Document> AllFolders { get; private set; }
        private static Feed<Document> AllEntriesFeed { get; set; }

        // locks and counters for handling known GData-server issues
        private static readonly object GetContentLock = new object();
        private static int _getContentAttempts;

        private static readonly object RenameEntryLock = new object();
        private static int _renameEntryAttempts;

        private static readonly object DeleteEntryLock = new object();
        private static int _deleteEntryAttempts;

        public static readonly string SInformerFolder = "SInformer bookmarks";

        /// <summary>
        /// Fetches/updates all DocumentEntries in a Dictionary and wraps them in a Noc class.
        /// </summary>
        /// <returns>Dictionary of ResourceId's and Noc objects.</returns>
        public static void UpdateAllEntries()
        {
            if (AllDocuments == null)
            {
                AllDocuments = new Dictionary<string, Document>();
            }

            if (AllFolders == null)
            {
                AllFolders = new Dictionary<string, Document>();
            }

            // let's first make sure the user is authenticated
            if (_settings == null)
            {
                //throw new Exception("User hasn't been authenticated - internet down?");
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Пользователь не был авторизован. Упал инет?", true, true);
                return;
            }

            try
            {
                var request = new DocumentsRequest(_settings)
                {
                    Service = { ProtocolMajor = 3 }
                    //BaseUri = DocumentsListQuery.documentsAclUri
                };
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();

                // we'll fetch all entries
                AllEntriesFeed = request.GetEverything();

                // if we've already retrieved items, let's clear the dictionaries before updating them
                if (AllDocuments.Count > 0) { AllDocuments.Clear(); }
                if (AllFolders.Count > 0) { AllFolders.Clear(); }

                foreach (var entry in AllEntriesFeed.Entries)
                {
                    // let's only add documents and folders
                    if (entry.Type == Document.DocumentType.Document)
                    {
                        AllDocuments.Add(entry.ResourceId, entry);
                    }
                    else if (entry.Type == Document.DocumentType.Folder)
                    {
                        AllFolders.Add(entry.ResourceId, entry);
                    }
                }
            }
            catch (GDataNotModifiedException)
            {
                // since doclist updates timestamps on feeds based on access,
                // etags are useless here and we shouldn't get here
                return;
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't fetch all entries - internet down?");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удается получить все данные с Google. Инет упал?", true, true);
                    return;
                }

                Trace.WriteLine(string.Format("\n{0} - NocsService: couldn't fetch all entries: {1}\n", DateTime.Now, error));
                //throw new GDataRequestException(Tools.TrimErrorMessage(error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удается получить все данные с Google. Инет упал? " + Tools.TrimErrorMessage(error), true, true);
                return;
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                Trace.WriteLine(string.Format("\n{0} - NocsService: couldn't fetch all entries: {1}\n", DateTime.Now, error));
                //throw new Exception(error);
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удается получить все данные с Google. Инет упал? " + error, true, true);                
                return;
            }
        }


        /// <summary>
        /// Creates a new document in Google Docs.
        /// </summary>
        /// <param name="folderId">An entry id for any given folder in which the new document is to be saved.</param>
        /// <param name="title">Title for the new document.</param>
        /// <param name="content">HTML content for the new document.</param>
        /// <param name="createDefaultDirectory">
        /// true = create a default directory ('Nocs')
        /// fales = don't create a default directory
        /// </param>
        /// <returns>A newly created Document.</returns>
        public static Document CreateNewDocument(string folderId, string title, string content, bool createDefaultDirectory)
        {
            DocumentEntry newEntry;
            Document newDocument;
            content = Tools.EncodeTo64(content); //Tools.FormatEditorContentToHtml(title, content);

            try
            {
                var request = new DocumentsRequest(_settings);
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();

                // we'll first create a default 'Nocs'-folder if one isn't already created
                if (createDefaultDirectory)
                {
                    var defaultFolder = new Document
                    {
                        Type = Document.DocumentType.Folder,
                        Title = SInformerFolder
                    };
                    defaultFolder = request.CreateDocument(defaultFolder);

                    // if we created our default directory, let's add it to our folder dictionary
                    if (defaultFolder != null)
                    {
                        AllFolders.Add(defaultFolder.ResourceId, defaultFolder);
                        folderId = defaultFolder.ResourceId;
                    }
                }

                SetupService(null, null, 3, null, null);
                var textStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                
                // we might be creating this document inside a particular folder
                var postUri = !string.IsNullOrEmpty(folderId) ?
                                new Uri(string.Format(DocumentsListQuery.foldersUriTemplate,folderId))
                              : new Uri(DocumentsListQuery.documentsBaseUri);

                newEntry = _documentService.Insert(postUri, textStream, DocumentContentType, title) as DocumentEntry;
            }
            catch (GDataRequestException exRequest)
            {
                var error = !string.IsNullOrEmpty(exRequest.ResponseString) ? exRequest.ResponseString : exRequest.Message;
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't create document - internet down?");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Google: Не удается создать документ. Инет упал? " + exRequest.Message, true, true);                    
                    return null;
                }


                // we'll also check for InvalidEntryException: Could not convert document
                // - assuming it's a known problem in GData API related to sharing, we will just ignore it
                if (error.ToLowerInvariant().Contains("could not convert document"))
                {
                    Debug.WriteLine(string.Format("Couldn't convert document while creating a document: {0}", title));
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Google: при создании не удается сконвертировать документ: {0}", title), true, true);
                    //return null;
                }
                
                Trace.WriteLine(string.Format("{0} - Couldn't create a new document: {1} - {2}", DateTime.Now, title, error));
                //throw new GDataRequestException(string.Format("Couldn't create a new document: {0} - {1}", title, Tools.TrimErrorMessage(error)));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать документ: {0} - {1}", title, Tools.TrimErrorMessage(error)), true, true);
                return null;

            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't create a new document: " + error);
                //throw new Exception(string.Format("Couldn't create document: {0} - {1}", title, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать документ: {0} - {1}", title, Tools.TrimErrorMessage(error)), true, true);
                return null;
            }

            // let's create a new Document
            if (newEntry != null)
            {
                newEntry.IsDraft = false;

                newDocument = new Document
                {
                    AtomEntry = newEntry,
                    Title = title,
                    Content = Tools.ParseContent(content)
                };                
                // let's add the new document to our document dictionary and return it
                AllDocuments.Add(newDocument.ResourceId, newDocument);
                return newDocument;
            }

            // we should never get here
            //throw new Exception((string.Format("Couldn't create document: {0} - internet down?", title)));
            SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать документ: Инет упал?"), true, true);
            return null;

        }


        public static string GetFolderId(string FolderName)
        {
            string retValue = "";
            foreach (var folder in NocsService.AllFolders.Values)
            {
                if (folder.Title==FolderName)
                {
                    retValue = folder.ResourceId;
                    break;
                    ;
                }
            }
            return retValue;
        }

        /// <summary>
        /// Creates a new folder in Google Docs.
        /// </summary>
        /// <param name="folderName">Name for the new folder.</param>
        public static void CreateNewFolder(string folderName)
        {
            try
            {
                var request = new DocumentsRequest(_settings);
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();

                var newFolder = new Document
                {
                    Type = Document.DocumentType.Folder,
                    Title = folderName
                };
                newFolder = request.CreateDocument(newFolder);

                // let's add the new directory to our folder dictionary
                if (newFolder != null)
                {
                    AllFolders.Add(newFolder.ResourceId, newFolder);
                }
            }
            catch (GDataRequestException exRequest)
            {
                var error = !string.IsNullOrEmpty(exRequest.ResponseString) ? exRequest.ResponseString : exRequest.Message;
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't create folder - internet down?");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать папку - Упал инет? "), true, true);
                    return;

                }

                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't create folder: " + error);
                //throw new GDataRequestException(string.Format("Couldn't create folder: {0} - {1}", folderName, Tools.TrimErrorMessage(error)));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать папку {0} - {1} ", folderName, Tools.TrimErrorMessage(error)), true, true);
                return;
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't create folder: " + error);
                //throw new Exception(string.Format("Couldn't create folder: {0} - {1}", folderName, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается создать папку {0} - {1} ", folderName, Tools.TrimErrorMessage(error)), true, true);                
                return;
            }
        }


        /// <summary>
        /// Fetches a potential updated Document for syncing purposes.
        /// If the document hasn't been updated in Google Docs, will return null.
        /// </summary>
        /// <param name="document">Document to be updated.</param>
        /// <returns>
        /// Document if an updated entry is found.
        /// null if no updated item is found.
        /// </returns>
        public static Document GetUpdatedDocument(Document document)
        {
            var originalEtag = document.DocumentEntry.Etag;
            Document refreshed;

            try
            {
                var request = new DocumentsRequest(_settings);
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();
                refreshed = request.Retrieve(document);
            }
            catch (GDataNotModifiedException)
            {
                // if response is 304 (NotModified) -> document hasn't changed
                Debug.WriteLine(string.Format("Document hasn't changed: {0} - {1} -> {2}", document.Title, originalEtag, document.ETag));
                return null;
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);

                // if we encounter a ResourceNotFoundException, there's no need to add an error job,
                // the AutoFetchAll-worker will handle removing the tab
                if (error.ToLowerInvariant().Contains("resourcenotfoundexception"))
                {
                    return null;
                }

                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't sync document - internet down?");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удается синхронизировать документ - Упал инет? ", true, true);
                    return null;

                }

                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't check if doc updated: " + error);
                //throw new GDataRequestException(string.Format("Couldn't check if document was updated: {0} - {1}",
                  //  document.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается проверить, изменился ли документ  {0} - {1}",document.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                return null;

            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't check if doc updated: " + ex.Message);
                //throw new GDataRequestException(string.Format("Couldn't check if document was updated: {0} - {1}",
                //    document.DocumentEntry.Title.Text, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается проверить, изменился ли документ  {0} - {1}", document.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                return null;
            }

            if (refreshed != null && refreshed.ETag != originalEtag)
            {
                Debug.WriteLine(string.Format("Found updated document: {0} - {1} -> {2}", refreshed.Title, originalEtag, refreshed.ETag));

                // let's update our internal dictionary
                if (refreshed.Type == Document.DocumentType.Folder)
                {
                    AllFolders[document.ResourceId] = refreshed;
                }
                else
                {
                    AllDocuments[document.ResourceId] = refreshed;
                }
                return refreshed;
            }

            // if we get here, the document hasn't updated
            return null;
        }


        /// <summary>
        /// Downloads the contents of a single document.
        /// </summary>
        /// <param name="doc">A Document object whose content is to be downloaded.</param>
        /// <returns>Content of the document as a string.</returns>
        public static Document GetDocumentContent(Document doc)
        {
            DocumentsRequest request;
            StreamReader reader;
            string html;

            try
            {
                request = new DocumentsRequest(_settings);
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();

                var stream = request.Download(doc, Document.DownloadType.html);
                reader = new StreamReader(stream);

                // let's read the stream to end to retrieve the entire html
                html = reader.ReadToEnd();
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't download content - internet down?");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается скачать контент - Упал инет? "), true, true);
                    return null;
                }

                var knownIssues = ConsecutiveKnownIssuesOccurred(GetContentLock, "GetDocumentContent", doc, error, ref _getContentAttempts, 1);
                if (knownIssues == KnownIssuesResult.Retry)
                {
                    doc = GetDocumentContent(doc);
                    doc.Summary = null;
                    _getContentAttempts = 0;
                    return doc;
                }
                if (knownIssues == KnownIssuesResult.LimitReached)
                {
                    return doc;
                }


                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't download content: " + error);
                //throw new GDataRequestException(string.Format("Couldn't download document: {0} - {1}", doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается скачать документ {0} - {1}" , doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                return null;

            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                //throw new Exception(string.Format("Couldn't download document: {0} - {1}", doc.DocumentEntry.Title.Text, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удается скачать документ {0} - {1}", doc.DocumentEntry.Title.Text, error), true, true);
                return null;

            }

            // let's first parse the Google Docs -specific html content
            var match = Tools.GetMatchForDocumentContent(html);
            if (match.Success)
            {
                // body found, let's now tweak the content before returning it
                var content = match.Groups[1].Value;
                doc.Content = Tools.ParseContent(content);
                return doc;
            }

            // if we get here, something went wrong - document content doesn't match
            //throw new Exception("Invalid html content for document: " + doc.DocumentEntry.Title.Text);
            SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Некорректный html-контент для документа: " + doc.DocumentEntry.Title.Text, true, true);
            return null;

        }


        /// <summary>
        /// Updates a given DocumentEntry with new content.
        /// </summary>
        /// <param name="doc">Document object to be updated.</param>
        public static Document SaveDocument(Document doc)
        {
            Working = true;

            var entryToUpdate = doc.DocumentEntry;
            var updatedContent = Tools.EncodeTo64(doc.Content); //Tools.FormatEditorContentToHtml(doc.Title, doc.Content);

            try
            {
                // the media feed is used to update a document's content body:
                // http://docs.google.com/feeds/default/media/ResourceId
                var mediaUri = new Uri(string.Format(DocumentsListQuery.mediaUriTemplate, doc.ResourceId));
                var textStream = new MemoryStream(Encoding.UTF8.GetBytes(updatedContent));

                var request = new DocumentsRequest(_settings);
                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.Proxy = GetProxy();
                
                // let's set ETag because we're making an update
                reqFactory.CustomHeaders.Add(string.Format("{0}: {1}", GDataRequestFactory.IfMatch, entryToUpdate.Etag));

                var oldEtag = entryToUpdate.Etag;
                doc.AtomEntry = request.Service.Update(mediaUri, textStream, DocumentContentType, entryToUpdate.Title.Text) as DocumentEntry;
                Debug.WriteLine(string.Format("ETag changed while saving {0}: {1} -> {2}", entryToUpdate.Title.Text, oldEtag, doc.ETag));
            }
            catch (GDataRequestException exRequest)
            {
                var response = exRequest.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed &&
                    exRequest.ResponseString.ToLowerInvariant().Contains("etagsmismatch"))
                {
                    // ETags don't match -> this document has been updated outside this instance of Nocs
                    // therefore instead of saving this file we will just wait for an update
                    // TODO: implement some faster way for updating?
                    Debug.WriteLine(string.Format("ETags don't match for {0} - document updated outside Nocs - returning an unchanged document", doc.ETag));
                    doc.Summary = "unchanged";
                    return doc;
                }

                var error = GetErrorMessage(exRequest);
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't download content, connection timed out");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удалось скачать контент. Время вышло." , true, true);
                    return null;

                }

                // we'll also check for InvalidEntryException: Could not convert document
                // - assuming it's a known problem in GData API related to sharing, we will update the document and return it
                if (error.ToLowerInvariant().Contains("could not convert document"))
                {
                    Debug.WriteLine(string.Format("Couldn't convert document: {0} -> updating it..", doc.DocumentEntry.Title.Text));
                    var updated = GetUpdatedDocument(doc);
                    if (updated != null)
                    {
                        doc = updated;
                    }
                }
                else
                {
                    Trace.WriteLine(string.Format("{0} - Couldn't save document: {1} - {2}", DateTime.Now, doc.DocumentEntry.Title.Text, error));
                    //throw new GDataRequestException(string.Format("Couldn't save document: {0} - {1}", doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалось сохранить документ: {0} - {1}", doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                    return null;

                }
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                //throw new Exception(string.Format("Couldn't save document: {0} - {1}", doc.DocumentEntry.Title.Text, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалось сохранить документ: {0} - {1}", doc.DocumentEntry.Title.Text, error), true, true);
                return null;
            }
            finally
            {
                Working = false;
            }

            // let's update internal directory to avoid ETag-mismatch
            AllDocuments[doc.ResourceId] = doc;
            return doc;
        }


        /// <summary>
        /// Renames an entry (Document or Folder) with a given id.
        /// </summary>
        /// <param name="entryId">ResourceId of the entry to be renamed.</param>
        /// <param name="newTitle">New title for the entry to be renamed.</param>
        /// <param name="entryType">Type of the entry (Document or Folder).</param>
        public static void RenameEntry(string entryId, string newTitle, Document.DocumentType entryType)
        {
            Working = true;

            // we will only rename documents and folders
            if (entryType != Document.DocumentType.Document && entryType != Document.DocumentType.Folder)
            {
                //throw new ArgumentException(string.Format("Invalid entryType ({0})", entryType));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Неправильный entryType ({0})", entryType), true, true);
                return ;
            }

            var doc = entryType == Document.DocumentType.Document ? AllDocuments[entryId] : AllFolders[entryId];
            var entryToUpdate = doc.DocumentEntry;
            var documentContent = doc.Content;

            try
            {
                _documentService.ProtocolMajor = 3;
                entryToUpdate.Title.Text = newTitle;
                // AtomEntry shouldn't have any text in its content field, so let's reset it for now
                entryToUpdate.Content.Content = string.Empty;
                entryToUpdate = entryToUpdate.Update() as DocumentEntry;
            }
            catch (GDataRequestException exRequest)
            {
                var response = exRequest.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed &&
                    exRequest.ResponseString.ToLowerInvariant().Contains("etagsmismatch"))
                {
                    // ETags don't match -> this document has been updated outside this instance of Nocs
                    // or it was just saved -> let's update it and try renaming again
                    Debug.WriteLine(string.Format("ETags don't match, couldn't find {0} - updating it and trying rename again..", doc.ETag));
                    doc = GetUpdatedDocument(doc);
                    RenameEntry(doc.ResourceId, newTitle, doc.Type);
                }
                else
                {
                    var error = GetErrorMessage(exRequest);
                    if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                    {
                        //throw new GDataRequestException("Couldn't rename entry, connection timed out");
                        SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удается переименовать объект. Время вышло.", true, true);
                        return;

                    }

                    var knownIssues = ConsecutiveKnownIssuesOccurred(RenameEntryLock, "RenameEntry", doc, error, ref _renameEntryAttempts, 1);
                    if (knownIssues == KnownIssuesResult.Retry)
                    {
                        doc = GetUpdatedDocument(doc);
                        RenameEntry(doc.ResourceId, newTitle, doc.Type);
                        doc.Summary = null;
                        _renameEntryAttempts = 0;
                        return;
                    }
                    else if (knownIssues == KnownIssuesResult.LimitReached)
                    {
                        return;
                    }

                    Trace.WriteLine(string.Format("Couldn't rename {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                    //throw new GDataRequestException(string.Format("Couldn't rename {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалост переименовать {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                    return;

                }
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                //throw new Exception(string.Format("Couldn't rename {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалост переименовать {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, error), true, true);
                return;
            }
            finally
            {
                Working = false;
            }

            // let's update the document (mostly for Etag) and update document dictionary
            if (entryToUpdate != null)
            {
                entryToUpdate.IsDraft = false;
                doc.AtomEntry = entryToUpdate;
                doc.Content = documentContent;
                if (entryType == Document.DocumentType.Document)
                {
                    AllDocuments[entryId] = doc;
                }
                else
                {
                    AllFolders[entryId] = doc;
                }
            }
        }


        /// <summary>
        /// Deletes an entry with a given id.
        /// </summary>
        /// <param name="entryId">ResourceId of the entry to be deleted.</param>
        /// <param name="entryType">Type of the entry (Document or Folder).</param>
        public static void DeleteEntry(string entryId, Document.DocumentType entryType)
        {
            Working = true;

            // we will only rename documents and folders
            if (entryType != Document.DocumentType.Document && entryType != Document.DocumentType.Folder)
            {
                //throw new ArgumentException(string.Format("Invalid entryType ({0})", entryType));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Некорректный entryType ({0})", entryType), true, true);
                return;

            }

            var doc = entryType == Document.DocumentType.Document ? AllDocuments[entryId] : AllFolders[entryId];
            var entryToDelete = doc.DocumentEntry;

            try
            {
                _documentService.ProtocolMajor = 3;
                entryToDelete.Delete();
            }
            catch (GDataRequestException exRequest)
            {
                var response = exRequest.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed &&
                    exRequest.ResponseString.ToLowerInvariant().Contains("etagsmismatch"))
                {
                    // ETags don't match -> this document has been updated outside this instance of Nocs
                    // or it was just saved -> let's update it and try renaming again
                    Debug.WriteLine(string.Format("ETags don't match, couldn't find {0} - updating it and trying delete again..", doc.ETag));
                    doc = GetUpdatedDocument(doc);
                    DeleteEntry(doc.ResourceId, doc.Type);
                }
                else
                {
                    var error = GetErrorMessage(exRequest);
                    if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                    {
                        //throw new GDataRequestException("Couldn't delete entry, connection timed out");
                        SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Не удалось удалить объект. Время вышло.", true, true);
                        return;

                    }

                    var knownIssues = ConsecutiveKnownIssuesOccurred(DeleteEntryLock, "DeleteEntry", doc, error, ref _deleteEntryAttempts, 1);
                    if (knownIssues == KnownIssuesResult.Retry)
                    {
                        doc = GetUpdatedDocument(doc);
                        DeleteEntry(doc.ResourceId, doc.Type);
                        doc.Summary = null;
                        _deleteEntryAttempts = 0;
                        return;
                    }
                    else if (knownIssues == KnownIssuesResult.LimitReached)
                    {
                        return;
                    }

                    Trace.WriteLine(string.Format("Couldn't delete {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                    //throw new GDataRequestException(string.Format("Couldn't delete {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалось удалить {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)), true, true);
                    return;

                }
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                //throw new Exception(string.Format("Couldn't delete entry: {0} - {1}", doc.DocumentEntry.Title.Text, error));
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add(string.Format("Не удалось удалить {0}: {1} - {2}", entryType, doc.DocumentEntry.Title.Text, error), true, true);
                return;
            }
            finally
            {
                Working = false;
            }

            // let's update the appropriate dictionary
            if (entryType == Document.DocumentType.Document)
            {
                AllDocuments.Remove(entryId);
            }
            else
            {
                AllFolders.Remove(entryId);
            }
        }


        /// <summary>
        /// Moves an entry (document or folder) into a folder.
        /// </summary>
        /// <param name="folder">A Document object representing the folder where the given entry will be moved.</param>
        /// <param name="entryToBeMoved">A Document object representing the entry (document or folder) that will be moved.</param>
        public static void MoveEntry(Document folder, Document entryToBeMoved)
        {
            DocumentsRequest request;
            Document movedEntry;

            try
            {
                request = new DocumentsRequest(_settings)
                {
                    Service = { ProtocolMajor = 3 }
                };

                var reqFactory = (GDataRequestFactory)request.Service.RequestFactory;
                reqFactory.CustomHeaders.Clear();
                reqFactory.Proxy = GetProxy();

                movedEntry = request.MoveDocumentTo(folder, entryToBeMoved);
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    throw new GDataRequestException("Couldn't move entry - internet down?");
                }

                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't move entry: " + error);
                throw new GDataRequestException(string.Format("Couldn't move entry: {0} - {1}", entryToBeMoved.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                throw new Exception(string.Format("Couldn't move entry: {0} - {1}", entryToBeMoved.DocumentEntry.Title.Text, error));
            }

            // let's update dictionaries
            if (movedEntry.Type == Document.DocumentType.Folder)
            {
                AllFolders[entryToBeMoved.ResourceId] = movedEntry;
            }
            else
            {
                AllDocuments[entryToBeMoved.ResourceId] = movedEntry;
            }
        }


        /// <summary>
        /// Removes an entry (document or folder) from all folders.
        /// </summary>
        /// <param name="entryToBeRemoved">A Document object representing the entry (document or folder) that will be removed from all folders.</param>
        public static void RemoveEntryFromAllFolders(Document entryToBeRemoved)
        {
            Working = true;

            Document updatedEntry;

            try
            {
                if (entryToBeRemoved.ParentFolders.Count == 0)
                {
                    Working = false;
                    return;
                }

                _documentService.ProtocolMajor = 3;

                // we will loop through all of the entry's parent folders
                foreach (var folder in entryToBeRemoved.ParentFolders)
                {
                    // To move a document or folder out of a folder, we need to send a HTTP DELETE request
                    // to the destination folder's edit link. The edit link will have a folder id and a document id,
                    // represented by folder_id and document_id respectively in the example below.
                    // DELETE /feeds/default/private/full/folder%3A<folder_id>/contents/document%3A<document_id>

                    var uri = new Uri(string.Format("{0}/contents/{1}", folder, entryToBeRemoved.ResourceId));
                    _documentService.Delete(uri, "*");
                }

                // in order to "empty" Document.ParentFolders, we'll update the whole document
                updatedEntry = GetUpdatedDocument(entryToBeRemoved);
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);
                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    throw new GDataRequestException("Couldn't remove entry from all folders - internet down?");
                }

                Trace.WriteLine(DateTime.Now + " - NocsService - couldn't remove entry from all folders: " + error);
                throw new GDataRequestException(string.Format("Couldn't remove entry from all folders: {0} - {1}", entryToBeRemoved.DocumentEntry.Title.Text, Tools.TrimErrorMessage(error)));
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                throw new Exception(string.Format("Couldn't remove entry from all folders: {0} - {1}", entryToBeRemoved.DocumentEntry.Title.Text, error));
            }
            finally
            {
                Working = false;
            }

            // let's update dictionaries
            if (updatedEntry != null)
            {
                if (updatedEntry.Type == Document.DocumentType.Folder)
                {
                    AllFolders[entryToBeRemoved.ResourceId] = updatedEntry;
                }
                else
                {
                    AllDocuments[entryToBeRemoved.ResourceId] = updatedEntry;
                }
            }
        }


        /// <summary>
        /// Will authenticate the user and setup the DocumentService used throughout NocsService.
        /// </summary>
        public static void AuthenticateUser(string userName, string password, bool forceAuthToken)
        {
            Working = true;

            try
            {
                if (forceAuthToken)
                {
                    _settings = null;
                }
                SetupService(userName, password, 3, null, null);

                _documentService.setUserCredentials(userName, password);
                _documentService.QueryClientLoginToken();
            }
            catch (InvalidCredentialsException)
            {
                Trace.WriteLine(DateTime.Now + " - NocsService: Invalid credentials");
                _documentService = null;
                //throw new Exception("Invalid credentials.");
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Google: Неправильные имя или пароль для входа.", true, true);                
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            catch (CaptchaRequiredException)
            {
                Trace.WriteLine(DateTime.Now + " - NocsService: CaptchaRequiredException");
                _documentService = null;
                //throw new Exception("Invalid credentials.");
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Google: Слишком часто проводится аутентификация.", true, true);
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            catch (AuthenticationException)
            {
                Trace.WriteLine(DateTime.Now + " - NocsService: AuthenticationException");
                _documentService = null;
                //throw new Exception("Invalid credentials.");
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Google: Неправильные имя или пароль для входа.", true, true);
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            catch (GDataRequestException exRequest)
            {
                var error = GetErrorMessage(exRequest);

                if (exRequest.ResponseString == null && error.ToLowerInvariant().Contains("execution of request failed"))
                {
                    //throw new GDataRequestException("Couldn't authenticate user, connection timed out");
                    SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Неудачная аутентификация в Google. Исчерпание времени ожидания", true, true);
                    TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                    return;
                }

                Trace.WriteLine(DateTime.Now + " - NocsService: " + error);
                _documentService = null;
                //throw new Exception(error);
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Ошибка входа в Google: " + error, true, true);
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            catch (Exception ex)
            {
                var error = GetErrorMessage(ex);
                Trace.WriteLine(DateTime.Now + " - NocsService: " + error);
                _documentService = null;
                //throw new Exception(error);
                SIinformer.Window.MainWindow.MainForm.GetLogger().Add("Ошибка входа в Google: " + error, true, true);
                TimerBasedAuthorsSaver.GetInstance().StopGoogleSync();
                return;
            }
            finally
            {
                Working = false;
            }
        }


        /// <summary>
        /// Cleans up the error message according to its type.
        /// </summary>
        private static string GetErrorMessage(Exception exception)
        {
            var error = string.Empty;

            if (exception.InnerException != null &&
                (exception.InnerException.Message.Contains("Invalid URI: The hostname could not be parsed") ||
                 exception.InnerException.Message.Contains("Invalid URI: A port was expected")))
            {
                error = "Invalid proxy settings.";
            }
            else if ((exception.Message.Contains("Invalid URI: The hostname could not be parsed") ||
                      exception.Message.Contains("Invalid URI: A port was expected")))
            {
                error = "Invalid proxy settings.";
            }
            else if (exception.InnerException != null && exception.InnerException.Message.ToLowerInvariant().Contains("proxy"))
            {
                error = exception.InnerException.Message;
            }
            else
            {
                if (exception.GetType() == typeof(GDataRequestException))
                {
                    var gDataRequestException = (GDataRequestException)exception;
                    error = !string.IsNullOrEmpty(gDataRequestException.ResponseString) ? gDataRequestException.ResponseString : exception.Message;
                }
            }

            return error;
        }


        /// <summary>
        /// Will check for known GData server issues - they happen occasionally.
        /// Normally we shouldn't get here, but if we do, we'll retry whatever action we were
        /// attempting once again, after which we'll just tag the document summary and return it,
        /// so it will be caught in Main.Workers and its tab will simply be removed.
        /// </summary>
        /// <param name="knownIssueLock">An object representing the lock to be used.</param>
        /// <param name="source">Method source for debugging purposes.</param>
        /// <param name="doc">Document which summary will be updated based on potential errors.</param>
        /// <param name="error">Error that occurred.</param>
        /// <param name="attemptCount">Reference to the integer representing the current count for this calling type.</param>
        /// <param name="retryLimit">Maximum number of retries.</param>
        /// <returns>
        /// true, if known issues found in given error for given no. of consecutive times (retryLimit)
        /// 
        /// false, if no known issues found or if known issues found for the nth time (retryLimit)
        /// </returns>
        private static KnownIssuesResult ConsecutiveKnownIssuesOccurred(object knownIssueLock, string source, Document doc, string error, ref int attemptCount, int retryLimit)
        {
            lock (knownIssueLock)
            {
                var knownIssueOccurred = false;

                if (error.ToLowerInvariant().Contains("not found"))
                {
                    Debug.WriteLine(source + ": document not found: " + doc.Title);
                    doc.Summary = "document not found";
                    knownIssueOccurred = true;
                }

                if (error.ToLowerInvariant().Contains("file is corrupt, or an unknown format"))
                {
                    Debug.WriteLine(source + ": file is corrupt, or an unknown format: " + doc.Title);
                    doc.Summary = "file is corrupt, or an unknown format";
                    knownIssueOccurred = true;
                }

                if (knownIssueOccurred && attemptCount < retryLimit)
                {
                    Debug.WriteLine(source + ": retrying retrieving document content");
                    attemptCount++;
                    return KnownIssuesResult.Retry;
                }
                
                if (knownIssueOccurred && attemptCount >= retryLimit)
                {
                    Debug.WriteLine(source + ": known issues found but retryLimit (" + retryLimit + ") reached");
                    attemptCount = 0;
                    return KnownIssuesResult.LimitReached;
                }
                
                attemptCount = 0;
                return KnownIssuesResult.NoneFound;
            }
        }


        /// <summary>
        /// Will clear the headers before any given request against GData API, and insert potential ETag's.
        /// Will also setup the proxy and services if they aren't initialized yet.
        /// </summary>
        /// <param name="userName">Username for Google services.</param>
        /// <param name="password">Password for Google services.</param>
        /// <param name="protocolMajor">ProtocolMajor to be used.</param>
        /// <param name="eTagType">If-Match / If-None-Match etc.</param>
        /// <param name="eTag">The actual ETag to be checked.</param>
        private static void SetupService(string userName, string password, int protocolMajor, string eTagType, string eTag)
        {
            // let's create our service if it's not already created
            if (_documentService == null)
            {
                _documentService = new DocumentsService("Nocs");
            }

            _documentService.ProtocolMajor = protocolMajor;
            var reqFactory = (GDataGAuthRequestFactory)_documentService.RequestFactory;
            reqFactory.ProtocolMajor = protocolMajor;
            reqFactory.KeepAlive = false;

            // let's first clear all customHeaders regardless
            reqFactory.CustomHeaders.Clear();
            if (!string.IsNullOrEmpty(eTagType) && !string.IsNullOrEmpty(eTag))
            {
                reqFactory.CustomHeaders.Add(string.Format("{0}: {1}", eTagType, eTag));
            }

            // let's also set up the request settings for API v3.0 requests
            if (_settings == null)
            {
                _settings = new RequestSettings("Nocs", userName, password)
                {
                    AutoPaging = true
                };
            }

            // finally let's setup a potential proxy
            reqFactory.Proxy = GetProxy();
        }


        public static bool UseProxy = false;
        public static bool AutomaticProxyDetection = true;
        public static string ProxyHost = "";
        public static string ProxyPort = "";
        public static string ProxyUsername = "";
        public static string ProxyPassword = "";
        
        /// <summary>
        /// Gets a WebProxy object based on user settings.
        /// </summary>
        /// <returns>
        /// a WebProxy object if user has proxy enabled
        /// an 'empty' WebProxy if proxy is disabled
        /// </returns>
        private static IWebProxy GetProxy()
        {
            // no need to continue if proxy isn't enabled
            if (!UseProxy)
            {
                return new WebProxy();
            }

            if (AutomaticProxyDetection)
            {
                return WebRequest.DefaultWebProxy;
            }

            var proxyHost = ProxyHost;
            var proxyPort = ProxyPort;
            var proxy = new WebProxy(string.Format("http://{0}:{1}/", proxyHost, proxyPort), true);

            // let's check for credentials
            if (!string.IsNullOrEmpty(ProxyUsername) &&
                !string.IsNullOrEmpty(ProxyPassword))
            {
                var proxyUsername = ProxyUsername;
                var proxyPassword = Tools.Decrypt(ProxyPassword);
                proxy.UseDefaultCredentials = false;
                proxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword);
            }
            else
            {
                proxy.Credentials = CredentialCache.DefaultCredentials;
                proxy.UseDefaultCredentials = true;
            }

            return proxy;
        }


        /// <summary>
        /// If credentials are missing, user hasn't been properly authenticated.
        /// </summary>
        /// <returns>true if user is authenticated, false if not</returns>
        public static bool UserIsAuthenticated()
        {
            return !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
        }
    }
}
