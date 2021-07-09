using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace ScraperControlLibrary
{
    public abstract class ViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> ValueStore = new();

        /// <summary>
        /// Document ID, if the id is null or empty it's a new document
        /// </summary>
        [IgnoreCarrier]
        public string _id { get; set; }

        /// <summary>
        /// Parent Document's ID
        /// </summary>
        [IgnoreCarrier]
        public string ParentID { get; set; }

        /// <summary>
        /// Just a name to display with each object 
        /// </summary>
        public string Name { get => Get(); set => Set(value); }

        public void Set(object value, object defaultValue = null, [CallerMemberName] string name = "")
        {
            var contains = ValueStore.ContainsKey(name);
            void onPropertyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            var targetValue = defaultValue != null && !contains ? defaultValue : value;
            if (targetValue is INotifyPropertyChanged property) property.PropertyChanged += (s, e) => onPropertyChanged();
            if (targetValue is INotifyCollectionChanged collection) collection.CollectionChanged += (s, e) => onPropertyChanged();
            if (!ValueStore.ContainsKey(name) || !Equals(ValueStore[name], targetValue))
            {
                ValueStore[name] = targetValue;
                if (contains) onPropertyChanged();
            }
        }
        public dynamic Get([CallerMemberName] string name = "") => ValueStore[name];

        public ViewModel()
        {
            foreach (var property in GetType().GetProperties())
            {
                var setter = property.GetSetMethod();
                if (property.PropertyType.GetTypeInfo().IsValueType || property.PropertyType.GetInterfaces().Contains(typeof(ICollection)))
                    setter?.Invoke(this, new object[] { Activator.CreateInstance(property.PropertyType) });
                else setter?.Invoke(this, new object[] { null });
            }
        }
        /// <summary>
        /// Finds the parent of the ViewModel from top down
        /// </summary>
        /// <param name="target">The ViewModel to find the parent of</param>
        /// <returns>The parent or null</returns>
        public ViewModel GetParent(ViewModel target)
        {
            var children = GetType().GetProperties().Where(x => x.PropertyType.GenericTypeArguments.FirstOrDefault()?.BaseType == typeof(ViewModel) && x.CanWrite).SelectMany(x => (IEnumerable<ViewModel>)x.GetValue(this)).ToList();
            if (children.Contains(target)) return this;
            else return children.Select(x => x.GetParent(target)).FirstOrDefault(x => x != null);
        }

        public List<PropertyInfo> GetViewModelCollectionTypes(ViewModel target)
        {
            return target.GetType().GetProperties().Where(x => x.PropertyType.GenericTypeArguments.Length > 0 && x.PropertyType.GenericTypeArguments.First().IsSubclassOf(typeof(ViewModel))).ToList();
        }
        public void Read()
        {
            GetViewModelCollectionTypes(this).ForEach(x =>
            {
                var internalType = x.PropertyType.GenericTypeArguments.First();
                var add = x.PropertyType.GetMethod("Add");
                var clear = x.PropertyType.GetMethod("Clear");
                var actual = x.GetValue(this);
                var instance = actual ?? Activator.CreateInstance(x.PropertyType);

                void act(string s)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        clear?.Invoke(instance, Array.Empty<object>());
                        var listType = typeof(List<>).MakeGenericType(internalType);
                        var values = JsonSerializer.Deserialize(s, listType);
                        if (values is IList list)
                            foreach (var item in list)
                            {
                                if (item is ViewModel b) b.Read();
                                add?.Invoke(instance, new object[] { item });
                            };
                        x.SetValue(this, instance);
                    });
                }

                if (x.Name == "VariableSources")
                    Scraper.HttpRequest.ReadMany("VariableSource", _id, act);
                else if (_id != null)
                    Scraper.HttpRequest.ReadMany(internalType.Name, _id, act);
                else
                    Scraper.HttpRequest.ReadAll(internalType.Name, act);
            });
        }
        public void Delete()
        {
            var parent = Scraper.Manager.GetParent(this);
            var requests = new List<(string collectionName, string parentID)>();
            Scraper.ModalInstance.ShowMessage("Deleting...", "Deleting", false);
            void something(ViewModel model)
            {
                GetViewModelCollectionTypes(model).ForEach(x =>
                {
                    var name = x.PropertyType.GenericTypeArguments.FirstOrDefault()?.Name;
                    var collection = (ICollection)x.GetValue(model);
                    if (collection.Count > 0) requests.Add((name, model._id));
                    foreach (var item in collection) something((ViewModel)item);
                });
            }
            something(this);

            void deleteOneThenRead()
            {
                Scraper.HttpRequest.DeleteOne(GetType().Name, _id, () => Scraper.ModalInstance.ShowMessage("Deleting...", "Finished"));
                if (parent != null) parent.Read(); else Scraper.Manager.Read();
            }

            var count = 0.0;
            if (requests.Count > 0)
            {
                requests.ForEach(item =>
                {
                    Scraper.HttpRequest.DeleteMany(item.collectionName, item.parentID, () =>
                    {
                        count++;
                        if (count == requests.Count) deleteOneThenRead();
                        else
                            Scraper.ModalInstance.ShowMessage("Deleting...", "Deleting " + Math.Ceiling(requests.Count / count * 100) + "%", false);
                    });
                });
            }
            else deleteOneThenRead();
        }
        public void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public event PropertyChangedEventHandler PropertyChanged;
    }
    [System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    sealed class PreventDefaultAttribute : Attribute { }
}
