# Project Structure

```text
IEC101MasterTester/
  .github/workflows/                  CI, release, and Pages workflows
  Controls/                           Reusable WPF controls
  docs/                               GitHub Pages website and user docs
  IecSlaveSimulator/                  Bench slave simulator
  Models/                             Application models and session records
  Properties/                         WPF project resources and settings
  Services/
    Diagnostics/                      Evidence recording and export
    Export/                           File export helpers
    Iec101/Native/                    IEC-101 protocol implementation
    Profiles/                         Device/profile defaults
    Redundancy/                       NUC dual-link controller
    Settings/                         User settings persistence
    Soe/                              SOE audit services
  SharedUi/                           Shared About window, theme, icons
  ViewModels/                         WPF view models and commands
  Views/                              WPF windows
  App.xaml                            WPF application entry resources
  MainWindow.xaml                     Main application shell
  IEC101MasterTester.csproj           Main app project
  IEC101MasterTester.slnx             Solution file
  packages.config                     NuGet packages for main app
```

## Main application

The main WPF application provides setup, value viewer, event log, line monitor, findings, SOE audit, availability, and NUC redundancy workspaces.

## Slave simulator

`IecSlaveSimulator/` provides a bench companion application for local testing, demonstration, and repeatable protocol scenarios.

## Documentation

User documentation is kept under `docs/` and linked from README and the GitHub Pages website.
