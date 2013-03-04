using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;


namespace Manson.AutoUpdater
{
    public class Client
    {
        #region private fields

        private int _currentVersionMajor;
        private int _currentVersionMinor;
        private int _currentVersionBuild;
        private int _currentVersionRevision;
        private int _serverVersionMajor;
        private int _serverVersionMinor;
        private int _serverVersionBuild;
        private int _serverVersionRevision;

        private string _currentDir = "";
        private string _currentVersionFile = "";
        private string _currentUpdateFile = "";
        private string _currentVersionInfoFile = "";
        #endregion



        #region Version processing

        /// <summary>
        /// Has update or not after checking
        /// </summary>
        public bool HasUpdate
        {
            get { return GetCurrentVersion() < GetServerVersion(); }
        }

        private long GetServerVersion()
        {
            long result = 0;
            var ver = _serverVersionMajor.ToString() + GetStringFromVersion(3, _serverVersionMinor) +
                      GetStringFromVersion(3, _serverVersionBuild) + GetStringFromVersion(3, _serverVersionRevision);
            result = long.Parse(ver);
            return result;
        }


        private long GetCurrentVersion()
        {
            long result = 0;
            var ver = _currentVersionMajor.ToString() + GetStringFromVersion(3, _currentVersionMinor) +
                      GetStringFromVersion(3, _currentVersionBuild) + GetStringFromVersion(3, _currentVersionRevision);
            result = long.Parse(ver);
            return result;
        }
        /// <summary>
        /// get symbol presentation, i.e. (002,012)
        /// </summary>
        /// <param name="digits"> number of digits</param>
        /// <param name="version">version</param>
        /// <returns></returns>
        private string GetStringFromVersion(int digits, int version)
        {
            var ver = version.ToString();
            if (ver.Length >= digits) return ver;
            string result = ver;
            for (int i = 0; i < digits - ver.Length; i++)
                result = "0" + result;
            return result;
        }

        #endregion

        #region Current version

        public string CurrentVersion { get; set; }


        public int CurrentVersionMajor
        {
            get { return _currentVersionMajor; }
        }

        public int CurrentVersionMinor
        {
            get { return _currentVersionMinor; }
        }

        public int CurrentVersionBuild
        {
            get { return _currentVersionBuild; }
        }

        public int CurrentVersionRevision
        {
            get { return _currentVersionRevision; }
        }

        #endregion

        #region Server version

        public string ServerVersion { get; set; }

        public int ServerVersionMajor
        {
            get { return _serverVersionMajor; }
        }

        public int ServerVersionMinor
        {
            get { return _serverVersionMinor; }
        }

        public int ServerVersionBuild
        {
            get { return _serverVersionBuild; }
        }

        public int ServerVersionRevision
        {
            get { return _serverVersionRevision; }
        }

        #endregion
        /// <summary>
        /// URL to information file with version info
        /// </summary>
        public string VersionFileUrl { get; set; }
        /// <summary>
        /// URL to information file with update info
        /// </summary>
        public string InfoFileUrl { get; set; }
        /// <summary>
        /// URL to information file with update itself
        /// </summary>
        public string UpdateFileUrl { get; set; }
        /// <summary>
        /// project name. Uses in Update folder creation, i.e. updates/projectName
        /// </summary>
        public string ProjectName { get; set; }
        // Update information if any in version.txt file
        public string UpdateInfo { get; set; }
        /// <summary>
        /// some kind of processing performs
        /// </summary>
        public bool IsBusy { get; set; }
       /// <summary>
        /// checking timer
        /// </summary>
        private Timer _updateTimer = null;
        /// <summary>
        /// has update event
        /// </summary>
        public EventHandler HasUpdateEvent = null;
        /// <summary>
        /// no updates
        /// </summary>
        public EventHandler HasNoUpdateEvent = null;
        /// <summary>
        /// downloaded update
        /// </summary>
        public EventHandler UpdateDownloadedEvent = null;
        /// <summary>
        /// Progress event.
        /// </summary>
        public EventHandler DownloadProgressEvent = null;
        /// <summary>
        /// error downloading
        /// </summary>
        public EventHandler ErrorDownloadingEvent = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="versionFileUrl">Url to file with version info</param>
        /// <param name="infoFileUrl">Url to file with update info</param>
        /// <param name="periodicalChecking">do periodical checking</param>
        /// <param name="hoursPerCheck">hours for for periodical checking</param>
        public Client(string projectName, string currentVersion, string versionFileUrl,  bool periodicalChecking = true, int hoursPerCheck=1)
        {
            VersionFileUrl = versionFileUrl;
            ProjectName = projectName;

            var parts = currentVersion.Split(".".ToCharArray());
            if (parts.Length != 4)
            {
                throw new Exception("Invalid current version format. Should be as of 1.0.0.2");
            }
            try
            {
                _currentVersionMajor = int.Parse(parts[0]);
                _currentVersionMinor = int.Parse(parts[1]);
                _currentVersionBuild = int.Parse(parts[2]);
                _currentVersionRevision = int.Parse(parts[3]);
            }
            catch (Exception ex)
            {                
                throw ex;
            }
            CurrentVersion = currentVersion;

            _currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            _currentDir = Path.Combine(_currentDir, "Updates");            
            if (!Directory.Exists(_currentDir))
                Directory.CreateDirectory(_currentDir);
            _currentDir = Path.Combine(_currentDir, ProjectName);
            if (!Directory.Exists(_currentDir))
                Directory.CreateDirectory(_currentDir);

            _currentVersionFile = Path.Combine(_currentDir, "version.txt");
            _currentVersionInfoFile = Path.Combine(_currentDir, "update.txt");
            if (periodicalChecking)
            {
                _updateTimer = new Timer(hoursPerCheck * 3600000);
                _updateTimer.Elapsed+=_updateTimer_Elapsed;
            }
        }

        /// <summary>
        /// start checking for updates
        /// </summary>
        public void StartChecking()
        {
            while (IsBusy)
            {
               Thread.Sleep(100);
            }
            IsBusy = true;
            if (IsPendingUpdate())
            {
                if (UpdateDownloadedEvent != null)
                    UpdateDownloadedEvent(this, null);
                IsBusy = false;
                return;
            }
            ClearUpdateFolder();
            DownloadInfoFile();
        }

        private void _updateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            StartChecking();
        }
        /// <summary>
        /// check if update downloaded and ready for replacement
        /// </summary>
        /// <returns></returns>
        public bool IsPendingUpdate()
        {
            var pending = Path.Combine(_currentDir, "pending_update");
            return File.Exists(pending);
        }
        /// <summary>
        /// if error occured during updating
        /// </summary>
        /// <returns></returns>
        public bool IsErrorUpdating()
        {
            var err = Path.Combine(_currentDir, "error_occured");
            return File.Exists(err);
        }
        /// <summary>
        /// gets the last log file info
        /// </summary>
        /// <returns></returns>
        public string GetLog()
        {
            var programDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var logFile = Path.Combine(programDir, "update.log");
            if (File.Exists(logFile))
                return File.ReadAllText(logFile);
            return "";
        }

        /// <summary>
        /// clear update folder
        /// </summary>
        public void ClearUpdateFolder()
        {
            if (Directory.Exists(_currentDir))
            {
                var files = Directory.GetFiles(_currentDir);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch 
                    {}
                }
            }
        }


        /// <summary>
        /// apply update. main application must exit
        /// runExecutableAfterUpdate - which exe should run after update
        /// </summary>
        public bool ApplyUpdate(string runExecutableAfterUpdate)
        {
            IsBusy = true;
            if (!IsPendingUpdate())
            {
                IsBusy = false;
                return false;
            }
            var updater = Path.Combine(_currentDir, "updater.exe");
            if (!File.Exists(updater))
            {
                IsBusy = false;
                return false;
            }
            var upd = new ProcessStartInfo(updater, runExecutableAfterUpdate);
            upd.WindowStyle = ProcessWindowStyle.Hidden;
            upd.CreateNoWindow = true;
            
            Process.Start(upd);
            IsBusy = false;
            return true;
        }



        #region Downloading and processing info file
        void DownloadInfoFile()
        {
            var webClient = new WebClient();
            try
            {
                webClient.DownloadFileCompleted += CompletedVersionInfoDownload;
                webClient.DownloadFileAsync(new Uri(VersionFileUrl), _currentVersionFile);
            }
            catch (Exception ex)
            {
                IsBusy = false;
                if (ErrorDownloadingEvent != null)
                    ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = ex });
            }
        }

       

        private void CompletedVersionInfoDownload(object sender, AsyncCompletedEventArgs e)
        {

            if (File.Exists(_currentVersionFile))
                ProcessInfoFile();
            else if (ErrorDownloadingEvent != null)
            {
                IsBusy = false;
                ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception("Unable download info file for project: " + ProjectName) });                
            }else
                IsBusy = false;

        }
        /// <summary>
        /// checking if update exist
        /// </summary>
        void ProcessInfoFile()
        {
            var lines = File.ReadAllLines(_currentVersionFile);            
            if (lines.Length >= 3)
            {
                var parts = lines[0].Split(".".ToCharArray());
                if (parts.Length != 4)
                {
                    IsBusy = false;
                    if (ErrorDownloadingEvent!=null)
                    ErrorDownloadingEvent(this, new DownloadEventArgs{ Exception = new Exception("Invalid server version format. Should be as of 1.0.0.2")});
                    return;
                }
                try
                {
                    _serverVersionMajor = int.Parse(parts[0]);
                    _serverVersionMinor = int.Parse(parts[1]);
                    _serverVersionBuild = int.Parse(parts[2]);
                    _serverVersionRevision = int.Parse(parts[3]);
                }
                catch (Exception ex)
                {
                    IsBusy = false;
                    if (ErrorDownloadingEvent != null)
                        ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception("Invalid server version format. Should be as of 1.0.0.2") });
                    return;
                }
                
                
                ServerVersion = lines[0];
                InfoFileUrl = lines[1];
                UpdateFileUrl = lines[2];



                if (HasUpdate)
                {
                    UpdateInfo = lines[0]; // if no firther information will be obtained, set server version info
                    if (!string.IsNullOrWhiteSpace(InfoFileUrl)) // if we hav url for info - download it
                    {
                        var webClient = new WebClient();
                        webClient.DownloadFileCompleted += CompletedInfoDownload;
                        try
                        {
                            webClient.DownloadFileAsync(new Uri(InfoFileUrl), _currentVersionInfoFile);
                        }
                        catch
                        {
                            IsBusy = false;
                        }
                    }
                    else
                    {
                        IsBusy = false;
                        if (HasUpdateEvent != null)
                            HasUpdateEvent(this, new HasUpdateEventArgs {UpdateInfo = UpdateInfo});
                    }
                }
                else
                {
                    if (HasNoUpdateEvent != null)
                        HasNoUpdateEvent(this, null);
                }
             
            }else if (lines.Length == 0)
            {
                IsBusy = false;
                if (ErrorDownloadingEvent != null)
                    ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception(string.Format("Unable to get update info by url {0}. It may me invalid or communication error", VersionFileUrl)) });
                if (HasNoUpdateEvent != null)
                    HasNoUpdateEvent(this, null);
            }
            IsBusy = false;
        }

        private void CompletedInfoDownload(object sender, AsyncCompletedEventArgs e)
        {
            if (File.Exists(_currentVersionInfoFile))
            {
                UpdateInfo = File.ReadAllText(_currentVersionInfoFile);
            }
            // in any curcumstances we rise current info
            IsBusy = false;
            if (HasUpdateEvent != null)
                HasUpdateEvent(this, new HasUpdateEventArgs { UpdateInfo = UpdateInfo });
        }
        #endregion


        #region downloading update ziped file
        /// <summary>
        /// download update and uncompress it
        /// </summary>
        public void DownloadUpdate()
        {
            IsBusy = true;
            _currentUpdateFile = Path.Combine(_currentDir, Path.GetFileName(UpdateFileUrl));
            if (File.Exists(_currentUpdateFile))
            {
                try
                {
                    File.Delete(_currentUpdateFile);
                }
                catch (Exception)
                {
                    if (ErrorDownloadingEvent != null)
                        ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception(string.Format("Unable to download update package. File {0} locked.", _currentUpdateFile)) });
                    IsBusy = false;
                    return;
                }
            }
            var webClient = new WebClient();
            webClient.DownloadFileCompleted += CompletedUpdateDownload;
            webClient.DownloadProgressChanged += ProgressChanged;
            try
            {
                webClient.DownloadFileAsync(new Uri(UpdateFileUrl), _currentUpdateFile);
            }
            catch
            {
                IsBusy = false;
            }
        }
        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (DownloadProgressEvent != null) DownloadProgressEvent(sender, e);
        }
        private void CompletedUpdateDownload(object sender, AsyncCompletedEventArgs e)
        {
            if (File.Exists(_currentUpdateFile))
            {
                try
                {
                    using (var stream = File.OpenRead(_currentUpdateFile))
                        stream.DecompressToDirectory(_currentDir, "", (s) => false);
                    // check if in update updater exists
                    var updater = Path.Combine(_currentDir, "updater.exe");
                    if (!File.Exists(updater))
                    {
                        IsBusy = false;
                        if (ErrorDownloadingEvent != null)
                            ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception(string.Format("Update package does not contains updater.exe file")) });
                        return;
                    }
                    // create marker for pending update. For example when program starts it see that update pending
                    var pending = Path.Combine(_currentDir, "pending_update");
                    File.WriteAllText(pending,"");
                    
                    IsBusy = false;
                    if (UpdateDownloadedEvent != null)
                        UpdateDownloadedEvent(this, null);
                    
                }
                catch (Exception ex)
                {
                    IsBusy = false;
                    if (ErrorDownloadingEvent != null)
                        ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception(string.Format("Unable to unzip update package {0}", _currentUpdateFile)) });                    
                }
            }
            else
            {
                IsBusy = false;
                if (ErrorDownloadingEvent != null)
                    ErrorDownloadingEvent(this, new DownloadEventArgs { Exception = new Exception(string.Format("Unable to download update package. ")) });
            }
            IsBusy = false;
        }
        #endregion

        
    }


    public class DownloadEventArgs: EventArgs
    {
        public Exception Exception { get; set; }        
    }

    public class HasUpdateEventArgs : EventArgs
    {
        public string UpdateInfo { get; set; }
    }
}
