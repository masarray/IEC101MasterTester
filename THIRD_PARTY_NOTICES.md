# Third-Party Notices

This document records known third-party components and redistribution notes for IEC101 Master Tester.

## Removed vendor protocol stack

The repository previously contained `Vendor/lib60870` source code. That vendor protocol source tree has been removed from the project source tree and build configuration in the native clean-room migration pass.

## NuGet packages

The WPF application uses packages declared in `packages.config`. Review the exact versions and licenses before publishing formal binary releases.

Known package at the time of this migration pass:

- `System.IO.Ports` 6.0.0

## Assets

Before public binary release, confirm ownership or license compatibility for:

- application icon files
- screenshot assets under `docs/assets/`
- profile or branding images

## Legal note

This file is an engineering audit aid, not legal advice. Perform a final legal/dependency review before distributing signed commercial or official release binaries.
