# Native clean-room pass 2 behavior-parity notes

Pass 2 focuses on runtime behavior parity after removing the bundled lib60870 source. The goal is not to add new UI features. The goal is to make the native clean-room IEC 60870-5-101 master feel closer to the previous runtime during FAT-style master/slave and NUC redundancy testing.

## Why behavior could feel different

A native stack can communicate correctly and still feel different from the previous runtime because IEC-101 behavior is shaped by small link-layer details:

- when FCB is toggled;
- whether DFC/busy backoff is respected;
- how ACD causes Class 1 polling priority;
- whether command confirmations are shown as operator values;
- whether clock sync is encoded as local wall-clock time or UTC-derived fields;
- whether the worker begins polling before the startup link reset/status handshake is complete.

## Pass 2 corrections

### FCB parity

The native master now toggles FCB only after a valid link-layer response is observed. If a poll/command exchange times out or receives an invalid frame, FCB is kept unchanged. This avoids sequence drift after transient serial loss.

### Startup handshake ordering

The worker is held until reset-remote-link and link-status startup exchanges have completed. This prevents the first Class 2 poll from racing ahead of the initial link handshake.

### DFC and busy backoff

Secondary frames with `DFC=1` now mark the link as busy and apply the configured `BusyBackoffMs` window before the next active exchange. DFC transitions are logged in the line monitor.

### ACD and Class 1 priority

Secondary frames with `ACD=1` now force the next poll window toward Class 1. ACD transitions are logged so the operator can see why polling moved from Class 2 to Class 1.

### Command confirmation display

Command ASDUs such as `C_SC_NA_1`, `C_DC_NA_1`, `C_RC_NA_1`, `C_SE_NA_1`, `C_IC_NA_1`, and `C_CS_NA_1` are kept in line/protocol evidence but no longer populate the Value Viewer as if they were process values. This matches the older user-facing behavior more closely.

### Clock sync time encoding

Native clock sync now encodes the supplied wall-clock fields directly. The master sends `DateTime.Now` for clock sync, matching the previous runtime expectation more closely than using UTC-derived fields.

### Command follow-up diagnostics

After a command/GI/clock-sync ASDU is sent, the native master arms a Class 1 follow-up window. If no command/application confirmation is observed before the window closes, a warning is logged.

## Validation checklist

Use this as a quick field checklist after pass 2:

- [ ] Master connects without lib60870 source in the repo.
- [ ] Native slave simulator starts and accepts the master link.
- [ ] Startup log shows reset/link-status before normal Class 2 polling.
- [ ] ACD=1 causes Class 1 polling priority.
- [ ] DFC=1 causes a visible busy/backoff warning and no aggressive polling burst.
- [ ] GI sends `C_IC_NA_1`, receives activation confirmation, receives process values, and receives activation termination.
- [ ] Command confirmation appears in Line Monitor / evidence, not as a process-value row in Value Viewer.
- [ ] NUC redundancy switchover kicks a fresh Class 2 poll on the new active link.
- [ ] FCB does not flip repeatedly during serial timeout/no-response conditions.

## Known limits still intentionally not claimed

This pass does not claim full IEC-101 conformance. The native stack is still a tester-focused implementation and should be hardened with golden traces before being described as a complete general-purpose IEC-101 library.
