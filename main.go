package main

import (
	"fmt"
	"net"
	"os"
	"os/exec"
	"strings"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
	"github.com/lxn/win"
)

// 程序启动时自动申请管理员权限
func init() {
	if !strings.Contains(strings.Join(os.Args, ""), "-noderun") {
		if !win.IsUserAnAdmin() {
			verb := "runas"
			exe, _ := os.Executable()
			cwd, _ := os.Getwd()
			args := append([]string{"-noderun"}, os.Args[1:]...)

			verbPtr, _ := win.UTF16PtrFromString(verb)
			exePtr, _ := win.UTF16PtrFromString(exe)
			cwdPtr, _ := win.UTF16PtrFromString(cwd)
			argPtr, _ := win.UTF16PtrFromString(strings.Join(args, " "))

			win.ShellExecute(0, verbPtr, exePtr, argPtr, cwdPtr, win.SW_NORMAL)
			os.Exit(0)
		}
	}
}

func main() {
	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit

	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 增强版 (IPv6 & Domain Support)",
		MinSize:  Size{Width: 500, Height: 400},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "目标地址 (输入域名如 google.com 或 IP):"},
			LineEdit{AssignTo: &remoteAddr, Text: "www.google.com"},
			Composite{
				Layout: HBox{MarginsZero: true},
				Children: []Widget{
					PushButton{
						Text: "添加路由",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "add", outTE)
						},
					},
					PushButton{
						Text: "删除路由",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "delete", outTE)
						},
					},
				},
			},
			Label{Text: "操作日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看系统路由表 (Print)",
				OnClicked: func() {
					exec.Command("cmd", "/c", "start route print").Run()
				},
			},
		},
	}.Run()); err != nil {
		fmt.Println(err)
	}
}

func handleRoute(input, action string, log *walk.TextEdit) {
	log.AppendText(fmt.Sprintf("[%s] 正在处理: %s\r\n", action, input))
	
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("错误: 无法解析地址 %v\r\n", err))
		return
	}

	for _, ip := range ips {
		var cmd *exec.Cmd
		if ip.To4() != nil {
			// IPv4 使用 route 命令
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			// IPv6 使用 netsh 命令
			if action == "add" {
				cmd = exec.Command("netsh", "interface", "ipv6", "add", "route", ip.String()+"/128", "interface=1")
			} else {
				cmd = exec.Command("netsh", "interface", "ipv6", "delete", "route", ip.String()+"/128", "interface=1")
			}
		}

		output, _ := cmd.CombinedOutput()
		log.AppendText(fmt.Sprintf("结果 (%s): %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("--- 操作完成 ---\r\n\r\n")
}
