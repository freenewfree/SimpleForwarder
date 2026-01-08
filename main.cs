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
        // 显式初始化，避免 CS8618 警告
        private ComboBox cbInterface = new ComboBox();
        private ComboBox cbGateway = new ComboBox();
        private ComboBox cbRouteList = new ComboBox();
        private ComboBox cbAction = new ComboBox();
        private CheckBox chkForward = new CheckBox();
        private CheckBox chkExtra = new CheckBox();
        private TextBox txtExtraRoutes = new TextBox();
        private Button btnExecute = new Button();
        private NotifyIcon trayIcon = new NotifyIcon();

        public MainForm()
        {
            this.Font = new Font("Microsoft YaHei", 9F);
            this.Text = "RouteForwarder - 不良林复刻最终版";
            this.ClientSize = new Size(480, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. 初始化托盘
            trayIcon.Text = "RouteForwarder";
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            // 2. 布局逻辑
            int labelLeft = 15, comboLeft = 85, spacing = 40;

            chkForward.Text = "路由转发"; chkForward.Left = 120; chkForward.Top = 12; chkForward.AutoSize = true;
            chkExtra.Text = "额外路由条目"; chkExtra.Left = 220; chkExtra.Top = 12; chkExtra.AutoSize = true; chkExtra.Checked = true;
            
            CheckInitialForwarding();

            Label lbl1 = new Label() { Text = "网卡接口:", Left = labelLeft, Top = 53, Width = 65 };
            cbInterface.Left = comboLeft; cbInterface.Top = 50; cbInterface.Width = 180; cbInterface.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lbl2 = new Label() { Text = "默认网关:", Left = labelLeft, Top = 53 + spacing, Width = 65 };
            cbGateway.Left = comboLeft; cbGateway.Top = 50 + spacing; cbGateway.Width = 180;

            Label lbl3 = new Label() { Text = "路由条目:", Left = labelLeft, Top = 53 + spacing * 2, Width = 65 };
            cbRouteList.Left = comboLeft; cbRouteList.Top = 50 + spacing * 2; cbRouteList.Width = 180; cbRouteList.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lbl4 = new Label() { Text = "路由动作:", Left = labelLeft, Top = 53 + spacing * 3, Width = 65 };
            cbAction.Left = comboLeft; cbAction.Top = 50 + spacing * 3; cbAction.Width = 180; cbAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction.Items.AddRange(new object[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;

            btnExecute.Text = "执行"; btnExecute.Left = 120; btnExecute.Top = 230; btnExecute.Width = 100; btnExecute.Height = 35;
            txtExtraRoutes.Left = 285; txtExtraRoutes.Top = 50; txtExtraRoutes.Width = 175; txtExtraRoutes.Height = 215; 
            txtExtraRoutes.Multiline = true; txtExtraRoutes.ScrollBars = ScrollBars.Both; txtExtraRoutes.Text = "8.8.8.8\r\n1.1.1.1";

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, lbl1, cbInterface, lbl2, cbGateway, lbl3, cbRouteList, lbl4, cbAction, btnExecute, txtExtraRoutes });

            RefreshNetworkInfo();
            LoadRouteFiles();

            // 绑定事件
            btnExecute.Click += ExecuteAction;
            chkForward.CheckedChanged += (s, e) => ToggleForwarding(chkForward.Checked);
            this.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); } };
        }

        private void CheckInitialForwarding()
        {
            try {
                // 使用默认值 0 处理空值，解决 CS8600/CS8605
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
            
            // 显式转换为 object[] 解决 CS8620
            var files = Directory.GetFiles(listDir, "*.txt")
                        .Select(Path.GetFileName)
                        .Where(f => f != null)
                        .Cast<object>()
                        .ToArray();

            if (files.Length > 0) {
                cbRouteList.Items.AddRange(files);
                cbRouteList.SelectedIndex = 0;
            } else {
                cbRouteList.Items.Add("无可用文件");
                cbRouteList.SelectedIndex = 0;
            }
        }

        private void ToggleForwarding(bool enable)
        {
            try {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", enable ? 1 : 0, RegistryValueKind.DWord);
                RunCmd("sc", "config RemoteAccess start= auto");
                RunCmd("net", enable ? "start RemoteAccess" : "stop RemoteAccess");
                MessageBox.Show("指令已发送，如未生效请重启电脑。");
            } catch (Exception ex) { MessageBox.Show("设置失败: " + ex.Message); }
        }

        // 修改 sender 为 object? 解决 CS8621
        private async void ExecuteAction(object? sender, EventArgs e)
        {
            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            btnExecute.Enabled = false;

            List<string> targets = new List<string>(txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            
            if (cbRouteList.SelectedItem != null && cbRouteList.Text != "无可用文件") {
                string fileName = cbRouteList.Text;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list", fileName);
                if (File.Exists(path)) targets.AddRange(File.ReadAllLines(path));
            }

            foreach (string line in targets) {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                try {
                    var ips = await Dns.GetHostAddressesAsync(t);
                    foreach (var ip in ips) {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            RunCmd("route", $"-p {action} {ip} mask 255.255.255.255 {gw} metric 1");
                    }
                } catch { }
            }
            btnExecute.Enabled = true;
            MessageBox.Show("执行完毕！");
        }

        private void RunCmd(string file, string args) {
            try { Process.Start(new ProcessStartInfo(file, args) { Verb = "runas", CreateNoWindow = true, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden }); } catch { }
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
