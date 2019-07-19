using System.Windows;

namespace SIinformer.Window
{
    /// <summary>
    /// Логика взаимодействия для ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow
    {
        public ErrorWindow(string text)
        {
            try
            {
                InitializeComponent();
                infoTextBox.Text = text;
            }
            catch 
            {}
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}