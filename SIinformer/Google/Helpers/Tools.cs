using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows.Forms;
using System.Linq;

using Google.Documents;
using Nocs.Properties;


namespace Nocs.Helpers
{
    /// <summary>
    /// A class for generic tools/snippets.
    /// </summary>
    public static class Tools
    {
        // constant helpers for HTML-parsing/encoding and encryption
        private const string LineBreakConstant = "=LBxR8p062zdf9a-";
        private const string TabConstant = "=TABzPGUxSzdf7y-";
        private const string SpaceConstant = "=DSxR8p07zDf8a-";
        private const string ApplicationEntropy = "asW68ER1klj32";

        private static string DefaultSaveFolder = "SInformer";
        /// <summary>
        /// Gets a regular expression Match for a Google Docs document content.
        /// </summary>
        /// <param name="html">HTML to be matched.</param>
        /// <returns>A Match object representing the content.</returns>
        public static Match GetMatchForDocumentContent(string html)
        {
            // let's first handle the line-breaks
            html = Regex.Replace(html, @"<div>\r\n\s*|\r\n</div>", string.Empty);
            html = Regex.Replace(html, @"\r\n", LineBreakConstant);

            // let's then remove the non-essentials
            html = Regex.Replace(html, @"\n|\r|\s\s+|<!--\[if.+?\]>|<\?XML:NAMESPACE.+?>|<!\[endif\]-->", string.Empty);

            // finally, let's extract the actual body and return a match for it
            return Regex.Match(html, @"<body.*?>(.*?)</body>", RegexOptions.Multiline);
        }


        /// <summary>
        /// Will modify the content of a Docs document to make it suitable for our TextEditor.
        /// </summary>
        /// <param name="content">HTML content to be modified</param>
        /// <returns>Modified content</returns>
        public static string ParseContent(string content)
        {
            // let's first find the line-breaks
            content = Regex.Replace(content, LineBreakConstant, "\n");

            // next let's remove double-breaks, the title and all other html tags
            content = Regex.Replace(content, @"\n\n|<title>.*?</title>|<[^>]*>", string.Empty);

            // finally let's decode the html content and return it
            return HttpUtility.HtmlDecode(content);
        }


        /// <summary>
        /// Will format the text-editor content into HTML for maintaining the outlook of a document as well as possible.
        /// </summary>
        /// <param name="title">Title for the document.</param>
        /// <param name="content">Content for the document</param>
        /// <returns>HTML representing the document.</returns>
        public static string FormatEditorContentToHtml(string title, string content)
        {
            // let's handle linebreaks, spaces and tabs
            content = content.Replace("\n ", LineBreakConstant + SpaceConstant);
            content = content.Replace("\n", LineBreakConstant);
            content = content.Replace("\t", TabConstant);
            content = content.Replace("  ", SpaceConstant + SpaceConstant);

            // let's then call the normal HtmlEncode
            var chars = HttpUtility.HtmlEncode(content).ToCharArray();
            var html = new StringBuilder();
            foreach (var c in chars)
            {
                if (c > 127) // above normal ASCII
                    html.Append("&#" + (int)c + ";");
                else
                    html.Append(c);
            }

            // finally let's format all constants to html tags/entities and return the entire html
            //html = html.Replace(LineBreakConstant, "<br>");
            //html = html.Replace(TabConstant, "&nbsp;&nbsp;&nbsp;&nbsp;"); // a tab = 4 spaces
            //html = html.Replace(SpaceConstant, "&nbsp;");
            //return string.Format("<html><head><title>{0}</title><body>{1}</body></html>", title, html);
            return html.ToString();
        }

        static public string EncodeTo64(string toEncode)
        {

            byte[] toEncodeAsBytes
                  = System.Text.UTF8Encoding.UTF8.GetBytes(toEncode);
            string returnValue
                  = System.Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }
        static public string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes
                = System.Convert.FromBase64String(encodedData);
            string returnValue =
               System.Text.UTF8Encoding.UTF8.GetString(encodedDataAsBytes);
            return returnValue;

        }


        public static string TrimErrorMessage(string error)
        {
            return string.IsNullOrEmpty(error) ? string.Empty : Regex.Replace(HttpUtility.HtmlDecode(error), @"(<[^>]+>|\r\n|\r|\n|\t)", string.Empty).Trim();
        }


        /// <summary>
        /// Method for checking whether user is connected to the Internet.
        /// </summary>
        /// <returns>true if user is connected, false if not</returns>
        public static bool IsConnected()
        {
            try
            {
                if (IsInternetConnected() && GoogleReturnsOK())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

// ReSharper disable InconsistentNaming
        private const int ERROR_SUCCESS = 0;
// ReSharper restore InconsistentNaming
        private static bool IsInternetConnected()
        {
            return true; // не будем проверять системными средствами наличие интернета. А то бывают ситуации, когда на компе его нет, однако доступен прокси на другом компе, тогда синхронизация не запускается

            const long dwConnectionFlags = 0;
            if (!InternetGetConnectedState(dwConnectionFlags, 0))
                return false;

            if (InternetAttemptConnect(0) != ERROR_SUCCESS)
                return false;

            return true;
        }


        [DllImport("wininet.dll", SetLastError = true)]
        private static extern int InternetAttemptConnect(uint res);


        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetGetConnectedState(long flags, long reserved);


        private static bool GoogleReturnsOK()
        {
            HttpWebRequest req;
            HttpWebResponse resp;
            try
            {
                req = (HttpWebRequest)WebRequest.Create("http://www.google.com");
                resp = (HttpWebResponse)req.GetResponse();
                return resp.StatusCode.ToString().Equals("OK");
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Encrypt(string plainText)
        {
            try
            {
                var encodedPlaintext = Encoding.UTF8.GetBytes(plainText);
                var encodedEntropy = Encoding.UTF8.GetBytes(ApplicationEntropy);
                var cipherText = ProtectedData.Protect(encodedPlaintext, encodedEntropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(cipherText);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string Decrypt(string base64Ciphertext)
        {
            try
            {
                var ciphertext = Convert.FromBase64String(base64Ciphertext);
                var encodedEntropy = Encoding.UTF8.GetBytes(ApplicationEntropy);
                var encodedPlaintext = ProtectedData.Unprotect(ciphertext, encodedEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(encodedPlaintext);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void PopulateFoldersForComboBox(ComboBox cmbFolders, bool createDefaultFolderIfNecessary)
        {
            // let's populate the folder comboBox, while making sure we have a 'Nocs'-folder
            // let's first create a 'No folder'
            var noFolder = new Document
            {
                Title = "No folder",
                Id = "draft"
            };
            cmbFolders.Items.Add(noFolder);

            var defaultFolderFound = true;
            foreach (var folder in NocsService.AllFolders.Values.OrderBy(d => d.Title))
            {
                if (folder.Title == "Nocs")
                {
                    defaultFolderFound = true;
                    cmbFolders.Items.Insert(1, folder);
                }
                else
                {
                    cmbFolders.Items.Add(folder);
                }
            }
            if (NocsService.AllFolders.Count == 0)
            {
                defaultFolderFound = false;
            }

            // let's make sure there's a 'Nocs'-folder
            if (!defaultFolderFound && createDefaultFolderIfNecessary)
            {
                var folder = new Document
                {
                    Title = "Nocs",
                    Id = "draft"
                };
                cmbFolders.Items.Insert(1, folder);
            }

            // let's try to find a default saving folder
            var found = false;
            if (!string.IsNullOrEmpty(DefaultSaveFolder))
            {
                foreach (Document folder in cmbFolders.Items)
                {
                    if (folder.Id != "draft" && folder.ResourceId == DefaultSaveFolder)
                    {
                        cmbFolders.SelectedItem = folder;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                cmbFolders.SelectedIndex = 0;
            }
        }


        /// <summary>
        /// Merges the text editor content based on:
        /// - the content at the time of last sync between Google Docs (save or content download)
        /// - the current editor content
        /// - the content that's coming from Google Docs
        /// </summary>
        /// <param name="editorLastSync">Editor content at the time of last sync.</param>
        /// <param name="editorNow">Editor content now.</param>
        /// <param name="contentComingFromGoogleDocs">New content coming from Google Docs</param>
        /// <returns>Merged content.</returns>
        public static string MergeText(string editorLastSync, string editorNow, string contentComingFromGoogleDocs)
        {
            var dmp = new diff_match_patch
            {
                Match_Distance = 1000,
                Match_Threshold = 0.5f,
                Patch_DeleteThreshold = 0.5f
            };

            // let's create patches based on editorContent on last sync and the new text from Google Docs
            var patches = dmp.patch_make(editorLastSync, contentComingFromGoogleDocs);
            Debug.WriteLine("SyncContentUpdated > Patches: \t\t\t\t\t" + dmp.patch_toText(patches));

            // let's apply those patches to the current editor text
            var results = dmp.patch_apply(patches, editorNow);

            // and return results
            return results[0].ToString();
        }
    }
}