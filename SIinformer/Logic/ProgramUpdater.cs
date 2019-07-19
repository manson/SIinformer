using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using Manson.AutoUpdater;
using SIinformer.Utils;
using SIinformer.Window;

namespace SIinformer.Logic
{
    public class ProgramUpdater
    {
        #region singleton
        private static ProgramUpdater _programUpdater = null;
        public static ProgramUpdater Instance
        {
            get
            {
                _programUpdater = _programUpdater ?? new ProgramUpdater();
                return _programUpdater;
            }
        }

        #endregion

        private Visibility _windowVisibility;
        private Logger _logger = null;
        public void Init(Visibility windowVisibility, Logger logger)
        {
            _windowVisibility = windowVisibility;
            _logger = logger;
            MainWindow.WindowVisibilityChanged += (state) => { _windowVisibility = state; };

            App.ProgramUpdater.HasUpdateEvent += (s, e) =>
                                                     {                                                         
                                                         if (_windowVisibility == Visibility.Visible)
                                                         {
                                                             var upateInfo = ((HasUpdateEventArgs) e).UpdateInfo;
                                                             if (upateInfo.Length > 800)
                                                                 upateInfo = upateInfo.Substring(0, 800) + "...";
                                                             if (MessageBox.Show("Обнаружено обновление программы:\r\n" + upateInfo + "\r\n\r\nСкачать его?", "Обновление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;
                                                         }                                                         
                                                          _logger.Add(string.Format("Обнаружено обновление программы. Текущая версия {0}, версия обновления {1}. Запуск скачивания... ", App.ProgramUpdater.CurrentVersion, App.ProgramUpdater.ServerVersion), true,false);
                                                         
                                                         App.ProgramUpdater.DownloadUpdate();
                                                     };
            App.ProgramUpdater.HasNoUpdateEvent += (s, e) =>
                                                       {
                                                           //if (_windowVisibility == Visibility.Visible)                                                         
                                                           _logger.Add("Обновление программы на сервере отсутствует", true, false);
                                                       };
            App.ProgramUpdater.UpdateDownloadedEvent += (s, e) =>
                                                            {

                                                                if (_windowVisibility == Visibility.Visible)
                                                                {
                                                                    if (MessageBox.Show("Обновление скачано и готово к применению.\r\nПрименить его (программа будет перезагружена)?", "Обновление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;                                                                    
                                                                }
                                                                _logger.Add("Обновление скачано и готово к применению. Запуск процесса обновления и перезагрузка программы...", true, false);
                                                                StopEverythingAndRestart();
                                                            };
            App.ProgramUpdater.DownloadProgressEvent += (s, e) =>
                                                            {
                                                                if (_windowVisibility == Visibility.Visible)
                                                                {
                                                                    var par = (DownloadProgressChangedEventArgs)e;
                                                                    _logger.Add("->Скачивание обновления: " + par.ProgressPercentage.ToString() + "% завершено.",true,false);  
                                                                }
                                                            };
            App.ProgramUpdater.ErrorDownloadingEvent += (s, e) =>
                                                            {
                                                                var par = (DownloadEventArgs) e;
                                                                _logger.Add("Ошибка при обновлении: " + par.Exception.Message, false,true);
                                                                if (_windowVisibility == Visibility.Visible) // если мы проверяем при открытой программе, то при ошибке спрашиваем что делать
                                                                    ProcessErrorUpdating(); 
                                                            };

            ProcessErrorUpdating();
        }

        private void ProcessErrorUpdating()
        {
            if (App.ProgramUpdater.IsErrorUpdating()) // если при предыдущем обновлении были проблемы
            {
                var result = MessageBox.Show(
                    "При предыдущем обновлении возникли проблемы:\r\n" + App.ProgramUpdater.GetLog() +
                    "\r\n\r\nНажмите ДА, чтобы попробовать снова применить скачанное обновление,\r\nНЕТ, чтобы заново проверить обновление на сервере и скачать,\r\nОТМЕНА, чтобы ничего сейчас не делать - вы можете попробовать ручками скопировать новые файлы из папки обновления",
                    "Ошибка обновления",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                    return;
                else if (result == MessageBoxResult.Yes)
                {
                    StopEverythingAndRestart();
                }
                else if (result == MessageBoxResult.No)
                {
                    App.ProgramUpdater.StartChecking();
                }
            }
            else
            {
                App.ProgramUpdater.StartChecking();
            }
        }

        private void StopEverythingAndRestart()
        {
            InfoUpdater.StopUpdating();
            InfoUpdater.Save(true, () =>
            {
                App.ProgramUpdater.ApplyUpdate(App.ExecutableFile);
                Process.GetCurrentProcess().Kill();
            });                                                                

        }
    }
}
