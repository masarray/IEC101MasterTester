# PROJECT_OVERVIEW_FOR_AI.md

## Purpose
This document is a compact architecture map for AI continuity.
It explains the repository structure, the role of each major layer, and the current outstanding issue so another AI can resume work without rereading the full chat history.

## One-Sentence Product Summary
This is a WPF `.NET Framework 4.8` IEC-101 master/slave testbench for PLN Pusertif style FAT and troubleshooting, with:
- a master analyzer UI
- a dedicated NUC redundancy workspace
- a dedicated IEC-101 slave simulator workspace
- availability, buffer, SOE, findings, and command lifecycle tooling

## High-Level Design
The repo now contains two closely related applications/workspaces:

1. `IEC101MasterTester`
   - master/analyzer side
   - main operator workspace
   - event log, value viewer, findings, NUC redundancy analysis, availability dashboard, SOE audit

2. `IecSlaveSimulator`
   - RTU/slave simulation side
   - dual-link slave foundation
   - shared buffer / command-state / runtime signal control
   - intended to behave like the RTU/gateway under test

The key rule across the project:
- communication logic stays inside `lib60870.NET`
- UI/analyzer layers are passive observers and validators
- redundancy policy belongs above the protocol stack

## Protocol Truth Rules

### Communication baseline
Reference IEC-101 profile used by this repo:
- `1200 bps`
- `8E1`
- `Link Address Length = 2`
- `CAASDU Length = 2`
- `IOA Length = 3`
- `Link Address = 105`
- `CAASDU = 105`
- `Originator Address = 0`

### Class Data semantics
`Class Data` is not a literal field inside the IOA payload.

It is delivery-context metadata inferred from IEC-101 transaction flow:
- response to `FC10` -> `Class 1`
- response to `FC11` -> `Class 2`
- `GI / INTERROGATED_BY_STATION` -> `Class 2`
- `BACKGROUND_SCAN / PERIODIC` -> `Class 2`
- spontaneous traffic is event-oriented and must not overwrite factual `COT`

Practical split used in this project:
- `COT` = factual application-layer cause
- `ACD` = factual link-layer indication from secondary frame
- `Class Data` = inferred delivery context

## Main Repository Structure

### `App.xaml` / `App.xaml.cs`
Current app entrypoint.
The app startup was changed so the NUC window opens as the default operator shell.

Important behavior:
- a shared `MainViewModel` is created at app startup
- the shared viewmodel is initialized once
- `NucRedundancyWindow` is opened as the default window
- the legacy `MainWindow` remains callable

Why this matters:
- the NUC window is being promoted toward the main operator shell
- shared state avoids the NUC window and legacy MainWindow behaving like two independent apps

### `MainWindow.xaml` / `MainWindow.xaml.cs`
Legacy master/analyzer workspace.
Still important for:
- baseline command workflow
- event log and value viewer behavior reference
- findings workflow
- opening specialized windows

Current role:
- remains available as legacy/advanced view
- not the primary target for new workflow, but still part of the product

### `Views/NucRedundancyWindow.xaml` / `.cs`
Dedicated NUC workspace.
This is now the main operational redundancy window.

Current behavior:
- compact ribbon top status
- dual-link visual with flow animation
- NUC event log
- value viewer
- status history
- SOE audit access
- availability dashboard access
- ability to open the legacy MainWindow

Important design direction:
- this window is becoming the primary operator shell for redundancy testing
- it should stay responsive and compact
- it should focus on auditability rather than decorative dashboards

Important current caution:
- NUC must not diverge from `MainWindow` on protocol truth
- if `Value Viewer` class/timestamp differs, compare:
  - `Services/Iec101/Iec101MasterService.cs`
  - `ViewModels/MainViewModel.cs`
  - metadata overwrite rules
  - NUC dual-link last-writer behavior

### `Views/NucSoeAuditWindow.xaml` / `.cs`
Dedicated NUC SOE audit workspace.
Used to inspect switchovers, buffer replay, and continuity evidence in more detail than the main NUC screen.

### `Views/AvailabilityDashboardWindow.xaml` / `.cs`
Dedicated availability analysis window.
It consumes passive analyzer state and computes availability-style scoring and trend visibility.

### `Views/BufferedEventAuditWindow.xaml` / `.cs`
Dedicated buffered-event/SOE replay analysis window.
Used for buffer capacity, replay continuity, duplicates, and sequence validation.

### `Views/SignalCommandWindow.xaml` / `.cs`
Command execution window.
Used for direct operate / select-before-operate flows.
It also shows command lifecycle feedback.

### `Views/LineMonitorWindow.xaml` / `.cs`
Technical line monitor.
Shows raw RX/TX and frame-level observations.

### `Views/FindingsWindow.xaml` / `.cs`
Analyzer findings window.
This is rule-engine oriented, not operator control.

### `Views/ConnectionSetupWindow.xaml` / `.cs`
General connection setup.
Used by the master side for normal IEC-101 settings.

### `Views/NucLinkSetupWindow.xaml` / `.cs`
NUC-specific link setup.
Defines link A / link B COM ports and redundancy mode.

### `SharedUi/ModernTheme.xaml`
Global dark industrial WPF theme.
It now carries the spreadsheet-like `DataGrid` behavior used across the project.

### `Services/Iec101/*`
Normal IEC-101 master implementation on the analyzer side.

Key files:
- `Services/Iec101/Iec101MasterService.cs`
- `Services/Iec101/Iec101DataMapper.cs`
- `Services/Iec101/LineMonitorFormatter.cs`
- `Services/Iec101/IIec101MasterService.cs`

This is the baseline master pipeline that talks to `lib60870.NET`.

### `Services/Redundancy/*`
NUC redundancy orchestration layer.

Key files:
- `Services/Redundancy/NucRedundancyService.cs`
- `Services/Redundancy/NucIec101LinkChannel.cs`
- `Services/Redundancy/INucRedundancyService.cs`
- `Services/Redundancy/INucLinkChannel.cs`

This layer is the abstraction above two serial channels.

Architectural rule:
- one outstation identity
- two physical serial links
- one active link at a time
- one standby link under supervision
- the redundancy controller decides link role

### `ViewModels/MainViewModel.cs`
This is the central analyzer brain for `IEC101MasterTester`.

It currently owns:
- event log generation
- value viewer updates
- NUC state visualization
- command lifecycle tracking
- findings generation
- availability and buffer state
- SOE / redundancy audit hooks

This file is large and central.
Most cross-window behavior flows through it.

It also carries the most important NUC-specific projection logic:
- NUC value aggregation
- NUC event log projection
- NUC line monitor projection
- metadata overwrite guards to prevent `GI/Class 2` traffic from blindly stomping stronger value metadata

### `ViewModels/CommandLifeTrackerEngine.cs`
Small dedicated command lifecycle engine.

Purpose:
- track TX
- track confirm / reject
- track timeout
- support command monitor rows

This is the command verdict backbone and is meant to be faster and more deterministic than general UI state.

### `ViewModels/*`
Other viewmodels contain supporting UI state:
- `NucEndpointPanelViewModel.cs`
- `NucLinkVisualViewModel.cs`
- `NucStatusBadgeViewModel.cs`
- `NucLinkSetupViewModel.cs`
- `ConnectionSetupViewModel.cs`
- `RelayCommand.cs`
- `ViewModelBase.cs`

### `Models/*`
Core shared data contracts.

Important models:
- `ConnectionSettings`
- `ValueViewerRow`
- `LineMonitorRow`
- `EventLogRow`
- `FindingRow`
- `CommandLifeMonitorRow`
- `AvailabilityTimelineRow`
- `RedundancyTimelineRow`
- `BufferReplaySession`
- `NucRedundancySettings`
- `NucRedundancySessionState`
- `NucChannelSnapshot`
- `NucChannelState`
- `NucChannelRole`
- `NucControllerState`
- `NucLinkHealthState`

### `Services/Profiles/*`
Official point profile layer.

Important files:
- `Services/Profiles/OfficialPointProfile.cs`
- `Services/Profiles/OfficialPointProfiles.cs`

This layer centralizes point metadata so the analyzer stops scattering raw IOA assumptions everywhere.

### `IecSlaveSimulator/*`
Dedicated slave simulator project/workspace inside the same repo.

Important files:
- `IecSlaveSimulator/MainWindow.xaml`
- `IecSlaveSimulator/MainWindow.xaml.cs`
- `IecSlaveSimulator/Views/NucSlaveWindow.xaml`
- `IecSlaveSimulator/Views/NucSlaveWindow.xaml.cs`
- `IecSlaveSimulator/Views/NucSlaveLinkSetupWindow.xaml`
- `IecSlaveSimulator/Views/NucSlaveLinkSetupWindow.xaml.cs`
- `IecSlaveSimulator/Services/SharedOutstationCore.cs`
- `IecSlaveSimulator/Services/NucDualLinkSlaveHost.cs`
- `IecSlaveSimulator/Services/NucActiveStandbyArbiter.cs`
- `IecSlaveSimulator/Services/NucSlaveController.cs`
- `IecSlaveSimulator/Services/Iec101SlaveService.cs`
- `IecSlaveSimulator/Services/BufferInjectionController.cs`
- `IecSlaveSimulator/Services/SharedEventBuffer.cs`
- `IecSlaveSimulator/Services/SharedSignalStore.cs`

The slave side is being refactored around:
- per-port endpoint state
- shared application core
- active/standby arbiter

## Current NUC Architecture Intent

### Master side
The NUC master/analyzer is supposed to:
- observe two physical ports
- decide active vs standby presentation
- log real SCADA-style events
- track command lifecycle
- expose health and availability state
- never invent protocol facts

### Slave side
The NUC slave simulator is supposed to:
- own the process image and event truth
- send class-1 / spontaneous events
- support buffer replay and command feedback
- maintain consistent state across both physical links
- expose the dual-link slave behavior through the RTU simulator

### Redundancy model
The intended model is:
- one logical outstation identity
- two redundant serial links
- same link-layer address and same CA on both channels
- one active traffic path
- one standby supervision path

## Important Runtime Concepts

### Command lifecycle
Command operation is tracked separately from general polling.
The command system uses:
- TX registration
- confirmation/reject matching
- timeout handling
- command-life monitor rows

This is intentionally more deterministic than general event logging.

### ACD / class
ACD and class behavior are analyzer-visible and affect rule logic.
The project treats them as part of SCADA behavior validation, not as UI decorations.

### GI
GI is one-shot behavior, not cyclic polling.
It must not be confused with background class-2 polling.

### Event log
The event log is intended to be SCADA-style:
- real incoming events
- command confirm/reject events
- selected protocol state events that matter to operator evidence

It should not become a raw frame spam dump.

## Outstanding Issue At The End Of The Thread
The main unresolved issue before this handoff:

### Symptom
On NUC, command confirmation feels slower or less stable than in the legacy MainWindow workflow.
At times GI also started to feel delayed when we experimented with making command confirmation more responsive.

### What was observed
- GI became responsive again after simplifying some NUC flow
- command confirmation still felt slower than expected
- attempts to move NUC command handling around sometimes made GI or command flow worse

### Likely root cause category
Not protocol framing.
More likely:
- UI refresh overhead from NUC state updates
- command-lifecycle / RX matching path too heavy
- repeated redundancy-visual refreshes during command RX
- too much observer work in the same callback path as command confirmation

### Current working conclusion
Command confirmation on NUC should stay on a lightweight path and should not wait for:
- heavy redundancy visual refresh
- excessive journal updates
- unnecessary state recomputation

The likely next step is to isolate exactly which post-RX work is still too heavy and keep only the minimum confirmation path.

## Important File Relationships

### `MainViewModel.cs`
Central coordinator:
- subscribes to master service events
- subscribes to NUC redundancy service events
- updates visual models
- logs event/history/findings
- routes command actions from windows

### `NucRedundancyService.cs`
Redundancy coordinator:
- owns two `NucIec101LinkChannel` objects
- decides active/standby role
- handles start/stop/switchover
- forwards channel events back to `MainViewModel`

### `NucIec101LinkChannel.cs`
Per-port wrapper:
- wraps one `IIec101MasterService`
- manages one channel role
- handles standby supervision behavior
- exposes per-channel snapshots

### `Iec101MasterService.cs`
Actual IEC-101 communication engine:
- serial port open/close
- lib60870 master object
- polling
- link-layer test
- command enqueue/worker loop

### `CommandLifeTrackerEngine.cs`
Pure lifecycle tracker:
- tracks command transaction state
- handles confirm/reject/timeout matching

### `SharedUi/ModernTheme.xaml`
Global styling layer:
- dark theme
- reusable DataGrid styling
- consistent operator look

## Practical Reading Order For Another AI
If another AI has to continue, read in this order:
1. `AGENTS.md`
2. this file
3. `CODEX_HANDOFF.md`
4. `ViewModels\MainViewModel.cs`
5. `Services\Redundancy\NucRedundancyService.cs`
6. `Services\Redundancy\NucIec101LinkChannel.cs`
7. `Services\Iec101\Iec101MasterService.cs`
8. `IecSlaveSimulator\Services\SharedOutstationCore.cs`
9. `IecSlaveSimulator\Services\NucDualLinkSlaveHost.cs`
10. `Views\NucRedundancyWindow.xaml(.cs)`

## Short Working Notes
- The repository now has a lot of specialized windows.
- The NUC workflow is becoming the primary operator shell.
- The slave simulator is its own important subsystem, not just a test utility.
- The biggest current risk is accidental coupling between command confirmation and redundancy UI refresh.
- Keep protocol truth in the communication layer.
- Keep observability and verdicts in the analyzer layer.

## If You Continue Development
Favor changes that:
- reduce unnecessary UI churn
- keep command handling lightweight
- preserve GI responsiveness
- keep redundancy visuals accurate but not expensive
- keep the slave simulator and master analyzer roles cleanly separated
