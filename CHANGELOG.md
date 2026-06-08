# Changelog

## Unreleased

### Changed

- Migrated the main IEC-101 runtime path to the project-owned native clean-room stack.
- Removed the previous `Vendor/lib60870` source tree from the repository and build configuration.
- Routed main application startup and NUC redundancy channels through `Iec101MasterServiceRouter` backed by `NativeIec101MasterService`.
- Migrated data mapping and line monitor formatting away from vendor protocol types.
- Migrated the IEC slave simulator to shared native FT1.2 and ASDU code.
- Added Apache-2.0 license files, third-party notice notes, native migration notes, and Windows build/release workflows.

### Validation Required

- Windows MSBuild Debug/Release build.
- Native simulator interoperability test.
- Golden trace decoder/encoder test coverage.
- Real device/gateway bench validation.
