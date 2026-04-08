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

## Current Communication Baseline
- `1200 bps`
- `8E1`
- `LinkAddressLength = 2`
- `CasduLength = 2`
- `IoaLength = 3`
- `LinkAddress = 105`
- `CasduAddress = 105`
- `OriginatorAddress = 0`

## Current Class Data Rule
- `Class Data` is delivery context, not an IOA attribute.
- Engine truth comes from:
  - primary request context (`FC10` vs `FC11`)
  - `COT`
  - GI state
  - link-layer ACD observations
- Current intended mapping:
  - `FC10 response` -> `Class 1`
  - `FC11 response` -> `Class 2`
  - `GI / INTERROGATED_BY_STATION` -> `Class 2`
  - `BACKGROUND_SCAN / PERIODIC` -> `Class 2`
  - `Spontaneous` stays event-oriented and must not overwrite `COT`

## Current Outstanding Issue
Current sensitive/debug-heavy areas:
- NUC UI can feel heavier than `MainWindow` because it updates:
  - value viewer
  - event log
  - line monitor
  - link trace
  - traffic badges
  - ribbon animations
- `Class Data` debugging must compare:
  - [Services/Iec101/Iec101MasterService.cs](D:/CODEX/NewEx/IEC101MasterTester/Services/Iec101/Iec101MasterService.cs)
  - [ViewModels/MainViewModel.cs](D:/CODEX/NewEx/IEC101MasterTester/ViewModels/MainViewModel.cs)
  - [MainWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/MainWindow.xaml)
  - [Views/NucRedundancyWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucRedundancyWindow.xaml)

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
