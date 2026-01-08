using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace RouteForwarderPro
{
    public class MainForm : Form
    {
        private TextBox txtRemoteAddr, txtGateway;
        private RichTextBox logBox;
        private Button btnAdd, btnDel, btnPrint;

        public MainForm()
        {
            // --- 界面美化 (不良林深色风格) ---
            this.Text = "RouteForwarder Pro (自动网关版)";
            this.Size = new Size(520, 500);
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            Font mainFont = new Font("Segoe UI", 9, FontStyle.Regular);
            
            Label lbl1 = new Label() { Text = "目标域名/IP:", Left = 20, Top = 20, Width = 100, Font = mainFont };
            txtRemoteAddr = new TextBox() { Left = 120, Top = 18, Width = 350, Text = "www.google.com", BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            Label lbl2 = new Label() { Text = "本地网关:", Left = 20, Top = 55, Width = 100, Font = mainFont };
            txtGateway = new TextBox() { Left = 120, Top = 53, Width = 350, Text = GetDefaultGateway(), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.Yellow, BorderStyle = BorderStyle.FixedSingle };
            
            btnAdd = new Button() { Text = "添加路由", Left = 120, Top = 90, Width = 110, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
            btnDel = new Button() { Text = "删除路由", Left = 240, Top = 90, Width = 110, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(200, 0, 0) };
            btnPrint = new Button() { Text = "查看路由表", Left = 360, Top = 90, Width = 110, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60) };

            logBox = new RichTextBox() { Left = 20, Top = 140, Width = 460, Height = 300, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9) };

            this.Controls.AddRange(new Control[] { lbl1, txtRemoteAddr, lbl2, txtGateway, btnAdd, btnDel, btnPrint, logBox });

            btnAdd.Click += (s, e) => HandleRoute("add");
            btnDel.Click += (s, e) => HandleRoute("delete");
            btnPrint.Click += (s, e) => Process.Start("cmd", "/c route print & pause");
        }

        // 自动获取网关的魔法函数
        private string GetDefaultGateway()
        {
            try {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Select(g => g.Address.ToString())
                    .FirstOrDefault(g => g.Contains(".") && g != "0.0.0.0") ?? "192.168.1.1";
            } catch { return "192.168.1.1"; }
        }

        private void HandleRoute(string action)
        {
            string input = txtRemoteAddr.Text.Trim();
            string gw = txtGateway.Text.Trim();
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 执行 {action}...\n");

            try {
                IPAddress[] ips = Dns.GetHostAddresses(input);
                foreach (var ip in ips) {
                    string cmd, args;
                    if (ip.AddressFamily == AddressFamily.InterNetwork) {
                        cmd = "route";
                        // 增加了网关参数，确保路由能成功添加
                        args = $"{action} {ip} mask 255.255.255.255 {gw} metric 1";
                    } else {
                        cmd = "netsh";
                        string nsAct = action == "add" ? "add" : "delete";
                        args = $"interface ipv6 {nsAct} route {ip}/128 interface=1";
                    }
                    RunCmd(cmd, args);
                }
            } catch (Exception ex) { logBox.AppendText($"失败: {ex.Message}\n"); }
        }

        private void RunCmd(string cmd, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd, args) { Verb = "runas", CreateNoWindow = true, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            try { 
                Process.Start(psi); 
                logBox.AppendText($"√ {cmd} {args}\n"); 
            } catch { 
                logBox.AppendText($"× 拒绝授权\n"); 
            }
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.Run(new MainForm()); }
    }
}
