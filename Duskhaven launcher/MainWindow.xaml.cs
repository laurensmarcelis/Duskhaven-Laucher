
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Duskhaven_launcher.Pages;
using System.Web.Script.Serialization;

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

        private string rootPath;
        private string clientZip;
        private string gameExe;
        private string dlUrl;
        private List<Item> fileList = new List<Item> ();
        private List<string> fileUpdateList = new List<string>();
        private LauncherStatus _status;
        private string uri = "https://duskhavenfiles.dev/";
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                setButtonState();
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
            gameExe = Path.Combine(rootPath, "wow.exe");
            clientZip = Path.Combine(rootPath, "WoW%203.3.5.zip");


        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {

            Assembly assembly = Assembly.GetExecutingAssembly();
            Version assemblyVersion = assembly.GetName().Version;
            AddActionListItem($"Launcher version: {assemblyVersion.ToString()}");
            if (File.Exists(Path.Combine(rootPath, "backup-launcher.exe")))
            {
                File.Delete(Path.Combine(rootPath, "backup-launcher.exe"));
            }

            if(getLauncherVersion())
            {
                //getNews();
                setButtonState();
                CheckForUpdates();
            }
            
        }
        private bool getLauncherVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version assemblyVersion = assembly.GetName().Version;
            Console.WriteLine($"Assembly version: {assemblyVersion}");

            // Replace these values with your own
            string owner = "laurensmarcelis";
            string repo = "Duskhaven-Launcher";
            
            // Get the latest release information from GitHub API
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            HttpWebRequest apiRequest = WebRequest.CreateHttp(apiUrl);
            apiRequest.UserAgent = "HttpClient";
            apiRequest.Accept = "application/vnd.github.v3+json";
            apiRequest.Method = "GET";
            HttpWebResponse apiResponse = (HttpWebResponse)apiRequest.GetResponse();
            StreamReader streamReader = new StreamReader(apiResponse.GetResponseStream());
            
            string apiResponseString = streamReader.ReadToEnd();
            try
            {
                // Deserialize the JSON string
                var startTag = "\"tag_name\":\"";
                var endTag = "\",";
                var startIndex = apiResponseString.IndexOf(startTag) + startTag.Length;
                var endIndex = apiResponseString.IndexOf(endTag, startIndex);
                var tagName = apiResponseString.Substring(startIndex, endIndex - startIndex);
                Console.WriteLine($"{tagName}");    

                if (tagName == assemblyVersion.ToString())
                {
                    return true;
                }
                else
                {
                    AddActionListItem($"Launcher out of date, newest version is {tagName}, your version is {assemblyVersion.ToString()}");
                    Status = LauncherStatus.launcherUpdate;

                    var startdlUrl = "\"browser_download_url\":\"";
                    var enddlUrl = "\"}],";
                    var startIndexdlUrl = apiResponseString.IndexOf(startdlUrl) + startdlUrl.Length;
                    var endIndexdlUrl = apiResponseString.IndexOf(enddlUrl, startIndexdlUrl);
                    dlUrl = apiResponseString.Substring(startIndexdlUrl, endIndexdlUrl - startIndexdlUrl);
                    return false;
                }

                // Use the deserialized object
                // ...
            }
            catch (Exception ex)
            {
                // Log the error
                MessageBox.Show($"Error checking for game updates:{ex}");
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
            string backupExePath = Path.Combine(rootPath,"backup-launcher.exe"); ; // Specify the path to save the backup file
            File.Move(oldExePath, backupExePath) ;
            // Code to close the application
            Close();

            // Wait for the application to exit
            while (Application.Current != null && Application.Current.MainWindow != null)
            {
                Thread.Sleep(100); // Wait for 0.1 seconds
            }
            // Replace the old executable file with the new one
            File.Move(newExePath, oldExePath);
            // Launch the new executable
            Process.Start(oldExePath);

        }
        private void CheckForUpdates()
        {
            AddActionListItem("Checking for valid WoW 3.3.5 installation...");
            if (!File.Exists(getFilePath("common.MPQ")) && !File.Exists(getFilePath("common-2.MPQ"))) {
                AddActionListItem("No WoW installation found");
                
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
                    fileList.Add(new Item { Name = match.Groups["name"].ToString(), Date = DateTime.Parse(match.Groups["date"].ToString()) });;
                }
            }
    
            foreach (Item file in fileList)
            {
                Console.WriteLine(file.Name,file.Date);
                long remoteFileSize = 0;
                long localFileSize = 0;

                /* Later to check etag
               var req = (HttpWebRequest)WebRequest.Create($"{uri}{file}");
               req.Method = "HEAD";
               req.MaximumResponseHeadersLength = int.MaxValue; // Set maximum response header length
               var res = (HttpWebResponse)req.GetResponse())
               var etag = res.Headers["ETag"];
               Console.WriteLine(etag);*/

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
                if (File.Exists(getFilePath(file.Name)))
                {
                    localFileSize = new FileInfo(getFilePath(file.Name)).Length;
                }
                else 
                {
                    fileUpdateList.Add(file.Name);
                    AddActionListItem($"{file.Name} is not installed, adding to download list");
                    continue;
                }
                Console.WriteLine($"{file.Name}: size local {localFileSize.ToString()} and from remote {remoteFileSize.ToString()}");
                Console.WriteLine(System.IO.File.GetLastWriteTime(getFilePath(file.Name)));
                if (remoteFileSize == localFileSize )
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

        private string getFilePath(string file)
        {
            string filePath = rootPath;
            if (file.Contains(".exe"))
            {
                filePath = Path.Combine(filePath, file);
            }
            if (file.Contains(".mpq") || file.Contains(".MPQ"))
            {
                if(!Directory.Exists(Path.Combine(filePath, "data")))
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

                DownloadFiles(fileUpdateList,0) ;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files:{ex}");
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
                    else if( index == files.Count -1)
                    {
                        Status = LauncherStatus.ready;
                        VersionText.Text = "Ready to enjoy Duskhaven";
                    }
                }
                else
                {
                    MessageBox.Show($"Error downloading game files:{e.Error}");
                    AddActionListItem($"Error downloading {files[index]}");
                }
            };
            Console.WriteLine($"{uri}{files[index]}");
            AddActionListItem($"Downloading {files[index]}");
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompleteCallback);
            webClient.DownloadFileAsync(new Uri($"{uri}{files[index]}"), getFilePath(files[index]), files[index]);
           
        }


        private void DownloadGameCompleteCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                AddActionListItem($"Installing {e.UserState}");

            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }
        }

        private async void getNews()
        {
            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://duskhaven-news.glitch.me/");
                client.DefaultRequestHeaders.Clear();
                //Define request data format
                client.DefaultRequestHeaders.Add("User-Agent", "Other");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync("");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    try
                    {
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        dynamic news = serializer.Deserialize<dynamic[]>(result);
                        Console.Write(news);

                        foreach (var newsItem in news)
                        {
                            NewsList.Text += $"{newsItem["channelName"]}:\n {newsItem["content"]}\n\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error parsing JSON: " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
        private void DownloadWotlkClientCompleteCallback(object sender, AsyncCompletedEventArgs e)
        {
            AddActionListItem($"Installing WotLK 3.3.5 client");
            VersionText.Text = "Extracting WotLK files to directory...";
            try
            {
                ZipFile.ExtractToDirectory(clientZip, rootPath);
                File.Delete(clientZip);
                VersionText.Text = "Extracting Done...";
                AddActionListItem($"Installing done");
                CheckForUpdates();
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            string downloadedMBs = Math.Round(e.BytesReceived / 1024.0 / 1024.0, 0).ToString() + " MB";
            string totalMBs = Math.Round(e.TotalBytesToReceive / 1024.0 / 1024.0, 0).ToString() + " MB";

            // Displays the operation identifier, and the transfer progress.
            VersionText.Text = $"{(string)e.UserState}    downloaded {downloadedMBs} of {totalMBs} bytes. {e.ProgressPercentage} % complete...";
            dlProgress.Visibility = Visibility.Visible;
            dlProgress.Value = e.ProgressPercentage;
        }

        private void InstallgameClient()
        {
            try
            {
                AddActionListItem($"Downloading WotLK 3.3.5 client");
                WebClient webClient = new WebClient();
                Status = LauncherStatus.downloadingGame;
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadWotlkClientCompleteCallback);
                webClient.DownloadFileAsync(new Uri($"{uri}WoW%203.3.5.zip"), clientZip);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files:{ex}");
                throw;
            }

        }
        private void DLButton_Click(object sender, RoutedEventArgs e)
        {
            InstallgameClient();
            
        }
        private void setButtonState()
        {
            if (Status == LauncherStatus.ready || Status == LauncherStatus.installClient || Status == LauncherStatus.failed || Status == LauncherStatus.install || Status == LauncherStatus.launcherUpdate)
            {
                PlayButton.IsEnabled = true;
            } else
            {
                PlayButton.IsEnabled = false;
            }
        }

        private void AddonButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Application.Current.Shutdown();
            }
        }

        private void Register_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.duskhaven.net/account/register/");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/duskhaven");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if(SettingsPage.isOpen) {
                SettingsPage.SlideOut();
            }
            else
            {
                SettingsPage.SlideIn();
            }
            
        }
    }


    /* use when a version is available */
    struct LauncherVersion
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal LauncherVersion(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }

        internal LauncherVersion(string _version)
        {
            string[] _versionStrings = _version.Split('.');
            if (_versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(_versionStrings[0]);
            minor = short.Parse(_versionStrings[1]);
            subMinor = short.Parse(_versionStrings[2]);
        }

        internal bool IsDifferentThan(LauncherVersion _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }
}

class Item
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
}