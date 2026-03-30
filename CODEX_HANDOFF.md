# CODEX_HANDOFF.md

## Latest local handoff - 2026-03-30

### Update snapshot - latest source of truth

Current working branch:
- `codex/link-trace-restore`

Current local focus:
- `Views/NucLinkTraceWindow.xaml`
- `Controls/NucLinkTraceTapeControl.cs`
- `Views/NucLinkTraceWindow.xaml.cs`

Current NUC Link Trace state:
- fixed `60 second` tape
- `TimelineBucketCount = 300`
- `BucketSizeSeconds = 0.2`
- two traffic lanes only:
  - `Link A`
  - `Link B`
- user rejected experimental variants with:
  - TX/RX split renderer
  - semantic overlay blocks
  - decorative shading clutter

Latest interaction fixes completed:
- plot click now only works inside the real graph area
- cursor/time mapping uses the same plot rect as the renderer
- selected position is bucket-based, not free-pixel based
- clicks outside graph area should not generate inspect time

Latest inspect-mode fixes completed:
- clicking the tape enters frozen inspect mode
- inspect keeps selected `windowStart/windowEnd` stable after click
- lower line-monitor grids now query around the selected bucket instead of drifting with live time
- `Live` resumes moving follow-right mode and clears inspect freeze

Current known caveat:
- graph-to-frame trust is better but not finished yet
- next work should focus on stronger bucket-to-real-event anchoring
- goal:
  - click a spike/burst
  - get matching GI/Class1/Class2 rows in the lower tables
  - keep the same result for the same clicked bucket
- do not reintroduce:
  - TX/RX split renderer
  - orange/semantic block overlays
  - decorative timeline experiments

Current working branch:
- `codex/link-trace-restore`

Current local focus:
- `Views/NucLinkTraceWindow.xaml`
- `Views/NucLinkTraceWindow.xaml.cs`

Latest restored Link Trace baseline:
- `NucLinkTraceWindow` is back in project and opens from `NucRedundancyWindow`
- timeline lanes are:
  - top = `Link A`
  - bottom = `Link B`
- direction encoding:
  - `TX` = blue spike up
  - `RX` = green spike down
- timeline detail window:
  - `60 seconds`
  - `TimelineBucketCount = 60`
  - `BucketSizeSeconds = 1`
- no gray shading
- no semantic coloring layer
- left labels are `A` / `B`
- help text is:
  - `Link A / Link B lanes • TX spike up • RX spike down • drag to scrub`

Latest runtime/resource repairs:
- `NucLinkTraceWindow` now has local fallback resources for:
  - `AppBackgroundBrush`
  - `AppForegroundBrush`
  - `SecondaryTextBrush`
  - `NucInnerBrush`
  - `CardBorderStyle`
  - `SectionTitleStyle`
  - `ActionButtonStyle`
- root `Window` background/foreground use literal colors to avoid early parse failure

Latest timeline behavior patch:
- detail timeline remains `60s`
- auto slider appears when total capture span exceeds `60s`
- clicking timeline sets `viewportStart`
- grids now read `50` events starting from `viewportStart`
- `Live` mode resets viewport to the newest 60-second window

Important known caveat for next Codex:
- this latest slider/viewportStart patch is build-clean, but runtime UX still needs verification
- user specifically wants recorder-like behavior:
  - click timeline -> move visible reading point
  - slider only when capture span > 60s
  - preview grids must reflect the selected time window clearly
- do not redesign Link Trace again
- do not remove the feature
- do not reintroduce:
  - gray shading
  - semantic color experiments
  - TX/RX as lane labels


## Purpose
This repository is evolving from a lightweight IEC-101 master tester into a PLN Pusertif-oriented protocol analyzer, behavior validator, findings engine, and FAT evidence collector.

This file is the cross-laptop handoff reference.
Read this before continuing work.

## Mandatory baseline
The software must follow:
- `AGENTS.md`
- `SPLN S6.003:2020` as the primary testing logic baseline

The analyzer/test logic must align to these testing domains:
- `ACD / class`
- `timing`
- `SOE`
- `redundancy`
- `GI`
- `command behavior`

If something is not explicit in the official test baseline:
- mark it as `ConfigurablePolicy`
- or `UnknownFromDocument`

Do not invent requirements outside the PLN testing profile.

## Product direction
This is not a generic IEC-60870 logger.

Target product role:
- protocol analyzer
- behavior validator
- rule-based findings engine
- FAT evidence collector

It must validate observable gateway behavior, not only capture frames.

## Non-negotiable architecture rules
All active IEC-101 communication must go through `lib60870.NET` only.

Analyzer/UI code may:
- read raw frame callbacks
- read ASDU callbacks
- read link-layer state callbacks
- normalize evidence
- generate findings and verdicts

Analyzer/UI code must not:
- create independent polling logic
- invent protocol state that overrides callback facts
- relabel `COT` based on guesswork
- fabricate `ACD`
- create alternative communication routines

## Current stable foundation
Already working reasonably well:
- event log capture
- ACD detection
- command lifecycle logging
- command tracker monitor in `SignalCommandWindow`
- findings engine foundation
- class-1 burst summary
- official point profile foundation
- buffer / SOE audit window MVP
- slave availability state machine foundation

Recent behavioral fixes already in place:
- command select/execute flow more stable
- command confirm timing tracked semantically
- auto GI on connect made deterministic
- class-1 burst summary no longer logs empty burst during GI window
- class-1 burst finalization uses non-blocking grace delay
- false finding for `Spont` + `Class 2` snapshot has been suppressed
- class-behavior findings are now profile-aware
- ACD expectation finding now uses official profile metadata
- availability dashboard now shows slave health state (`Disconnected`, `Transport up`, `Link responsive`, `Application responsive`, `No application data`, `Silent`, `Degraded`)
- slave availability findings now distinguish silent / stale application data / degraded response pressure without declaring false transport disconnect

## Current major direction
Next major workstreams:
1. `Availability dashboard`
2. findings/rule engine refinement to match PLN timing/SOE/GI logic
3. point-profile adoption deeper into analyzer paths
4. deepen `NUC redundancy` from MVP observer into fuller dual-link controller if project scope confirms it
5. refine slave availability thresholds and timeline chart from passive evidence

## Current project risk
The project still contains legacy addressing assumptions and hardcoded IOAs in multiple places.

This means:
- current analyzer behavior can be technically useful
- but not yet fully compliant with official PLN Pusertif point mapping

Therefore the next safe step is:
- introduce centralized point profiles
- migrate raw IOA assumptions to stable `PointKey` references
- preserve current working behavior while doing so

## Official testing intent summary
PLN Pusertif is validating that the gateway behaves correctly as an integrated system:
- IEC 61850 on IED/SAS side
- IEC 60870-5-101 / 104 on master side
- correct point mapping
- correct class/event behavior
- correct `ACD` / `COT` / timestamp behavior
- correct command-result behavior
- robust SOE buffering and replay
- correct main/backup redundancy behavior
- correct GI/communication feature behavior
- repeatable operation across repeated tests

Operational test areas that matter for this software:
- `MLK`
- `TSS`
- `TSD`
- `TM`
- `RCD`
- `RCA`
- `TPI`
- `CTC`
- `SOE`
- time synchronization
- communication features

## Official PLN communication profile baseline
### IEC-101 reference profile
- Link Address length = `2 octets`
- CAASDU length = `2 octets`
- IOA length = `3 octets`
- COT length = `2 octets`
- Baud rate = `1200 bps`
- Serial framing = `8E1`
- Reference ports in forms: `COM21/22`
- CAASDU main = `105`
- CAASDU backup = `105`

### IEC-104 reference profile
- Reference IP = `172.21.1.35`
- CAASDU = `105`

### Equipment-side assumptions in forms
- serial: `RS-232 / RS-485`
- supported serial data rates: `300-19200`
- serial ports: `4 ports (2 redundant)`
- ethernet: `100 Base`
- ethernet ports: `4`
- IEC-101 mandatory
- IEC-104 tested profile exists
- IEC-61850 used on SAS/IED side

## Official PLN point/addressing direction
Do not keep scattering raw IOAs across random files.

Create a centralized `OfficialPointProfile` with:
- `LegacyProfile`
- `PlnPusertif101Profile`
- `PlnPusertif104Profile`

All analyzer rules should eventually resolve stable internal `PointKey` first, then IOA/type metadata from the active profile.

Point groups that must be modeled from official profile:
- TSS / single-point / MLK points
- TSD / double-point points
- analog / TM / TPI points
- command / RCA / CTC points
- command-to-feedback relations

## Critical protocol interpretation rules
### ACD / class / COT
For IEC-101 test behavior:
- TSS, TSD, MLK behave as class-1 style events
- `ACD = 1` expected for those resulting event behaviors
- `COT = 3 / Spont` expected

For analog / TM / RCA templates:
- `ACD = 0` expected profile behavior
- `COT = 3` still expected per testing profile

Important command distinction:
- do not require command request frames themselves to assert `ACD`
- instead validate:
  - command issued
  - result/status event observed
  - resulting event follows correct class/ACD behavior when applicable

### Redundancy GI policy
Do not hardcode GI-after-switchover as universal truth.

Treat it as:
- `ConfigurablePolicy`
- profile-dependent

Redundancy verdict must record:
- switchover observed
- communication continuity
- GI observed / not observed

But GI-after-switchover must remain configurable unless project owner explicitly fixes the rule.

## Window strategy
### MainWindow
Keep `MainWindow` stable and focused on normal operations:
- value viewer
- event log
- status history
- command panel
- lightweight analyzer visibility

It should not become the main FAT workspace for all specialized tests.

### Dedicated test windows
Use separate windows for specialized FAT workflows:
- `NUCRedundancyWindow`
- `AvailabilityTestWindow`
- `BufferedEventAuditWindow`

Reason:
- better maintainability
- lower risk to `MainWindow`
- clearer operator workflow
- easier isolation of specialized logic/reporting

### NUC redundancy ownership model
Decision already made:
- when `NUCRedundancyWindow` opens, `MainWindow` communication session must stop
- COM/communication ownership moves exclusively to the NUC window
- `MainWindow` should be hidden while NUC session runs
- when NUC window closes:
  - its session stops
  - ports are released
  - `MainWindow` shows again

Implementation note:
- UX can look like main window closes
- safer implementation is `disconnect + hide`, not destroy the main window

### Availability and buffer windows
Preferred strategy:
- separate windows
- but still reuse shared analyzer/evidence pipeline
- do not create duplicated protocol parsing logic per window

## Communication ownership rule
One communication owner at a time.

Never allow two active controllers to open/use the same COM port simultaneously.

Safe ownership model:
- `MainWindow` owns normal live session
- `NUCRedundancyWindow` owns exclusive redundancy session when opened
- other analytical windows should be observer/reporting layers, not parallel controllers

## Required architecture direction
Refactor safely toward these layers:

### 1. OfficialPointProfile layer
Single source of truth for:
- `PointKey`
- IOA
- TypeId
- mnemonic
- category
- expected class behavior
- expected `COT`
- timestamp expectation
- related command/feedback mapping
- engineering range

### 2. Shared evidence pipeline
Protocol events should be parsed once and normalized once.

All windows/rules should consume shared evidence, not re-parse independently.

### 3. Rule engine
All pass/fail logic and findings must live here.

Rule engine responsibilities:
- classify
- correlate
- threshold-check
- generate finding records
- support report/dashboard views

### 4. Specialized test windows
Each window should consume evidence/rule outputs, not duplicate protocol truth logic.

## Required core models
The codebase should move toward these models.

### PointDefinition
- `PointKey`
- `Ioa`
- `TypeId`
- `Mnemonic`
- `Name`
- `Category`
- `Bay`
- `ValueKind`
- `IecClass`
- `ExpectedCot`
- `HasTimestamp`
- `RelatedCommandPointKey`
- `RelatedFeedbackPointKey`
- `EngineeringMin`
- `EngineeringMax`
- `RawMin`
- `RawMax`
- `Notes`

### ProtocolEvidence
- `TimestampUtc`
- `Source`
- `Direction`
- `Ioa`
- `TypeId`
- `Cot`
- `AcdObserved`
- `ValueText`
- `ValueRaw`
- `LinkPath`
- `IsBufferedReplay`
- `EventSequence`
- `ParsedMeaning`

### FindingRecord
- `Severity`
- `RuleCode`
- `Category`
- `Title`
- `Summary`
- `Observed`
- `Expected`
- `EvidenceCount`
- `PrimaryIoa`
- `RelatedIoas`
- `FirstSeenUtc`
- `LastSeenUtc`
- `PassFailImpact`
- `SuggestedAction`

### CommandTransaction
- `CommandId`
- `PointKey`
- `CommandIoa`
- `RelatedFeedbackIoa`
- `IssuedAtUtc`
- `AcknowledgedAtUtc`
- `ChangedAtUtc`
- `ExpectedMaxAckMs`
- `ExpectedMaxChangeMs`
- `ResultState`
- `TimeoutStage`
- `EvidenceIds`

### LinkChannelState
- `ChannelName`
- `Role`
- `Connected`
- `ActiveForTraffic`
- `FaultPointKey`
- `LastSwitchUtc`
- `SwitchoverCount`

### BufferReplaySession
- `SessionId`
- `DisconnectedAtUtc`
- `ReconnectedAtUtc`
- `BufferedEventCount`
- `ReplayEventCount`
- `MissingEventCount`
- `DuplicateEventCount`
- `FifoViolationCount`
- `SampleCheckCount`
- `SampleTimestampViolationCount`
- `MeetsMinimum600Events`

## Required finding categories
The findings engine should align to these categories:
- `ACD`
- `ClassBehavior`
- `Timing`
- `SOE`
- `Buffer`
- `Redundancy`
- `GI`
- `Command`
- `AnalogAccuracy`
- `TimeSync`
- `CommunicationFeatures`

Minimum rule codes to support over time:
- `ACD_EXPECTED_NOT_OBSERVED`
- `ACD_STUCK_ASSERTED`
- `CLASS1_WITHOUT_ACD`
- `CLASS2_MISCLASSIFIED`
- `STATUS_PROPAGATION_EXCEEDED`
- `TIMETAG_DELTA_EXCEEDED`
- `COMMAND_ACK_TIMEOUT`
- `COMMAND_EFFECT_TIMEOUT`
- `ANALOG_UPDATE_DELAY_EXCEEDED`
- `SOE_MIN_CAPACITY_NOT_MET`
- `SOE_FIFO_VIOLATION`
- `SOE_REPLAY_MISSING_EVENTS`
- `SOE_REPLAY_DUPLICATE_EVENTS`
- `SOE_TIMESTAMP_DELTA_EXCEEDED`
- `REDUNDANCY_SWITCHOVER_FAILED`
- `REDUNDANCY_STATUS_POINT_MISSING`
- `REDUNDANCY_COMM_LOSS_EXCEEDED`
- `REDUNDANCY_UNEXPECTED_REVERT`
- `REDUNDANCY_SWITCHOVER_OBSERVED_WITH_GI`
- `REDUNDANCY_SWITCHOVER_OBSERVED_WITHOUT_GI`
- `GI_INCOMPLETE_RESPONSE`
- `PARTY_LINE_BEHAVIOR_FAILURE`
- `MULTI_MASTER_BEHAVIOR_FAILURE`
- `BOUNCING_DELAY_FAILURE`
- `COMTRADE_TRANSFER_FAILURE`
- `ANALOG_ACCURACY_EXCEEDED`
- `SETPOINT_FEEDBACK_MISMATCH`
- `COMMAND_RESULT_EVENT_WITHOUT_ACD`
- `COMMAND_NO_RESULT_EVENT`
- `TAP_POSITION_MISMATCH`
- `TAP_COMMAND_TO_FEEDBACK_TIMEOUT`

## Roadmap
### Phase 1 - Official point profile migration
Goal:
- introduce `OfficialPointProfile`
- centralize official PLN IOA/type metadata
- map existing hardcoded IOA assumptions to `PointKey`
- preserve current behavior

Current status:
- foundation completed
- project now has:
  - `Models\PointDefinition.cs`
  - `Services\Profiles\OfficialPointProfile.cs`
  - `Services\Profiles\OfficialPointProfiles.cs`
- mapper and selected-command suggestion already consume official profile metadata with legacy fallback
- build verified after phase completion

Practical work:
- inspect current hardcoded IOAs and type semantics
- add point profile models/services
- keep legacy behavior available
- begin using point lookup in event log/ACD/findings paths

### Phase 2 - Findings/rule engine refinement
Goal:
- separate analyzer logic from UI concerns
- align findings to official profile metadata and thresholds

Practical work:
- move toward rule-code-driven findings
- keep current `Findings` UI, but feed it more structured data
- refine binary class-2-only and analog spontaneous detection

Current status:
- in progress, first milestone completed
- `FindingRow` now has `RuleCode`
- class behavior findings now resolve official point metadata
- ACD expectation finding implemented for official class-1 spontaneous points
- command-related rejection findings now use explicit rule codes

### Phase 3 - Buffer / SOE test window
Goal:
- support formal SOE/buffer validation per PLN expectations

Required outputs:
- disconnected state detection
- buffered event count
- replay count
- missing count
- duplicate count
- FIFO verdict
- sample timestamp verdict
- final findings summary

Important formal baseline:
- minimum replay capacity to validate = `600 events`
- do not hardcode `9000` as pass threshold

Current status:
- MVP implemented and build-verified
- dedicated window added:
  - `Views\BufferedEventAuditWindow.xaml`
  - `Views\BufferedEventAuditWindow.xaml.cs`
- session model added:
  - `Models\BufferReplaySession.cs`
- `MainWindow` now opens the SOE / Buffer Audit window
- `MainViewModel` now tracks:
  - disconnect detection
  - reconnect detection
  - replay count
  - duplicate count
  - FIFO violation count
  - sample-check count (`7%`)
  - minimum-600 verdict
- findings generated from replay anomalies currently include:
  - `SOE_FIFO_VIOLATION`
  - `SOE_REPLAY_DUPLICATE_EVENTS`
  - `SOE_MIN_CAPACITY_NOT_MET`
- current logic is intentionally lightweight and evidence-oriented; deeper gap/timestamp analysis can be expanded in later refinement

### Phase 4 - NUC redundancy link test window
Goal:
- support dedicated redundancy/failover validation

Required outputs:
- active link
- main/backup status
- `L1FT` / `L2FT` / `IEDF` visibility
- communication continuity gap
- switchover timeline
- GI observed / not observed
- findings summary

Policy note:
- GI-after-switchover remains configurable unless fixed by project owner

Current status:
- MVP implemented and build-verified
- dedicated window added:
  - `Views\NucRedundancyWindow.xaml`
  - `Views\NucRedundancyWindow.xaml.cs`
- timeline model added:
  - `Models\RedundancyTimelineRow.cs`
- toolbar entry added in `MainWindow`
- opening NUC window now performs exclusive-mode handoff:
  - disconnect active session if connected
  - hide `MainWindow`
  - show `NucRedundancyWindow`
  - show `MainWindow` again when NUC window closes
- current redundancy analyzer tracks:
  - `L1FT`
  - `L2FT`
  - `IEDF`
  - inferred active link (conservative / may stay `Unknown`)
  - switchover count
  - GI observed within post-switch window
  - communication continuity gap from disconnect/reconnect timestamps
- important limitation:
  - Phase 4.4 now introduces reusable backend ownership through `Services\Redundancy\NucRedundancyService.cs`
  - the NUC backend now owns two internal `Iec101MasterService` instances and starts/stops both links from the exclusive NUC session
  - the NUC backend forwards per-channel connection, line-monitor, and value callbacks back to `MainViewModel`
  - NUC `Send GI` is now routed through the NUC backend instead of the hidden main session
  - full merged dual-link analyzer/journal is still not complete yet; current forwarding is focused on redundancy evidence and safe backend reuse

### Phase 5 - Availability dashboard
Goal:
- long-run telemetry/anomaly visibility
- report dashboard

Required outputs:
- session uptime
- reconnect count
- longest downtime
- event throughput
- protocol/frame errors
- ACD assertion count
- findings trend
- link switchover count

Important note:
- do not pretend there is a formal `48h pass/fail` requirement unless explicitly fixed by active project policy or official doc text
- if used as 48h test workflow, model it as dashboard/report session first

Current status:
- foundation implemented and build-verified
- dedicated window added:
  - `Views\AvailabilityDashboardWindow.xaml`
  - `Views\AvailabilityDashboardWindow.xaml.cs`
- timeline model added:
  - `Models\AvailabilityTimelineRow.cs`
- toolbar entry added in `MainWindow`
- current dashboard exposes live telemetry from shared analyzer state:
  - uptime
  - reconnect count
  - total downtime
  - longest downtime
  - event throughput
  - protocol error count
  - ACD assertion count
  - findings trend
  - link switchover count
- this is intentionally a telemetry/anomaly dashboard first, not a formal SPLN pass/fail 48h verdict engine yet

### Phase 6 - Command analyzer expansion
Goal:
- correlate command issuance -> ack -> result event/feedback -> completion

Required behavior:
- command-to-feedback mapping via official profile
- correct ACD/class validation on resulting events
- structured command verdicts

### Phase 7 - Communication feature analysis
Goal:
- support additional PLN communication feature tests

Areas:
- GI completeness
- party line
- multi-master
- bouncing delay
- COMTRADE observation

## Current implementation priorities
When resuming work, prioritize in this order:
1. deepen findings/rule engine for timing/SOE/GI rules
2. continue replacing legacy hardcoded IOA semantics with profile-driven lookup
3. expand SOE replay audit depth only after shared evidence is stable
4. deepen Phase 4 from observer-mode into configurable dual-link session control if/when scope is approved
5. deepen Phase 5 from telemetry dashboard into report/export session if project policy fixes availability criteria

## What must not be invented
Do not treat these as fixed requirements unless explicitly adopted in current project policy:
- exact IEC-101 polling frame sequence beyond observable class/ACD results
- exact retry/backoff counts
- exact 48-hour mandatory pass threshold
- exact vendor-specific command confirmation flow
- mandatory GI after every switchover
- COMTRADE transport details not present in current codebase

Represent such items as:
- `ConfigurablePolicy`
- `UnknownFromDocument`

## Files to inspect first when continuing
1. `D:\CODEX\NewEx\IEC101MasterTester\AGENTS.md`
2. `D:\CODEX\NewEx\IEC101MasterTester\CODEX_HANDOFF.md`
3. `D:\CODEX\NewEx\IEC101MasterTester\ViewModels\MainViewModel.cs`
4. `D:\CODEX\NewEx\IEC101MasterTester\Services\Iec101\Iec101MasterService.cs`
5. `D:\CODEX\NewEx\IEC101MasterTester\Services\Iec101\Iec101DataMapper.cs`
6. `D:\CODEX\NewEx\IEC101MasterTester\MainWindow.xaml`
7. `D:\CODEX\NewEx\IEC101MasterTester\MainWindow.xaml.cs`
8. `D:\CODEX\NewEx\IEC101MasterTester\Models`
9. `D:\CODEX\NewEx\IEC101MasterTester\Views`
10. `D:\CODEX\NewEx\IEC101MasterTester\DocTestReference`

## Current known UX/logic decisions
- `SignalCommandWindow` has compact command tracker on the right
- command tracker uses short lifecycle labels (`TX`, `OK`, `REJ`, `TO`)
- findings button should visually alert when unread findings exist
- `BufferedEventAuditWindow` is a separate observer/reporting window, not a communication owner
- `NucRedundancyWindow` is a separate exclusive workspace; today it disconnects/hides the main workspace before opening
- event log should stay SCADA-like, not become protocol spam
- `ValueViewer` row metadata can be overwritten by later traffic; do not use it as sole truth for verdicts
- class-1 burst summary should be compact in `Event`
- summary terminology should align with PLN/GI vocabulary:
  - `binary`
  - `analog`
  - `command`

## Current important caveat
PDF extraction from `DocTestReference` was not reliably available in this environment.

Treat the design direction in this file as the working aligned roadmap based on:
- project decisions made in this thread
- PLN-oriented test interpretation already summarized

Do not claim a requirement is directly quoted from PDF unless you have verified the exact document section.

## Verification
Build command:
`C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe IEC101MasterTester.csproj /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false`

Latest known build status:
- `2026-03-19`: build succeeded with `0 Warning(s)` and `0 Error(s)` after Phase 4 MVP changes
- `2026-03-19`: build succeeded with `0 Warning(s)` and `0 Error(s)` after Phase 4.4 dual-link backend foundation wiring

Current known verification caveat:
- the project has had intermittent unrelated WPF/XAML build issues during some sessions
- always report honestly whether a build was actually run

## How to continue on another laptop
Open this repo, read:
- `D:\CODEX\NewEx\IEC101MasterTester\AGENTS.md`
- `D:\CODEX\NewEx\IEC101MasterTester\CODEX_HANDOFF.md`

Then continue from the current roadmap.

If using a new thread, mention:
- this is a PLN Pusertif-oriented IEC-101/104 analyzer project
- baseline logic follows `SPLN S6.003:2020`
- next implementation phase currently starts from `OfficialPointProfile` migration
