# Troubleshooting

## COM port is not available

Check:

- another application is not using the COM port;
- USB-to-serial driver is installed;
- the selected COM port is correct;
- the application has been restarted after plugging the converter;
- the simulator and tester are not trying to open the same physical port directly.

## Port is open but communication timeout appears

`PORT OPEN` only means the serial transport is open. `COMM TIMEOUT` means expected protocol response was not received.

Check:

- TX/RX wiring direction;
- baud rate;
- parity and stop bits;
- link address;
- balanced/unbalanced mode;
- converter health;
- whether the slave runtime is started;
- whether the target supports the selected link-layer behavior.

## Link is responsive but Value Viewer is empty

This usually means the link-layer is alive but application data has not been received.

Check:

- General Interrogation was sent;
- Class 1 data is being requested after GI;
- slave returns GI/interrogated data, not only background scan data;
- CASDU and IOA length match the target;
- the target database contains enabled points;
- NUC active link and slave active link are aligned.

## Only analog/background scan values appear

Background scan values prove Class 2 traffic is flowing, but they do not prove the complete startup image is ready.

Actions:

1. Click **Send GI**.
2. Inspect Line Monitor for `C_IC_NA_1` and returned information objects.
3. Confirm Class 1 requests after GI.
4. Confirm digital indications are enabled in the simulator or target device.

## Command is transmitted but feedback does not change

Check:

- command IOA;
- selected command type;
- direct operate vs select-before-operate policy;
- slave command permission;
- feedback IOA mapping;
- negative confirmation in Line Monitor;
- command response monitor rows.

## NUC Link B does not recover after repeated disconnect/reconnect

Check:

- Link B COM port is still open;
- simulator Link B runtime is running;
- standby supervision traffic is visible;
- `RECOVERING` state changes back to responsive;
- Link B is not blocked by another process;
- the old active link is not holding the only application-active role on the slave side.

## Application closes with a WPF exception

If a window was closed during an active session, reopen the latest build and confirm the issue still occurs. Include the full exception text, window name, and whether the session was running.

## GitHub Actions release package is missing

A portable package is created on tag push matching:

```text
v*.*.*
```

Example:

```powershell
git tag v0.1.0
git push origin v0.1.0
```
