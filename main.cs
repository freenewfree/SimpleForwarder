using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace RouteForwarder
{
    public partial class MainForm : Form
    {
        private ComboBox cbInterface = new ComboBox();
        private ComboBox cbGateway = new ComboBox();
        private ComboBox cbRouteList = new ComboBox();
        private ComboBox cbAction = new ComboBox();
        private CheckBox chkForward = new CheckBox();
        private CheckBox chkExtra = new CheckBox();
        private TextBox txtExtraRoutes = new TextBox();
        private Button btnExecute = new Button();
        private Button btnPrint = new Button(); // 补回查看按钮
        private NotifyIcon trayIcon = new NotifyIcon();

        public MainForm()
        {
            this.Font = new Font("Microsoft YaHei", 9F);
            this.Text = "RouteForwarder - 不良林复刻增强版";
            this.ClientSize = new Size(480, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 初始化托盘
            trayIcon.Text = "RouteForwarder";
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            // 1. 顶部勾选框
            chkForward.Text = "路由转发"; chkForward.Left = 120; chkForward.Top = 15; chkForward.AutoSize = true;
            chkExtra.Text = "额外路由条目"; chkExtra.Left = 220; chkExtra.Top = 15; chkExtra.AutoSize = true; chkExtra.Checked = true;
            
            // 2. 左侧标签与下拉框
            int labelLeft = 15, comboLeft = 85, startTop = 55, spacing = 40;
            string[] labels = { "网卡接口:", "默认网关:", "路由条目:", "路由动作:" };
            ComboBox[] combos = { cbInterface, cbGateway, cbRouteList, cbAction };

            for (int i = 0; i < labels.Length; i++)
            {
                Label lbl = new Label { Text = labels[i], Left = labelLeft, Top = startTop + (i * spacing) + 3, Width = 65 };
                combos[i].Left = comboLeft; 
                combos[i].Top = startTop + (i * spacing); 
                combos[i].Width = 180;
                if (i != 1) combos[i].DropDownStyle = ComboBoxStyle.DropDownList;
                
                this.Controls.Add(lbl);
                this.Controls.Add(combos[i]);
            }

            // 3. 右侧文本框与按钮布局
            cbAction.Items.AddRange(new object[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;
            
            // 两个按钮横向排列
            btnPrint.Text = "查看路由表"; btnPrint.Left = 35; btnPrint.Top = 235; btnPrint.Width = 100; btnPrint.Height = 35;
            btnExecute.Text = "执行"; btnExecute.Left = 160; btnExecute.Top = 235; btnExecute.Width = 100; btnExecute.Height = 35;
            
            txtExtraRoutes.Left = 285; txtExtraRoutes.Top = 55; txtExtraRoutes.Width = 175; txtExtraRoutes.Height = 215; 
            txtExtraRoutes.Multiline = true; txtExtraRoutes.ScrollBars = ScrollBars.Both; txtExtraRoutes.Text = "8.8.8.8\r\n1.1.1.1";

            // 底部链接
            LinkLabel link = new LinkLabel() { Text = "https://youtube.com/@bulianglin", Left = 15, Top = 290, Width = 250 };
            link.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo("cmd", $"/c start {link.Text}") { CreateNoWindow = true });

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, btnPrint, btnExecute, txtExtraRoutes, link });

            // 4. 数据填充与事件
            CheckInitialForwarding();
            RefreshNetworkInfo();
            LoadRouteFiles();

            btnExecute.Click += ExecuteAction;
            btnPrint.Click += (s, e) => RunCmd("cmd", "/c route print & pause", false); // 显示窗口运行
            chkForward.CheckedChanged += (s, e) => ToggleForwarding(chkForward.Checked);
            this.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); } };
        }

        private void CheckInitialForwarding()
        {
            try {
                object? val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", 0);
                chkForward.Checked = (val is int i) ? i == 1 : false;
            } catch { }
        }

        private void RefreshNetworkInfo()
        {
            try {
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                foreach (var adapter in adapters) {
                    cbInterface.Items.Add(adapter.Name);
                    var props = adapter.GetIPProperties();
                    foreach (var gw in props.GatewayAddresses) {
                        string ip = gw.Address.ToString();
                        if (ip.Contains(".") && !cbGateway.Items.Contains(ip)) cbGateway.Items.Add(ip);
                    }
                }
                if (cbInterface.Items.Count > 0) cbInterface.SelectedIndex = 0;
                if (cbGateway.Items.Count > 0) cbGateway.SelectedIndex = 0;
            } catch { }
        }

        private void LoadRouteFiles()
        {
            string listDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list");
            if (!Directory.Exists(listDir)) Directory.CreateDirectory(listDir);
            var files = Directory.GetFiles(listDir, "*.txt").Select(Path.GetFileName).Cast<object>().ToArray();
            if (files.Length > 0) { cbRouteList.Items.AddRange(files); cbRouteList.SelectedIndex = 0; }
            else { cbRouteList.Items.Add("无可用文件"); cbRouteList.SelectedIndex = 0; }
        }

        private void ToggleForwarding(bool enable)
        {
            try {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", enable ? 1 : 0, RegistryValueKind.DWord);
                RunCmd("sc", "config RemoteAccess start= auto");
                RunCmd("net", enable ? "start RemoteAccess" : "stop RemoteAccess");
                MessageBox.Show("指令已发送，如未生效请确保以管理员权限运行并重启。");
            } catch { }
        }

        private async void ExecuteAction(object? sender, EventArgs e)
        {
            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            btnExecute.Enabled = false;
            List<string> targets = new List<string>(txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (cbRouteList.SelectedItem != null && cbRouteList.Text != "无可用文件") {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list", cbRouteList.Text);
                if (File.Exists(path)) targets.AddRange(File.ReadAllLines(path));
            }
            foreach (string line in targets.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t) && !t.StartsWith("#"))) {
                try {
                    var ips = await Dns.GetHostAddressesAsync(line);
                    foreach (var ip in ips) if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        RunCmd("route", $"-p {action} {ip} mask 255.255.255.255 {gw} metric 1");
                } catch { }
            }
            btnExecute.Enabled = true;
            MessageBox.Show("执行完毕！");
        }

        private void RunCmd(string file, string args, bool hide = true) {
            try { 
                ProcessStartInfo psi = new ProcessStartInfo(file, args) { 
                    Verb = "runas", 
                    CreateNoWindow = hide, 
                    UseShellExecute = true, 
                    WindowStyle = hide ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal 
                };
                Process.Start(psi); 
            } catch { }
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
