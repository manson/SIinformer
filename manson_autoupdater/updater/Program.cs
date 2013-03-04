using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace updater
{
    class Program
    {
        private static string parentFolder = "";
        private static string backupFolder = "";
        private static string parentExe = "";
        private static string error_file = "";
        static List<Tuple<string,string>> updatedFiles = new List<Tuple<string, string>>();
        static void Main(string[] args)
        {
            if (args.Length==0) return;
            //Console.WriteLine(args[0]);
            parentExe = args[0];
            var programFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            error_file = Path.Combine(programFolder, "error_occured");
            var parentDirectory = Directory.GetParent(programFolder);
            if (!parentDirectory.Exists) return;
            
            backupFolder = Path.Combine(parentDirectory.FullName, "Backup");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            backupFolder = Path.Combine(backupFolder, DateTime.Today.ToShortDateString());
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            parentDirectory = Directory.GetParent(parentDirectory.FullName);
            if (!parentDirectory.Exists) return;

            parentFolder = parentDirectory.FullName;

            ClearLog();
            Log("Запуска процесса обновления...");
            // только если все ок, чистим папку обновления, а иначе у нас там стоит маркер ошибки
            if (ProcessFolder(programFolder, parentDirectory.FullName, backupFolder))
                ClearUpdateFolder(programFolder);
            Process.Start(parentExe);
        }

        static void ClearUpdateFolder(string folder)
        {
            var updateFiles = Directory.GetFiles(folder);
            foreach (var updateFile in updateFiles)
            {
                if (updateFile.EndsWith("updater.exe")) continue;
                try
                {
                    if (File.Exists(updateFile))
                        File.Delete(updateFile);
                }
                catch 
                {}
            }
            var updateFolders = Directory.GetDirectories(folder);
            foreach (var updateFolder in updateFolders)
            {
                ClearUpdateFolder(updateFolder);
                try
                {
                    Directory.Delete(updateFolder,true);
                }
                catch
                {}
            }
        }

        static bool ProcessFolder(string from, string to, string backupTo)
        {
            
            if (!Directory.Exists(to)) Directory.CreateDirectory(to);
            if (!Directory.Exists(backupTo)) Directory.CreateDirectory(backupTo);
            // get update files
            var updateFiles = Directory.GetFiles(from);
            foreach (var updateFile in updateFiles)
            {
                var file = Path.GetFileName(updateFile);
                if (file == "pending_update" || file == "updater.exe" || file == "update.zip" || file == "error_occured") continue;
                var destFile = Path.Combine(to, file);
                var backupFile = Path.Combine(backupTo, file);               
                bool succ = false;
                int cnt = 5;
                while (cnt > 0 && !succ)
                {
                    Log("Обновляется файл " + destFile);
                    try
                    {
                        if (File.Exists(destFile))
                        {
                            File.Copy(destFile, backupFile,true);
                            if (updatedFiles.FirstOrDefault(x => x.Item1 == destFile)==null)
                                updatedFiles.Add(new Tuple<string, string>(destFile, backupFile));
                        }
                        File.Copy(updateFile, destFile,true);
                        
                        succ = true;
                    }
                    catch (Exception ex)
                    {
                        Log("Ошибка: " + ex.Message);
                    }
                    if (!succ)
                    {
                        cnt--;
                        Log("Пауза...");
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                if (!succ)
                {
                    Log("Ошибка обновления. Откатываемся на предыдущую версию...");
                    File.WriteAllText(error_file, "Ошибка обновления. См. лог-файл.");
                    RollBack();
                    return false;
                }
            }
            var updateFolders = Directory.GetDirectories(from);
            foreach (var updateFolder in updateFolders)
            {
                var ind = updateFolder.LastIndexOf("\\");
                var pureFolder = updateFolder.Substring(ind+1);
                if (
                    !ProcessFolder(Path.Combine(from, pureFolder), Path.Combine(to, pureFolder),
                                   Path.Combine(backupTo, pureFolder))) return false;
            }
            return true;
        }

        static void RollBack()
        {
            Log("Откат изменений...");
            foreach (Tuple<string, string> updatedFile in updatedFiles)
            {
                Log(updatedFile.Item1);
                try
                {
                    File.Copy(updatedFile.Item2, updatedFile.Item1, true);
                }
                catch 
                {}
            }
        }

        static void ClearLog()
        {
            var logFile = Path.Combine(parentFolder, "update.log");
            if (File.Exists(logFile))
            {
                try
                {
                    File.Delete(logFile);
                }
                catch (Exception)
                {
                    
                }
            }
        }

        static void Log(string message)
        {
            var logFile = Path.Combine(parentFolder, "update.log");
            File.AppendAllText(logFile, DateTime.Now.ToString() + "   " + message + "\r\n");
        }
    }
}
