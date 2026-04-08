package main

import (
	"encoding/csv"
	"fmt"
	"image/color"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/app"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"

	"github.com/shirou/gopsutil/v3/cpu"
	psDisk "github.com/shirou/gopsutil/v3/disk"
	"github.com/shirou/gopsutil/v3/mem"
)

// ─────────────────────────────────────────────────────────────────────────────
//  COLORS
// ─────────────────────────────────────────────────────────────────────────────

var (
	clrBg      = color.NRGBA{R: 0x0b, G: 0x13, B: 0x26, A: 0xff}
	clrSurface = color.NRGBA{R: 0x17, G: 0x1f, B: 0x33, A: 0xff}
	clrAccent  = color.NRGBA{R: 0x0e, G: 0xa5, B: 0xe9, A: 0xff}
	clrAmber   = color.NRGBA{R: 0xf5, G: 0x9e, B: 0x0b, A: 0xff}
	clrRed     = color.NRGBA{R: 0xf8, G: 0x71, B: 0x71, A: 0xff}
	clrGreen   = color.NRGBA{R: 0x34, G: 0xd3, B: 0x99, A: 0xff}
	clrText    = color.NRGBA{R: 0xf1, G: 0xf5, B: 0xf9, A: 0xff}
	clrMuted   = color.NRGBA{R: 0x64, G: 0x74, B: 0x8b, A: 0xff}
	clrTrack   = color.NRGBA{R: 0x1e, G: 0x2d, B: 0x45, A: 0xff}
)

// ─────────────────────────────────────────────────────────────────────────────
//  CUSTOM FYNE THEME — Obsidian Flux
// ─────────────────────────────────────────────────────────────────────────────

type obsidianTheme struct{}

func (obsidianTheme) Color(n fyne.ThemeColorName, _ fyne.ThemeVariant) color.Color {
	switch n {
	case theme.ColorNameBackground:
		return clrBg
	case theme.ColorNameForeground:
		return clrText
	case theme.ColorNamePrimary:
		return clrAccent
	case theme.ColorNameButton:
		return clrSurface
	case theme.ColorNameHover:
		return color.NRGBA{R: 0x1e, G: 0x32, B: 0x50, A: 0xff}
	case theme.ColorNameInputBackground:
		return clrTrack
	case theme.ColorNameDisabled:
		return clrMuted
	case theme.ColorNameShadow:
		return color.NRGBA{A: 0x88}
	case theme.ColorNameHeaderBackground:
		return color.NRGBA{R: 0x0d, G: 0x18, B: 0x29, A: 0xff}
	case theme.ColorNameScrollBar:
		return color.NRGBA{R: 0x2a, G: 0x35, B: 0x48, A: 0xff}
	case theme.ColorNameSeparator:
		return color.NRGBA{R: 0x2a, G: 0x35, B: 0x48, A: 0xff}
	}
	return theme.DefaultTheme().Color(n, theme.VariantDark)
}

func (obsidianTheme) Font(s fyne.TextStyle) fyne.Resource {
	return theme.DefaultTheme().Font(s)
}

func (obsidianTheme) Icon(n fyne.ThemeIconName) fyne.Resource {
	return theme.DefaultTheme().Icon(n)
}

func (obsidianTheme) Size(n fyne.ThemeSizeName) float32 {
	return theme.DefaultTheme().Size(n)
}

// ─────────────────────────────────────────────────────────────────────────────
//  GAUGE BAR — custom colored progress bar
// ─────────────────────────────────────────────────────────────────────────────

type GaugeBar struct {
	widget.BaseWidget
	value float64 // 0.0–1.0
	track *canvas.Rectangle
	fill  *canvas.Rectangle
}

func NewGaugeBar() *GaugeBar {
	g := &GaugeBar{
		track: canvas.NewRectangle(clrTrack),
		fill:  canvas.NewRectangle(clrAccent),
	}
	g.ExtendBaseWidget(g)
	return g
}

func (g *GaugeBar) SetValue(v float64) {
	g.value = v
	g.fill.FillColor = gaugeColor(v)
	g.Refresh()
}

func (g *GaugeBar) MinSize() fyne.Size { return fyne.NewSize(200, 16) }

func (g *GaugeBar) CreateRenderer() fyne.WidgetRenderer {
	return &gaugeRenderer{g: g}
}

type gaugeRenderer struct{ g *GaugeBar }

func (r *gaugeRenderer) Objects() []fyne.CanvasObject {
	return []fyne.CanvasObject{r.g.track, r.g.fill}
}
func (r *gaugeRenderer) Destroy()           {}
func (r *gaugeRenderer) MinSize() fyne.Size { return r.g.MinSize() }
func (r *gaugeRenderer) Layout(sz fyne.Size) {
	r.g.track.Resize(sz)
	r.g.track.Move(fyne.NewPos(0, 0))
	fw := sz.Width * float32(r.g.value)
	if fw < 0 {
		fw = 0
	}
	if fw > sz.Width {
		fw = sz.Width
	}
	r.g.fill.FillColor = gaugeColor(r.g.value)
	r.g.fill.Resize(fyne.NewSize(fw, sz.Height))
	r.g.fill.Move(fyne.NewPos(0, 0))
}
func (r *gaugeRenderer) Refresh() {
	r.Layout(r.g.Size())
	r.g.track.Refresh()
	r.g.fill.Refresh()
}

func gaugeColor(v float64) color.Color {
	if v < 0.5 {
		return clrAccent
	}
	if v <= 0.8 {
		return clrAmber
	}
	return clrRed
}

// ─────────────────────────────────────────────────────────────────────────────
//  FILE ITEM
// ─────────────────────────────────────────────────────────────────────────────

type fileItem struct {
	path string
	size int64
}

// ─────────────────────────────────────────────────────────────────────────────
//  APP STATE
// ─────────────────────────────────────────────────────────────────────────────

type App struct {
	win fyne.Window

	// Monitor
	cpuPct, cpuStatus      *canvas.Text
	ramVal, ramStatus      *canvas.Text
	dskVal, dskStatus      *canvas.Text
	gauCPU, gauRAM, gauDsk *GaugeBar
	refreshLbl             *canvas.Text

	// Dump
	dumpItems      []fileItem
	dumpList       *widget.List
	dumpStatus     *canvas.Text
	dumpSelectedID widget.ListItemID

	// Temp
	tempItems      []fileItem
	tempList       *widget.List
	tempStatus     *canvas.Text
	tempSelectedID widget.ListItemID

	// Log
	logData   [][]string
	logTable  *widget.Table
	logStatus *canvas.Text

	// Global UI
	statusLeft *canvas.Text
}

// ─────────────────────────────────────────────────────────────────────────────
//  MAIN
// ─────────────────────────────────────────────────────────────────────────────

func main() {
	a := app.NewWithID("com.obsidian.sysmonitor")
	a.Settings().SetTheme(obsidianTheme{})

	w := a.NewWindow("System Monitor & Cleanup  ·  Obsidian Flux")
	w.Resize(fyne.NewSize(1080, 700))
	w.SetFixedSize(false)
	w.CenterOnScreen()

	sm := &App{win: w}
	sm.buildUI()

	// Live metric ticker
	go func() {
		// Prime CPU counter
		cpu.Percent(0, false) //nolint
		time.Sleep(500 * time.Millisecond)
		t := time.NewTicker(2 * time.Second)
		for range t.C {
			sm.refreshMonitor()
		}
	}()

	w.ShowAndRun()
}

// ─────────────────────────────────────────────────────────────────────────────
//  UI BUILD
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) buildUI() {
	tabs := container.NewAppTabs(
		container.NewTabItem("📊  System Monitor", sm.monitorPage()),
		container.NewTabItem("🗑  Dump Cleanup", sm.dumpPage()),
		container.NewTabItem("🧹  Temp Cleanup", sm.tempPage()),
		container.NewTabItem("📋  Event Log", sm.logPage()),
		container.NewTabItem("📈  VM Metrics", sm.grafanaPage()),
	)
	tabs.SetTabLocation(container.TabLocationLeading)

	// Top bar
	appTitle := canvas.NewText("SYSTEM MONITOR & CLEANUP", clrText)
	appTitle.TextStyle = fyne.TextStyle{Bold: true}
	appTitle.TextSize = 16

	liveLabel := canvas.NewText("● LIVE", clrAccent)
	liveLabel.TextStyle = fyne.TextStyle{Bold: true}
	liveLabel.TextSize = 10

	topBar := container.NewBorder(nil, nil, appTitle, liveLabel)

	// Status bar
	sm.statusLeft = canvas.NewText("Ready", clrAccent)
	sm.statusLeft.TextSize = 10
	statusRight := canvas.NewText(time.Now().Format("Mon Jan 2 2006"), clrMuted)
	statusRight.TextSize = 10
	statusBar := container.NewBorder(nil, nil, sm.statusLeft, statusRight)

	// Root layout
	root := container.NewBorder(topBar, statusBar, nil, nil, tabs)
	sm.win.SetContent(root)
}

// ─────────────────────────────────────────────────────────────────────────────
//  PAGE 0 — SYSTEM MONITOR
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) monitorPage() fyne.CanvasObject {
	// CPU card
	cpuTitle := styledText("CPU USAGE", 9, clrMuted, false)
	sm.cpuPct = styledText("—%", 36, clrAccent, true)
	sm.cpuStatus = styledText("● —", 9, clrAccent, true)
	sm.gauCPU = NewGaugeBar()
	cpuCard := widget.NewCard("", "", container.NewVBox(
		container.NewBorder(nil, nil, cpuTitle, sm.cpuStatus),
		sm.cpuPct,
		sm.gauCPU,
		styledText("% Processor Time · _Total · refreshes every 2s", 8, clrMuted, false),
	))

	// RAM card
	ramTitle := styledText("RAM USAGE", 9, clrMuted, false)
	sm.ramVal = styledText("— GB / — GB", 18, clrText, true)
	sm.ramStatus = styledText("● —", 9, clrAccent, true)
	sm.gauRAM = NewGaugeBar()
	ramCard := widget.NewCard("", "", container.NewVBox(
		container.NewBorder(nil, nil, ramTitle, sm.ramStatus),
		sm.ramVal,
		sm.gauRAM,
	))

	// Disk card
	dskTitle := styledText(`DISK C:\ USAGE`, 9, clrMuted, false)
	sm.dskVal = styledText("— GB / — GB", 18, clrText, true)
	sm.dskStatus = styledText("● —", 9, clrAccent, true)
	sm.gauDsk = NewGaugeBar()
	dskCard := widget.NewCard("", "", container.NewVBox(
		container.NewBorder(nil, nil, dskTitle, sm.dskStatus),
		sm.dskVal,
		sm.gauDsk,
	))

	// Legend card
	sm.refreshLbl = styledText("Last refreshed: —", 9, clrMuted, false)
	legendCard := widget.NewCard("", "", container.NewVBox(
		styledText("STATUS LEGEND", 8, clrMuted, true),
		container.NewHBox(
			styledText("●  Normal (<50%)", 9, clrAccent, false),
			styledText("    ●  Moderate (51–80%)", 9, clrAmber, false),
			styledText("    ●  Critical (>80%)", 9, clrRed, false),
		),
		sm.refreshLbl,
	))

	ramDiskRow := container.NewGridWithColumns(2, ramCard, dskCard)

	return container.NewVBox(cpuCard, ramDiskRow, legendCard)
}

// ─────────────────────────────────────────────────────────────────────────────
//  REFRESH METRICS
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) refreshMonitor() {
	// CPU
	if pcts, err := cpu.Percent(0, false); err == nil && len(pcts) > 0 {
		v := pcts[0] / 100.0
		sm.gauCPU.SetValue(v)
		sm.cpuPct.Text = fmt.Sprintf("%.0f%%", pcts[0])
		sm.cpuPct.Color = gaugeColor(v)
		sm.cpuStatus.Text = statusLabel(v)
		sm.cpuStatus.Color = gaugeColor(v)
		sm.cpuPct.Refresh()
		sm.cpuStatus.Refresh()
	}

	// RAM
	if v, err := mem.VirtualMemory(); err == nil {
		pct := v.UsedPercent / 100.0
		sm.gauRAM.SetValue(pct)
		sm.ramVal.Text = fmt.Sprintf("%.1f GB / %.1f GB  (%.0f%%)",
			float64(v.Used)/1e9, float64(v.Total)/1e9, v.UsedPercent)
		sm.ramStatus.Text = statusLabel(pct)
		sm.ramStatus.Color = gaugeColor(pct)
		sm.ramVal.Refresh()
		sm.ramStatus.Refresh()
	}

	// Disk
	if u, err := psDisk.Usage(`C:\`); err == nil {
		pct := u.UsedPercent / 100.0
		sm.gauDsk.SetValue(pct)
		sm.dskVal.Text = fmt.Sprintf("%.1f GB / %.1f GB  (%.0f%%)",
			float64(u.Used)/1e9, float64(u.Total)/1e9, u.UsedPercent)
		sm.dskStatus.Text = statusLabel(pct)
		sm.dskStatus.Color = gaugeColor(pct)
		sm.dskVal.Refresh()
		sm.dskStatus.Refresh()
	}

	sm.refreshLbl.Text = "Last refreshed: " + time.Now().Format("15:04:05")
	sm.refreshLbl.Refresh()
}

func statusLabel(v float64) string {
	if v < 0.5 {
		return "● NORMAL"
	}
	if v <= 0.8 {
		return "● MODERATE"
	}
	return "● CRITICAL"
}

// ─────────────────────────────────────────────────────────────────────────────
//  PAGE 1 — DUMP FILE CLEANUP
// ─────────────────────────────────────────────────────────────────────────────

var dumpScanPaths = []string{
	`C:\Windows\MEMORY.DMP`,
	`C:\Windows\Minidump`,
	`C:\Windows\LiveKernelReports`,
	`C:\ProgramData\Microsoft\Windows\WER\ReportArchive`,
	`C:\ProgramData\Microsoft\Windows\WER\ReportQueue`,
	`C:\Windows\Temp`,
}

func (sm *App) dumpPage() fyne.CanvasObject {
	sm.dumpStatus = styledText("Click 'Scan' to begin.", 10, clrMuted, false)
	sm.dumpSelectedID = -1
	sm.dumpList = sm.makeFileList(&sm.dumpItems, func(id widget.ListItemID) {
		sm.dumpSelectedID = id
	})

	btnScan := actionButton("🔍  Scan for Dump Files", func() {
		sm.scanFiles(&sm.dumpItems, sm.dumpList, sm.dumpStatus, dumpScanPaths, "*.dmp", true)
	})
	btnDel := dangerButton("🗑  Delete Selected", func() {
		idx := sm.dumpSelectedID
		if idx < 0 || idx >= len(sm.dumpItems) {
			dialog.ShowInformation("Info", "No file selected.", sm.win)
			return
		}
		if idx < 0 || idx >= len(sm.dumpItems) {
			dialog.ShowInformation("Info", "No file selected.", sm.win)
			return
		}
		target := []fileItem{sm.dumpItems[idx]}
		sm.confirmDelete(target, func() {
			sm.scanFiles(&sm.dumpItems, sm.dumpList, sm.dumpStatus, dumpScanPaths, "*.dmp", true)
		})
	})
	btnAll := warnButton("⚠  Delete All", func() {
		if len(sm.dumpItems) == 0 {
			dialog.ShowInformation("Info", "No files found. Run scan first.", sm.win)
			return
		}
		sm.confirmDelete(sm.dumpItems, func() {
			sm.scanFiles(&sm.dumpItems, sm.dumpList, sm.dumpStatus, dumpScanPaths, "*.dmp", true)
		})
	})

	title := styledText("🗑  DUMP FILE CLEANUP", 14, clrText, true)
	sub := styledText("Scan for Windows crash dump files (.dmp) across system directories.", 10, clrMuted, false)
	btns := container.NewHBox(btnScan, btnDel, btnAll)

	return container.NewBorder(
		container.NewVBox(title, sub, btns, sm.dumpStatus),
		nil, nil, nil,
		sm.dumpList,
	)
}

// ─────────────────────────────────────────────────────────────────────────────
//  PAGE 2 — TEMP FILE CLEANUP
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) tempPage() fyne.CanvasObject {
	sm.tempStatus = styledText("Click 'Scan' to begin.", 10, clrMuted, false)
	sm.tempSelectedID = -1
	sm.tempList = sm.makeFileList(&sm.tempItems, func(id widget.ListItemID) {
		sm.tempSelectedID = id
	})

	tempPaths := func() []string {
		return []string{os.TempDir(), `C:\Windows\Temp`, `C:\Windows\Prefetch`}
	}

	btnScan := actionButton("🔍  Scan for Temp Files", func() {
		sm.scanFiles(&sm.tempItems, sm.tempList, sm.tempStatus, tempPaths(), "*", false)
	})
	btnDel := dangerButton("🗑  Delete Selected", func() {
		idx := sm.tempSelectedID
		if idx < 0 || idx >= len(sm.tempItems) {
			dialog.ShowInformation("Info", "No file selected.", sm.win)
			return
		}
		if idx < 0 || idx >= len(sm.tempItems) {
			dialog.ShowInformation("Info", "No file selected.", sm.win)
			return
		}
		sm.confirmDelete([]fileItem{sm.tempItems[idx]}, func() {
			sm.scanFiles(&sm.tempItems, sm.tempList, sm.tempStatus, tempPaths(), "*", false)
		})
	})
	btnAll := warnButton("⚠  Delete All", func() {
		if len(sm.tempItems) == 0 {
			dialog.ShowInformation("Info", "No files found. Run scan first.", sm.win)
			return
		}
		sm.confirmDelete(sm.tempItems, func() {
			sm.scanFiles(&sm.tempItems, sm.tempList, sm.tempStatus, tempPaths(), "*", false)
		})
	})

	title := styledText("🧹  TEMP FILE CLEANUP", 14, clrText, true)
	sub := styledText("Scan top-level files in TEMP, Windows\\Temp, and Prefetch directories.", 10, clrMuted, false)

	return container.NewBorder(
		container.NewVBox(title, sub, container.NewHBox(btnScan, btnDel, btnAll), sm.tempStatus),
		nil, nil, nil,
		sm.tempList,
	)
}

// ─────────────────────────────────────────────────────────────────────────────
//  SHARED: SCAN FILES
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) scanFiles(items *[]fileItem, list *widget.List, status *canvas.Text, paths []string, pattern string, recursive bool) {
	status.Text = "Scanning…"
	status.Color = clrMuted
	status.Refresh()

	var found []fileItem
	for _, p := range paths {
		info, err := os.Stat(p)
		if err != nil {
			continue
		}
		if !info.IsDir() {
			// Single file
			if matchPattern(pattern, filepath.Base(p)) {
				found = append(found, fileItem{path: p, size: info.Size()})
			}
			continue
		}
		entries, err := os.ReadDir(p)
		if err != nil {
			continue
		}
		for _, e := range entries {
			if e.IsDir() {
				if !recursive {
					continue
				}
				// Walk subdirs
				_ = filepath.WalkDir(filepath.Join(p, e.Name()), func(sub string, d os.DirEntry, werr error) error {
					if werr != nil || d.IsDir() {
						return nil
					}
					if matchPattern(pattern, d.Name()) {
						if fi, err2 := d.Info(); err2 == nil {
							found = append(found, fileItem{path: sub, size: fi.Size()})
						}
					}
					return nil
				})
				continue
			}
			if matchPattern(pattern, e.Name()) {
				if fi, err2 := e.Info(); err2 == nil {
					found = append(found, fileItem{path: filepath.Join(p, e.Name()), size: fi.Size()})
				}
			}
		}
	}

	*items = found
	list.Refresh()

	if len(found) == 0 {
		status.Text = "✔  No files found."
		status.Color = clrGreen
	} else {
		var total int64
		for _, f := range found {
			total += f.size
		}
		status.Text = fmt.Sprintf("Found %d file(s)  ·  Total: %s", len(found), humanSize(total))
		status.Color = clrAmber
	}
	status.Refresh()
}

func matchPattern(pattern, name string) bool {
	if pattern == "*" || pattern == "*.*" {
		return true
	}
	matched, _ := filepath.Match(pattern, name)
	return matched
}

// ─────────────────────────────────────────────────────────────────────────────
//  SHARED: CONFIRM DELETE
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) confirmDelete(targets []fileItem, rescan func()) {
	msg := fmt.Sprintf("Delete %d file(s)?\n\nThis cannot be undone.", len(targets))
	dialog.ShowConfirm("Confirm Delete", msg, func(ok bool) {
		if !ok {
			return
		}
		var del, skip int
		for _, f := range targets {
			if err := os.Remove(f.path); err == nil {
				del++
			} else {
				skip++
			}
		}
		rescan()
		dialog.ShowInformation("Done",
			fmt.Sprintf("Deleted: %d\nSkipped (locked/missing): %d", del, skip),
			sm.win)
	}, sm.win)
}

// ─────────────────────────────────────────────────────────────────────────────
//  SHARED: MAKE FILE LIST WIDGET
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) makeFileList(items *[]fileItem, onSel func(widget.ListItemID)) *widget.List {
	l := widget.NewList(
		func() int { return len(*items) },
		func() fyne.CanvasObject {
			return widget.NewLabel("[ xxxxxxxxx ]  /path/to/file")
		},
		func(id widget.ListItemID, o fyne.CanvasObject) {
			if id < len(*items) {
				f := (*items)[id]
				o.(*widget.Label).SetText(fmt.Sprintf("[ %-9s ]  %s", humanSize(f.size), f.path))
			}
		},
	)
	l.OnSelected = onSel
	return l
}

// ─────────────────────────────────────────────────────────────────────────────
//  PAGE 3 — EVENT LOG VIEWER
// ─────────────────────────────────────────────────────────────────────────────

var logCols = []string{"Timestamp", "Level", "Source", "Event ID", "Message"}
var logColW = []float32{160, 80, 180, 75, 400}

func (sm *App) logPage() fyne.CanvasObject {
	sm.logStatus = styledText("Select a log and click Load.", 10, clrMuted, false)

	logSelect := widget.NewSelect([]string{"Application", "System", "Security"}, nil)
	logSelect.SetSelected("Application")

	sm.logTable = widget.NewTable(
		func() (int, int) { return len(sm.logData), len(logCols) },
		func() fyne.CanvasObject {
			return widget.NewLabel("                    ")
		},
		func(id widget.TableCellID, o fyne.CanvasObject) {
			lbl := o.(*widget.Label)
			if id.Row < len(sm.logData) && id.Col < len(sm.logData[id.Row]) {
				lbl.SetText(sm.logData[id.Row][id.Col])
			}
			if id.Row == -1 {
				lbl.TextStyle = fyne.TextStyle{Bold: true}
			}
		},
	)
	for i, w := range logColW {
		sm.logTable.SetColumnWidth(i, w)
	}

	btnLoad := actionButton("🔄  Load Events", func() {
		sm.loadEventLog(logSelect.Selected)
	})

	title := styledText("📋  EVENT LOG VIEWER", 14, clrText, true)
	top := container.NewVBox(
		title,
		container.NewHBox(
			styledText("Select Log:", 10, clrText, false),
			logSelect,
			btnLoad,
		),
		sm.logStatus,
	)

	return container.NewBorder(top, nil, nil, nil, sm.logTable)
}

// ─────────────────────────────────────────────────────────────────────────────
//  LOAD EVENT LOG via PowerShell
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) loadEventLog(logName string) {
	sm.logStatus.Text = "Loading…"
	sm.logStatus.Color = clrMuted
	sm.logStatus.Refresh()

	go func() {
		script := fmt.Sprintf(
			`Get-EventLog -LogName '%s' -Newest 300 -ErrorAction SilentlyContinue | `+
				`Select-Object `+
				`@{N='Time';E={$_.TimeGenerated.ToString('yyyy-MM-dd HH:mm:ss')}},`+
				`@{N='Level';E={$_.EntryType}},`+
				`@{N='Source';E={$_.Source}},`+
				`@{N='EventID';E={$_.EventID}},`+
				`@{N='Message';E={($_.Message -replace '\r?\n',' ').Substring(0,[Math]::Min(300,$_.Message.Length))}} | `+
				`ConvertTo-Csv -NoTypeInformation`,
			logName,
		)
		out, err := exec.Command("powershell", "-NoProfile", "-NonInteractive", "-Command", script).Output()
		if err != nil {
			sm.logStatus.Text = "Error loading log: " + err.Error()
			sm.logStatus.Color = clrRed
			sm.logStatus.Refresh()
			return
		}

		r := csv.NewReader(strings.NewReader(string(out)))
		records, _ := r.ReadAll()

		var rows [][]string
		for i, rec := range records {
			if i == 0 {
				continue // skip CSV header
			}
			if len(rec) >= 5 {
				rows = append(rows, []string{rec[0], rec[1], rec[2], rec[3], rec[4]})
			}
		}

		sm.logData = rows
		sm.logTable.Refresh()
		sm.logStatus.Text = fmt.Sprintf("Loaded %d entries from '%s'", len(rows), logName)
		sm.logStatus.Color = clrGreen
		sm.logStatus.Refresh()
	}()
}

// ─────────────────────────────────────────────────────────────────────────────
//  UI HELPERS
// ─────────────────────────────────────────────────────────────────────────────

func styledText(s string, size float32, clr color.Color, bold bool) *canvas.Text {
	t := canvas.NewText(s, clr)
	t.TextSize = size
	t.TextStyle = fyne.TextStyle{Bold: bold}
	return t
}

func actionButton(label string, fn func()) *widget.Button {
	b := widget.NewButton(label, fn)
	b.Importance = widget.HighImportance
	return b
}

func dangerButton(label string, fn func()) *widget.Button {
	b := widget.NewButton(label, fn)
	b.Importance = widget.DangerImportance
	return b
}

func warnButton(label string, fn func()) *widget.Button {
	b := widget.NewButton(label, fn)
	b.Importance = widget.WarningImportance
	return b
}

// ─────────────────────────────────────────────────────────────────────────────
//  HUMAN-READABLE SIZE
// ─────────────────────────────────────────────────────────────────────────────

func humanSize(b int64) string {
	switch {
	case b >= 1_073_741_824:
		return fmt.Sprintf("%.2f GB", float64(b)/1_073_741_824)
	case b >= 1_048_576:
		return fmt.Sprintf("%.1f MB", float64(b)/1_048_576)
	case b >= 1_024:
		return fmt.Sprintf("%d KB", b/1_024)
	default:
		return fmt.Sprintf("%d B", b)
	}
}

// ─────────────────────────────────────────────────────────────────────────────
//  STATUS HELPER
// ─────────────────────────────────────────────────────────────────────────────

func (sm *App) setStatus(msg string) {
	if sm.statusLeft != nil {
		sm.statusLeft.Text = msg
		sm.statusLeft.Refresh()
	}
}
