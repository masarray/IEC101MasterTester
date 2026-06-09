# FAQ

## What is IEC101 Master Tester?

It is a Windows IEC 60870-5-101 master tester and analyzer for serial SCADA communication, FAT/SAT support, gateway troubleshooting, redundancy observation, command verification, and protocol evidence capture.

## Is it free?

Yes. The source code is released under Apache-2.0.

## Does it require installation?

No installer is required when using the Windows portable release ZIP. Extract the ZIP and run the executable.

## Can I use it for professional FAT, SAT, or commissioning work?

Yes, as an engineering support tool. It should be used with an approved test procedure, correct isolation boundary, and project acceptance criteria. For official records, keep exported evidence together with signed FAT/SAT forms.

## Can it control real equipment?

The application includes command workflows. Use them only when the target device, test boundary, and site procedure allow command execution. Always validate command behavior in simulator or bench mode before real equipment testing.

## Why is the link responsive but Value Viewer still empty?

A responsive link means the serial/protocol layer is answering. It does not guarantee that the application image has been received. Run or inspect General Interrogation, Class 1 polling, and GI response data in Line Monitor.

## Why do I see only analog/background scan values after startup?

That means background Class 2 traffic is arriving, but the full GI image may not have been received yet. Run GI manually and inspect whether digital indications and activation termination arrive.

## What is NUC redundancy mode?

NUC redundancy mode observes two IEC-101 serial links as active/standby channels. It is useful for testing switchover, standby supervision, recovery, and post-switch application image behavior.

## Why does standby link have fewer TX/RX frames?

Standby link normally receives supervision traffic, not full application polling. Lower traffic on standby can be normal as long as it remains responsive and ready for promotion.

## What should I include when reporting a bug?

Include:

- application version or commit;
- Windows version;
- COM port settings;
- IEC profile settings;
- screenshot of the main window;
- Line Monitor excerpt around the issue;
- whether the target was simulator, RTU, or gateway;
- steps to reproduce.

## Can I modify the source?

Yes. Build instructions are provided in [Build from Source](BUILD_FROM_SOURCE.md). Contributions are welcome under the project contribution rules.
