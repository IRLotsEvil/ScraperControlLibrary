using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScraperControlLibrary.Modal
{
    /// <summary>
    /// Interaction logic for Results.xaml
    /// </summary>
    public partial class Results : UserControl
    {
        public Results()
        {
            InitializeComponent();
        }
    }
    public class TypeSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }
        public HierarchicalDataTemplate DictionaryTemplate { get; set; }
        public HierarchicalDataTemplate ListTemplate { get; set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is IDictionary)
                return DictionaryTemplate;
            else if (item is IList)
                return ListTemplate;
            else return DefaultTemplate;
        }
    }
}
