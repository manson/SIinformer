using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Resources;
using System.Windows.Threading;
using SIinformer.Utils;
using SIinformer.Window;
using FontStyle = System.Drawing.FontStyle;

namespace SIinformer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            try
            {
                var testFont = new Font("Monotype Corsiva", 16.0f, FontStyle.Italic);
                if (testFont.Name != "Monotype Corsiva")
                {
                    string windir = Environment.GetEnvironmentVariable("windir");
                    if (windir != null)
                    {
                        string fontsDir = Path.Combine(windir, "fonts");
                        if (Directory.Exists(fontsDir))
                        {
                            string dest = Path.Combine(fontsDir, "MTCORSVA.TTF");
                            if (!File.Exists(dest))
                            {
                                StreamResourceInfo sri =
                                    GetResourceStream(new Uri("pack://application:,,,/Resources/MTCORSVA.TTF"));
                                if (sri != null)
                                {
                                    var buffer = new byte[sri.Stream.Length];
                                    sri.Stream.Read(buffer, 0, (int) sri.Stream.Length);
                                    File.WriteAllBytes(dest, buffer);
                                    PInvoke.AddFontResource(dest);
                                    PInvoke.FontsAdded();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            DispatcherUnhandledException += AppDispatcherUnhandledException;
        }

        //private void ShowAppCallback(string args)
        //{
        //    // Обнаружен запуск копии приложения с аргументами e.Args
        //    if ((MainWindow != null) && (((MainWindow)MainWindow).LoadedFlag))
        //    {
        //        ((MainWindow)MainWindow).GetLogger().Add("Повторный запуск приложения");
        //        ((MainWindow)MainWindow).ShowWindow();
        //    }
        //}

        private void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string error = e.Exception.InnerException != null
                               ? string.Format(
                                     "Источник ошибки:{0}  {1}.{0}Причина ошибки:{0}  {2}.{0}Стек:{0}{3}{0}InnerException:{0}{4}",
                                     Environment.NewLine,
                                     e.Exception.Source, e.Exception.Message, e.Exception.StackTrace,
                                     e.Exception.InnerException.StackTrace)
                               : string.Format(
                                     "Источник ошибки:{0}  {1}.{0}Причина ошибки:{0}  {2}.{0}Стек:{0}{3}{0}",
                                     Environment.NewLine,
                                     e.Exception.Source, e.Exception.Message, e.Exception.StackTrace);
            File.AppendAllText(Setting.ErrorLogFileName(), error, Encoding.GetEncoding(1251));
            var ew = new ErrorWindow(error);
            if ((MainWindow != null) && (((MainWindow) MainWindow).LoadedFlag)) ew.Owner = MainWindow;
            ew.ShowDialog();
            e.Handled = true;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(1);
        }

        #region SingleInstance

        private readonly string _notifierName =
            AppDomain.CurrentDomain.BaseDirectory.Where(c => char.IsLetterOrDigit(c)).Aggregate("",
                                                                                                (current, c) =>
                                                                                                current + c);

        private Semaphore _notifier;
        private bool _releaseByClose;

        private bool ReleaseByClose
        {
            get { return _releaseByClose; }
            set
            {
                lock (this)
                {
                    _releaseByClose = value;
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool isNotExists;
            _notifier = new Semaphore(0, 1, _notifierName, out isNotExists);
            if (!isNotExists)
            {
                _notifier.Release();
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Shutdown();
                return;
            }
            var notifyThread = new Thread(NotifierThread);
            notifyThread.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            var currentNotifier = new Semaphore(0, 1, _notifierName);
            ReleaseByClose = true;
            currentNotifier.Release();
        }

        private void NotifierThread()
        {
            while (!ReleaseByClose)
            {
                _notifier.WaitOne();
                if (!ReleaseByClose)
                {
                    // Повторный запуск приложения
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                                      new Action(delegate
                                                     {
                                                         if ((MainWindow != null) &&
                                                             (((MainWindow) MainWindow).LoadedFlag))
                                                         {
                                                             ((MainWindow) MainWindow).ShowWindow();
                                                         }
                                                     }));
                }
            }
        }

        #endregion
    }

    public static class PInvoke
    {
        private const uint WmFontchange = 0x1D;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [DllImport("gdi32.dll")]
        public static extern int AddFontResource(string lpszFilename);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static void FontsAdded()
        {
            SendMessage(HwndBroadcast, WmFontchange, IntPtr.Zero, IntPtr.Zero);
        }
    }


}