using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;

namespace RouteForwarderPro
{
    public class MainForm : Form
    {
        private TextBox txtRemoteAddr;
        private RichTextBox logBox;
        private Button btnAdd, btnDel, btnPrint;

        public MainForm()
        {
            this.Text = "RouteForwarder 增强版 (C# 原生)";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            Label lbl = new Label() { Text = "目标域名/IP:", Left = 20, Top = 20, Width = 100 };
            txtRemoteAddr = new TextBox() { Left = 120, Top = 20, Width = 320, Text = "www.google.com" };
            
            btnAdd = new Button() { Text = "添加路由", Left = 120, Top = 60, Width = 100 };
            btnDel = new Button() { Text = "删除路由", Left = 230, Top = 60, Width = 100 };
            btnPrint = new Button() { Text = "查看路由表", Left = 340, Top = 60, Width = 100 };

            logBox = new RichTextBox() { Left = 20, Top = 100, Width = 440, Height = 280, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime };

            this.Controls.Add(lbl);
            this.Controls.Add(txtRemoteAddr);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnDel);
            this.Controls.Add(btnPrint);
            this.Controls.Add(logBox);

            btnAdd.Click += (s, e) => HandleRoute("add");
            btnDel.Click += (s, e) => HandleRoute("delete");
            btnPrint.Click += (s, e) => {
                ProcessStartInfo psi = new ProcessStartInfo("cmd", "/c route print & pause") { UseShellExecute = true };
                Process.Start(psi);
            };
        }

        private void HandleRoute(string action)
        {
            string input = txtRemoteAddr.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 准备 {action}: {input}\n");

            try
            {
                IPAddress[] ips = Dns.GetHostAddresses(input);
                foreach (var ip in ips)
                {
                    string cmd, args;
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
                    {
                        cmd = "route";
                        args = $"{action} {ip.ToString()} mask 255.255.255.255";
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
                    {
                        cmd = "netsh";
                        string netshAction = (action == "add") ? "add" : "delete";
                        args = $"interface ipv6 {netshAction} route {ip.ToString()}/128 interface=1";
                    }
                    else continue;

                    RunCmd(cmd, args, ip.ToString());
                }
            }
            catch (Exception ex)
            {
                logBox.AppendText($"错误: {ex.Message}\n");
            }
        }

        private void RunCmd(string cmd, string args, string ip)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd, args)
            {
                Verb = "runas", // 核心：强制弹出管理员权限对话框
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };
            try {
                Process.Start(psi);
                logBox.AppendText($"成功: {ip}\n");
            } catch {
                logBox.AppendText($"失败: 用户拒绝授权或权限不足 ({ip})\n");
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
