package main

import (
	"fmt"
	"image/color"
	"net/url"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

// ─────────────────────────────────────────────────────────────────────────────
//  GRAFANA CONSTANTS
// ─────────────────────────────────────────────────────────────────────────────

const (
	GrafanaBaseURL   = "https://107.191.176.44"
	GrafanaDashUID   = "vmware-windows-slowness"
	GrafanaOrgID     = "1"
	GrafanaTheme     = "dark"
	DefaultTimeRange = "now-3h"
)

var (
	colorOrange = clrAmber
	colorRed    = clrRed
	colorGreen  = clrGreen
	colorPurple = color.NRGBA{R: 0xa8, G: 0x55, B: 0xf7, A: 0xff}
	colorAccent = clrAccent
)

type grafanaPanel struct {
	ID          int
	Title       string
	Description string
	Category    string // "summary", "vmware", "windows"
	BadgeColor  color.Color
}

var panels = []grafanaPanel{
	{2, "VMs with CPU Ready > 5%", "VMware: CPU contention count", "summary", colorOrange},
	{3, "VMs Ballooning Memory", "VMware: Active balloon driver", "summary", colorOrange},
	{4, "VMs with Disk Latency > 20ms", "VMware + OS: I/O slowness", "summary", colorRed},
	{5, "Windows Hosts — High Page File", "OS: Hosts paging to disk", "summary", colorPurple},
	{6, "Exporters DOWN", "Unreachable monitoring targets", "summary", colorRed},
	{7, "Active Firing Alerts", "Total alerts currently firing", "summary", colorRed},
	{11, "VM CPU Ready % — Top VMs", "VMware: Who is waiting for CPU", "vmware", colorOrange},
	{12, "ESXi Host CPU Utilisation", "VMware: Over-committed hosts", "vmware", colorOrange},
	{13, "CPU Ready % by Datacenter", "Contention heatmap across regions", "vmware", colorOrange},
	{21, "VM Memory Balloon (MB)", "VMware: Active ballooning per VM", "vmware", colorPurple},
	{22, "VM Memory Swap-in Rate", "VMware: Critical swap activity (MB/s)", "vmware", colorRed},
	{31, "VMware Disk Read/Write Latency", "VMware: Datastore latency in ms", "vmware", colorRed},
	{32, "Windows Disk Queue Length", "OS: Disk saturation per volume", "windows", colorRed},
	{33, "Windows Disk Free Space %", "OS: Free space per volume", "windows", colorGreen},
	{41, "VMware Network Throughput", "VMware: vNIC Tx/Rx (Mbps)", "vmware", colorAccent},
	{42, "VMware Network Packet Drops", "VMware: vSwitch drop count", "vmware", colorRed},
	{43, "Windows Network Throughput", "OS: NIC bytes/s", "windows", colorAccent},
	{44, "Windows Network Packet Drops", "OS: NIC discard rate", "windows", colorRed},
	{51, "Active Firing Alerts Table", "All alerts with remediation layer", "summary", colorRed},
}

// ─────────────────────────────────────────────────────────────────────────────
//  TAB 5 — GRAFANA METRICS PAGE
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) grafanaPage() fyne.CanvasObject {
	title := styledText("📈 Prometheus — VM Slowness Diagnostics", 14, clrText, true)
	info := styledText("Navigate between infrastructure layers. Click 'Open Panel' to view high-resolution metrics in your browser.", 9, clrMuted, false)
	info.TextStyle.Italic = true

	// Time range selector
	timeSelect := widget.NewSelect([]string{
		"Last 30 minutes", "Last 1 hour", "Last 3 hours", "Last 6 hours", "Last 24 hours",
	}, nil)
	timeSelect.SetSelected("Last 3 hours")

	timeMap := map[string]string{
		"Last 30 minutes": "now-30m",
		"Last 1 hour":     "now-1h",
		"Last 3 hours":    "now-3h",
		"Last 6 hours":    "now-6h",
		"Last 24 hours":   "now-24h",
	}

	btnFull := actionButton("🌐  Open Full Dashboard", func() {
		u := sm.buildDashboardURL(timeMap[timeSelect.Selected])
		sm.openInBrowser(u)
	})

	btnRefresh := widget.NewButtonWithIcon("🔄", theme.ViewRefreshIcon(), func() {
		sm.setStatus("Refreshing Slowness Diagnostics...")
	})

	controls := container.NewHBox(
		styledText("Time Range:", 10, clrText, false),
		timeSelect,
		layoutSpacer(20),
		btnRefresh,
		btnFull,
	)

	// Panel Grid
	grid := container.NewVBox()

	// Helper to add sections
	addSection := func(name string, filter string, headColor fyne.ThemeColorName) {
		grid.Add(layoutSpacer(10))
		grid.Add(styledText(name, 11, theme.DefaultTheme().Color(headColor, theme.VariantDark), true))

		row := container.NewGridWithColumns(4)
		for _, p := range panels {
			if p.Category != filter {
				continue
			}
			row.Add(sm.makePanelCard(p, timeSelect, timeMap))
		}
		grid.Add(row)
	}

	addSection("📊 SUMMARY INDICATORS", "summary", theme.ColorNamePrimary)
	addSection("🔵 VMWARE VSPHERE LAYER", "vmware", theme.ColorNameWarning)
	addSection("🟢 WINDOWS OS LAYER", "windows", theme.ColorNameSuccess)

	scroll := container.NewVScroll(grid)

	return container.NewBorder(
		container.NewVBox(title, info, controls, widget.NewSeparator()),
		nil, nil, nil,
		scroll,
	)
}

func (sm *App) makePanelCard(p grafanaPanel, sel *widget.Select, tmap map[string]string) fyne.CanvasObject {
	badge := styledText(sm.layerBadgeText(p.Category), 8, clrText, true)

	title := widget.NewLabel(p.Title)
	title.Wrapping = fyne.TextWrapWord
	title.TextStyle = fyne.TextStyle{Bold: true}

	desc := widget.NewLabel(p.Description)
	desc.Wrapping = fyne.TextWrapWord
	desc.TextStyle = fyne.TextStyle{Italic: true}

	btn := widget.NewButton("Open Panel", func() {
		u := sm.buildPanelURL(p.ID, tmap[sel.Selected])
		sm.openInBrowser(u)
	})
	btn.Importance = widget.MediumImportance

	cardContent := container.NewVBox(badge, title, desc, btn)

	bg := canvas.NewRectangle(color.NRGBA{40, 40, 45, 255})
	return container.NewStack(bg, container.NewPadded(cardContent))
}

// ─────────────────────────────────────────────────────────────────────────────
//  HELPERS & URL BUILDERS
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) buildDashboardURL(timeFrom string) string {
	return fmt.Sprintf("%s/?orgId=%s&search=open", GrafanaBaseURL, GrafanaOrgID)
}

func (sm *App) buildPanelURL(id int, timeFrom string) string {
	return fmt.Sprintf("%s/d/%s/title?orgId=%s&panelId=%d&from=%s&to=now&theme=%s",
		GrafanaBaseURL, GrafanaDashUID, GrafanaOrgID, id, timeFrom, GrafanaTheme)
}

func (sm *App) openInBrowser(rawURL string) {
	u := parseURL(rawURL)
	if u != nil {
		fyne.CurrentApp().OpenURL(u)
		sm.setStatus("Opening browser for " + rawURL)
	}
}

func (sm *App) layerBadgeText(cat string) string {
	switch cat {
	case "vmware":
		return "VMWARE LAYER"
	case "windows":
		return "WINDOWS OS"
	default:
		return "INFRA SUMMARY"
	}
}

func parseURL(raw string) *url.URL {
	u, _ := url.Parse(raw)
	return u
}

func layoutSpacer(w float32) fyne.CanvasObject {
	return canvas.NewRectangle(color.Transparent)
}
