using System.Windows;
using System.Windows.Controls;

namespace ScraperControlLibrary
{
    /// <summary>
    /// Interaction logic for Scraper.xaml
    /// </summary>
    public partial class Scraper : UserControl
    {
        public static HttpRequestManager HttpRequest { get; set; } = new("192.168.1.194", "3211", "Playground");
        public static ScraperManager Manager { get; set; } = new();
        public static Modal ModalInstance { get; set; } = new();
        public Scraper()
        {
            InitializeComponent();
            Manager.Read();
        }
    }
}
