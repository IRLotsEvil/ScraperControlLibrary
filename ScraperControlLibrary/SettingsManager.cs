using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace ScraperControlLibrary
{
    public class LocalFileStorage : IDictionary<string,object>
    {
        private readonly List<DictionaryEntry> items = new();

        public string FilesPath { get; set; }

        public ICollection<string> Keys => items.Select(x=>(string)x.Key).ToList();

        public ICollection<object> Values => items.Select(x => x.Value).ToList();

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public object this[string key] 
        { 
            get => TryGetValue(key, out var value) ? value : null;
            set => Add(key, value);
        }

        public LocalFileStorage(string path, Dictionary<string, object> Defaults)
        {
            FilesPath = path;
            if (!File.Exists(FilesPath))
            {
                foreach (var kv in Defaults) Add(kv.Key, kv.Value);
                CommitSettings();
            }
            else LoadSettings();

        }
        /// <summary>
        /// Records and commits all the settings to the json file
        /// </summary>
        public void CommitSettings()
        {
            var _Settings = items.Select(x => new Setting { Name = (string)x.Key, Value = x.Value.ToString(), TypeName = x.Value.GetType().AssemblyQualifiedName }).ToList();
            using var writer = File.CreateText(FilesPath);
            writer.Write(JsonSerializer.Serialize(_Settings));
        }

        /// <summary>
        /// Reads the json file, converts the settings to a readable state in the dictionary
        /// </summary>
        public void LoadSettings()
        {
            Clear();
            foreach(var _Setting in JsonSerializer.Deserialize<List<Setting>>(File.ReadAllText(FilesPath)))
                Add(_Setting.Name, Convert.ChangeType(_Setting.Value, Type.GetType(_Setting.TypeName)));
        }

        public void Add(string key, object value)
        {
            if (ContainsKey(key)) Remove(key);
            items.Add(new DictionaryEntry(key, value));
            CommitSettings();
        }
        public bool ContainsKey(string key)
        {
            return items.Select(x => x.Key).Contains(key);
        }

        public bool Remove(string key)
        {
            foreach (var item in items)
            {
                if((string)item.Key == key)
                {
                    items.Remove(item);
                    CommitSettings();
                    return true;
                }
            }
            return false;
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            foreach (var item in items)
            {
                if(key == (string)item.Key)
                {
                    value = item.Value;
                    return true;
                }
            }
            value = false;
            return false;
        }

        public void Add(KeyValuePair<string, object> item) => Add(item.Key, item.Value);

        public void Clear() => items.Clear();

        public bool Contains(KeyValuePair<string, object> item)
        {
            foreach (var i in items)
                if(item.Key == (string)i.Key && item.Value.Equals(i.Value))
                    return true;
            return false;
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, object> item) => Remove(item.Key);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        private class Setting
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string TypeName { get; set; }
        }
    }
}
