# AI_QUICK_MAP.md

## What This Repo Is
WPF `.NET Framework 4.8` IEC-101 testing suite for master analysis, NUC redundancy, SOE/buffer auditing, availability, and a dedicated slave simulator.

## Fast File Map

| Area | Main Files | What They Control |
|---|---|---|
| App startup | `App.xaml`, `App.xaml.cs` | Default entry window, shared viewmodel bootstrap |
| Legacy master UI | `MainWindow.xaml`, `MainWindow.xaml.cs` | Original analyzer workspace, still callable |
| NUC master workspace | `Views/NucRedundancyWindow.xaml`, `Views/NucRedundancyWindow.xaml.cs` | Primary redundancy operator UI, ribbons, value viewer, event log, status history |
| NUC redundancy backend | `Services/Redundancy/NucRedundancyService.cs`, `Services/Redundancy/NucIec101LinkChannel.cs` | Active/standby orchestration, channel role switching, per-port snapshots |
| IEC-101 master engine | `Services/Iec101/Iec101MasterService.cs` | Actual lib60870.NET communication, serial I/O, polling, link test, command enqueue |
| Command lifecycle | `ViewModels/CommandLifeTrackerEngine.cs`, `ViewModels/MainViewModel.cs` | TX/confirm/reject/timeout tracking, command monitor rows |
| Findings engine | `ViewModels/MainViewModel.cs`, `Models/FindingRow.cs`, `Views/FindingsWindow.xaml(.cs)` | Rule-based findings and verdict display |
| Availability dashboard | `Views/AvailabilityDashboardWindow.xaml(.cs)`, `Models/AvailabilityTimelineRow.cs` | Uptime/downtime/reconnect scoring and trend view |
| Buffered event audit | `Views/BufferedEventAuditWindow.xaml(.cs)`, `Models/BufferReplaySession.cs` | SOE replay, duplicates, FIFO, 600+ buffer test |
| NUC SOE audit | `Views/NucSoeAuditWindow.xaml(.cs)` | Redundancy-specific SOE and switchover evidence |
| Slave simulator app | `IecSlaveSimulator\MainWindow.xaml`, `IecSlaveSimulator\Views\NucSlaveWindow.xaml(.cs)` | RTU/slave simulator shell |
| Slave dual-link host | `IecSlaveSimulator\Services\NucDualLinkSlaveHost.cs`, `IecSlaveSimulator\Services\NucSlaveController.cs` | Dual-link slave ownership and orchestration |
| Shared slave application core | `IecSlaveSimulator\Services\SharedOutstationCore.cs`, `IecSlaveSimulator\Services\SharedSignalStore.cs`, `IecSlaveSimulator\Services\SharedEventBuffer.cs` | One shared process image across both slave links |
| Theme | `SharedUi/ModernTheme.xaml` | Global dark UI and DataGrid behavior |
| Official point profiles | `Services/Profiles/OfficialPointProfile.cs`, `Services/Profiles/OfficialPointProfiles.cs` | Central point mapping and profile truth |

## Core Architecture Rules
- `lib60870.NET` is the only IEC-101 communication path.
- UI/analyzer layers are passive.
- NUC A/B are two physical channels for one logical outstation identity.
- One active link, one standby link.
- Do not invent protocol facts in UI logic.

## Current Important Behavior
- `NUCWindow` is becoming the main operator shell.
- `MainWindow` remains callable as legacy/advanced.
- Slave simulator is its own workspace and should be treated as a separate subsystem.
- Event log should stay SCADA-like, not raw frame spam.

## Current Outstanding Issue
The last active problem is command confirmation latency on NUC:
- GI is okay again
- command confirmation still feels slower than MainWindow
- likely cause is extra UI/observer work in the NUC command RX path

## Best Starting Point For Debugging
If command responsiveness is the topic:
1. `ViewModels/MainViewModel.cs`
2. `Services/Redundancy/NucRedundancyService.cs`
3. `Services/Redundancy/NucIec101LinkChannel.cs`
4. `Services/Iec101/Iec101MasterService.cs`
5. `ViewModels/CommandLifeTrackerEngine.cs`

If slave dual-link behavior is the topic:
1. `IecSlaveSimulator\Services\SharedOutstationCore.cs`
2. `IecSlaveSimulator\Services\NucDualLinkSlaveHost.cs`
3. `IecSlaveSimulator\Services\NucSlaveController.cs`
4. `IecSlaveSimulator\Views\NucSlaveWindow.xaml.cs`

## Practical Rule For Future Changes
Keep protocol truth in services, keep verdicts in the viewmodel/rule layer, and keep the UI lightweight.

