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

namespace KnightsWarriorAutoupdater
{
    public class ConstFile
    {
        public const string TEMPFOLDERNAME = "TempFolder";
        public const string CONFIGFILEKEY = "config_";
        public const string FILENAME = "AutoUpdater.config";
        public const string ROOLBACKFILE = "SIinformer.exe";
        public const string MESSAGETITLE = "Автообновление Информатора СИ";
        public const string CANCELORNOT = "Идет процесс обновления Информатора СИ. Вы на самом деле хотите отменить установку?";
        public const string APPLYTHEUPDATE = "Необходимо перезапустить программу, чтобы применить обновления. Пожалуйста, нажмите OK, чтобы перезапустить ее!";
        public const string NOTNETWORK = "Не удалось обновить Информатор СИ. Сейчас произойдет перезапуск программы. Пожалуйста, попытайтесь снова обновиться.";
    }
}
