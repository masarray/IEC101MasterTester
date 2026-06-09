# Release Checklist

Use this checklist before publishing a public Windows portable release.

## Repository readiness

- Default branch is `main`.
- GitHub Pages source is set to **GitHub Actions**.
- `Windows Build` workflow is green on `main`.
- README, documentation, and landing page describe the product from a user perspective.
- Release package contains the main application, simulator, license, notices, and essential docs.
- No build output folders are committed.

## Local smoke test

Build Release locally or download the workflow artifact, then verify:

1. Run `IEC101MasterTester.exe`.
2. Run `tools\IecSlaveSimulator\IecSlaveSimulator.exe`.
3. Start a single-link session with matching serial settings.
4. Confirm values and events appear.
5. Start a NUC dual-link session.
6. Confirm startup General Interrogation populates digital and analog data.
7. Disconnect Link A and confirm Link B switchover is visible.
8. Reconnect the link and confirm recovery state is understandable.
9. Send a safe test command in simulator mode and confirm the lifecycle monitor reports confirmation and feedback.
10. Open Line Monitor and capture a small evidence sample.

## Publish release

Create a version tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow creates:

- `IEC101MasterTester-v0.1.0-windows-portable.zip`
- `IEC101MasterTester-v0.1.0-windows-portable.sha256`

## Post-release check

- Open the GitHub Release page.
- Download the ZIP and checksum.
- Extract to a clean folder.
- Run the smoke test again from the extracted portable package.
- Open the GitHub Pages website and confirm the Download link points to the Releases page.
