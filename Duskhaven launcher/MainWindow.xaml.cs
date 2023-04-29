
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Web.Script.Serialization;
using System.Net.NetworkInformation;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Configuration;
using System.Runtime.InteropServices;

namespace Duskhaven_launcher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate,
        checking,
        install,
        installClient,
        launcherUpdate,
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Stopwatch sw;
        private string rootPath;
        private string clientZip;
        private string gameExe;
        private string tempDl;
        private string installPath;
        private string dlUrl;
        private List<Item> fileList = new List<Item>();
        private List<string> fileUpdateList = new List<string>();
        private LauncherStatus _status;
        private string uri = "https://duskhavenfiles.dev/";
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                SetButtonState();
                switch (_status)
                {
                    case LauncherStatus.checking:
                        PlayButton.Content = "Checking For Updates";
                        break;
                    case LauncherStatus.ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.install:
                        PlayButton.Content = "Install";
                        break;
                    case LauncherStatus.installClient:
                        PlayButton.Content = "Install WoW";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading...";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                    case LauncherStatus.launcherUpdate:
                        PlayButton.Content = "Update Launcher";
                        break;
                    default:
                        break;
                }

            }

        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            installPath = rootPath;
            gameExe = Path.Combine(rootPath, "wow.exe");
            tempDl = Path.Combine(rootPath, "downloads");
            clientZip = Path.Combine(tempDl, "WoW%203.3.5.zip");


        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.Save();
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            Version assemblyVersion = assembly.GetName().Version;
            AddActionListItem($"Launcher version: {assemblyVersion.ToString()}");
            if (File.Exists(Path.Combine(rootPath, "backup-launcher.exe")))
            {
                File.Delete(Path.Combine(rootPath, "backup-launcher.exe"));
            }


            if (await GetLauncherVersion())
            {
                GetServerStatus();
                GetNews();
                SetButtonState();
                CheckForUpdates();
            }

        }
        private async void GetServerStatus()
        {
            string ipAddress = "51.75.147.219"; // replace with the IP address you want to check
            int timeout = 1000; // timeout in milliseconds
            Ping pingSender = new Ping();
            int numberOfPings = 3; // number of pings to send

            Uri imageUri = new Uri("pack://application:,,,/images/online.png");
            BitmapImage bitmapImage = new BitmapImage(imageUri);
            long totalRtt = 0;
            for (int i = 0; i < numberOfPings; i++)
            {
                PingReply reply = await pingSender.SendPingAsync(ipAddress, timeout);

                if (reply.Status == IPStatus.Success)
                {
                    totalRtt += reply.RoundtripTime;
                }
            }

            if (totalRtt > 0)
            {
                double avgRtt = Math.Round(totalRtt / (double)numberOfPings);
                Console.WriteLine($"Average RTT: {avgRtt} ms");
                Latency.Text = $"{avgRtt} ms";
            }
            else
            {
                imageUri = new Uri("pack://application:,,,/images/offline.png");
                bitmapImage = new BitmapImage(imageUri);


                Console.WriteLine("Unable to ping the IP address.");
            }

            ServerStatus.Source = bitmapImage;

            ServerStatus.Visibility = Visibility.Visible;
        }
        private async Task<bool> GetLauncherVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version assemblyVersion = assembly.GetName().Version;
            Console.WriteLine($"Assembly version: {assemblyVersion}");

            // Replace these values with your own
            string owner = "laurensmarcelis";
            string repo = "Duskhaven-Launcher";

            // Get the latest release information from GitHub API
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            try
            {
                dynamic githubData = await GetJson(apiUrl);
                var tagName = githubData["tag_name"];

                if (tagName == assemblyVersion.ToString())
                {
                    return true;
                }
                else
                {
                    AddActionListItem($"Launcher out of date, newest version is {tagName}, your version is {assemblyVersion.ToString()}");
                    Status = LauncherStatus.launcherUpdate;

                    dlUrl = githubData["assets"][0]["browser_download_url"];
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error checking for game updates:{ex}");
                Debug.WriteLine(ex.Message);
            }

            return false;

        }
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {

            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = rootPath;
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
            else if (Status == LauncherStatus.install)
            {
                InstallGameFiles(false);
            }
            else if (Status == LauncherStatus.installClient)
            {
                InstallgameClient();
            }
            else if (Status == LauncherStatus.launcherUpdate)
            {
                UpdateLauncher();
            }
        }
        private void UpdateLauncher()
        {
            // Download the new executable file
            string downloadUrl = dlUrl; // Specify the URL to download the new executable
            Console.WriteLine(downloadUrl);
            string newExePath = Path.Combine(rootPath, "temp.exe"); // Specify the path to save the downloaded file
            using (var client = new WebClient())
            {
                client.DownloadFile(downloadUrl, newExePath);
            }

            // Rename the old executable file
            string oldExePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            string backupExePath = Path.Combine(rootPath, "backup-launcher.exe"); ; // Specify the path to save the backup file
            File.Move(oldExePath, backupExePath);
            // Code to close the application
            Close();

            // Wait for the application to exit
            while (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
            {
                Thread.Sleep(100); // Wait for 0.1 seconds
            }
            // Replace the old executable file with the new one
            File.Move(newExePath, oldExePath);
            // Launch the new executable
            Process.Start(oldExePath);

        }
        private Boolean HasGameClient()
        {
            if (!File.Exists(GetFilePath("common.MPQ")) && !File.Exists(GetFilePath("common-2.MPQ")))
            {

                return false;
            }
            return true;
        }
        private void CheckForUpdates()
        {

            AddActionListItem("Checking for valid WoW 3.3.5 installation...");
            if (!HasGameClient())
            {
                AddActionListItem("No WoW 3.3.5 installation found");

                Status = LauncherStatus.installClient;
                return;
            }
            AddActionListItem("Valid WoW 3.3.5 installation found, let's check the Duskhaven files...");
            fileUpdateList.Clear();
            fileList.Clear();
            Status = LauncherStatus.checking;
            AddActionListItem("Checking local files");

            WebRequest request = WebRequest.Create(uri);
            WebResponse response = request.GetResponse();
            //Regex regex = new Regex("<a href=\"\\.\\/(?<name>.*(mpq|MPQ|exe))\">");
            Regex regex = new Regex("(?s)<tr\\b[^>]*>(.*?(\"\\.\\/(?<name>\\S*(mpq|MPQ|exe))\").*?(datetime=\\\"(?<date>\\S*)\\\").*?)<\\/tr>");
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string result = reader.ReadToEnd();

                MatchCollection matches = regex.Matches(result);
                if (matches.Count == 0)
                {
                    Console.WriteLine("parse failed.");
                    return;
                }

                foreach (Match match in matches)
                {
                    if (!match.Success) { continue; }
                    if (match.Groups["name"].ToString().Contains("DuskhavenLauncher")) { continue; }
                    fileList.Add(new Item { Name = match.Groups["name"].ToString(), Date = DateTime.Parse(match.Groups["date"].ToString()) }); ;
                }
            }

            foreach (Item file in fileList)
            {
                Console.WriteLine(file.Name, file.Date);
                long remoteFileSize = 0;
                long localFileSize = 0;

                // Get the size of the remote file
                var checkRequest = (HttpWebRequest)WebRequest.Create($"{uri}{file.Name}");

                checkRequest.Method = "HEAD";
                using (var checkResponse = checkRequest.GetResponse())
                {
                    if (checkResponse is HttpWebResponse httpResponse)
                    {


                        remoteFileSize = httpResponse.ContentLength;
                    }
                }

                // Get the size of the local file
                if (File.Exists(GetFilePath(file.Name)))
                {
                    localFileSize = new FileInfo(GetFilePath(file.Name)).Length;
                }
                else
                {
                    fileUpdateList.Add(file.Name);
                    AddActionListItem($"{file.Name} is not installed, adding to download list");
                    continue;
                }
                Console.WriteLine($"{file.Name}: size local {localFileSize.ToString()} and from remote {remoteFileSize.ToString()}");
                Console.WriteLine(System.IO.File.GetLastWriteTime(GetFilePath(file.Name)));
                if (remoteFileSize == localFileSize && !file.Name.Contains("exe"))
                {

                    AddActionListItem($"{file.Name} is up to date, NO update required");
                    Console.WriteLine("The remote file and the local file have the same size.");
                }
                else if (remoteFileSize == localFileSize && file.Name.Contains("exe") && file.Date < System.IO.File.GetLastWriteTime(GetFilePath(file.Name)))
                {
                    AddActionListItem($"{file.Name} is up to date, NO update required");
                    Console.WriteLine("The remote file and the local file have the same size.");
                }
                else
                {
                    fileUpdateList.Add(file.Name);
                    AddActionListItem($"{file.Name} is out of date, adding to update list");
                    Console.WriteLine($"{file.Name} is out of date and needs an update.");

                }

            }

            if (fileUpdateList.Count == 0)
            {
                Status = LauncherStatus.ready;
            }

            else if (fileUpdateList.Count == fileList.Count)
            {
                Status = LauncherStatus.install;
            }
            else
            {
                InstallGameFiles(true);
            }
        }

        public string SHA256CheckSum(string filePath)
        {
            using (SHA256 SHA256 = SHA256Managed.Create())
            {
                using (FileStream fileStream = File.OpenRead(filePath))
                    return Convert.ToBase64String(SHA256.ComputeHash(fileStream));
            }
        }

        private string GetFilePath(string file)
        {
            string filePath = rootPath;
            if (file.Contains(".exe"))
            {
                filePath = Path.Combine(filePath, file);
            }
            if (file.Contains(".mpq") || file.Contains(".MPQ"))
            {
                if (!Directory.Exists(Path.Combine(filePath, "data")))
                {
                    Directory.CreateDirectory(Path.Combine(filePath, "data"));
                }
                filePath = Path.Combine(filePath, "data", file);
            }
            if (file.Contains(".wtf"))
            {
                if (Directory.Exists(Path.Combine(filePath, "data", "enGB")))
                {
                    filePath = Path.Combine(filePath, "data", "enGB", file);
                }
                else if (Directory.Exists(Path.Combine(filePath, "data", "enUS")))
                {
                    filePath = Path.Combine(filePath, "data", "enUS", file);
                }
                else
                {
                    Directory.CreateDirectory(Path.Combine(filePath, "data", "enGB"));
                    filePath = Path.Combine(filePath, "data", "enGB", file);
                }
            }
            return filePath;
        }
        private void AddActionListItem(string action)
        {
            ActionList.Text += $"• {action}\n";
        }
        private void InstallGameFiles(bool _isUpdate, bool client = false)
        {
            try
            {
                if (_isUpdate)
                {
                    AddActionListItem($"Updating files");
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    AddActionListItem($"Installing files needed to play");
                    Status = LauncherStatus.downloadingGame;
                    //_onlineVersion = new Version(webClient.DownloadString("version file link"));

                }

                DownloadFiles(fileUpdateList, 0);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                System.Windows.MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }

        }

        private void DownloadFiles(List<string> files, int index)
        {
            WebClient webClient = new WebClient();
            webClient.DownloadFileCompleted += (sender, e) =>
            {
                if (e.Error == null)
                {

                    if (index < files.Count - 1)
                    {
                        DownloadFiles(files, index + 1);
                    }
                    else if (index == files.Count - 1)
                    {
                        Status = LauncherStatus.ready;
                        VersionText.Text = "Ready to enjoy Duskhaven";
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show($"Error downloading game files:{e.Error}");
                    AddActionListItem($"Error downloading {files[index]}");
                }
            };
            sw = Stopwatch.StartNew();
            AddActionListItem($"Downloading {files[index]}");
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompleteCallback);
            webClient.DownloadFileAsync(new Uri($"{uri}{files[index]}"), Path.Combine(tempDl, files[index]), files[index]);

        }


        private void DownloadGameCompleteCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {

                AddActionListItem($"Installing {e.UserState.ToString()}");
                File.Copy(Path.Combine(tempDl, e.UserState.ToString()), GetFilePath(e.UserState.ToString()), true);
                File.Delete(Path.Combine(tempDl, e.UserState.ToString()));
                sw.Stop();

            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                System.Windows.MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }
        }
        public async Task<dynamic> GetJson(string url)
        {
            dynamic result = null;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (url.Contains("git"))
            {
                request.UserAgent = "HttpClient";
                request.Accept = "application/vnd.github.v3+json";
                request.Method = "GET";

            }

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream);
                        string responseString = await reader.ReadToEndAsync();

                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        result = serializer.Deserialize<dynamic>(responseString);
                    }
                }
            }

            return result;
        }
        private async void GetNews()
        {
            try
            {
                dynamic news = await GetJson("https://duskhaven-news.glitch.me");
                foreach (var item in news)
                {
                    var sanitized = item["content"];
                    sanitized = Regex.Replace(sanitized, "@(everyone|here)", "To all users");

                    // Replace **text** with "text"
                    sanitized = Regex.Replace(sanitized, @"\*{2}(.*?)\*{2}", "$1");
                    sanitized = Regex.Replace(sanitized, @"_{2}(.*?)_{2}", "$1");
                    sanitized = Regex.Replace(sanitized, "<.*>", "");
                    Run channel = new Run($"{item["channelName"]}:\n");
                    channel.FontWeight = FontWeights.Bold;
                    Run content = new Run(sanitized + ":\n\n");
                    NewsList.Inlines.Add(channel);
                    NewsList.Inlines.Add(content);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error: " + ex.Message);
            }
        }
        private void DownloadWotlkClientCompleteCallback(object sender, AsyncCompletedEventArgs e)
        {
            AddActionListItem($"Installing WotLK 3.3.5 client");
            VersionText.Text = "Extracting WotLK files to directory...";
            Thread.Sleep(10);
            try
            {
                ZipFile.ExtractToDirectory(clientZip, rootPath);
                File.Delete(clientZip);
                VersionText.Text = "Extracting Done...";
                AddActionListItem($"Installing done");
                sw.Stop();
                string assemblyName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                CheckForUpdates();
                if(folderPath == rootPath)
                {
                    return;
                }
                if(System.Windows.MessageBox.Show($"Client is installed and will restart from the new location: {installPath}") == MessageBoxResult.OK)
                {
                    
                    Process.Start(rootPath);
                    Close();
                    File.Move(assemblyLocation, Path.Combine(rootPath, assemblyName));

                    while (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
                    {
                        Thread.Sleep(100); // Wait for 0.1 seconds
                    }
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                System.Windows.MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            string downloadedMBs = Math.Round(e.BytesReceived / 1024.0 / 1024.0, 0).ToString() + " MB";
            string totalMBs = Math.Round(e.TotalBytesToReceive / 1024.0 / 1024.0, 0).ToString() + " MB";
            string speed = $"{e.BytesReceived / 1024 / 1024 / sw.Elapsed.TotalSeconds:F2} MB/s";
            // Displays the operation identifier, and the transfer progress.
            // SpeedText.Text = speed;
            VersionText.Text = $"{(string)e.UserState} downloaded {downloadedMBs} of {totalMBs} | {e.ProgressPercentage} % complete...";
            dlProgress.Visibility = Visibility.Visible;
            dlProgress.Value = e.ProgressPercentage;
        }

        private void InstallgameClient()
        {
            try
            {
                AddActionListItem($"Where would you like to download and install Duskhaven?");
                var folderDialog = new FolderBrowserDialog();
                folderDialog.SelectedPath = rootPath;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    installPath = folderDialog.SelectedPath;
                    string assemblyName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
                    string assemblyLocation = Assembly.GetExecutingAssembly().Location;

                    
                    rootPath = installPath;
                    gameExe = Path.Combine(rootPath, "wow.exe");
                    tempDl = Path.Combine(rootPath, "downloads");
                    clientZip = Path.Combine(tempDl, "WoW%203.3.5.zip");
                    System.Windows.MessageBox.Show($"Alrighty then we will install everything in {rootPath}");
                    Console.WriteLine(assemblyLocation);
                    string newLocation = Path.Combine(rootPath, assemblyName);
                    if (File.Exists(newLocation) && rootPath != folderDialog.SelectedPath)
                    {
                        File.Delete(newLocation);
                    }

                    AddActionListItem($"Checking if there is already a valid WoW 3.3.5 installation in {rootPath}");
                    if(HasGameClient())
                    {
                        AddActionListItem($"Good news everyone! there is a valid WoW 3.3.5 installation in {rootPath}");
                        if (System.Windows.MessageBox.Show($"Good news everyone! there is a valid WoW 3.3.5 installation in {rootPath}\nLet's go there and continue") == MessageBoxResult.OK)
                        {
                            
                            File.Move(assemblyLocation, Path.Combine(rootPath, assemblyName));

                            while (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
                            {
                                Thread.Sleep(100); // Wait for 0.1 seconds
                            }

                        }
                        return;  
                    }

                    if (!Directory.Exists(tempDl))
                    {
                        Directory.CreateDirectory(tempDl);
                    }
                }
                else
                {
                    return;
                }

                AddActionListItem($"Downloading WotLK 3.3.5 client");
                sw = Stopwatch.StartNew();
                WebClient webClient = new WebClient();

                Status = LauncherStatus.downloadingGame;
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadWotlkClientCompleteCallback);
                webClient.DownloadFileAsync(new Uri($"{uri}WoW%203.3.5.zip"), clientZip);


            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                System.Windows.MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }

        }
        private void DLButton_Click(object sender, RoutedEventArgs e)
        {
            InstallgameClient();

        }
        private void SetButtonState()
        {
            if (Status == LauncherStatus.ready || Status == LauncherStatus.installClient || Status == LauncherStatus.failed || Status == LauncherStatus.install || Status == LauncherStatus.launcherUpdate)
            {
                PlayButton.IsEnabled = true;
            }
            else
            {
                PlayButton.IsEnabled = false;
            }
        }

        private void AddonButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (Status == LauncherStatus.downloadingGame || Status == LauncherStatus.downloadingUpdate)
                {
                    if (System.Windows.MessageBox.Show("Are you sure you want to close the launcher when it is downloading files? You will have to download the files again", "Launcher is downloading files!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        Close();

                    }
                    else
                    {
                        return;
                    }
                }
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void Register_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.duskhaven.net");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (Status == LauncherStatus.downloadingGame || Status == LauncherStatus.downloadingUpdate)
            {
                if (System.Windows.MessageBox.Show("Are you sure you want to close the launcher when it is downloading files? You will have to download the files again", "Launcher is downloading files!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Close();

                }
                else
                {
                    return;
                }
            }
            Close();

        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/duskhaven");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPage.isOpen)
            {
                SettingsPage.SlideOut();
            }
            else
            {
                SettingsPage.SlideIn();
            }

        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}

class Item
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
}