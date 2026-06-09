# Changelog

## 0.1.0 - 2026-06-09

### Added

- Public Windows portable release workflow with SHA256 checksum generation.
- GitHub Pages deployment from `docs/` through GitHub Actions.
- Windows build workflow for the main application and the built-in slave simulator.
- User-focused README, Quick Start, FAQ, Troubleshooting, Professional Use, Validation, Architecture, Build, and Repository Setup documentation.
- Interactive landing page with screenshot fullscreen preview, zoom, pan, polished product icon usage, and refined engineering-product motion.
- Application image readiness awareness for startup interrogation and NUC redundancy sessions.
- NUC active/standby redundancy observation, switchover visibility, and recovery state reporting.
- Built-in IEC-101 slave simulator packaged under `tools/IecSlaveSimulator` in the portable release.

### Changed

- Repository documentation now focuses on product usage, download, build, validation, professional engineering workflow, and contribution.
- Release package is prepared as a clean Windows portable ZIP for GitHub Releases.
- Main application build uses .NET Framework/WPF assemblies without external project package restore.
- Landing page visual polish improved for a more credible public engineering product presentation.

### Release validation notes

Before publishing a public release, run the Windows Build workflow or build locally, then validate:

- main application starts;
- slave simulator starts;
- single-link master/slave communication works;
- NUC dual-link session starts;
- startup General Interrogation populates values;
- command lifecycle monitor reports confirmation/feedback;
- Link A/Link B switchover and recovery are observable;
- Line Monitor and Event Log capture usable evidence.
