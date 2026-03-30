# USER_MANUAL.md

## Purpose

Quick operator guide for the current project state.

## Main Windows

### NUC Redundancy Window

Primary operator shell for dual-link observation.

Use it to:
- monitor active link and standby link
- watch continuity gap and switchover state
- open link trace, SOE audit, availability, and other tools

### NUC Link Trace

File:
- [Views/NucLinkTraceWindow.xaml](D:/CODEX/NewEx/IEC101MasterTester/Views/NucLinkTraceWindow.xaml)

Current behavior:
- fixed 60-second tape view
- `Link A` and `Link B` traffic lanes
- click chart to inspect time
- lower grids show rows around the selected bucket/time

Important current rule:
- click inside the traffic plot only
- click outside the graph area should do nothing
- `Live` returns to moving live follow mode
- click on the tape enters inspect mode and freezes the selection window

How to use:
1. Open `NUC Link Trace`.
2. Let live tape move normally.
3. Click a spike or burst area in the traffic lane.
4. Check `Read Position`.
5. Verify the lower line-monitor tables now show rows from that selected bucket.
6. Press `Live` to return to live follow mode.

### Buffered Event Audit

Use it to inspect:
- replay count
- duplicate events
- FIFO violations
- minimum 600-event evidence

### Availability Dashboard

Use it to inspect:
- uptime
- reconnect count
- longest downtime
- throughput and protocol trend

### Findings Window

Use it to inspect analyzer findings and rule-based verdicts.

## Main Workflow

Typical operator flow:
1. Configure connection.
2. Start communication.
3. Observe event log and value viewer.
4. Open specialized windows when needed:
   - `NUC Link Trace` for traffic/time inspection
   - `Buffered Event Audit` for replay/SOE investigation
   - `Availability Dashboard` for long-run health
   - `Findings` for analyzer verdicts

## Notes

- This project is designed for practical FAT/troubleshooting, not decorative visualization.
- Protocol truth comes from service callbacks, not UI guesses.
- If a trace view seems visually wrong, verify the linked line-monitor rows before trusting the picture.
