using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using Manson.AutoUpdater;
using System.IO;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("starting...");
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();//"1.0.0.0";
            var programFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            
            //var version_file = Path.Combine(programFolder, "version.txt");
            //if (File.Exists(version_file))
            //{
            //    var lines = File.ReadAllLines(version_file);
            //    version = lines[0];
            //}

            var cl = new Client("test",version, "http://mt2007-cat.ru/downloads/siinformer/version1.txt", false); // указываем неправильный адрес
            cl.HasUpdateEvent += (s, e) =>
                                     {
                                         Console.WriteLine("Has updates!\r\n" + ((HasUpdateEventArgs) e).UpdateInfo);
                                         Console.WriteLine("Downloading...");
                                         cl.DownloadUpdate();
                                     };
            cl.HasNoUpdateEvent += (s, e) =>
            {
                Console.WriteLine("No updates!");
            };
            cl.UpdateDownloadedEvent += (s, e) =>
                                            {
                                                Console.WriteLine("downloaded.");
                                                Console.WriteLine("need to restart program...");
                                                var me = System.Reflection.Assembly.GetEntryAssembly().Location;
                                                cl.ApplyUpdate(me);
                                                Process.GetCurrentProcess().Kill(); 
                                            };
            cl.DownloadProgressEvent += (s, e) =>
                                            {
                                                var par = (DownloadProgressChangedEventArgs) e;
                                                Console.Write(par.ProgressPercentage.ToString() + "%...");                                                            
                                            };
            cl.ErrorDownloadingEvent += (s, e) =>
                                            {
                                                var par = (DownloadEventArgs)e;
                                                Console.WriteLine(par.Exception.Message);
                                            };
            if (cl.IsErrorUpdating())
            {
                Console.WriteLine("Last update error occured");
                Console.WriteLine(cl.GetLog());

            }
            if (cl.IsPendingUpdate())
            {
                Console.WriteLine("need to restart program...");
                var me = System.Reflection.Assembly.GetEntryAssembly().Location;
                cl.ApplyUpdate(me);
                Process.GetCurrentProcess().Kill();               
            }

            cl.StartChecking();

            cl.VersionFileUrl = "http://mt2007-cat.ru/downloads/siinformer/version.txt"; // ставим правильный адрес и перезапускаем проверку. (Отработка некорретных ситуаций)
            cl.StartChecking();
            Console.WriteLine("waiting...");
            Console.ReadKey();
        }

    }
}
