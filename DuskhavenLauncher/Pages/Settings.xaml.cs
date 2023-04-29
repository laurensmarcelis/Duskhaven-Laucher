using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DuskhavenLauncher.Pages
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        public bool isOpen = false;
        public MainWindow MainWindow { get; set; }
        public Settings()
        {
            InitializeComponent();
        }

        private void Image_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SlideOut();
        }

        public void SlideIn()
        {

            Storyboard slideIn = (Storyboard)FindResource("SlideIn");
            slideIn.Begin();
            isOpen = true;
        }

        public void SlideOut()
        {
            Storyboard SlideOut = (Storyboard)FindResource("SlideOut");
            SlideOut.Begin();
            isOpen = false;
        }

        private void Addons_folder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string addonsPath = Path.Combine(Directory.GetCurrentDirectory(), "Interface", "AddOns");
            if (Directory.Exists(addonsPath))
            {
                Process.Start(addonsPath);
            }
            else
            {
                MessageBox.Show($"seems there is no installation or just missing the addons folder");
            }

        }

        private void Install_folder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Process.Start(Directory.GetCurrentDirectory());
        }

        private void Windowed_fix_Click(object sender, RoutedEventArgs e)
        {
            string configFile = Path.Combine(Directory.GetCurrentDirectory(), "WTF", "Config.wtf");
            if (File.Exists(configFile))
            {
                string windowLine = "SET gxWindow \"0\"";
                string setLine = "SET gxWindow \"1\"";
                string fileContent = File.ReadAllText(configFile);
                Console.WriteLine(fileContent.Contains(windowLine));
                if (fileContent.Contains(windowLine))
                {
                    fileContent = fileContent.Replace(windowLine, setLine);
                }
                else
                {
                    if (fileContent.Contains(setLine))
                    {
                        MessageBox.Show($"The config line for gxWindow is already set as \"1\" \npress start and just enjoy the game");
                        return;
                    }
                    else
                    {
                        File.AppendAllText(configFile, Environment.NewLine + setLine);
                        MessageBox.Show($"Config set \nstart the game and enjoy");
                        return;
                    }

                }
                File.WriteAllText(configFile, fileContent);
                MessageBox.Show($"Config set \nstart the game and enjoy");
            }
            else
            {
                MessageBox.Show($"can't find a config file, should be in {configFile}");
            }
        }

        private void Cache_fix_Click(object sender, RoutedEventArgs e)
        {
            string cachePath = Path.Combine(Directory.GetCurrentDirectory(), "Cache");
            // string accountCache = Path.Combine(Directory.GetCurrentDirectory(), "WTF", "Account");
            // string[] cacheList = { cachePath, accountCache };

            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
                MessageBox.Show($"Removed the cache folder at: {cachePath}");
            }
            else
            {
                MessageBox.Show($"No cache to clear");
            }
        }

        private async void Verify_PatchZ_Click(object sender, RoutedEventArgs e)
        {
            await MainWindow.CheckSignature("patch-Z.mpq");

        }
    }
}
