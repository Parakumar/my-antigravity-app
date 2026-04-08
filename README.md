# System Monitor & Cleanup — README

## Overview
ACE tool hub is a C# WinForms desktop utility (.NET Framework 4.8) for real-time system monitoring and maintenance on Windows Server 2016 / 2019 / 2022.

---

## Build Instructions

### Prerequisites
- Visual Studio 2019+ **or** .NET SDK 4.8 / MSBuild 16+
- Windows environment (WinForms requires Windows)

### Build via .NET CLI
```
cd "d:\repo\System monittor & Cleanup"
dotnet build SystemMonitorApp.csproj -c Release
```
Output: `bin\Release\net48\SystemMonitorApp.exe`

### Build via Visual Studio
1. Open `SystemMonitorApp.csproj`
2. Set configuration to **Release**
3. **Build → Build Solution** (Ctrl+Shift+B)

---

## Deployment

### Installation Path
```
C:\Apps\SystemMonitor\SystemMonitorApp.exe
```

### Desktop & Start Menu Shortcut
Create a shortcut pointing to `SystemMonitorApp.exe`.  
Right-click shortcut → **Properties → Shortcut → Advanced → Run as administrator ✓**

### Auto-Start via Task Scheduler (Optional)
```xml
<!-- Trigger: At logon | Run Level: Highest -->
schtasks /create /tn "SystemMonitorCleanup" ^
  /tr "C:\Apps\SystemMonitor\SystemMonitorApp.exe" ^
  /sc onlogon /rl highest /f
```

---

## Permissions

The application **must run as Local Administrator** for full feature access.

For non-admin users, add them to these local groups:

| Feature                  | Required Group                     |
|--------------------------|------------------------------------|
| CPU Usage (Tab 1)        | **Performance Monitor Users**      |
| Event Log (Tab 4)        | **Event Log Readers**              |
| Dump / Temp Delete       | Administrator (full access needed) |

```powershell
# Add user to Performance Monitor Users
net localgroup "Performance Monitor Users" DOMAIN\username /add

# Add user to Event Log Readers
net localgroup "Event Log Readers" DOMAIN\username /add
```

---

## Features

| Tab | Name | Description |
|-----|------|-------------|
| 1 | System Monitor | Real-time CPU / RAM / Disk C: with color-coded progress bars (refresh every 2 s) |
| 2 | Dump Cleanup | Scans for `.dmp` files across Windows crash-dump locations; select & delete |
| 3 | Temp Cleanup | Scans `%TEMP%`, `C:\Windows\Temp`, `C:\Windows\Prefetch` for top-level files |
| 4 | Event Log | Loads last 300 entries from Application / System / Security with color-coded rows |

---

## Compatibility

| OS | Supported |
|----|-----------|
| Windows Server 2016 | ✔ |
| Windows Server 2019 | ✔ |
| Windows Server 2022 | ✔ |
| Windows 10 / 11     | ✔ |

Runtime requirement: **.NET Framework 4.8** (pre-installed on Server 2019+; install via Windows Update on Server 2016).

---

## Project Structure
```
SystemMonitorApp.csproj   ← .NET 4.8 WinForms project
Program.cs                ← STAThread entry point
MainForm.cs               ← Full UI + all 4-tab logic (single file, no Designer)
README.md                 ← This file
```

---

## Security Notes
- The app uses `PerformanceCounter`, `ManagementObjectSearcher` (WMI), `System.IO.DriveInfo`, and `System.Diagnostics.EventLog` — all require appropriate permissions.
- File deletion is confirmed via `MessageBox` before execution.
- Locked/in-use files are silently skipped and counted as "skipped" in the result dialog.
