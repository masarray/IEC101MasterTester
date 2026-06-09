# Architecture

IEC101 Master Tester is a Windows WPF application organized around communication services, diagnostics services, view models, and operator-facing windows.

## Main components

```text
Services/Iec101/Native/
  Frames/      FT1.2 frame encode/decode
  Asdu/        IEC-101 ASDU encode/decode
  Master/      master communication service

Services/Redundancy/
  NUC dual-link session control
  active/standby state
  recovery and switchover handling
  application image readiness

Services/Diagnostics/
  protocol evidence recording
  export support
  line monitor formatting

ViewModels/
  WPF presentation state and commands

Views/
  WPF windows and user interface

IecSlaveSimulator/
  bench slave simulator for repeatable local testing
```

## Design principles

- Preserve raw protocol evidence.
- Keep protocol facts separate from UI interpretation.
- Distinguish transport state from protocol responsiveness.
- Distinguish link readiness from application image readiness.
- Keep active/standby redundancy state visible.
- Prefer bounded live buffers for long test sessions.
- Treat findings as review prompts, not automatic acceptance decisions.

## Application image readiness

The redundancy engine tracks whether received data represents a complete startup/application image or only background traffic.

Typical states:

- `Empty` — no application object observed.
- `Bootstrapping` — startup image acquisition is in progress.
- `Partial` — some data is available but not enough to call the image complete.
- `Ready` — GI/interrogated data has been observed.
- `Stale` — image exists but may need refresh after link events.
- `Failed` — startup image acquisition did not complete within the expected window.
