using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

namespace ScraperControlLibrary
{
    public class HttpRequestManager : INotifyPropertyChanged
    {

        /// <summary>
        /// A collection of messages from the server
        /// </summary>
        public ObservableCollection<MongoUpdate> ServerMessages
        {
            get => _ServerMessages;
            private set
            {
                if (_ServerMessages != value)
                {
                    _ServerMessages = value;
                    OnPropertyChanged();
                }
            }
        }
        private ObservableCollection<MongoUpdate> _ServerMessages = new();

        /// <summary>
        /// Definitions for the state of the server
        /// </summary>
        public enum ServerStatus { Connecting, Connected, Reconnecting, Closed };

        /// <summary>
        /// Type of TextSearch
        /// </summary>
        public enum TextSearchType { Contains, Equals, EndsWith, BeginsWith };

        /// <summary>
        /// Current Update Id 
        /// </summary>
        public int CurrentCount
        {
            get => _CurrentCount;
            set
            {
                if (_CurrentCount != value)
                {
                    _CurrentCount = value;
                    OnPropertyChanged();
                }
            }
        }
        private int _CurrentCount = 0;

        /// <summary>
        /// Host name of the server e.g IP, domain
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Port to connect to on the server
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// Name of the database to make changes to
        /// </summary>
        public string DataBaseName { get; set; }

        /// <summary>
        /// How often to check the server
        /// </summary>
        public int HealthCheckInterval { get; set; } = 200;

        /// <summary>
        /// How often to check the server
        /// </summary>
        public int UpdateInterval { get; set; } = 0;

        /// <summary>
        /// How long it takes for the health check to timeout
        /// </summary>
        public int HealthCheckTimeout { get; set; } = 1000;

        /// <summary>
        /// The current server status
        /// </summary>
        public ServerStatus Status
        {
            get => _Status;
            private set
            {
                if (_Status != value)
                {
                    _Status = value;
                    OnPropertyChanged();
                }
            }
        }
        private ServerStatus _Status = ServerStatus.Connecting;

        /// <summary>
        /// Shows whether the client is sending requests to the server
        /// </summary>
        public bool IsRequestingActive { get; private set; } = false;


        /// <summary>
        /// If the server isn't active, this is where our requests will stay until the server is available
        /// </summary>
        private Queue<(string, string, object, Action<string>, Action)> StoredRequests = new();

        private readonly Dictionary<string, string> AddressBook = new()
        {
            { "Ping", "pingAlive" },
            { "Update", "getUpdates" },
            { "Mongo", "mongo" },
            { "Scraper", "scraper" }
        };

        public HttpRequestManager(string hostname, string port, string database) => NewConnection(hostname,port,database);

        public HttpRequestManager() => StartFromSettings();

        public void StartFromSettings()
        {
            CurrentCount = (int)Scraper.SettingsManager["UpdateID"];
            NewConnection((string)Scraper.SettingsManager["HostName"], (string)Scraper.SettingsManager["Port"], (string)Scraper.SettingsManager["DataBase"]);
        }

        public void CommitToSettings()
        {
            Scraper.SettingsManager["UpdateID"] = CurrentCount;
            Scraper.SettingsManager["HostName"] = HostName;
            Scraper.SettingsManager["Port"] = Port;
            Scraper.SettingsManager["DataBase"] = DataBaseName;
        }

        private string GetAddress(string routeName, params string[] routes) => AddressBook.ContainsKey(routeName) ? string.Join("/", new List<string>() { "http://" + HostName + ":" + Port, AddressBook[routeName] }.Concat(routes)) : null;

        /// <summary>
        /// Method used to check the server is active, will cycle continuously until it's told to stop
        /// </summary>
        private void StartHealthCheck()
        {
            HttpWebRequest CreateRequest()
            {
                var req = HttpWebRequest.CreateHttp(GetAddress("Ping"));
                req.Timeout = HealthCheckTimeout;
                return req;
            }
            void ContinuePinging(IAsyncResult result = null)
            {
                if (IsRequestingActive)
                {
                    if (result == null)
                    {
                        Status = ServerStatus.Connecting;
                        var pingrequest = CreateRequest();
                        pingrequest.BeginGetResponse(ContinuePinging, pingrequest);
                    }
                    else
                    {
                        var pingrequest = (HttpWebRequest)result.AsyncState;
                        try
                        {
                            using var response = pingrequest.EndGetResponse(result);
                            Status = ServerStatus.Connected;
                            while (StoredRequests.Count > 0)
                            {
                                var (method, address, document, OnMessage, OnAction) = StoredRequests.Dequeue();
                                HttpHandler(method, address, document, OnMessage, OnAction);
                            }
                            Thread.Sleep(HealthCheckInterval);
                            pingrequest = CreateRequest();
                            pingrequest.BeginGetResponse(ContinuePinging, pingrequest);
                        }
                        catch (WebException e)
                        {
                            if (Status != ServerStatus.Connecting) Status = ServerStatus.Reconnecting;
                            pingrequest = CreateRequest();
                            pingrequest.BeginGetResponse(ContinuePinging, pingrequest);
                        }
                    }
                }
            }
            ContinuePinging();
        }

        /// <summary>
        /// Method used to handle web requests, if the server is inactive or a request fails it will be stored in the stored requests queue 
        /// and tried again on the next health check if the server is active
        /// </summary>
        /// <param name="method">HTTP Method</param>
        /// <param name="address">Address e.g http://example.com, http://localhost</param>
        /// <param name="document">Any object to deserialize including arrays</param>
        /// <param name="OnMessage">Invokes when the server responds with a json message</param>
        /// <param name="OnFinished">Invokes when the server is finished responding</param>
        private void HttpHandler(string method, string address, object document = null, Action<string> OnMessage = null, Action OnFinished = null)
        {
            void FinishResponse(IAsyncResult result)
            {
                var request = (HttpWebRequest)result.AsyncState;
                try
                {
                    using var response = request.EndGetResponse(result);
                    using var stream = new StreamReader(response.GetResponseStream());
                    var message = stream.ReadToEnd();
                    OnMessage?.Invoke(message);
                    OnFinished?.Invoke();
                }
                catch (WebException e)
                {
                    StoredRequests.Enqueue((method, address, document, OnMessage, OnFinished));
                }
            }
            if (IsRequestingActive)
            {
                if (Status == ServerStatus.Connected)
                {
                    var request = HttpWebRequest.CreateHttp(address);
                    request.Method = method;
                    if (document != null)
                    {
                        var doc = document.GetType() == typeof(string) ? (string)document : JsonSerializer.Serialize(document);
                        var encoding = Encoding.UTF8.GetBytes(doc);
                        request.ContentLength = encoding.Length;
                        request.ContentType = "application/json";
                        using var stream = request.GetRequestStream();
                        stream.Write(encoding, 0, encoding.Length);
                        stream.Flush();
                    }
                    request.BeginGetResponse(FinishResponse, request);
                }
                else StoredRequests.Enqueue((method, address, document, OnMessage, OnFinished));
            }
        }
        /// <summary>
        /// Synchronous version of the HttpHandler
        /// </summary>
        /// <param name="method">HTTP Method</param>
        /// <param name="address">Address e.g http://example.com, http://localhost</param>
        /// <param name="document">Any object to deserialize including arrays</param>
        /// <param name="HasMessage"></param>
        /// <returns>The result will be a string or null</returns>
        private string HttpHandler(string method, string address, object document = null, bool HasMessage = false)
        {
            var src = new TaskCompletionSource<string>();
            if (HasMessage)
                HttpHandler(method, address, document, message => src.SetResult(message));
            else
                HttpHandler(method, address, document, null, () => src.SetResult(null));
            return src.Task.Result;
        }

        /// <summary>
        /// Begin connecting to the server
        /// </summary>
        public void Start()
        {
            IsRequestingActive = true;
            Status = ServerStatus.Connecting;
            StartHealthCheck();
            GetUpdates();
        }

        /// <summary>
        /// Stop all requests and close the HttpRequestManager
        /// </summary>
        public void Stop()
        {
            IsRequestingActive = false;
            StoredRequests.Clear();
            Status = ServerStatus.Closed;
        }

        /// <summary>
        /// Starts the HttpRequestManager with the details for a new connection
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="port"></param>
        /// <param name="database"></param>
        public void NewConnection(string hostname, string port, string database)
        {
            if (IsRequestingActive) Stop();
            HostName = hostname;
            Port = port;
            DataBaseName = database;
            Start();
        }

        /// <summary>
        /// Insert a document or documents into a collection
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="document"></param>
        /// <param name="onfinished">A callback for when the request has finished</param>
        public void Create(string collectionName, object document, Action onfinished) =>
            HttpHandler("POST", GetAddress("Mongo", DataBaseName, collectionName), document, null, onfinished);

        /// <summary>
        /// Insert a document or documents into a collection
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="document"></param>
        /// <param name="onmessage">A callback for when the request has finished</param>
        public void CreateThenGetIDs(string collectionName, object document, Action<string> onmessage) =>
            HttpHandler("POST", GetAddress("Mongo", DataBaseName, collectionName), document, onmessage);

        /// <summary>
        /// Update a document or documents to the collection, if the document id does not exist or isn't present the document or documents will be inserted instead
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="document"></param>
        /// <param name="onfinished">A callback for when the request has finished</param>
        public void Update(string collectionName, object document, Action onfinished) =>
            HttpHandler("POST", GetAddress("Mongo", DataBaseName, collectionName), document, null, onfinished);

        /// <summary>
        /// Delete a single document based on it's ID
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="id">Id of the document</param>
        /// <param name="onfinished">A callback for when the request has finished</param>
        public void DeleteOne(string collectionName, string id, Action onfinished) =>
            HttpHandler("DELETE", GetAddress("Mongo", DataBaseName, collectionName) + "?_id=" + id, null, null, onfinished);

        /// <summary>
        /// Delete multiple documents from the collection based on a parent's ID
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="parentid">The parent's ID to search for</param>
        /// <param name="onfinished">A callback for when the request has finished</param>
        public void DeleteMany(string collectionName, string parentid, Action onfinished) =>
            HttpHandler("DELETE", GetAddress("Mongo", DataBaseName, collectionName) + "?ParentID=" + parentid, null, null, onfinished);

        /// <summary>
        /// Delete Multiple documents from all collections based on a parent's ID
        /// </summary>
        /// <param name="parentid">The parent's ID to search for</param>
        /// <param name="onfinished">A callback for when the request has finished</param>
        public void DeleteAll(string parentid, Action onfinished) =>
            HttpHandler("DELETE", GetAddress("Mongo", DataBaseName) + "?ParentID=" + parentid, null, null, onfinished);


        /// <summary>
        /// Reads a document from the collection with the given id
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="id">Id of the document</param>
        /// <param name="onmessage">A callback when that's invoked after the server has given a response</param>
        public void ReadOne(string collectionName, string id, Action<string> onmessage) =>
            HttpHandler("GET", GetAddress("Mongo", DataBaseName, collectionName) + "?id=" + id, null, onmessage);

        /// <summary>
        /// Reads multiple documents from a collection based on a parent's ID
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="parentid">The parent's ID to search for</param>
        /// <param name="onmessage">A callback when that's invoked after the server has given a response</param>
        public void ReadMany(string collectionName, string parentid, Action<string> onmessage) =>
            HttpHandler("GET", GetAddress("Mongo", DataBaseName, collectionName) + "?ParentID=" + parentid, null, onmessage);


        /// <summary>
        /// Reads all the documents from a collection
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <returns></returns>
        public string ReadAll(string collectionName) => HttpHandler("GET", GetAddress("Mongo", DataBaseName, collectionName), null, true);

        /// <summary>
        /// Reads all the documents from a collection with a callback when the server returns it's message
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="onmessage">A callback when that's invoked after the server has given a response</param>
        public void ReadAll(string collectionName, Action<string> onmessage) =>
            HttpHandler("GET", GetAddress("Mongo", DataBaseName, collectionName), null, onmessage);

        /// <summary>
        /// Applies aggregation to a collection using a pipeline and returns the results
        /// </summary>
        /// <param name="collectionName">Collection to do changes one</param>
        /// <param name="pipeline">A pipeline of commands to aggregate with</param>
        /// <param name="onmessage">A callback when that's invoked after the server has given a response</param>
        public void Aggregate(string collectionName, string pipeline, Action<string> onmessage) =>
            HttpHandler("PUT", GetAddress("Mongo", DataBaseName, collectionName), pipeline, onmessage);

        /// <summary>
        /// Start getting updates
        /// </summary>
        private void GetUpdates()
        {
            void Received(string message)
            {
                var item = JsonSerializer.Deserialize<MongoUpdateWrapper>(message);
                CurrentCount = item.count + 1;
                Scraper.SettingsManager["UpdateID"] = CurrentCount;
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    ServerMessages = new ObservableCollection<MongoUpdate>(item.updates.Reverse<MongoUpdate>().Concat(ServerMessages));
                    OnNewMongoUpdates(CurrentCount, item.updates);
                });
                Thread.Sleep(UpdateInterval);
                HttpHandler("GET", GetAddress("Update", DataBaseName) + "?id=" + CurrentCount + "&updateBlock=-1", null, Received);
            }
            HttpHandler("GET", GetAddress("Update", DataBaseName) + "?id=" + CurrentCount, null, Received);
        }

        /// <summary>
        /// Activate the scraper
        /// </summary>
        /// <param name="protocol"></param>
        /// <param name="hostName"></param>
        /// <param name="profile"></param>
        /// <param name="IsIndexSearch"></param>
        public void BeginScraping(Protocol protocol, string hostName, ScraperSearchProfile profile, bool IsIndexSearch = false)
        {
            var address = GetAddress("Scraper") + "/" + protocol + "/" + hostName;
            var gets = profile.VariableSources.Where(x => x.MethodType == MethodType.GET && (x.IsRequired || !x.IsRequired && x.UseVariable)).Select(x => x.Name + "=" + x.Value).ToList();
            var post = string.Join("&", profile.VariableSources.Where(x => x.MethodType == MethodType.POST && (x.IsRequired || !x.IsRequired && x.UseVariable)).Select(x => x.Name + "=" + x.Value));
            if (gets.Count > 0) address += "?" + string.Join("&", gets);
            var Indexer = profile.VariableSources.FirstOrDefault(x => x.IsIndex);
            if (IsIndexSearch && Indexer != null)
                HttpHandler("POST", address, new { Indexer, profile.Queries, profile.QueryDelay, PostData = post }, null);
            else
                HttpHandler("POST", address, new { profile.Queries, profile.QueryDelay, PostData = post }, null);

        }

        public void GetDocumentFromText(string collection, string value, Action<string> onMessage)
        {
            var pipeline = "[{\"$match\":{\"$expr\":{\"$function\":{\"lang\":\"js\",\"body\":\"function(t, v){ return JSON.stringify(t).includes(v);}\",\"args\":[\"$$ROOT\",\"" + value + "\"]}}}}]";

            Aggregate(collection, pipeline, onMessage);
        }

        public void GetDocumentFromProperty(string collection, string property, string value, TextSearchType searchType, Action<string> onMessage)
        {
            var Func =
                searchType == TextSearchType.Contains ? "function(p,v){return p.includes(v);}" :
                searchType == TextSearchType.Equals ? "function(p,v){return p === v;}" :
                searchType == TextSearchType.BeginsWith ? "function(p,v){return p.beginsWith(v);}" :
                searchType == TextSearchType.EndsWith ? "function(p,v){return p.endsWith(v);}" : null;
            var pipeline = "[{$match:{$expr:{$function:{lang:\"js\",body:\"" + Func + "\",args:[\"$" + property + "\",\"" + value + "\"]}}}}]";
            Aggregate(collection, pipeline, onMessage);
        }

        private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void OnNewMongoUpdates(int count, List<MongoUpdate> updates) =>
            NewMongoUpdates?.Invoke(this, new MongoUpdateEventArgs { CurrentIdPosition = count, MongoUpdates = updates });

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<MongoUpdateEventArgs> NewMongoUpdates;
    }
    public class MongoUpdateWrapper
    {
        public List<MongoUpdate> updates { get; set; }
        public int count { get; set; }
    }

    public class MongoUpdate
    {
        public string _id { get; set; }
        public string collection { get; set; }
        public string type { get; set; }
        public string doc_id { get; set; }
        public List<string> Added { get; set; }
        public List<string> Deleted { get; set; }
        public List<string> Updated { get; set; }
    }

    public class MongoUpdateEventArgs : EventArgs
    {
        public int CurrentIdPosition { set; get; }
        public List<MongoUpdate> MongoUpdates { set; get; }
    }
}
