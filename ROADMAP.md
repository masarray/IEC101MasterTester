# ROADMAP.md

## Current Direction

The project should stay:
- protocol-correct
- operator-usable
- lightweight
- evidence-oriented

## Near-Term Priorities

### 1. Native IEC-101 stack migration

Goal:
- remove `lib60870.NET` vendor dependency with a project-owned C# IEC-60870-5-101 stack
- preserve wire correctness, operator workflow, and current analyzer behavior
- keep `lib60870.NET` as the active baseline until native mode is proven

Why this must be staged:
- `lib60870.NET` currently owns `CS101Master`, callbacks, ASDU types, command objects, polling, and link state behavior
- current UI models (`LineMonitorRow`, `ValueViewerRow`) are mostly neutral, so the best migration point is below `IIec101MasterService`
- direct replacement would risk `COT`, `ACD`, Class 1/Class 2 behavior, command confirmation, and GI timing

Required architecture:
- `Services/Iec101/Native/Frames`
- `Services/Iec101/Native/LinkLayer`
- `Services/Iec101/Native/Asdu`
- `Services/Iec101/Native/Master`
- `Services/Iec101/Native/Diagnostics`

Internal model targets:
- `Iec101Frame`
- `Iec101ControlField`
- `Iec101Asdu`
- `Iec101InformationObject`
- `Iec101CauseOfTransmission`
- `Iec101TypeId`
- `Iec101QualityDescriptor`
- `Iec101ApplicationProfile`

Implementation phases:
1. Passive FT1.2 decoder:
   - parse `0xE5`, fixed frames, and variable frames
   - validate length/checksum/end byte
   - parse control field and link address using active settings
   - extract `ACD/DFC` only from secondary frames
   - preserve raw bytes for every unknown or invalid frame
2. Native ASDU codec:
   - support Type ID, VSQ, COT, originator, CASDU, IOA
   - honor COT/CASDU/IOA lengths from settings/profile
   - parse CP24Time2a and CP56Time2a
   - initially support the product-critical types:
     - `M_SP_NA_1`, `M_SP_TA_1`, `M_SP_TB_1`
     - `M_DP_NA_1`, `M_DP_TA_1`, `M_DP_TB_1`
     - `M_ME_NA_1`, `M_ME_TA_1`, `M_ME_TD_1`
     - `M_ME_NB_1`, `M_ME_TB_1`, `M_ME_TE_1`
     - `M_ME_NC_1`, `M_ME_TC_1`, `M_ME_TF_1`
     - `M_ST_NA_1` and timed variants
     - `M_IT_NA_1` and timed variants
     - `M_EI_NA_1`
     - `C_IC_NA_1`
     - `C_SC_NA_1`, `C_DC_NA_1`, `C_RC_NA_1`, `C_SE_NA_1`
3. Mapper migration:
   - adapt current `Iec101DataMapper` and `LineMonitorFormatter` to consume internal models
   - add `lib60870 -> internal model` adapter
   - keep current UI and event/log/finding surfaces stable
4. Golden trace test harness:
   - capture known-good raw TX/RX sessions from current `lib60870` mode
   - assert native decoder output matches factual fields: frame type, control bits, `ACD`, `DFC`, Type ID, `COT`, CASDU, IOA, value, quality, timestamps
   - assert native encoder bytes for GI, poll, clock sync, single/double/step command, and setpoint are stable
5. Native unbalanced master:
   - open `SerialPort`
   - reset link / reset FCB
   - request link status
   - poll Class 1 while `ACD=1`
   - poll Class 2 as background
   - send one-shot startup/manual GI
   - execute queued commands
   - implement retry, timeout, busy backoff, FCB/FCV handling
6. Engine selection:
   - keep `Lib60870` as default
   - add `NativeExperimental` as explicit opt-in
   - promote to `NativeStable` only after simulator and field validation
7. Vendor removal:
   - remove `Vendor\lib60870` and project compile include only after native stable passes all gates

Validation gates:
- MSBuild succeeds with `0 Warning(s)` and `0 Error(s)`
- native passive decoder matches golden traces
- native encoder produces expected TX bytes for supported commands
- NUC redundancy still enforces single communication owner
- `COT` remains factual, never guessed
- `ACD` remains factual from secondary control bits
- Class 1/Class 2 remains analyzer metadata and never overwrites protocol facts
- real RTU/simulator test confirms GI, Class 1, Class 2, command confirmation, and setpoint behavior

Clean-room rule:
- do not copy code from `lib60870.NET` or other GPL/commercial stacks
- use public protocol documentation, interoperability guides, and project-owned traces as behavioral references

### 2. NUC Link Trace trustworthiness

Status:
- recorder-style 60-second tape is in place
- plot click is now restricted to graph area
- inspect mode now freezes the selected time window

Next:
- tighten bucket-to-event anchoring
- validate that spike areas consistently resolve to matching GI/Class1/Class2 rows
- remove any remaining cursor/tape mistrust

### 3. Findings and rule refinement

Next:
- continue moving logic toward explicit rule codes
- keep analyzer verdicts tied to real IEC evidence
- keep `Class Data` tied to delivery context:
  - `FC10` -> `Class 1`
  - `FC11` / `GI` / `BACKGROUND_SCAN` -> `Class 2`
  - do not treat class as a literal IOA field

### 4. Point-profile adoption

Next:
- replace remaining scattered hardcoded IOA assumptions
- keep `OfficialPointProfile` as single metadata source

### 5. Redundancy workflow hardening

Next:
- continue refining exclusive NUC session behavior
- keep active/standby observation stable and operator-readable

### 6. SOE and availability depth

Next:
- deepen replay evidence analysis
- improve long-run health summaries without inventing pass/fail policy

## Medium-Term Goals

- stronger command lifecycle correlation
- richer NUC switchover evidence
- better export/report quality for FAT evidence
- tighter coupling between findings, evidence, and operator windows

## Guardrails

- do not invent protocol facts
- do not remove `lib60870.NET` until the native stack passes the migration gates above
- do not turn operator windows into heavy visual experiments
- do not sacrifice wire correctness for UI polish
