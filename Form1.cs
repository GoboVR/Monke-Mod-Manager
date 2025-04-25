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
        private Dictionary<string, string> _download = new();
        private Dictionary<string, string> _location = new();
        private string _version = "1.0.0";
        public bool installing = false;
        public short CurrentVersion = 1;

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
                    Properties.Settings.Default.installDirectory = _installDirectory;

                    if (Directory.Exists(DefaultSteamInstallDirectory) || _installDirectory == null)
                        _installDirectory = DefaultSteamInstallDirectory;
                    else if (Directory.Exists(DefaultOculusInstallDirectory) || _installDirectory == null)
                        _installDirectory = DefaultOculusInstallDirectory;
                    else if(_installDirectory == null)
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

        private void UnzipFile(byte[] data, string directory)
        {
            using var ms = new MemoryStream(data);
            using var zip = new Unzip(ms);
            zip.ExtractToDirectory(directory);
        }

        private byte[] DownloadFile(string url)
        {
            using var client = new WebClient { Proxy = null };
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
                    Process.Start(Path.Combine(yes, "Updater.exe"));
                    Environment.Exit(0);
                }
            } 
            catch(Exception e){MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}
        }
        
        public void ConfigFix()
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
    }
}
