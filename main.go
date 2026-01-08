package main

import (
	"fmt"
	"net"
	"os/exec"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
)

func main() {
	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit

	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 简易版 (请右键管理员运行)",
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
			Label{Text: "操作日志 (若无反应，请检查是否以管理员身份运行):"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看系统路由表",
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
	log.AppendText(fmt.Sprintf("[%s] 开始处理: %s\r\n", action, input))
	
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("错误: 无法解析 %v\r\n", err))
		return
	}

	for _, ip := range ips {
		var cmd *exec.Cmd
		if ip.To4() != nil {
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			if action == "add" {
				cmd = exec.Command("netsh", "interface", "ipv6", "add", "route", ip.String()+"/128", "interface=1")
			} else {
				cmd = exec.Command("netsh", "interface", "ipv6", "delete", "route", ip.String()+"/128", "interface=1")
			}
		}

		output, _ := cmd.CombinedOutput()
		log.AppendText(fmt.Sprintf("IP: %s -> %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("--- 执行完毕 ---\r\n\r\n")
}
