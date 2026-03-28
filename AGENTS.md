# AGENTS.md

## Purpose
This repository is a lightweight WPF `.NET Framework 4.8` IEC-60870-5-101 Master Tester focused on practical FAT / troubleshooting.

The priority order is:
1. Protocol correctness on the wire
2. Stable operator workflow
3. Lightweight observability
4. UI polish

Do not sacrifice protocol correctness for architecture experiments.

## Hard rules for Codex
- All IEC-101 communication must go through `lib60870.NET` only.
- Analyzer/UI code must be passive. It may read callbacks and infer diagnostics, but it must not create its own communication routine.
- Do not invent custom IEC-101 framing, serial protocol logic, reflection-based link hacks, or analyzer-driven polling.
- When including vendor source trees such as `Vendor\lib60870\**\*.cs`, always exclude generated/build artifact folders:
  - `Vendor\lib60870\Properties\AssemblyInfo.cs`
  - `Vendor\lib60870\obj\**\*.cs`
  - `Vendor\lib60870\bin\**\*.cs`
- Never let project files compile vendor `obj/bin` contents, because it causes duplicate assembly and `System.Reflection` attribute errors.
- Trust official callback data first:
  - `asdu.Cot`
  - raw RX/TX frame callbacks
  - link-layer state callbacks
- `COT` shown to user must come from `asdu.Cot`, not from guessed state.
- `ACD` must come from actual secondary frame control bits, not from fabricated UI state.
- `Class 1 / Class 2` is analyzer metadata only. It must never overwrite factual protocol information such as `COT`.
- If in doubt, prefer `Unknown` over inventing a classification.

## Working rules for Codex
- Follow official `lib60870.NET` behavior first.
- Prefer the working pattern from the official / proven unbalanced master example:
  - open `SerialPort`
  - create `CS101Master(serialPort, LinkLayerMode.UNBALANCED, ...)`
  - use `PollSingleSlave(...)` and `Run()`
- Do not re-introduce `SerialPortStream` or reflection-based link-layer hacks into the active IEC-101 path unless explicitly requested.
- For this project, unbalanced IEC-101 behavior is the main priority. Balanced mode is secondary.
- UI/operator-facing labels should be simple. Avoid dumping raw protocol enums into main panels if a short operator label is enough.
- `Line Monitor` is technical. `Event Log` and `Status History` are operator-facing.
- `Event Log` should behave like SCADA-style event journaling, not a spam log of every protocol detail.
- `Findings` is a separate analyzer-oriented window and may evolve into a higher-level communication health dashboard.

## Current protocol principles
- `ACD` is important and operator-visible.
- `Class 1` should be prioritized when `ACD=1`.
- `Class 2` is background polling.
- `GI` is not cyclic polling. It should be one-shot on connect if enabled, or manual by user.
- Default timing currently follows conservative IEC-Test style baseline:
  - `Class 2 Poll Interval = 100 ms`
  - `Run Loop Delay = 100 ms`
  - `Class 1 Poll Interval = 100 ms`
  - `Busy Backoff = 150 ms`
  - `GI Startup Delay = 800 ms`

## Important files
- `D:\CodexCrut\NewEx\IEC101MasterTester\Services\Iec101\Iec101MasterService.cs`
- `D:\CodexCrut\NewEx\IEC101MasterTester\Services\Iec101\Iec101DataMapper.cs`
- `D:\CodexCrut\NewEx\IEC101MasterTester\ViewModels\MainViewModel.cs`
- `D:\CodexCrut\NewEx\IEC101MasterTester\ViewModels\ConnectionSetupViewModel.cs`
- `D:\CodexCrut\NewEx\IEC101MasterTester\Models\ConnectionSettings.cs`
- `D:\CodexCrut\NewEx\IEC101MasterTester\MainWindow.xaml`
- `D:\CodexCrut\NewEx\IEC101MasterTester\MainWindow.xaml.cs`

## Build command
Use MSBuild for verification:
`D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe IEC101MasterTester.csproj /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false`

## When user says “lanjutkan progress”
Start from `CODEX_HANDOFF.md`, inspect the files listed there, and continue from the current state without asking for a full intro.

