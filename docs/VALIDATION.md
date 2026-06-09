# Validation Guide

Use this checklist before relying on a new build for engineering work.

## Build validation

- [ ] Main WPF app builds in Debug.
- [ ] Main WPF app builds in Release.
- [ ] Slave simulator builds in Debug.
- [ ] Slave simulator builds in Release.
- [ ] Portable package starts on a clean Windows machine.

## Single-link validation

- [ ] Port opens.
- [ ] Link becomes responsive.
- [ ] General Interrogation is transmitted.
- [ ] Digital indications are received.
- [ ] Analog values are received.
- [ ] Class 1 polling retrieves event data.
- [ ] Class 2 polling retrieves background/cyclic data.
- [ ] Command confirmation appears in the command monitor.
- [ ] Feedback value changes after command execution.

## NUC redundancy validation

- [ ] Link A starts active.
- [ ] Link B starts standby.
- [ ] Standby link remains supervised.
- [ ] Link A disconnect promotes Link B.
- [ ] Link A reconnect recovers into standby.
- [ ] Repeated disconnect/reconnect does not leave a link permanently stuck.
- [ ] Post-switch GI behavior is visible.
- [ ] Value Viewer remains coherent after switchover.
- [ ] Command workflow works after switchover.

## Evidence validation

- [ ] Line Monitor shows TX/RX direction correctly.
- [ ] ACD and DFC are visible when present.
- [ ] COT comes from decoded ASDU where available.
- [ ] Unknown frames are preserved rather than hidden.
- [ ] Exported evidence is sufficient to reproduce the observed issue.
