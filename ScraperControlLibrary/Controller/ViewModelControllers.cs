using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using ScraperControlLibrary.Controller;

namespace ScraperControlLibrary
{

    public class RelayMultiConverter : IMultiValueConverter
    {
        private readonly Func<object[], Type, object, CultureInfo, object> _Convert;
        private readonly Func<object, Type[], object, CultureInfo, object[]> _ConvertBack;
        public RelayMultiConverter(Func<object[], Type, object, CultureInfo, object> Convert, Func<object, Type[], object, CultureInfo, object[]> ConvertBack)
        {
            _Convert = Convert;
            _ConvertBack = ConvertBack ?? ((a, b, c, d) => null);
        }
        public RelayMultiConverter(Func<object[], Type, object, CultureInfo, object> Convert) : this(Convert, null) { }
        public RelayMultiConverter(Func<object[], object> Convert) : this((a, b, c, d) => Convert.Invoke(a)) { }
        public RelayMultiConverter(Func<object[], object> Convert, Func<object, object[]> ConvertBack) : this((a, b, c, d) => Convert.Invoke(a), (a, b, c, d) => ConvertBack.Invoke(a)) { }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => _Convert(values, targetType, parameter, culture);
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => _ConvertBack(value, targetTypes, parameter, culture);
    }

    public class RelayValueConverter : IValueConverter
    {
        private readonly Func<object, Type, object, CultureInfo, object> _Convert;
        private readonly Func<object, Type, object, CultureInfo, object> _ConvertBack;

        public RelayValueConverter(Func<object, Type, object, CultureInfo, object> Convert, Func<object, Type, object, CultureInfo, object> ConvertBack)
        {
            _Convert = Convert;
            _ConvertBack = ConvertBack ?? ((a, b, c, d) => null);
        }
        public RelayValueConverter(Func<object, Type, object, CultureInfo, object> Convert) : this(Convert, null) { }
        public RelayValueConverter(Func<object, object> Convert) : this(new Func<object, Type, object, CultureInfo, object>((a, b, c, d) => Convert.Invoke(a)), null) { }
        public RelayValueConverter(Func<object, object> Convert, Func<object, object> ConvertBack) : this((a, b, c, d) => Convert(a), (a, b, c, d) => ConvertBack(a)) { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _Convert(value, targetType, parameter, culture);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => _ConvertBack(value, targetType, parameter, culture);
    }

    public class RelayCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Func<object, bool> _canexecute;
        private readonly Action<object> _execute;

        public bool CanExecute(object parameter) => _canexecute(parameter);
        public bool CheckAfterExecute { get; set; } = false;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, new EventArgs());

        public void Execute(object parameter)
        {
            _execute(parameter);
            if (CheckAfterExecute) RaiseCanExecuteChanged();
        }

        public RelayCommand(Func<object, bool> canexecute, Action<object> execute)
        {

            if (execute == null)
                throw new Exception("Need an Action");
            _execute = execute;
            _canexecute = canexecute;
        }
        public RelayCommand(Action<object> action) : this(x => true, action) { }
    }

    public class RelayCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Func<T, bool> _canexecute;
        private readonly Action<T> _execute;

        public bool CanExecute(object parameter) => _canexecute((T)parameter);
        public bool CheckAfterExecute { get; set; } = false;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, new EventArgs());

        public void Execute(object parameter)
        {
            _execute((T)parameter);
            if (CheckAfterExecute) RaiseCanExecuteChanged();
        }

        public RelayCommand(Func<T, bool> canexecute, Action<T> execute)
        {

            if (execute == null)
                throw new Exception("Need an Action");
            _execute = execute;
            _canexecute = canexecute;
        }
        public RelayCommand(Action<T> action) : this(x => true, action) { }
    }

    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    sealed class ScraperDisplayNameAttribute : Attribute
    {
        readonly string name;
        public ScraperDisplayNameAttribute(string name) => this.name = name;
        public string Name => name;
    }

    [ContentProperty("Contents")]
    public class CommandParameters : MarkupExtension
    {
        private string ID { get; set; } = Guid.NewGuid().ToString();
        public Collection<object> Contents { get; set; } = new Collection<object>();
        private DependencyObject TargetObject;
        private DependencyProperty TargetProperty;
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget sp)
            {
                if (sp.TargetObject is DependencyObject o && sp.TargetProperty is DependencyProperty p)
                {
                    TargetObject = o;
                    TargetProperty = p;
                }
                else
                    return this;
            }
            return PullAllValues();
        }

        private void IsBindingSet(string name, object value)
        {
            if (value is ArgumentBinding binding && binding.Attached == null)
            {
                var property = DependencyProperty.RegisterAttached(ID + "_" + name, typeof(object), TargetObject.GetType(), new PropertyMetadata(default, (a, b) =>
                {
                    TargetObject.SetValue(TargetProperty, PullAllValues());
                    RaiseCanExecute();
                }));
                binding.Attached = property;
                BindingOperations.SetBinding(TargetObject, property, binding.Binding);
            }
        }

        private void RaiseCanExecute()
        {
            if (TargetProperty.Name == "CommandParameter")
            {
                var command = TargetObject.GetType().GetProperty("Command")?.GetValue(TargetObject);
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private object[] PullAllValues()
        {

            Contents.OfType<ArgumentBinding>().Select((x, i) => (i, x)).ToList().ForEach(x => IsBindingSet(x.i.ToString(), x.x));

            var vals = Contents.Select(x => x is ArgumentBinding binding && binding.Attached != null ? TargetObject?.GetValue(binding.Attached) : x).ToArray();
            return vals;
        }
    }
    public class ArgumentBinding
    {
        public BindingBase Binding { get; set; }
        public DependencyProperty Attached { get; set; }
    }
    public class ModalController : ViewModel
    {
        public GridLength Width { get => Get(); set => Set(value, new GridLength(600, GridUnitType.Pixel)); }
        public GridLength Height { get => Get(); set => Set(value, new GridLength(300, GridUnitType.Pixel)); }
        public object Content { get => Get(); set => Set(value); }
        public bool CanClose { get => Get(); set => Set(value, true); }
        public object Additional { get => Get(); set => Set(value); }

        public void ShowMessage(string title, string message, bool canclose = true, double width = 240, double height = 100)
        {
            Name = title;
            Content = new ModalMessage { Message = message };
            Width = new GridLength(width, GridUnitType.Pixel);
            Height = new GridLength(height, GridUnitType.Pixel);
            CanClose = canclose;
        }
        public void ShowAddEdit(object target, object additional = null)
        {
            var type = (target is Type ty) ? ty : target.GetType();
            var content = (ViewModel)(target is ViewModel vm ? vm : Activator.CreateInstance(type));
            var title = new List<string> { content._id != null ? "Edit" : "New" };
            var attr = type.GetCustomAttributes(false).OfType<ScraperDisplayNameAttribute>().FirstOrDefault();
            title.Add(attr != null ? attr.Name : type.Name);
            if (content._id != null) title.Add(content.Name);
            Name = string.Join(" - ", title.ToArray());
            Additional = additional;
            Content = content;
            Width = new GridLength(500, GridUnitType.Pixel);
            Height = new GridLength(300, GridUnitType.Pixel);
        }
        public void ShowURLDeconstructor()
        {
            Name = "Convert URL to a Site Profile";
            Content = new URLDeconstructor();
            Width = new GridLength(400, GridUnitType.Pixel);
            Height = new GridLength(185, GridUnitType.Pixel);
        }
        public void CloseModal()
        {
            Name = null;
            Content = null;
            Additional = null;
        }
    }

    public class ModalMessage
    {
        public string Message { get; set; }
    }

    public class JsonResults
    {
        public List<JsonContainer> Results { get; set; }

    }

    public class LayerController : ViewModel
    {
        public bool IsUpdateLayerOpen { get => Get(); set => Set(value); }
    }
    public class URLDeconstructor : INotifyPropertyChanged
    {
        private string _ProfileName = "";
        public string ProfileName
        {
            get => _ProfileName;
            set
            {
                if (_ProfileName != value)
                {
                    _ProfileName = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _URL = "";
        public string URL
        {
            get => _URL;
            set
            {
                if (_URL != value)
                {
                    _URL = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class RelayTargetCommand : RoutedCommand
    {
        public RelayTargetCommand()
        {
                  
        }
    }
}
