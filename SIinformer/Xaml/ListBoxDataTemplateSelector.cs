using System.Windows;
using System.Windows.Controls;
using SIinformer.Logic;

namespace SIinformer.Xaml
{
    public class ListBoxDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            System.Windows.Window window = Application.Current.MainWindow;
            return item != null && item is Category
                       ? window.FindResource("ListItemsTemplate_Category") as DataTemplate
                       : window.FindResource("ListItemsTemplate_Author") as DataTemplate;
        }
    }
}