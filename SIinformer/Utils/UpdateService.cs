using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnightsWarriorAutoupdater;
using System.Net;
using System.Windows;
using System.Xml;

namespace SIinformer.Utils
{
    public class UpdateService
    {
        static UpdateService service = null;
        public static UpdateService GetInstance()
        {
            if (service == null) service = new UpdateService();
            return service;
        }

        public void StartUpdate()
        {
            #region check and download new version program
            bool bHasError = false;
            IAutoUpdater autoUpdater = new KnightsWarriorAutoupdater.AutoUpdater();
            try
            {
                autoUpdater.CheckUpdates(
                    (UpdateInfo info) =>
                    {
                        if (info.hasUpdate)
                        {
                            if (MessageBox.Show("Найдены обновления:\r\n\r\n" + info.updateInfo + "\r\nОбновить? \r\nЗамененные файлы Информатора будут скопированы в папку AppBackup", "Система обновления Информатора СИ", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                DownloadingInfo di = new DownloadingInfo();
                                autoUpdater = new KnightsWarriorAutoupdater.AutoUpdater();// обнулим все внутренние объекты
                                autoUpdater.Update(di);
                            }
                        }
                    }
                    );
            }
            catch (WebException exp)
            {
                MessageBox.Show("Can not find the specified resource");
                bHasError = true;
            }
            catch (XmlException exp)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (NotSupportedException exp)
            {
                bHasError = true;
                MessageBox.Show("Upgrade address configuration error");
            }
            catch (ArgumentException exp)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (Exception exp)
            {
                bHasError = true;
                MessageBox.Show("An error occurred during the upgrade process");
            }
            finally
            {
                if (bHasError == true)
                {
                    try
                    {
                        autoUpdater.RollBack();
                    }
                    catch (Exception)
                    {
                        //Log the message to your file or database
                    }
                }
            }
            #endregion

        }
    }
}
