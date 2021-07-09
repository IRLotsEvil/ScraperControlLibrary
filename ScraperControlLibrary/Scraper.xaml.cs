using ScraperControlLibrary.Controller;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Collections.Generic;

namespace ScraperControlLibrary
{
    /// <summary>
    /// Interaction logic for Scraper.xaml
    /// </summary>
    public partial class Scraper : UserControl
    {
        public static LocalFileStorage SettingsManager { get; set; } = new LocalFileStorage("C:\\Users\\gobbe\\Documents\\Settings.json", new Dictionary<string, object>
        {
            { "HostName", "localhost" },
            { "Port", "3211" },
            { "DataBase", "Playground" },
            { "UpdateID", 0 }
        });
        public static HttpRequestManager HttpRequest { get; set; } = new();
        public static ScraperManager Manager { get; set; } = new();
        public static ModalController ModalInstance { get; set; } = new();
        public static LayerController LayerInstance { get; set; } = new();

        public Scraper()
        {
            InitializeComponent();
            Manager.Read();
        }
    }
}
