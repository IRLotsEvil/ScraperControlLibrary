using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ScraperControlLibrary
{
    /// <summary>
    /// Used to determine where each variable is converted
    /// </summary>
    public enum MethodType { GET, POST }

    /// <summary>
    /// Simple http protocol
    /// </summary>
    public enum Protocol { http, https }

    /// <summary>
    /// Variable type for templated variables
    /// </summary>
    public enum VariableType { String, Boolean, Incremental }

    /// <summary>
    /// Controller that contains all of the structure for updating and retrieving definitons from the database and sending the requests to start scraping
    /// </summary>
    public class ScraperManager : ViewModel
    {
        /// <summary>
        /// A collection of scraper site profiles
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ScraperSiteProfile> ScraperSiteProfiles { get; set; } = new();
    }

    /// <summary>
    /// A profile for scraping a specific site (needs to be updated with route/page scraping?)
    /// </summary>
    [ScraperDisplayName("Site Profile")]
    public class ScraperSiteProfile : ViewModel
    {
        /// <summary>
        /// Host address e.g google.com, 127.0.0.1
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// The protocol this address has
        /// </summary>
        public Protocol Protocol { get; set; }

        /// <summary>
        /// A collection of templated variables for searching
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<TemplateVariable> TemplateVariables { get; set; } = new();

        /// <summary>
        /// A collection of scraper search profiles
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ScraperSearchProfile> ScraperSearchProfiles { get; set; } = new();

    }

    /// <summary>
    /// A scraping profile for a custom search
    /// </summary>
    [ScraperDisplayName("Search Profile")]
    public class ScraperSearchProfile : ViewModel
    {
        /// <summary>
        /// A collection of query collection nodes for querying a web page
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<QueryCollectionNode> Queries { get; set; } = new();

        /// <summary>
        /// A collection of stored values for the templated variables
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<TemplateVariable> VariableSources { get; set; } = new();

        /// <summary>
        /// A delay to query after the page has loaded for when a page is dealyed in rednering or the information in the page hasn't loaded dynamically
        /// </summary>
        public double QueryDelay { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    [ScraperDisplayName("Templated Variable")]
    public class TemplateVariable : ViewModel
    {
        [IgnoreCarrier]
        public string TemplateID { get; set; }
        [IgnoreCarrier]
        public VariableType VariableType { get; set; }
        public MethodType MethodType { get; set; }
        public bool IsRequired { get; set; } = true;
        [IgnoreCarrier]
        public bool UseVariable { get => Get(); set => Set(value, true); }
        [IgnoreCarrier]
        public object Value
        {
            get => Get();
            set
            {
                if (value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String) Set(element.GetString());
                    else if (element.ValueKind == JsonValueKind.Number) Set(element.GetDouble());
                    else if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) Set(element.GetBoolean());
                }
                else Set(value);
            }
        }
        [IgnoreCarrier(nameof(VariableType), VariableType.Incremental)]
        public double Increments { get; set; }
        [IgnoreCarrier(nameof(VariableType), VariableType.Incremental)]
        public double Maximum { get; set; }
        [IgnoreCarrier(nameof(VariableType), VariableType.Incremental)]
        public double Minimum { get; set; }
        [IgnoreCarrier(nameof(VariableType), VariableType.Incremental)]
        public bool IsIndex { get; set; } = false;

        public TemplateVariable() { }
        public TemplateVariable(VariableType variableType)
        {
            VariableType = variableType;
            Value = variableType switch { VariableType.Boolean => true, VariableType.Incremental => 0, VariableType.String => "", _ => null };
        }
    }
    [ScraperDisplayName("Variable")]
    public class VariableDefined : ViewModel
    {
        public string ProfileID { get; set; }
        public string TemplateID { get; set; }
        public object Value { get; set; }
        public bool UseVariable { get; set; }
    }
    [ScraperDisplayName("Collection Node")]
    public class QueryCollectionNode : ViewModel
    {
        public string Query { get; set; }
        public ObservableCollection<QueryElementNode> Nodes { get; set; }
    }
    [ScraperDisplayName("Element Node")]
    public class QueryElementNode : ViewModel
    {
        public string Query { get; set; }
        [IgnoreCarrier]
        public bool IsUnique { get => Get(); set => Set(value); }
        [IgnoreCarrier]
        public bool IsTopLevel { get; set; }
        [JsonIgnore]
        public ObservableCollection<QueryElementNode> Children { get; set; }
        [JsonIgnore]
        public ObservableCollection<QueryAttributeNode> Attributes { get; set; }
    }
    [ScraperDisplayName("Attribute Node")]
    public class QueryAttributeNode : ViewModel
    {
        public string AttributeNode { get; set; }
    }
}
