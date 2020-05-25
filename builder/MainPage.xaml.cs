using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace builder
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            Process();
        }


        async void Process()
        {
            try
            {
                var builder = new WebsiteBuilder();

                string outPath = await builder.Build();

                await Show("Done! Output in " + outPath);
            }
            catch (Exception e)
            {
                await Show("Error: " + e.Message);
            }

            App.Current.Exit();
        }


        async Task Show(string message)
        {
            Debug.WriteLine(message);

            await new MessageDialog(message).ShowAsync();
        }
    }
}
