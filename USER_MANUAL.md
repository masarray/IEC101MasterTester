# IEC101 Master Tester User Manual

IEC101 Master Tester is a Windows desktop tool for IEC 60870-5-101 serial master testing, SCADA FAT evidence, RTU/gateway troubleshooting, SOE review, and NUC dual-link redundancy observation.

## Main workflow

1. Open the application.
2. Configure the serial port and IEC-101 profile.
3. Start a single-link or dual-link redundancy session.
4. Verify link status, Class 1/Class 2 polling, General Interrogation behavior, and application image readiness.
5. Use Value Viewer and Event Log for operator-level review.
6. Use Line Monitor for frame-level evidence.
7. Export screenshots or traces when documenting a finding.

## Connection setup

Typical parameters:

- COM port.
- Baud rate.
- Data bits, parity, and stop bits.
- Link address length.
- Link address.
- CASDU length.
- CASDU value.
- IOA length.
- Originator address.
- Balanced/unbalanced mode selection.

For most RTU and gateway test cases, confirm the exact profile from the project interoperability sheet before connecting.

## Value Viewer

Value Viewer shows the current application image received from the slave/outstation.

Important columns:

- `IOA` — information object address.
- `Name / Label` — configured or inferred signal label.
- `Type` — IEC-101 information type.
- `Value` — decoded value.
- `Quality` — decoded quality indicator when available.
- `Slave Timestamp` — timestamp from the slave when present.
- `COT` — cause of transmission.
- `Class` — analyzer classification for Class 1/Class 2 handling.

If Value Viewer remains empty while the link is responsive, inspect the Line Monitor and GI status. A responsive link is not the same as a complete application image.

## Event Log

Event Log is the operator-facing journal. It is intended for readable SCADA-style review, such as:

- initial value received;
- spontaneous indication;
- command transmitted;
- command confirmed;
- GI activity;
- redundancy switchover;
- timeout, no-response, or recovery condition.

## Line Monitor

Line Monitor is the technical evidence window. Use it when you need to inspect:

- TX/RX direction;
- fixed or variable frame;
- ACD/DFC state;
- Class 1/Class 2 request;
- Type ID;
- COT;
- CASDU;
- IOA;
- quality;
- raw frame details.

For protocol troubleshooting, Line Monitor is usually the most important view.

## General Interrogation

General Interrogation is used to build or refresh the application image. The tester tracks whether data came from background scan traffic or from GI/interrogated responses.

Expected startup behavior:

1. Serial port opens.
2. Link-layer handshake becomes responsive.
3. Active link is selected.
4. Startup GI is sent when the application image is empty.
5. Class 1 data is drained.
6. Value Viewer receives digital and analog objects.
7. Normal Class 1/Class 2 polling resumes.

If only cyclic analog/background values appear, run GI manually and inspect the Line Monitor. This usually means the slave did not provide a complete GI response or the active/standby ownership needs review.

## NUC dual-link redundancy

The NUC redundancy workspace tracks two serial links as an active/standby pair.

Typical behavior:

- Active link performs application polling and commands.
- Standby link remains supervised and ready.
- If active link fails, the standby link can be promoted.
- After switchover, the tester verifies whether the application image is still fresh or needs GI refresh.
- The old active link should recover into standby when communication returns.

Status interpretation:

- `PORT OPEN` means the serial transport is open.
- `PORT CLOSED` means the serial transport is not open.
- `COMM RESPONSIVE` means valid protocol response was observed.
- `COMM TIMEOUT` means the port may be open but protocol response is missing.
- `RECOVERING` means the engine is actively probing or re-opening the link.
- `Active` and `Standby` are roles, not proof of application image completeness.

## Commands

The command window supports selected IEC-101 command workflows such as single, double, regulating, and setpoint commands depending on configured signal type.

Before issuing commands to real equipment:

- Confirm the IOA.
- Confirm select-before-operate policy.
- Confirm safe operating state.
- Confirm command feedback mapping.
- Capture evidence in Line Monitor.
- Use an isolated test boundary unless the site procedure explicitly authorizes live command testing.

## SOE buffer audit

SOE audit helps inspect replay behavior, duplicate event handling, sequence/order concerns, and buffer capacity observations.

Use it as supporting evidence, not as a replacement for an approved FAT/SAT record.

## Availability dashboard

The availability dashboard is designed for long-session observation. It helps engineers see communication gaps, degraded periods, and session continuity over time.

## Findings

Findings are analyzer warnings and diagnostics. They are not final acceptance decisions. Treat them as prompts for review and confirm with Line Monitor evidence, project specification, and test procedure.
