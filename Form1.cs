using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using MonkeModManager.SimpleJSON;

namespace MonkeModManager
{
    public partial class Form1 : Form
    {
        private WebView2 _webView;
        private string _installDirectory = @"";
        private const string DefaultOculusInstallDirectory = @"C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag";
        private const string DefaultSteamInstallDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag";
        private Dictionary<string, string> _download = [];
        private Dictionary<string, string> _location = [];
        private string _version = "1.1.1";
        public bool installing = false;
        public short CurrentVersion = 3;

        public Form1()
        {
            UpdateChecker();
            this.Icon = (Icon)new ComponentResourceManager(typeof(Form1)).GetObject("$this.Icon");
            this.Text = @"Monke Mod Manager";
            this.Width = 800;
            this.Height = 600;

            _webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_webView);

            this.Load += async (s, e) =>
            {
                await _webView.EnsureCoreWebView2Async();

                string html = LoadHtml("MonkeModManager.Index.html");
                string logoB64 = LoadLogo("MonkeModManager.logo.png");
                html = html.Replace("{{LOGO_B64}}", logoB64);

                _webView.NavigateToString(html);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _webView.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
                    PostRaw(@"{""version"":""" + _version + @"""}");
                    _installDirectory = Properties.Settings.Default.installDirectory;

                    if (Directory.Exists(DefaultSteamInstallDirectory) && string.IsNullOrEmpty(_installDirectory))
                        _installDirectory = DefaultSteamInstallDirectory;
                    else if (Directory.Exists(DefaultOculusInstallDirectory) && string.IsNullOrEmpty(_installDirectory))
                        _installDirectory = DefaultOculusInstallDirectory;
                    else if(!Directory.Exists(DefaultSteamInstallDirectory) && !Directory.Exists(DefaultOculusInstallDirectory) && string.IsNullOrEmpty(_installDirectory))
                        FindGameFolder();

                    PostPath(_installDirectory);
                    ConfigFix();
                };
            };
        }

        private void OnWebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            var node = JSON.Parse(e.WebMessageAsJson);
            if (node == null) return;

            string action = node["action"];
            switch (action)
            {
                case "browseFolder":
                    FindGameFolder();
                    break;

                case "checkMod":
                {
                    string mod = node["mod"];
                    string download = node["download"];
                    string location = node["location"];
                    _download[mod] = download;
                    _location[mod] = location;
                    break;
                }

                case "uncheckMod":
                {
                    string mod = node["mod"];
                    _download.Remove(mod);
                    _location.Remove(mod);
                    break;
                }

                case "installMods":
                {
                    if (installing) return;
                    installing = true;
                    UpdateStatus("Installing mods...");

                    foreach (var entry in _download)
                    {
                        string modName = entry.Key;
                        string url = entry.Value;

                        UpdateStatus($"Downloading... {modName}");
                        string fileName = Path.GetFileName(url);
                        byte[] data = DownloadFile(url);

                        UpdateStatus($"Installing... {modName}");

                        string targetDir = _installDirectory;

                        if (_location.TryGetValue(modName, out var modLocation) && !string.IsNullOrEmpty(modLocation))
                            targetDir = Path.Combine(_installDirectory, modLocation);
                        else
                        {
                            if (entry.Key != "BepInEx")
                            {
                                targetDir = Path.Combine(_installDirectory, @"BepInEx\plugins", Regex.Replace(modName, @"\s+", ""));
                                Directory.CreateDirectory(targetDir);
                            }
                            else
                                targetDir = _installDirectory;
                        }

                        if (Path.GetExtension(fileName).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            File.WriteAllBytes(Path.Combine(targetDir, fileName), data);

                            string legacyPluginPath = Path.Combine(_installDirectory, @"BepInEx\plugins", fileName);
                            if (File.Exists(legacyPluginPath)) File.Delete(legacyPluginPath);
                        }
                        else
                        {
                            UnzipFile(data, targetDir);
                        }
                    }

                    installing = false;
                    UpdateStatus("Idle");
                    break;
                }

                case "uninstallAll":
                {
                    UpdateStatus("Removing all mods...");
                    var confirm = MessageBox.Show("You are about to remove ALL of your mods.\nThey will not be recoverable.",
                        "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (confirm != DialogResult.OK) return;

                    string pluginPath = Path.Combine(_installDirectory, "BepInEx/plugins");
                    if (Directory.Exists(pluginPath))
                    {
                        try
                        {
                            Directory.Delete(pluginPath, true);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    UpdateStatus("Idle");
                    break;
                }

                case "openGameFolder":
                    if (!string.IsNullOrEmpty(_installDirectory) && Directory.Exists(_installDirectory))
                        Process.Start(_installDirectory);
                    break;

                case "openConfig":
                    if (!string.IsNullOrEmpty(_installDirectory) && Directory.Exists(_installDirectory))
                        Process.Start(Path.Combine(_installDirectory, "BepInEx/config"));
                    break;

                case "openModsFolder":
                    if (!string.IsNullOrEmpty(_installDirectory) && Directory.Exists(_installDirectory))
                        Process.Start(Path.Combine(_installDirectory, "BepInEx/plugins"));
                    break;
                case "discord":
                    Process.Start("https://discord.gg/b2MhDBAzTv");
                    break;
                case "mFile":
                    FindMMMFile();
                    break;
            }
        }

        private void PostRaw(string json)
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void PostPath(string path)
        {
            PostRaw($@"{{""path"":""{path.Replace(@"\", @"\\")}""}}");
        }

        private void UpdateStatus(string status)
        {
            PostRaw($@"{{""statusText"":""{status}""}}");
        }

        private static string LoadHtml(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }

        private static string LoadLogo(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using var ms = new MemoryStream();
            stream?.CopyTo(ms);
            return Convert.ToBase64String(ms.ToArray());
        }

        private async void FindGameFolder()
        {
            try
            {
                using var fileDialog = new OpenFileDialog();
                fileDialog.FileName = "Gorilla Tag.exe";
                fileDialog.Filter = @"Exe Files (.exe)|*.exe";
                fileDialog.FilterIndex = 1;

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = fileDialog.FileName;
                    if (Path.GetFileName(path).Equals("Gorilla Tag.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        _installDirectory = Path.GetDirectoryName(path);
                        PostPath(_installDirectory);
                        Properties.Settings.Default.installDirectory = _installDirectory;
                        Properties.Settings.Default.Save();
                        UpdateStatus("Game Folder Found!");
                        await Task.Delay(5000);
                        UpdateStatus("Idle");
                    }
                    else
                    {
                        MessageBox.Show("That's not Gorilla Tag! Try again.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        FindGameFolder();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private async void FindMMMFile()
        {
            try
            {
                using var fileDialog = new OpenFileDialog();
                fileDialog.Multiselect = false;
                fileDialog.Filter = @"MMM Files (.mmm)|*.mmm";
                fileDialog.FilterIndex = 1;

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = fileDialog.FileName;
                    if (Path.GetExtension(path).Equals(".mmm", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateStatus("Installing MMM File...");
                        await InstallMMMFile(Path.GetFullPath(path));
                        UpdateStatus("Idle");
                    }
                    else
                    {
                        MessageBox.Show("That's not an mmm file! Try again.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async Task InstallMMMFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("Please select a valid MMM file.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                var p = Path.Combine(Directory.GetCurrentDirectory(), @"temp");
                byte[] data = File.ReadAllBytes(path);
                if(!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"temp"))) Directory.CreateDirectory(p);
                UnzipFile(data, p);

                if (File.Exists(Path.Combine(p, "Info.json")))
                {
                    var f = JsonToDictionary(Path.Combine(p, "Info.json"));
                    

                    foreach (KeyValuePair<string, string> l in f)
                    {
                        switch (l.Key)
                        {
                            case "dll":
                                if(!Directory.Exists(Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}"))) 
                                    Directory.CreateDirectory(Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}"));
                                File.Copy(Path.Combine(p, "mod.dll"), Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}",  $"{l.Value}.dll"));
                                break;
                            case "download":
                                var file = DownloadFile(l.Value);
                                var fe = Path.GetFileName(l.Value);
                                var fee = Path.GetFileNameWithoutExtension(l.Value);
                                if (Path.GetExtension(fe).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    if(!Directory.Exists(Path.Combine(_installDirectory, $"BepInEx/plugins/{fee}"))) 
                                        Directory.CreateDirectory(Path.Combine(_installDirectory, $"BepInEx/plugins/{fee}"));
                                    File.WriteAllBytes(Path.Combine(_installDirectory, $"BepInEx/plugins/{fee}", fe!), file);
                                }
                                else
                                {
                                    if(!Directory.Exists(Path.Combine(_installDirectory, $"BepInEx/plugins/{fee}"))) 
                                        Directory.CreateDirectory(Path.Combine(_installDirectory, $"BepInEx/plugins/{fee}"));
                                    UnzipFile(file, Path.Combine(_installDirectory, $@"BepInEx/plugins/{fee}"));
                                }
                                break;
                            case "zip":
                                var john = File.ReadAllBytes(Path.Combine(p, "mod.zip"));
                                if(!Directory.Exists(Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}"))) 
                                    Directory.CreateDirectory(Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}"));
                                UnzipFile(john, Path.Combine(_installDirectory, $"BepInEx/plugins/{l.Value}"));
                                break;
                        }
                    }
                }
                Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), @"temp"), true);
            }
        }

        private void UnzipFile(byte[] data, string directory)
        {
            using var ms = new MemoryStream(data);
            using var zip = new Unzip(ms);
            zip.ExtractToDirectory(directory);
        }

        private static byte[] DownloadFile(string url)
        {
            using WebClient client = new WebClient();
            client.Proxy = null;
            return client.DownloadData(url);
        }

        private CookieContainer PermCookie;
        private string GetSite(string url)
        {
            PermCookie ??= new CookieContainer();
            HttpWebRequest RQuest = (HttpWebRequest)HttpWebRequest.Create(url);
            RQuest.Method = "GET";
            RQuest.KeepAlive = true;
            RQuest.CookieContainer = PermCookie;
            RQuest.ContentType = "application/x-www-form-urlencoded";
            RQuest.Referer = "";
            RQuest.UserAgent = "Monke-Mod-Manager";
            RQuest.Proxy = null;
            HttpWebResponse Response = (HttpWebResponse)RQuest.GetResponse();
            StreamReader Sr = new StreamReader(Response.GetResponseStream());
            string Code = Sr.ReadToEnd();
            Sr.Close();
            return Code;
        }

        private void UpdateChecker()
        {
            try
            {
                short version = Convert.ToInt16(GetSite("https://raw.githubusercontent.com/NgbatzYT/Monke-Mod-Manager/master/version"));
                if (version > CurrentVersion)
                {
                    string yes = Directory.GetCurrentDirectory();
                    MessageBox.Show("Update Available!", "Notice!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if(File.Exists(Path.Combine(yes, "Updater.exe")))
                        Process.Start(Path.Combine(yes, "Updater.exe"));
                    else
                    {
                        var eat = MessageBox.Show("The Updater isn't installed would you like to install it?", "Notice!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (eat == DialogResult.Yes)
                        {
                            var eated = DownloadFile("https://github.com/ngbatzyt/monke-mod-manager/releases/latest/download/Updater.exe");
                            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), @"Updater.exe"), eated);
                            Process.Start(Path.Combine(Directory.GetCurrentDirectory(), @"Updater.exe"));
                        }
                        else
                        {
                            Process.Start("https://github.com/ngbatzyt/monke-mod-manager/releases/latest/");
                        }
                    }
                    Environment.Exit(0);
                }
            } 
            catch(Exception e){MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}
        }
        
        private void ConfigFix()
        {
            if (!File.Exists(Path.Combine(_installDirectory, @"BepInEx\config\BepInEx.cfg")))
            {
                return;
            }

            string c = File.ReadAllText(Path.Combine(_installDirectory, @"BepInEx\config\BepInEx.cfg"));
            if (!c.Contains("HideManagerGameObject = false"))
            {
                return;
            }
               
            string e = c.Replace("HideManagerGameObject = false", "HideManagerGameObject = true");
            File.WriteAllText(Path.Combine(_installDirectory, @"BepInEx\config\BepInEx.cfg"), e);
        } 
        
        private static Dictionary<string, string> JsonToDictionary(string path)
        {
            var result = new Dictionary<string, string>();
    
            string jsonString = File.ReadAllText(path);
            JSONNode root = JSON.Parse(jsonString);

            if (root != null && root.IsObject)
            {
                var obj = root.AsObject;
                foreach (var key in obj.Keys)
                {
                    result[key] = obj[(string)key].Value;
                }
            }

            return result;
        }

    }
}
