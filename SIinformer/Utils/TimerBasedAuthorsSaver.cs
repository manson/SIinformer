using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows.Threading;
using SIinformer.Logic;
//using Monitor = SISyncronizer.Monitor;

namespace SIinformer.Utils
{
    public class TimerBasedAuthorsSaver
    {
        private static TimerBasedAuthorsSaver instance = null;
        private Timer timer = null;

        public bool SynchroGoogle
        {
            get; set;
        }

        public static TimerBasedAuthorsSaver GetInstance()
        {
            return instance;
        }

        public static void StartMonitoring(bool GoogleSyncAsWell)
        {
            if (TimerBasedAuthorsSaver.instance == null)            
                TimerBasedAuthorsSaver.instance = new TimerBasedAuthorsSaver(GoogleSyncAsWell);
            
        }
        /// <summary>
        /// конструктор объекта
        /// </summary>
        /// <param name="GoogleSyncAsWell">при запуске так же мониторить и синхронизировать ссылки на гугл</param>
        public TimerBasedAuthorsSaver(bool GoogleSyncAsWell)
        {
            SynchroGoogle = GoogleSyncAsWell;
            timer = new Timer();
            timer.Interval = 60000; // время
            timer.Elapsed += (o, e) => CheckIfDataNeedToSave();
            timer.Start();
        }

        public void CheckIfDataNeedToSave()
        {
            // вызываем сохранение базы данных в нужном контексте (потоке)
            SIinformer.Window.MainWindow.MainForm.InvokeIfRequired(() =>
                                                                       {
                                                                           // если есть авторы с изменившимися данными, записываем их
                                                                           if ((from a in InfoUpdater.Authors
                                                                                where a.Changed
                                                                                select a).Count() > 0)
                                                                           {
                                                                               InfoUpdater.Save(false);
                                                                           }
                                                                       }, DispatcherPriority.Background);

           // GoogleSyncing();        
        }


        #region Работа с синхронизацией с Гуглом

        

        //private SISyncronizer.Monitor google_monitor = null;

        //public bool GoogleSyncActive()
        //{
        //    return google_monitor != null;
        //}
        /// <summary>
        /// отдельно останавливаем синхронизацию с гуглом
        /// </summary>
        //public void StopGoogleSync()
        //{
        //    SynchroGoogle = false;
        //    if (google_monitor!=null)
        //        google_monitor.Stop();
        //    google_monitor = null;

        //}
        /// <summary>
        /// отдельно запускаем синхронизацию с гуглом, например из настроек
        /// </summary>
        //public void StartGoogleSync()
        //{
        //    SynchroGoogle = true;
        //    google_monitor = new Monitor(SIinformer.Window.MainWindow.GetSettings().GoogleLogin, SIinformer.Window.MainWindow.GetSettings().GooglePassword);
        //    google_monitor.Start();
        //    //GoogleSyncing();
            
        //}

        //private void GoogleSyncing()
        //{
        //    // проверим, нужно ли синхронизировать с гуглом
        //    if (SynchroGoogle)
        //    {
        //        // вызываем сохранение данных в нужном контексте (потоке)
        //        SIinformer.Window.MainWindow.MainForm.InvokeIfRequired(() =>
        //        {
        //            // если есть авторы с изменившимися данными, синхронизируем их
        //            if ((from a in InfoUpdater.Authors
        //                 where a.ChangedGoogle
        //                 select a).Count() > 0)
        //            {
        //                if (GoogleSyncActive())
        //                    if (!google_monitor.IsSyncing())
        //                        google_monitor.SyncWithGoogle();
        //                    else
        //                    {
        //                        StartGoogleSync();
        //                    }
        //            }
        //        }, DispatcherPriority.Background);
        //    }
        //    else
        //    {
        //        // проверим, монитор у нас существует или нет (возможно он в прошлый раз при остановке не успел выгрузиться)
        //        if (GoogleSyncActive())
        //            StopGoogleSync();
        //    }
        //}



        #endregion

    }
}
