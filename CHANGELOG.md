# Changelog

## Unreleased

### Added

- Windows build workflow.
- Portable release workflow with checksum generation.
- GitHub Pages deployment workflow.
- User-focused README, Quick Start, FAQ, Troubleshooting, Professional Use, Validation, Architecture, and Build documentation.
- Improved NUC startup GI behavior and application image readiness tracking.
- Improved active/standby redundancy recovery and state visibility.

### Changed

- Public documentation now focuses on product usage, download, build, validation, and professional engineering workflow.
- Release package now includes the slave simulator under `tools/IecSlaveSimulator`.

### Notes

- Validate every release candidate with simulator and bench testing before using it for professional FAT/SAT evidence.
