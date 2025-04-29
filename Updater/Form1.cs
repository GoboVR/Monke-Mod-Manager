using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Updater
{
    public partial class Form1 : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }
        public Form1()
        {
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            InitializeComponent();
            label1.Text = "Downloading...";
            this.MouseDown += Form_MouseDown;
            
            IntPtr hRegion = CreateRoundRectRgn(0, 0, Width, Height, 50, 50);
            SetWindowRgn(this.Handle, hRegion, true);
            
            Dragging(label1);
        }

        private async void Updater()
        {
            label1.Text = "Downloading...";
            byte[] john = DownloadFile("https://github.com/ngbatzyt/monke-mod-manager/releases/latest/download/MonkeModManager.msi");
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "temp", "MonkeModManager.msi"), john);
            label1.Text = "Installing...";
            Process process = new Process();
            process.StartInfo.FileName = "msiexec";
            process.StartInfo.Arguments = $"/i \"{Path.Combine(Directory.GetCurrentDirectory(), "temp", "MonkeModManager.msi")}\" /qn";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
            Environment.Exit(0);
        }
        private byte[] DownloadFile(string url)
        {
            using WebClient client = new WebClient();
            client.Proxy = null;
            return client.DownloadData(url);
        }
        
        private void Dragging(Control ctrl)
        {
            ctrl.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }
        
        private void UnzipFile(byte[] data, string directory)
        {
            using var ms = new MemoryStream(data);
            using var zip = new Unzip(ms);
            zip.ExtractToDirectory(directory);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Updater();
        }
    }
}
