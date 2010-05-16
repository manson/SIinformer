using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SIinformer.Window;

namespace SIinformer.Controls
{
    /// <summary>
    /// Interaction logic for ToolBar.xaml
    /// </summary>
    public partial class ToolBar : UserControl
    {
        public ToolBar()
        {
            InitializeComponent();
        }


        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainForm.ShowSettingsWindow();
        }


        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SIinformer.Utils.TimerBasedAuthorsSaver.GetInstance().SynchroGoogle)
            {
                MainWindow.MainForm.GetLogger().Add("Синхронизация с Гуглом невозможна, так как она не настроена.");
                return;
            }
            MainWindow.MainForm.GetLogger().Add("Синхронизация с Google запущена.");
            SIinformer.Utils.TimerBasedAuthorsSaver.GetInstance().CheckIfDataNeedToSave();
        }
    }
}
