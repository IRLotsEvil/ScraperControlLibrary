using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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

namespace ScraperControlLibrary
{
    /// <summary>
    /// Interaction logic for AddEdit.xaml
    /// </summary>
    public partial class AddEdit : UserControl
    {

        public ViewModel ParentModel
        {
            get { return (ViewModel)GetValue(ParentModelProperty); }
            set { SetValue(ParentModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ParentModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ParentModelProperty =
            DependencyProperty.Register("ParentModel", typeof(ViewModel), typeof(AddEdit), new PropertyMetadata(default));




        public AddEdit()
        {
            InitializeComponent();
        }
        public static RelayValueConverter EnumExtractor = new RelayValueConverter((a, b, c, d) => a != null && a.GetType().IsEnum ? a.GetType().GetEnumValues() : default);

        public RelayCommand<IEnumerable<Carrier>> FinishedAddEdit => new(x =>
        {
            var httpmanager = Scraper.HttpRequest;
            var scrapermanager = Scraper.Manager;
            var modalinstance = Scraper.ModalInstance;
            var Content = (ViewModel)modalinstance.Content;
            var _Type = Content.GetType();
            var name = _Type.GetCustomAttributes(false).OfType<ScraperDisplayNameAttribute>().FirstOrDefault();
            modalinstance.ShowMessage(Content._id != null ? "Updating..." : "Adding...", name != null ? name.Name : _Type.Name, false);
            x.OfType<Carrier>().ToList().ForEach(y =>
            {
                var property = _Type.GetProperty(y.PropertyName);
                var value = typeof(TypeCode).GetEnumNames().Skip(5).Take(11).Contains(Type.GetTypeCode(property.PropertyType).ToString()) ? Convert.ChangeType(y.Value, property.PropertyType) : y.Value;
                if (property.SetMethod != null) property.SetValue(Content, value);
            });
            ViewModel parentModel = ParentModel ?? (Content != null ? scrapermanager.GetParent(Content) : null);
            if (parentModel != null) Content.ParentID = parentModel._id;

            void showmessage(bool update)
            {
                if (parentModel == null && !update) scrapermanager.Read(); else parentModel.Read();
                modalinstance.ShowMessage("Finished!", update ? "Updated!" : "Created!");
            }
            if (Content._id != null)
                httpmanager.Update(_Type.Name, Content, () => showmessage(true));
            else
                httpmanager.Create(_Type.Name, Content, () => showmessage(false));
        });
    }
    public class CarrierConverter : IValueConverter
    {
        public object Convert(object a, Type targetType, object parameter, CultureInfo culture)
        {
            if (a != null)
            {
                var c = a.GetType().GetProperties().Where(x => Type.GetTypeCode(x.PropertyType) != TypeCode.Object && x.SetMethod != null).Where(x =>
                {
                    var Ignore = x.GetCustomAttributes(typeof(IgnoreCarrierAttribute), false).OfType<IgnoreCarrierAttribute>().FirstOrDefault();
                    if (Ignore != null)
                    {
                        if (!string.IsNullOrEmpty(Ignore.Dependant) && Ignore.Value != null)
                        {
                            var property = a.GetType().GetProperty(Ignore.Dependant);
                            if (property != null && property.PropertyType == Ignore.Value.GetType())
                            {
                                var Value = property.GetValue(a);
                                return Value.Equals(Ignore.Value);
                            }
                        }
                        else return false;
                    }
                    return true;
                }).Select(x => new Carrier(x.Name, x.GetValue(a), x.PropertyType)).ToList();
                var (index, carrier) = c.Select((x, i) => (index: i, carrier: x)).First(x => x.carrier.PropertyName == "Name");
                c.RemoveAt(index);
                return c.Prepend(carrier);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    [System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    sealed class IgnoreCarrierAttribute : Attribute
    {
        readonly string dependant;
        readonly object value;
        public string Dependant { get => dependant; }
        public object Value { get => value; }
        public IgnoreCarrierAttribute() { }
        public IgnoreCarrierAttribute(string dependant, object value)
        {
            this.dependant = dependant;
            this.value = value;
        }
    }

    public class Carrier : INotifyPropertyChanged
    {
        public string PropertyName { get; set; }
        private object _Value;
        public object Value
        {
            get => _Value;
            set
            {
                if (!Equals(_Value, value))
                {
                    _Value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }
        public Type PropertyType { get; set; }
        public Carrier(string name, object value, Type propertytype)
        {
            PropertyName = name;
            Value = value;
            PropertyType = propertytype;
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
