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

func main() {
	// 1. 自动请求管理员权限 (不良林同款逻辑)
	if !win.IsUserAnAdmin() {
		verb := "runas"
		exe, _ := os.Executable()
		cwd, _ := os.Getwd()
		args := strings.Join(os.Args[1:], " ")
		verbPtr, _ := win.UTF16PtrFromString(verb)
		exePtr, _ := win.UTF16PtrFromString(exe)
		cwdPtr, _ := win.UTF16PtrFromString(cwd)
		argPtr, _ := win.UTF16PtrFromString(args)
		win.ShellExecute(0, verbPtr, exePtr, argPtr, cwdPtr, win.SW_NORMAL)
		os.Exit(0)
	}

	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit

	// 2. 构建图形界面
	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 增强版",
		MinSize:  Size{Width: 500, Height: 400},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "目标域名/IP (如: google.com):"},
			LineEdit{AssignTo: &remoteAddr, Text: "www.google.com"},
			Composite{
				Layout: HBox{MarginsZero: true},
				Children: []Widget{
					PushButton{
						Text: "添加路由 (Add)",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "add", outTE)
						},
					},
					PushButton{
						Text: "删除路由 (Delete)",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "delete", outTE)
						},
					},
				},
			},
			Label{Text: "运行日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看当前路由表",
				OnClicked: func() {
					exec.Command("cmd", "/c", "start route print").Run()
				},
			},
		},
	}.Run()); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func handleRoute(input, action string, log *walk.TextEdit) {
	log.AppendText(fmt.Sprintf("[%s] 正在处理: %s\r\n", action, input))
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("错误: %v\r\n", err))
		return
	}

	for _, ip := range ips {
		var cmd *exec.Cmd
		if ip.To4() != nil {
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			// IPv6 处理
			if action == "add" {
				cmd = exec.Command("netsh", "interface", "ipv6", "add", "route", ip.String()+"/128", "interface=1")
			} else {
				cmd = exec.Command("netsh", "interface", "ipv6", "delete", "route", ip.String()+"/128", "interface=1")
			}
		}
		output, _ := cmd.CombinedOutput()
		log.AppendText(fmt.Sprintf("IP: %s -> %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("--- 操作完毕 ---\r\n\r\n")
}
