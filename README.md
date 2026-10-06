# BCD Boot Manager — Multi-OS Fast Switcher

A modern, standalone Windows desktop application designed for power users and administrators managing multi-boot Windows environments. It provides one-click instant booting into selected operating systems, in-place display name editing, automatic disk and partition scanning, and default OS selection.

---

## Key Features

- **⚡ Instant One-Click Reboot (No Confirmation Prompt)**:
  - Directly sets the one-time boot sequence (`bcdedit /bootsequence {guid}`) and executes immediate restart (`shutdown /r /t 0 /f`) with zero confirmation dialogs, exactly as required for rapid system switching.
- **🛡️ Always Runs Elevated**:
  - Embedded Windows application manifest with `requestedExecutionLevel level="requireAdministrator"` ensures administrative privileges on startup.
- **Strict Unique Identifier Enforcement**:
  - Never uses the ambiguous literal `"default"`.
  - Always operates exclusively using the exact 36-character unique GUID (`{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}`).
- **💾 Automatic Storage & Windows Detection**:
  - Scans all system storage drives (`DriveInfo`) upon startup.
  - Inspects file systems, labels, free space, and detects offline/online Windows installations.
  - Automatically correlates storage partitions with BCD boot loader entries (`partition=C:`, `\Device\HarddiskVolumeX`).
- **✏️ Inline OS Display Name Editing**:
  - Change the friendly description of any boot entry (`bcdedit /set {guid} description "..."`) directly from the card.
- **★ Set Default OS**:
  - Switch the system's primary default boot entry with a single click (`bcdedit /default {guid}`).
- **⚙️ Boot Menu Timeout Control**:
  - Modify the boot manager timeout (seconds) directly in the header toolbar (`bcdedit /timeout <seconds>`).
- **💾 BCD Store Backup**:
  - Export full BCD store backups to any location before making major changes (`bcdedit /export <path>`).
- **🎨 Modern Windows 11 Fluent Theme**:
  - Dark mode with rounded cards, status badges (`● CURRENT RUNNING OS`, `★ DEFAULT BOOT OS`), and clear visual indicators.

---

## Architecture & Code Structure

Organized strictly by layer first, then feature:

```
c:\Security\BCDeditTool\
├── Configuration/
│   └── BootManager/
│       └── BootManagerOptions.cs       # Strongly typed IOptions with DataAnnotations
├── Interfaces/
│   └── BootManager/
│       ├── IBcdCommandService.cs       # Low-level bcdedit & shutdown interaction
│       ├── IDriveDetectionService.cs   # Storage volume and Windows install scanner
│       └── IBootManagerFacade.cs       # High-level coordinator
├── Models/
│   └── BootManager/
│       ├── BcdEntry.cs                 # Enriched boot loader entry record
│       ├── DriveVolumeInfo.cs          # Disk volume and kernel version record
│       ├── BcdBootManagerConfig.cs     # Global boot manager settings
│       └── BcdOperationResult.cs       # Command execution outcome
├── Services/
│   └── BootManager/
│       ├── BcdCommandService.cs        # Asynchronous bcdedit execution & parser
│       ├── DriveDetectionService.cs    # Disk scan & DOS device path resolution
│       └── BootManagerFacade.cs        # Facade combining drives & BCD entries
├── ViewModels/
│   └── BootManager/
│       ├── MainViewModel.cs            # Root MVVM orchestrator
│       ├── BcdEntryViewModel.cs        # Entry-level commands & inline editing
│       ├── RelayCommand.cs             # Synchronous command
│       └── AsyncRelayCommand.cs        # Asynchronous command with cancellation
├── Views/
│   └── BootManager/
│       ├── MainWindow.xaml             # Modern Windows 11 XAML interface
│       ├── MainWindow.xaml.cs          # View code-behind
│       ├── BoolToVisibilityConverter.cs
│       └── InverseBoolConverter.cs
├── app.manifest                        # Administrator execution level requirement
├── appsettings.json                    # Configuration settings
└── BcdEditTool.csproj                  # .NET 10 WPF project
```

---

## Build & Publishing

### Framework-Dependent Single-File (Small footprint ~1.1 MB):
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Fully Self-Contained Standalone Executable (Runs on any Windows PC without .NET installed):
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin\Release\Standalone
```
Executable output: `bin\Release\Standalone\BcdEditTool.exe`

---

## Safety & Security Considerations

- BCD modifications and system reboot commands require elevated administrator access.
- Always use the **"💾 Backup BCD"** feature before modifying identifiers on production systems.
