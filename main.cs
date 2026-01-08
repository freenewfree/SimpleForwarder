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
        private ComboBox cbInterface, cbGateway, cbRouteList, cbAction;
        private CheckBox chkForward, chkExtra;
        private TextBox txtExtraRoutes;
        private Button btnExecute;
        private NotifyIcon trayIcon;

        public MainForm()
        {
            this.Font = new Font("Microsoft YaHei", 9F); // 换成雅黑，显示更工整
            this.Text = "RouteForwarder - 不良林复刻最终版";
            this.ClientSize = new Size(480, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. 初始化托盘
            trayIcon = new NotifyIcon() { Text = "RouteForwarder", Icon = SystemIcons.Application, Visible = true };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            // 2. 布局逻辑
            int labelLeft = 15, comboLeft = 85, spacing = 40;

            chkForward = new CheckBox() { Text = "路由转发", Left = 120, Top = 12, AutoSize = true };
            chkExtra = new CheckBox() { Text = "额外路由条目", Left = 220, Top = 12, AutoSize = true, Checked = true };
            
            // 初始化检查转发状态
            CheckInitialForwarding();

            Label lbl1 = new Label() { Text = "网卡接口:", Left = labelLeft, Top = 53, Width = 65 };
            cbInterface = new ComboBox() { Left = comboLeft, Top = 50, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lbl2 = new Label() { Text = "默认网关:", Left = labelLeft, Top = 53 + spacing, Width = 65 };
            cbGateway = new ComboBox() { Left = comboLeft, Top = 50 + spacing, Width = 180 };

            Label lbl3 = new Label() { Text = "路由条目:", Left = labelLeft, Top = 53 + spacing * 2, Width = 65 };
            cbRouteList = new ComboBox() { Left = comboLeft, Top = 50 + spacing * 2, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lbl4 = new Label() { Text = "路由动作:", Left = labelLeft, Top = 53 + spacing * 3, Width = 65 };
            cbAction = new ComboBox() { Left = comboLeft, Top = 50 + spacing * 3, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbAction.Items.AddRange(new string[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;

            btnExecute = new Button() { Text = "执行", Left = 120, Top = 230, Width = 100, Height = 35 };
            txtExtraRoutes = new TextBox() { Left = 285, Top = 50, Width = 175, Height = 215, Multiline = true, ScrollBars = ScrollBars.Both, Text = "8.8.8.8\r\n1.1.1.1" };

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, lbl1, cbInterface, lbl2, cbGateway, lbl3, cbRouteList, lbl4, cbAction, btnExecute, txtExtraRoutes });

            // 3. 填充动态数据
            RefreshNetworkInfo();
            LoadRouteFiles();

            // 4. 绑定事件
            btnExecute.Click += ExecuteAction;
            chkForward.CheckedChanged += (s, e) => ToggleForwarding(chkForward.Checked);
            this.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); } };
        }

        private void CheckInitialForwarding()
        {
            try {
                object val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", 0);
                chkForward.Checked = (int)val == 1;
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
            var files = Directory.GetFiles(listDir, "*.txt").Select(Path.GetFileName).ToArray();
            cbRouteList.Items.AddRange(files);
            if (cbRouteList.Items.Count > 0) cbRouteList.SelectedIndex = 0;
            else cbRouteList.Items.Add("无可用文件");
        }

        private void ToggleForwarding(bool enable)
        {
            try {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", enable ? 1 : 0, RegistryValueKind.DWord);
                RunCmd("sc", $"config RemoteAccess start= auto");
                RunCmd("net", enable ? "start RemoteAccess" : "stop RemoteAccess");
                MessageBox.Show("指令已发送，如未生效请确保以管理员权限运行并重启。");
            } catch (Exception ex) { MessageBox.Show("设置失败: " + ex.Message); }
        }

        private async void ExecuteAction(object sender, EventArgs e)
        {
            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            btnExecute.Enabled = false;

            List<string> targets = new List<string>(txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            
            if (cbRouteList.SelectedItem != null && cbRouteList.Text != "无可用文件") {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list", cbRouteList.Text);
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
