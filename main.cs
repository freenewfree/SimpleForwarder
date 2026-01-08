using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;

namespace RouteForwarder
{
    public partial class MainForm : Form
    {
        private ComboBox? cbInterface, cbGateway, cbRouteList, cbAction;
        private CheckBox? chkForward, chkExtra;
        private TextBox? txtExtraRoutes;
        private Button? btnPrint, btnExecute;
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;

        public MainForm()
        {
            this.Font = new Font("Tahoma", 8.25F);
            this.Text = "RouteForwarder - 不良林复刻增强版";
            this.ClientSize = new Size(480, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            InitTrayIcon();

            // 布局控件
            chkForward = new CheckBox() { Text = "路由转发", Left = 180, Top = 12, AutoSize = true };
            chkExtra = new CheckBox() { Text = "额外路由条目", Left = 280, Top = 12, AutoSize = true, Checked = true };
            
            // 初始化转发开关状态
            CheckForwardingStatus();

            int labelLeft = 15, comboLeft = 85, spacing = 32;
            cbInterface = new ComboBox() { Left = comboLeft, Top = 45, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbGateway = new ComboBox() { Left = comboLeft, Top = 45 + spacing, Width = 180 };
            cbRouteList = new ComboBox() { Left = comboLeft, Top = 45 + spacing * 2, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbAction = new ComboBox() { Left = comboLeft, Top = 45 + spacing * 3, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbAction.Items.AddRange(new string[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;

            btnExecute = new Button() { Text = "执行", Left = 160, Top = 210, Width = 100, Height = 30 };
            txtExtraRoutes = new TextBox() { Left = 285, Top = 45, Width = 175, Height = 195, Multiline = true, ScrollBars = ScrollBars.Both, Text = "8.8.8.8\r\n1.1.1.1" };

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, cbInterface, cbGateway, cbRouteList, cbAction, btnExecute, txtExtraRoutes });

            FillGatewayOptions();
            LoadRouteFiles();

            btnExecute.Click += ExecuteAction;
            chkForward.CheckedChanged += ToggleForwarding;
            this.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); } };
        }

        private void CheckForwardingStatus()
        {
            try {
                object val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", 0);
                if (chkForward != null) chkForward.Checked = (int)val == 1;
            } catch { }
        }

        private void ToggleForwarding(object? sender, EventArgs e)
        {
            if (chkForward == null) return;
            int val = chkForward.Checked ? 1 : 0;
            try {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "IPEnableRouter", val, RegistryValueKind.DWord);
                RunCmd("sc", "config RemoteAccess start= auto");
                RunCmd("net", chkForward.Checked ? "start RemoteAccess" : "stop RemoteAccess");
                MessageBox.Show($"路由转发已{(chkForward.Checked ? "开启" : "关闭")}！\n注意：部分系统需重启后生效。");
            } catch (Exception ex) { MessageBox.Show("操作失败，请确认以管理员权限运行: " + ex.Message); }
        }

        private void LoadRouteFiles()
        {
            if (cbRouteList == null) return;
            string listDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list");
            if (Directory.Exists(listDir)) {
                var files = Directory.GetFiles(listDir, "*.txt").Select(Path.GetFileName).ToArray();
                cbRouteList.Items.AddRange(files);
                if (cbRouteList.Items.Count > 0) cbRouteList.SelectedIndex = 0;
            }
        }

        private async void ExecuteAction(object? sender, EventArgs e)
        {
            if (cbAction == null || cbGateway == null || txtExtraRoutes == null || btnExecute == null) return;
            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            btnExecute.Enabled = false;

            List<string> targets = new List<string>(txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            
            // 读取选中的文件
            if (cbRouteList?.SelectedItem != null) {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list", cbRouteList.SelectedItem.ToString());
                if (File.Exists(path)) targets.AddRange(File.ReadAllLines(path));
            }

            foreach (string line in targets.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t) && !t.StartsWith("#"))) {
                try {
                    if (line.Any(char.IsDigit)) { // IP 或 CIDR
                        string cmdArgs = $"-p {action} {line} mask 255.255.255.255 {gw} metric 1";
                        if (line.Contains("/")) cmdArgs = $"-p {action} {line.Split('/')[0]} mask {CidrToMask(line.Split('/')[1])} {gw} metric 1";
                        RunCmd("route", cmdArgs);
                    } else { // 域名
                        var ips = await Dns.GetHostAddressesAsync(line);
                        foreach (var ip in ips) RunCmd("route", $"-p {action} {ip} mask 255.255.255.255 {gw} metric 1");
                    }
                } catch { }
            }
            btnExecute.Enabled = true;
            MessageBox.Show("执行完成！");
        }

        private string CidrToMask(string cidr) { /* 简单实现略，默认返回 255.255.255.0 或原样 */ return "255.255.255.0"; }

        private void RunCmd(string file, string args) {
            Process.Start(new ProcessStartInfo(file, args) { Verb = "runas", CreateNoWindow = true, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
        }

        private void InitTrayIcon() {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("退出", null, (s, e) => { if (trayIcon != null) trayIcon.Visible = false; Application.Exit(); });
            trayIcon = new NotifyIcon() { Text = "RouteForwarder", Icon = SystemIcons.Application, ContextMenuStrip = trayMenu, Visible = true };
        }

        private void FillGatewayOptions() {
            try {
                var gws = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses).Select(g => g.Address.ToString()).Where(g => g.Contains(".")).ToList();
                if (cbGateway != null) { cbGateway.Items.AddRange(gws.ToArray()); if (gws.Count > 0) cbGateway.Text = gws[0]; }
            } catch { }
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
