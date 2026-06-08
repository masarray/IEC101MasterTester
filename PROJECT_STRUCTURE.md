# Project Structure

## Root

- `README.md` - public project overview.
- `ROADMAP.md` - native-stack validation and release roadmap.
- `LICENSE` - Apache-2.0 license text.
- `NOTICE` - project notice.
- `THIRD_PARTY_NOTICES.md` - dependency and asset audit notes.
- `IEC101MasterTester.csproj` - WPF .NET Framework 4.8 application project.
- `IEC101MasterTester.slnx` - Visual Studio solution file.
- `App.xaml` / `App.xaml.cs` - application startup.
- `MainWindow.xaml` / `MainWindow.xaml.cs` - main WPF shell.

## Native IEC-101 Stack

- `Services/Iec101/Native/Iec101ApplicationProfile.cs` - IEC-101 profile lengths and defaults.
- `Services/Iec101/Native/Frames/` - FT1.2 frame model, control field, encoder/decoder, primary frame factory.
- `Services/Iec101/Native/Asdu/` - ASDU model, Type ID/COT enums, information object model, quality descriptor, codec.
- `Services/Iec101/Native/Master/NativeIec101MasterService.cs` - native unbalanced master service.
- `Services/Iec101/Iec101MasterServiceRouter.cs` - app-facing master service router.
- `Services/Iec101/IIec101MasterService.cs` - master service interface.
- `Services/Iec101/Iec101DataMapper.cs` - native ASDU to UI value mapping.
- `Services/Iec101/LineMonitorFormatter.cs` - native frame/ASDU line monitor rows.

## Diagnostics and Evidence

- `Services/Diagnostics/BoundedUiBuffer.cs` - bounded live UI collection helper.
- `Services/Diagnostics/ProtocolEvidenceRecorder.cs` - raw TX/RX evidence ring buffer.
- `Services/Diagnostics/ProtocolEvidenceExportService.cs` - evidence export to CSV.
- `Services/Soe/` - SOE replay and audit logic.
- `Services/Redundancy/` - NUC redundancy orchestration.

## UI and View Models

- `ViewModels/MainViewModel.cs` - main application coordinator.
- `ViewModels/ConnectionSetupViewModel.cs` - connection/profile settings.
- `ViewModels/CommandLifeTrackerEngine.cs` - command lifecycle tracking.
- `Views/` - WPF feature windows.
- `SharedUi/` - shared About window and common UI pieces.
- `Controls/` - custom controls.

## Simulator

- `IecSlaveSimulator/` - native IEC-101 slave simulator used for bench/demo validation.
- `IecSlaveSimulator/Services/Iec101SlaveService.cs` - native simulator runtime.

## GitHub Pages

- `docs/index.html` - landing page.
- `docs/styles.css` - landing visual system.
- `docs/script.js` - reveal/interaction script.
- `docs/assets/` - screenshots and web assets.
- `docs/NATIVE_CLEANROOM_MIGRATION.md` - native migration notes.

## Removed Vendor Code

The previous `Vendor/lib60870` source tree is intentionally absent. Do not re-add vendor protocol source or build includes unless the repository license strategy is deliberately changed and documented.
