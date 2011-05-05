/*****************************************************************
 * Copyright (C) Knights Warrior Corporation. All rights reserved.
 * 
 * Author:   圣殿骑士（Knights Warrior） 
 * Email:    KnightsWarrior@msn.com
 * Website:  http://www.cnblogs.com/KnightsWarrior/       http://knightswarrior.blog.51cto.com/
 * Create Date:  5/8/2010 
 * Usage:
 *
 * RevisionHistory
 * Date         Author               Description
 * 
*****************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIinformer.Utils;

namespace KnightsWarriorAutoupdater
{
    public interface IAutoUpdater
    {
        void Update(DownloadingInfo downloadingInfo);

        void CheckUpdates(Action<UpdateInfo> done);

        void RollBack();
    }

    public class UpdateInfo
    {
        public bool hasUpdate { get; set; }
        public string updateInfo { get; set; }
    }
    /// <summary>
    /// класс привязки для отображения текущего состояния процесса скачивания. К объекту этого класса байндится интерфейс приложения
    /// </summary>
    public class DownloadingInfo: BindableObject
    {
        /// <summary>
        /// всего файлов на скачивание
        /// </summary>
        int _TotalFiles = 0;
        public int TotalFiles { get { return _TotalFiles; } set { _TotalFiles = value; RaisePropertyChanged("TotalFiles"); } }
        /// <summary>
        /// Кол-во байтов скачать всего
        /// </summary>
        long _MaxBytesTotal = 0;
        public long MaxBytesTotal { get { return _MaxBytesTotal; } set { _MaxBytesTotal = value; RaisePropertyChanged("MaxBytesTotal"); } }
        /// <summary>
        /// кол-во батйтов скачать для текущего файла
        /// </summary>
        int _MaxBytesCurrentFile=0;
        public int MaxBytesCurrentFile { get { return _MaxBytesCurrentFile; } set { _MaxBytesCurrentFile = value; RaisePropertyChanged("MaxBytesCurrentFile"); } }
        /// <summary>
        /// Всего скачано байтов
        /// </summary>
        int _DownloadedBytesTotal = 0;
        public int DownloadedBytesTotal { get { return _DownloadedBytesTotal; } set { _DownloadedBytesTotal = value; RaisePropertyChanged("DownloadedBytesTotal"); } }
        /// <summary>
        /// Всего скачано байтов для текущего файла
        /// </summary>
        int _DownloadedBytesCurrent = 0;
        public int DownloadedBytesCurrent { get { return _DownloadedBytesCurrent; } set { _DownloadedBytesCurrent = value; RaisePropertyChanged("DownloadedBytesCurrent"); } }
        /// <summary>
        /// Имя файла, который сейчас скачивается
        /// </summary>
        string _CurrentFile = "";
        public string CurrentFile { get { return _CurrentFile; } set { _CurrentFile = value; RaisePropertyChanged("CurrentFile"); } }


    }
}
