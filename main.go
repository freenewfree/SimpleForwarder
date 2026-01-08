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

func init() {
	// 强制管理员权限启动
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
		Title:    "RouteForwarder 增强版",
		MinSize:  Size{Width: 500, Height: 400},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "目标地址 (域名或 IP):"},
			LineEdit{AssignTo: &remoteAddr, Text: "www.google.com"},
			Composite{
				Layout: HBox{MarginsZero: true},
				Children: []Widget{
					PushButton{
						Text: "添加路由 (IPv4/IPv6)",
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
			Label{Text: "运行日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看系统路由表",
				OnClicked: func() {
					exec.Command("cmd", "/c", "start route print").Run()
				},
			},
		},
	}.Run()); err != nil {
		// 如果启动失败，弹出消息框告知原因
		walk.MsgBox(nil, "错误", "程序启动失败: "+err.Error(), walk.MsgBoxIconError)
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
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			// IPv6 增加处理
			if action == "add" {
				cmd
