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
    public class Modal : ViewModel
    {
        public GridLength Width { get => Get(); set => Set(value, new GridLength(600, GridUnitType.Pixel)); }
        public GridLength Height { get => Get(); set => Set(value, new GridLength(300, GridUnitType.Pixel)); }
        public object Content { get => Get(); set => Set(value); }
        public bool CanClose { get => Get(); set => Set(value, true); }
        public object Additional { get => Get(); set => Set(value); }

        public bool IsUpdateLayerOpen { get => Get(); set => Set(value); }

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

        public RelayCommand CloseLayer => new(x =>
        {
            Name = null;
            Content = null;
            Additional = null;
        });
        public RelayCommand AddEditViewModel => new(x => ShowAddEdit(x));
        public RelayCommand NewSiteProfile => new(x => ShowAddEdit(typeof(ScraperSiteProfile)));
        public RelayCommand NewSearchProfile => new(x => ShowAddEdit(typeof(ScraperSearchProfile), x));
        public RelayCommand NewStringTemplateVariable => new(x => ShowAddEdit(new TemplateVariable(VariableType.String), x));
        public RelayCommand NewBooleanTemplateVariable => new(x => ShowAddEdit(new TemplateVariable(VariableType.Boolean), x));
        public RelayCommand NewIncrementalTemplateVariable => new(x => ShowAddEdit(new TemplateVariable(VariableType.Incremental), x));
        public RelayCommand<ViewModel> DeleteItem => new(x => x.Delete());
        public RelayCommand<ScraperSearchProfile> NewCollectionNode => new(x => ShowAddEdit(typeof(QueryCollectionNode), x));
        public RelayCommand OpenUpdateLayer => new(x =>
        {
            if (IsUpdateLayerOpen)
                IsUpdateLayerOpen = false;
            else
                IsUpdateLayerOpen = true;
        });
        public RelayCommand NewElementNode => new(x =>
        {
            if (x is QueryElementNode qen && qen.Attributes.Count > 0)
                ShowMessage("Error", "Can't add a child to a node with attributes", true, 300, 150);
            else if (x is QueryCollectionNode QCN)
            {
                if (QCN.Nodes.Count == 0)
                    ShowAddEdit(new QueryElementNode { IsTopLevel = true, IsUnique = true }, x);
                else
                    ShowAddEdit(new QueryElementNode { IsTopLevel = true }, x);
            }
            else
            {
                ShowAddEdit(typeof(QueryElementNode), x);
            }
        });


        public RelayCommand<QueryElementNode> MakeUnique => new(x =>
        {
            var parent = (QueryCollectionNode)Scraper.Manager.GetParent(x);
            var changes = parent.Nodes.Select(y =>
            {
                y.IsUnique = (y == x);
                return y;
            });
            Scraper.HttpRequest.Update(x.GetType().Name, changes.ToList(), () =>
            {
                ShowMessage("Error", "It's Unique", true, 300, 150);
                parent.Read();
            });
        });


        public RelayCommand<QueryElementNode> NewAttributeNode => new(x =>
        {
            if (x.Children.Count > 0)
                ShowMessage("Error", "Can't add an attribute to a node with children", true, 300, 150);
            else
                ShowAddEdit(typeof(QueryAttributeNode), x);
        });
        public RelayCommand<ScraperSearchProfile> UpdateVariables => new(x =>
        {
            var httprequest = Scraper.HttpRequest;
            var Add = x.VariableSources.Where(y => y._id == null).Select(y => new VariableDefined { _id = y._id, UseVariable = y.UseVariable, Value = y.Value, TemplateID = y.TemplateID, ProfileID = x._id }).ToList();
            var Update = x.VariableSources.Where(y => y._id != null).Select(y => new VariableDefined { _id = y._id, UseVariable = y.UseVariable, Value = y.Value, TemplateID = y.TemplateID, ProfileID = x._id }).ToList();
            ShowMessage("Adding", "Adding new variables", false, 300, 150);
            httprequest.Create(typeof(VariableDefined).Name, Add, () =>
            {
                ShowMessage("Updating", "Updating old variables", false, 300, 150);
                httprequest.Update(typeof(VariableDefined).Name, Update, () => ShowMessage("Finished", "Completed!", true, 300, 150));
            });
        });
        public RelayCommand DeconstructURL => new(x =>
        {
            Name = "Convert URL to a Site Profile";
            Content = new URLDeconstructor();
            Width = new GridLength(400, GridUnitType.Pixel);
            Height = new GridLength(185, GridUnitType.Pixel);
        });
        public RelayCommand<URLDeconstructor> AddURL => new(x =>
        {
            var SiteProfile = new ScraperSiteProfile() { Name = x.ProfileName };
            var full = Regex.Match(x.URL, @"(?:(.+):\/{2})?(.+)");
            var protocol = full.Groups[1] ?? null;
            SiteProfile.Protocol = Enum.TryParse<Protocol>(protocol.Value, out var result) ? result : default;
            var seperated = full.Groups[2].Value.Split('?');
            SiteProfile.Hostname = seperated[0];
            var Variables = new ObservableCollection<TemplateVariable>();
            if (seperated.Length > 1)
            {
                Variables = new ObservableCollection<TemplateVariable>(seperated[1].Split('&').Select(x =>
                {
                    var template = new TemplateVariable() { MethodType = MethodType.GET };
                    var iable = x.Split('=');
                    template.Name = iable[0];
                    if (double.TryParse(iable[1], out var number))
                    {
                        template.Value = number;
                        template.VariableType = VariableType.Incremental;
                    }
                    else if (iable[1].ToLower() == "true" || iable[1].ToLower() == "false")
                    {
                        template.Value = iable[1].ToLower() == "true";
                        template.VariableType = VariableType.Boolean;
                    }
                    else
                    {
                        template.Value = iable[1];
                        template.VariableType = VariableType.String;
                    }
                    return template;
                }));
            }
            ShowMessage("Adding", "Creating new Site Profile", false, 300, 150);
            Scraper.HttpRequest.CreateThenGetIDs(typeof(ScraperSiteProfile).Name, SiteProfile, id =>
            {
                var withId = Variables.Select(va => new TemplateVariable { MethodType = va.MethodType, Name = va.Name, VariableType = va.VariableType, ParentID = id, Value = va.Value });
                ShowMessage("Adding", "Adding new variables", false, 300, 150);
                Scraper.HttpRequest.Create(typeof(TemplateVariable).Name, withId, () =>
                {
                    ShowMessage("Adding", "Creating default Search Profile", true, 300, 150);
                    Scraper.HttpRequest.Create(typeof(ScraperSearchProfile).Name, new ScraperSearchProfile { ParentID = id, Name = "Asserted Profile", QueryDelay = 3000 }, () =>
                    {
                        ShowMessage("Done!", "Finished!", true, 300, 150);
                        Scraper.Manager.Read();
                    });
                });
            });
        });
        public RelayCommand<ScraperSearchProfile> RunSearch => new(x =>
        {
            var site = (ScraperSiteProfile)Scraper.Manager.GetParent(x);
            Scraper.HttpRequest.BeginScraping(site.Protocol, site.Hostname, x);
        });
        public RelayCommand<ScraperSearchProfile> RunIndexSearch => new(x =>
        {
            var site = (ScraperSiteProfile)Scraper.Manager.GetParent(x);
            Scraper.HttpRequest.BeginScraping(site.Protocol, site.Hostname, x, true);
        });
    }

    public class ModalMessage
    {
        public string Message { get; set; }
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
}
