# Roadmap

IEC101 Master Tester aims to become a practical, trustworthy Windows tool for IEC 60870-5-101 FAT evidence, troubleshooting, redundancy observation, and protocol validation.

## Current focus

- Improve deterministic startup General Interrogation behavior.
- Harden NUC active/standby recovery after repeated disconnect/reconnect.
- Add repeatable test traces for frame and ASDU encoding/decoding.
- Improve exported evidence for project FAT/SAT reports.
- Keep the GitHub repository release-ready and user-oriented.

## Near-term milestones

### v0.1.x — usability and release hygiene

- Portable Windows release package.
- GitHub Actions build verification.
- GitHub Pages website.
- Quick Start, FAQ, Troubleshooting, and Build docs.
- Screenshot-rich README.

### v0.2.x — protocol validation depth

- Golden trace fixtures for common IEC-101 frames.
- Automated tests for FT1.2 frame parsing.
- Automated tests for ASDU decoding and encoding.
- Simulator-based startup GI validation.
- Command lifecycle validation scenarios.

### v0.3.x — NUC redundancy hardening

- Faster and clearer active/standby switchover evidence.
- Better recovery state reporting.
- Post-switch application image validation.
- Long-session continuity metrics.
- Exportable redundancy timeline.

### v0.4.x — reporting and project evidence

- Cleaner CSV/JSON trace exports.
- FAT evidence snapshot export.
- Session summary report.
- Findings export.
- SOE audit report improvements.

## Long-term ideas

- More interoperability profile presets.
- Better signal database import/export.
- More guided troubleshooting diagnostics.
- Expanded simulator scenarios.
- Optional structured test-plan runner.
