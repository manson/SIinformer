using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnightsWarriorAutoupdater;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;

namespace AutoUpdater.AutoUpdateHelper
{
    public class DownloadEngine
    {
        #region The private fields
        private bool isFinished = false;
        private List<DownloadFileInfo> downloadFileList = null;
        private List<DownloadFileInfo> allFileList = null;
        private ManualResetEvent evtDownload = null;
        private ManualResetEvent evtPerDonwload = null;
        private WebClient clientDownload = null;
        private DownloadingInfo downloadingInfo = null;
        private string backup_folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppBackup", string.Format("{0}_{1}", DateTime.Today.ToShortDateString().Replace(".", ""), DateTime.Now.ToShortTimeString().Replace(":", "")));
        private Action<bool> ExitAction = null;
        #endregion

        public DownloadEngine(List<DownloadFileInfo> downloadFileListTemp, DownloadingInfo DownloadingInfo, Action<bool> exitAction)
        {
            if (DownloadingInfo == null) return;
            ExitAction = exitAction;

            downloadingInfo = DownloadingInfo;
            this.downloadFileList = downloadFileListTemp;
            allFileList = new List<DownloadFileInfo>();
            foreach (DownloadFileInfo file in downloadFileListTemp)
            {
                allFileList.Add(file);
            }
            LetItGo();
        }

        #region The method and event
        public void StopDownloading()
        {
            if (!isFinished && DialogResult.No == MessageBox.Show(ConstFile.CANCELORNOT, ConstFile.MESSAGETITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Question))
            {               
                return;
            }
            else
            {
                if (clientDownload != null)
                    clientDownload.CancelAsync();

                evtDownload.Set();
                evtPerDonwload.Set();
            }
        }

        private void LetItGo()
        {
            evtDownload = new ManualResetEvent(true);
            evtDownload.Reset();
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.ProcDownload));
        }

        long total = 0;
        long nDownloadedTotal = 0;

        private void ProcDownload(object o)
        {
            string tempFolderPath = Path.Combine(CommonUnitity.SystemBinUrl, ConstFile.TEMPFOLDERNAME);
            if (!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }


            evtPerDonwload = new ManualResetEvent(false);

            foreach (DownloadFileInfo file in this.downloadFileList)
            {
                total += file.Size;
            }
            downloadingInfo.TotalFiles = this.downloadFileList.Count;
            downloadingInfo.MaxBytesTotal = total;
            try
            {
                while (!evtDownload.WaitOne(0, false))
                {
                    if (this.downloadFileList.Count == 0)
                        break;

                    DownloadFileInfo file = this.downloadFileList[0];


                    //Debug.WriteLine(String.Format("Start Download:{0}", file.FileName));

                    downloadingInfo.CurrentFile = file.FileName;
                    

                    //Download
                    clientDownload = new WebClient();

                    //Added the function to support proxy
                    clientDownload.Proxy = System.Net.WebProxy.GetDefaultProxy();
                    clientDownload.Proxy.Credentials = CredentialCache.DefaultCredentials;
                    clientDownload.Credentials = System.Net.CredentialCache.DefaultCredentials;
                    //End added

                    clientDownload.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        try
                        {                           
                            downloadingInfo.DownloadedBytesCurrent = e.ProgressPercentage;
                            downloadingInfo.DownloadedBytesTotal = (int)((nDownloadedTotal + e.BytesReceived) * 100 / total);
                        }
                        catch
                        {
                            //log the error message,you can use the application's log code
                        }

                    };

                    clientDownload.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                    {
                        try
                        {
                            DealWithDownloadErrors();
                            DownloadFileInfo dfile = e.UserState as DownloadFileInfo;
                            nDownloadedTotal += dfile.Size;                           
                            downloadingInfo.DownloadedBytesCurrent = 0;
                            downloadingInfo.DownloadedBytesTotal = (int)(nDownloadedTotal * 100 / total);
                            evtPerDonwload.Set();
                        }
                        catch (Exception)
                        {
                            //log the error message,you can use the application's log code
                        }

                    };

                    evtPerDonwload.Reset();

                    //Download the folder file
                    string tempFolderPath1 = CommonUnitity.GetFolderUrl(file);
                    if (!string.IsNullOrEmpty(tempFolderPath1))
                    {
                        tempFolderPath = Path.Combine(CommonUnitity.SystemBinUrl, ConstFile.TEMPFOLDERNAME);
                        tempFolderPath += tempFolderPath1;
                    }
                    else
                    {
                        tempFolderPath = Path.Combine(CommonUnitity.SystemBinUrl, ConstFile.TEMPFOLDERNAME);
                    }

                    clientDownload.DownloadFileAsync(new Uri(file.DownloadUrl), Path.Combine(tempFolderPath, file.FileFullName), file);

                    //Wait for the download complete
                    evtPerDonwload.WaitOne();

                    clientDownload.Dispose();
                    clientDownload = null;

                    //Remove the downloaded files
                    this.downloadFileList.Remove(file);
                }

            }
            catch (Exception)
            {
                ShowErrorAndRestartApplication();
                //throw;
            }

            //When the files have not downloaded,return.
            if (downloadFileList.Count > 0)
            {
                return;
            }

            //Test network and deal with errors if there have 
            DealWithDownloadErrors();

            //Debug.WriteLine("All Downloaded");
            foreach (DownloadFileInfo file in this.allFileList)
            {
                string tempUrlPath = CommonUnitity.GetFolderUrl(file);
                string oldPath = string.Empty;
                string newPath = string.Empty;
                try
                {
                    if (!string.IsNullOrEmpty(tempUrlPath))
                    {
                        oldPath = Path.Combine(CommonUnitity.SystemBinUrl + tempUrlPath.Substring(1), file.FileName);
                        newPath = Path.Combine(CommonUnitity.SystemBinUrl + ConstFile.TEMPFOLDERNAME + tempUrlPath, file.FileName);
                    }
                    else
                    {
                        oldPath = Path.Combine(CommonUnitity.SystemBinUrl, file.FileName);
                        newPath = Path.Combine(CommonUnitity.SystemBinUrl + ConstFile.TEMPFOLDERNAME, file.FileName);
                    }

                    //just deal with the problem which the files EndsWith xml can not download
                    System.IO.FileInfo f = new FileInfo(newPath);
                    if (!file.Size.ToString().Equals(f.Length.ToString()) && file.FileName.ToString().EndsWith(".xml"))
                    {
                        ShowErrorAndRestartApplication();
                    }


                    //Added for dealing with the config file download errors
                    string newfilepath = string.Empty;
                    if (newPath.Substring(newPath.LastIndexOf(".") + 1).Equals(ConstFile.CONFIGFILEKEY))
                    {
                        if (System.IO.File.Exists(newPath))
                        {
                            if (newPath.EndsWith("_"))
                            {
                                newfilepath = newPath;
                                newPath = newPath.Substring(0, newPath.Length - 1);
                                oldPath = oldPath.Substring(0, oldPath.Length - 1);
                            }
                            File.Move(newfilepath, newPath);
                        }
                    }
                    //End added

                    if (File.Exists(oldPath))
                    {
                        MoveFolderToOld(oldPath, newPath);
                    }
                    else
                    {
                        //Edit for config_ file
                        if (!string.IsNullOrEmpty(tempUrlPath))
                        {
                            if (!Directory.Exists(CommonUnitity.SystemBinUrl + tempUrlPath.Substring(1)))
                            {
                                Directory.CreateDirectory(CommonUnitity.SystemBinUrl + tempUrlPath.Substring(1));


                                MoveFolderToOld(oldPath, newPath);
                            }
                            else
                            {
                                MoveFolderToOld(oldPath, newPath);
                            }
                        }
                        else
                        {
                            MoveFolderToOld(oldPath, newPath);
                        }

                    }
                }
                catch (Exception exp)
                {
                    //log the error message,you can use the application's log code
                }

            }

            //After dealed with all files, clear the data
            this.allFileList.Clear();

            if (this.downloadFileList.Count == 0)
                Exit(true);
            else
                Exit(false);

            evtDownload.Set();
        }

        //To delete or move to old files
        void MoveFolderToOld(string oldPath, string newPath)
        {
            if (!Directory.Exists(backup_folder))
                Directory.CreateDirectory(backup_folder);

            if (File.Exists(oldPath + ".old"))
                File.Delete(oldPath + ".old");

            if (File.Exists(oldPath))
            {
                File.Copy(oldPath, Path.Combine(backup_folder, Path.GetFileName(oldPath)));// скопируем в папку бекапов приложения
                File.Move(oldPath, oldPath + ".old");
            }


            File.Move(newPath, oldPath);
            //File.Delete(oldPath + ".old");
        }

       

       

        delegate void ExitCallBack(bool success);
        private void Exit(bool success)
        {
            if (ExitAction != null)
            {
                ExitAction(success);
            }
            //if (this.InvokeRequired)
            //{
            //    ExitCallBack cb = new ExitCallBack(Exit);
            //    this.Invoke(cb, new object[] { success });
            //}
            //else
            //{
            //    this.isFinished = success;
            //    this.DialogResult = success ? DialogResult.OK : DialogResult.Cancel;
            //    this.Close();
            //}
        }

        public void OnCancel()
        {
            //bCancel = true;
            //evtDownload.Set();
            //evtPerDonwload.Set();
            ShowErrorAndRestartApplication();
        }

        private void DealWithDownloadErrors()
        {
            try
            {
                //Test Network is OK or not.
                Config config = Config.LoadConfig(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConstFile.FILENAME));
                WebClient client = new WebClient();
                client.DownloadString(config.ServerUrl);
            }
            catch (Exception)
            {
                //log the error message,you can use the application's log code
                ShowErrorAndRestartApplication();
            }
        }

        private void ShowErrorAndRestartApplication()
        {
            MessageBox.Show(ConstFile.NOTNETWORK, ConstFile.MESSAGETITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            CommonUnitity.RestartApplication();
        }

        #endregion
    }
}
