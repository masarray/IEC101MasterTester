# Quick Start

This guide gets IEC101 Master Tester running quickly with the included slave simulator.

## 1. Download

1. Open the project releases page.
2. Download the Windows portable ZIP.
3. Extract it to a local folder.
4. Run `IEC101MasterTester.exe`.

## 2. Run a simulator test

The portable package includes a slave simulator under:

```text
tools\IecSlaveSimulator\IecSlaveSimulator.exe
```

Start the simulator first, configure the COM ports, then start runtime.

## 3. Start the master tester

1. Open IEC101 Master Tester.
2. Click **Setup** or **Configure**.
3. Select the COM port connected to the simulator or device.
4. Confirm baud rate, parity, stop bits, link address, CASDU, and IOA length.
5. Start a single-link or NUC redundancy session.

## 4. Confirm healthy startup

A healthy startup should show:

- Port open.
- Communication responsive.
- General Interrogation or startup image activity.
- Value Viewer populated.
- Line Monitor showing TX/RX frames.

If the link is responsive but Value Viewer is empty, open Line Monitor and confirm whether GI data has been received. Background scan values alone do not prove that the complete application image is ready.

## 5. Test NUC redundancy

1. Start dual-link mode.
2. Confirm Link A active and Link B standby.
3. Disconnect Link A or stop its simulator link.
4. Confirm Link B is promoted to active.
5. Reconnect Link A.
6. Confirm Link A recovers as standby.
7. Test GI and command behavior after switchover.

## 6. Capture evidence

Use:

- Value Viewer for current process image.
- Event Log for operator-readable activity.
- Line Monitor for protocol evidence.
- Link Trace for redundancy timing and continuity.
- SOE Audit for event replay checks.
