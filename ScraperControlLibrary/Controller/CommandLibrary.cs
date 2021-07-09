using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ScraperControlLibrary.Controller
{
    public static class CommandLibrary
    {
        public static RelayCommand CloseLayer => new(x => Scraper.ModalInstance.CloseModal());
        public static RelayCommand AddEditViewModel => new(x => Scraper.ModalInstance.ShowAddEdit(x));
        public static RelayCommand NewSiteProfile => new(x => Scraper.ModalInstance.ShowAddEdit(typeof(ScraperSiteProfile)));
        public static RelayCommand NewSearchProfile => new(x => Scraper.ModalInstance.ShowAddEdit(typeof(ScraperSearchProfile), x));
        public static RelayCommand NewStringTemplateVariable => new(x => Scraper.ModalInstance.ShowAddEdit(new TemplateVariable(VariableType.String), x));
        public static RelayCommand NewBooleanTemplateVariable => new(x => Scraper.ModalInstance.ShowAddEdit(new TemplateVariable(VariableType.Boolean), x));
        public static RelayCommand NewIncrementalTemplateVariable => new(x => Scraper.ModalInstance.ShowAddEdit(new TemplateVariable(VariableType.Incremental), x));
        public static RelayCommand<ViewModel> DeleteItem => new(x => x.Delete());
        public static RelayCommand<ScraperSearchProfile> NewCollectionNode => new(x => Scraper.ModalInstance.ShowAddEdit(typeof(QueryCollectionNode), x));
        public static RelayCommand OpenUpdateLayer => new(x =>
        {
            if (Scraper.LayerInstance.IsUpdateLayerOpen)
                Scraper.LayerInstance.IsUpdateLayerOpen = false;
            else
                Scraper.LayerInstance.IsUpdateLayerOpen = true;
        });
        public static RelayCommand NewElementNode => new(x =>
        {
            if (x is QueryElementNode qen && qen.Attributes.Count > 0)
                Scraper.ModalInstance.ShowMessage("Error", "Can't add a child to a node with attributes", true, 300, 150);
            else if (x is QueryCollectionNode QCN)
            {
                if (QCN.Nodes.Count == 0)
                    Scraper.ModalInstance.ShowAddEdit(new QueryElementNode { IsTopLevel = true, IsUnique = true }, x);
                else
                    Scraper.ModalInstance.ShowAddEdit(new QueryElementNode { IsTopLevel = true }, x);
            }
            else
            {
                Scraper.ModalInstance.ShowAddEdit(typeof(QueryElementNode), x);
            }
        });


        public static RelayCommand<QueryElementNode> MakeUnique => new(x =>
        {
            var parent = (QueryCollectionNode)Scraper.Manager.GetParent(x);
            var changes = parent.Nodes.Select(y =>
            {
                y.IsUnique = (y == x);
                return y;
            });
            Scraper.HttpRequest.Update(x.GetType().Name, changes.ToList(), () =>
            {
                Scraper.ModalInstance.ShowMessage("Error", "It's Unique", true, 300, 150);
                parent.Read();
            });
        });

        public static RelayCommand<QueryElementNode> NewAttributeNode => new(x =>
        {
            if (x.Children.Count > 0)
                Scraper.ModalInstance.ShowMessage("Error", "Can't add an attribute to a node with children", true, 300, 150);
            else
                Scraper.ModalInstance.ShowAddEdit(typeof(QueryAttributeNode), x);
        });
        public static RelayCommand<ScraperSearchProfile> UpdateVariables => new(x =>
        {
            var httprequest = Scraper.HttpRequest;
            var Add = x.VariableSources.Where(y => y._id == null).Select(y => new VariableDefined { _id = y._id, UseVariable = y.UseVariable, Value = y.Value, TemplateID = y.TemplateID, ProfileID = x._id }).ToList();
            var Update = x.VariableSources.Where(y => y._id != null).Select(y => new VariableDefined { _id = y._id, UseVariable = y.UseVariable, Value = y.Value, TemplateID = y.TemplateID, ProfileID = x._id }).ToList();
            Scraper.ModalInstance.ShowMessage("Adding", "Adding new variables", false, 300, 150);
            httprequest.Create(typeof(VariableDefined).Name, Add, () =>
            {
                Scraper.ModalInstance.ShowMessage("Updating", "Updating old variables", false, 300, 150);
                httprequest.Update(typeof(VariableDefined).Name, Update, () => Scraper.ModalInstance.ShowMessage("Finished", "Completed!", true, 300, 150));
            });
        });
        public static RelayCommand DeconstructURL => new(x => Scraper.ModalInstance.ShowURLDeconstructor());
        public static RelayCommand<URLDeconstructor> AddURL => new(x =>
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
            Scraper.ModalInstance.ShowMessage("Adding", "Creating new Site Profile", false, 300, 150);
            Scraper.HttpRequest.CreateThenGetIDs(typeof(ScraperSiteProfile).Name, SiteProfile, id =>
            {
                var withId = Variables.Select(va => new TemplateVariable { MethodType = va.MethodType, Name = va.Name, VariableType = va.VariableType, ParentID = id, Value = va.Value });
                Scraper.ModalInstance.ShowMessage("Adding", "Adding new variables", false, 300, 150);
                Scraper.HttpRequest.Create(typeof(TemplateVariable).Name, withId, () =>
                {
                    Scraper.ModalInstance.ShowMessage("Adding", "Creating default Search Profile", true, 300, 150);
                    Scraper.HttpRequest.Create(typeof(ScraperSearchProfile).Name, new ScraperSearchProfile { ParentID = id, Name = "Asserted Profile", QueryDelay = 3000 }, () =>
                    {
                        Scraper.ModalInstance.ShowMessage("Done!", "Finished!", true, 300, 150);
                        Scraper.Manager.Read();
                    });
                });
            });
        });
        public static RelayCommand<ScraperSearchProfile> RunSearch => new(x =>
        {
            var site = (ScraperSiteProfile)Scraper.Manager.GetParent(x);
            Scraper.HttpRequest.BeginScraping(site.Protocol, site.Hostname, x);
        });
        public static RelayCommand<ScraperSearchProfile> RunIndexSearch => new(x =>
        {
            var site = (ScraperSiteProfile)Scraper.Manager.GetParent(x);
            Scraper.HttpRequest.BeginScraping(site.Protocol, site.Hostname, x, true);
        });
        public static RelayCommand AddWatcher => new(x =>
        {
            if (x is QueryElementNode qen || x is QueryAttributeNode qan)
            {
                
            }
        });
        public static RelayCommand RemoveWatcher => new(x =>
        {
            
        });
        public static RelayCommand<QueryCollectionNode> GetResults => new(x=> 
        {
            Scraper.ModalInstance.ShowMessage("Retrieving...","Getting Results...",false);
            Scraper.HttpRequest.ReadAll(x.Name,s=> 
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(s);
                object returnItem(JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                        return je.EnumerateArray().Select(y=> 
                        {
                            if (y.ValueKind is JsonValueKind.Array or JsonValueKind.Object) return new JsonCollectionContainer { Values = (List<JsonContainer>)returnItem(y) };
                            else return (JsonContainer)new JsonValueContainer { Value = y.GetString() };
                        }).ToList();
                    else if (je.ValueKind == JsonValueKind.Object)
                    {
                        return je.EnumerateObject().Select(y =>
                        {
                            if (y.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) return new JsonPropertyCollectionContainer { Name = y.Name, Values = (List<JsonContainer>)returnItem(y.Value) };
                            else return (JsonContainer)new JsonPropertyValueContainer { Name = y.Name, Value = y.Value.GetString() };
                        }).ToList();
                    }
                    else return new JsonValueContainer { Value = je.GetString() };
                }

                var results = (List<JsonContainer>)returnItem(doc);

                var t = results.First().GetType();

                Scraper.ModalInstance.Width = new System.Windows.GridLength(600);
                Scraper.ModalInstance.Height = new System.Windows.GridLength(400);
                Scraper.ModalInstance.Name = "Results";
                Scraper.ModalInstance.Content = new JsonResults { Results = results };
                Scraper.ModalInstance.CanClose = true;
            });
        });
    }

    public class JsonContainer { }
    public class JsonPropertyContainer : JsonContainer { public string Name { get; set; } }
    public class JsonPropertyCollectionContainer : JsonPropertyContainer { public List<JsonContainer> Values { get; set; } }
    public class JsonPropertyValueContainer : JsonPropertyContainer { public string Value { get; set; } }
    public class JsonValueContainer : JsonContainer { public string Value { get; set; }}
    public class JsonCollectionContainer : JsonContainer { public List<JsonContainer> Values { get; set; } }
}

