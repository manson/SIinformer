using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SIinformer.Logic;

namespace SIinformer.Window
{
    /// <summary>
    /// Логика взаимодействия для RenameWindow.xaml
    /// </summary>
    public partial class RenameWindow
    {
        private readonly RenameType _renameType;

        public RenameWindow()
        {
            InitializeComponent();
            _renameType = RenameType.NewCategory;
            Title = "Создать категорию";
            ResultNewName = "";
            NewName.Text = "";
            NewName.Focus();
        }

        public RenameWindow(Author author) :
            this()
        {
            _renameType = RenameType.RenameAuthor;
            Title = "Переименовать автора";
            ResultNewName = author.Name;
            NewName.Text = author.Name;
        }

        public RenameWindow(Category category)
            : this()
        {
            _renameType = RenameType.RenameCategory;
            Title = "Переименовать категорию";
            ResultNewName = category.Name;
            NewName.Text = category.Name;
        }

        public string ResultNewName { get; set; }

        private void NonRectangularWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void NewName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ResultNewName = NewName.Text.Trim();
            switch (_renameType)
            {
                case RenameType.NewCategory:
                case RenameType.RenameCategory:
                    ButtonOK.IsEnabled = !InfoUpdater.Categories.Contains(ResultNewName);
                    break;
                case RenameType.RenameAuthor:
                    ButtonOK.IsEnabled = true;
                    break;
            }
        }

        private void NewName_KeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter) && (ButtonOK.IsEnabled))
                DialogResult = true;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public enum RenameType
    {
        NewCategory,
        RenameCategory,
        RenameAuthor
    }
}