
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Net.NetworkInformation;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.Threading.Tasks;
using FastRsync.Signature;
using FastRsync.Delta;
using FastRsync.Diagnostics;
using FastRsync.Core;
using System.Net.Http;
using System.Net.Http.Headers;
using DuskhavenLauncher.Helpers;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Security.Policy;
using System.Linq;
using static System.Net.WebRequestMethods;
using File = System.IO.File;

namespace DuskhavenLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate,
        checking,
        install,
        findClient,
        launcherUpdate,
    }
    enum ActionType
    {
        successAction,
        failAction,
        defaultAction,
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Stopwatch sw;
        public string rootPath;
        private string gameExe;
        private string tempDl;
        private string installPath;
        private string dlUrl;
        private List<Item> fileList = new List<Item>();
        private List<string> fileUpdateList = new List<string>();
        private LauncherStatus _status;
        private string uri = "http://65.109.128.248:8080/PatchFiles/";
        private ManualResetEvent dllLoadedEvent = new ManualResetEvent(false);
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
                        VersionText.Text = "Ready to enjoy Duskhaven";
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.install:
                        PlayButton.Content = "Install";
                        break;
                    case LauncherStatus.findClient:
                        PlayButton.Content = "Find WoW";
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
        }
       
        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            SettingsPage.MainWindow = this;
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
            string ipAddress = "51.75.147.219";
            int timeout = 1000;
            Ping pingSender = new Ping();
            int numberOfPings = 3;

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

        public async Task CheckSignature(string fileName)
        {
            var signatureBuilder = new SignatureBuilder();
            using (var basisStream = new FileStream(GetFilePath(fileName), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var signatureStream = new FileStream(Path.Combine(tempDl, fileName + ".sig"), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Console.WriteLine("creating signature");
                Action<int> myCallbackFunction = (progress) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        VersionText.Text = $"verifying patch-Z file | {progress}% complete...";
                        dlProgress.Visibility = Visibility.Visible;
                        dlProgress.Value = progress;
                    });
                };
                signatureBuilder.ChunkSize = 31744;
                signatureBuilder.ProgressReport = new PercentProgressReporter(myCallbackFunction);
                await signatureBuilder.BuildAsync(basisStream, new SignatureWriter(signatureStream));

            }
            Console.WriteLine("signature created");
            AddActionListItem($"patch-Z verified checking further actions...", ActionType.successAction);
            var sha = SHA256CheckSum(Path.Combine(tempDl, fileName + ".sig"));
            AddActionListItem($"SHA IS {sha}", ActionType.successAction);
            dynamic returned = await GetJson($"https://65.109.128.248:5000/Patchers/sha/{sha}");
            if (returned == null)
            {
                AddActionListItem($"We are creating a difference between your patch-Z and server", ActionType.successAction);
                Console.WriteLine("is null let's fuck");
                var returnedobj = await PostSig(fileName);
                if (returnedobj != null)
                {
                    Console.WriteLine(returnedobj.toString());
                }
            }
            else
            {
                Console.WriteLine("we good");
                if (!returned["isPatchZ"])
                {
                    AddActionListItem($"Downloading Patch-Z diff");
                    await ApplyPatch(GetFilePath(fileName), returned["deltaLink"]);
                }
                else
                {
                    AddActionListItem($"{fileName} is up to date, NO update required", ActionType.successAction);
                }


            }

        }

        private async Task ApplyPatch(string fileName, string url)
        {
            string pureFileName = Path.GetFileName(fileName);
            string deltaPath = Path.Combine(tempDl, pureFileName + ".rdiff");

            using (var client = new WebClient())
            {
                client.DownloadFileCompleted += (sender, e) =>
                {
                    if (e.Error == null)
                    {

                        AddActionListItem($"applying patch for {pureFileName}");
                        var delta = new DeltaApplier
                        {
                            SkipHashCheck = true
                        };
                        using (var basisStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var deltaStream = new FileStream(deltaPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var newFileStream = new FileStream(Path.Combine(tempDl, pureFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                        {
                            delta.Apply(basisStream, new BinaryDeltaReader(deltaStream, new ConsoleProgressReporter()), newFileStream);
                        }
                        File.Copy(Path.Combine(tempDl, pureFileName), fileName, true);
                        AddActionListItem($"patch applied for {pureFileName}", ActionType.successAction);
                        File.Delete(Path.Combine(tempDl, pureFileName));
                    }
                    else
                    {
                    }
                };

                client.DownloadProgressChanged += (sender, e) =>
                {
                    DownloadProgressCallback(sender, e, pureFileName);
                }; 
                await client.DownloadFileTaskAsync(new Uri(url), deltaPath);
            }
            
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

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Duskhaven-Launcher");
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("HttpClient");
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    dynamic githubData = JsonConvert.DeserializeObject(responseBody);

                    Version latestVersion = Version.Parse(githubData.tag_name.ToString());

                    if (assemblyVersion >= latestVersion)
                    {
                        AddActionListItem($"⚠We are generating a difference of the Patch-Z file, this will make the launcher hang for a few seconds⚠");
                        return true;
                    }
                    else
                    {
                        AddActionListItem($"Launcher out of date, newest version is {githubData.tag_name}, your version is {assemblyVersion}");
                        Status = LauncherStatus.launcherUpdate;
                        dlUrl = githubData.assets[0].browser_download_url;
                        return false;
                    }
                }
            }
            catch (HttpRequestException ex)
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
            else if (Status == LauncherStatus.findClient)
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
        private async void CheckForUpdates()
        {
            if (!Directory.Exists(tempDl))
            {
                Directory.CreateDirectory(tempDl);
            }

            AddActionListItem("Checking for valid WoW 3.3.5 installation...");
            if (!HasGameClient())
            {
                Status = LauncherStatus.findClient;
                AddActionListItem("No WoW 3.3.5 installation found", ActionType.failAction);
                return;
            }
            AddActionListItem("Valid WoW 3.3.5 installation found, let's check the Duskhaven files...", ActionType.successAction);
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
                    AddActionListItem($"{file.Name} is not installed, adding to download list", ActionType.failAction);
                    continue;
                }
                Console.WriteLine($"{file.Name}: size local {localFileSize.ToString()} and from remote {remoteFileSize.ToString()}");
                Console.WriteLine(System.IO.File.GetLastWriteTime(GetFilePath(file.Name)));
                


                if (remoteFileSize == localFileSize && !file.Name.Contains("exe"))
                {


                    AddActionListItem($"{file.Name} is up to date, NO update required", ActionType.successAction);
                    Console.WriteLine("The remote file and the local file have the same size.");
                }
                else if (remoteFileSize == localFileSize && file.Name.Contains("exe") && file.Date < System.IO.File.GetLastWriteTime(GetFilePath(file.Name)))
                {
                    AddActionListItem($"{file.Name} is up to date, NO update required", ActionType.successAction);
                    Console.WriteLine("The remote file and the local file have the same size.");
                }
                else
                {
                    if (file.Name.Contains("patch-Z"))
                    {
                        AddActionListItem($"checking {file.Name}, this may take a while...");
                        Console.WriteLine("we got a patch Z");
                        await CheckSignature(file.Name);
                        continue;
                    }
                    fileUpdateList.Add(file.Name);
                    AddActionListItem($"{file.Name} is out of date, adding to update list", ActionType.failAction);
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
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public string GetFilePath(string file)
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
        private void AddActionListItem(string action, ActionType actionType = ActionType.defaultAction)
        {
            string actionText = "";
            switch (actionType)
            {
                case ActionType.defaultAction:
                    actionText += "⚪";
                    break;
                case ActionType.failAction:
                    actionText += "❎";
                    break;
                case ActionType.successAction:
                    actionText += "✅";
                    break;
            }

            ActionList.Text += $"{actionText} {action}\n";
        }
        private void InstallGameFiles(bool _isUpdate)
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

        public void DownloadFiles(List<string> files, int index)
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
                    AddActionListItem($"Error downloading {files[index]}", ActionType.failAction);
                }
            };
            sw = Stopwatch.StartNew();
            AddActionListItem($"Downloading {files[index]}");

            webClient.DownloadProgressChanged += (sender, e) =>
            {
                DownloadProgressCallback(sender, e, files[index]);
            };
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

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = "HttpClient";
                request.ServerCertificateValidationCallback = delegate { return true; };
                if (url.Contains("git"))
                {
                    request.Accept = "application/vnd.github.v3+json";
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
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    var resp = (HttpWebResponse)ex.Response;
                    if (resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else
                    {
                        return "gofuckyourself";
                    }
                }
                else
                {
                    return "gofuckyourself";
                }

            }
            return result;
        }

        public async Task<dynamic> PostSig(string fileName)
        {
            dynamic result = null;
            try
            {
                AddActionListItem($"Requesting change from server");
                Console.WriteLine("we here in the hood creating a request");
                string url = @"https://65.109.128.248:5000/Patchers/api/upload";
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                    {
                        return true;
                    }
                };
                // Set the file path of the file to upload
                string filePath = Path.Combine(tempDl, fileName + ".sig");
                if (File.Exists(filePath))
                {
                    Console.WriteLine("we got it");
                }
                // Create the HTTP content for the file upload
                MultipartFormDataContent content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                content.Add(fileContent, "Files", Path.GetFileName(filePath));
                Console.WriteLine("WE GOT SOME OF THA GOOD CONTENT");
                Console.WriteLine(content);
                // Create the HTTP client and send the request
                HttpClient client = new HttpClient(handler);
                HttpResponseMessage response = await client.PostAsync(url, content);
                // Get the response content
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("File uploaded successfully!");
                }
                else
                {
                    Console.WriteLine("Error uploading file: " + responseContent);
                }

                string link = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response body: " + await response.Content.ReadAsStringAsync());
                AddActionListItem($"Requesting change from successful", ActionType.successAction);
                await ApplyPatch(GetFilePath(fileName), link);
                //JavaScriptSerializer serializer = new JavaScriptSerializer();
                //result = serializer.Deserialize<dynamic>(responseString);

            }
            catch (Exception ex)
            {
            }
            return result;
        }
        private async void GetNews()
        {
            try
            {
                dynamic news = await GetJson("https://duskhaven-news.glitch.me/changelog");
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

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e, string fileName)
        {
            string downloadedMBs = Math.Round(e.BytesReceived / 1024.0 / 1024.0, 0).ToString() + " MB";
            string totalMBs = Math.Round(e.TotalBytesToReceive / 1024.0 / 1024.0, 0).ToString() + " MB";
            //string speed = $"{e.BytesReceived / 1024 / 1024 / sw.Elapsed.TotalSeconds:F2} MB/s";
            // Displays the operation identifier, and the transfer progress.
            // SpeedText.Text = speed;
            VersionText.Text = $"{fileName} downloaded {downloadedMBs} of {totalMBs} | {e.ProgressPercentage} % complete...";
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
                    //System.Windows.MessageBox.Show($"Alrighty then we will install everything in {rootPath}");
                    Console.WriteLine(assemblyLocation);
                    string newLocation = Path.Combine(rootPath, assemblyName);
                    if (File.Exists(newLocation) && rootPath != folderDialog.SelectedPath)
                    {
                        File.Delete(newLocation);
                    }

                    AddActionListItem($"Checking if there is already a valid WoW 3.3.5 installation in {rootPath}");
                    if (HasGameClient())
                    {
                        AddActionListItem($"Good news everyone! there is a valid WoW 3.3.5 installation in {rootPath}", ActionType.successAction);
                        if (System.Windows.MessageBox.Show($"Good news everyone! there is a valid WoW 3.3.5 installation in {rootPath}\n I will go to there and open the folder for you but you will need to click me again to continue the installation process") == MessageBoxResult.OK)
                        {

                            if (!(assemblyLocation == Path.Combine(rootPath, assemblyName)))
                            {


                                File.Move(assemblyLocation, Path.Combine(rootPath, assemblyName));
                                Process.Start(rootPath);
                                Close();
                                while (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
                                {
                                    Thread.Sleep(100); // Wait for 0.1 seconds
                                }
                            }



                        }
                        return;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"No valid WoW 3.3.5 installation in {rootPath}.\n Please make sure you select a folder with a valid 3.3.5 wow installation before we can continue installing Duskhaven");
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
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
            }

        }
        private void SetButtonState()
        {
            if (Status == LauncherStatus.ready || Status == LauncherStatus.failed || Status == LauncherStatus.findClient || Status == LauncherStatus.install || Status == LauncherStatus.launcherUpdate)
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