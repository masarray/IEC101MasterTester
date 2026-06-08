# AGENTS.md

Development rules for IEC101 Master Tester.

## Product Standard

This repository is a user-facing engineering tool, not an internal experiment log. Public wording should explain:

- what the application does
- who should use it
- how to build it
- how to validate it
- what is stable and what still needs testing

Avoid public-facing wording such as "the owner should", "the bot found", or "Codex handoff" in README, landing pages, and release notes.

## Native IEC-101 Rule

The active IEC-101 communication path is the project-owned native clean-room stack under:

- `Services/Iec101/Native/Frames`
- `Services/Iec101/Native/Asdu`
- `Services/Iec101/Native/Master`

Do not reintroduce `Vendor/lib60870`, `lib60870.CS101`, or copied implementation code from GPL/commercial protocol libraries.

Allowed behavioral references:

- public IEC interoperability knowledge
- project-owned raw traces
- bench/simulator evidence
- field observations captured by this application

Forbidden migration shortcuts:

- copying source code from `lib60870.NET`, lib60870 C, or other GPL/commercial stacks
- adding vendor source trees to the project build
- hiding protocol assumptions behind UI labels
- inventing COT/ACD/DFC facts that were not present in the received frame

## Protocol Fact Discipline

- COT must be factual from the ASDU when present.
- ACD and DFC must be factual from secondary control bits.
- Class 1/Class 2 may be UI/analyzer metadata, but must not overwrite protocol facts.
- Raw TX/RX evidence must remain exportable for validation.
- Unknown or unsupported frames should be preserved, not silently discarded.

## Build Discipline

Before release:

1. Build Debug and Release on Windows with Visual Studio/MSBuild.
2. Confirm no source/build references to `lib60870` remain.
3. Confirm `Vendor/lib60870` is absent.
4. Run simulator/bench validation.
5. Update release notes with exact validation status.

## UI Discipline

The UI is an engineering cockpit. Keep it readable, calm, modern, and evidence-first.

Avoid:

- default-looking WPF grids without hierarchy
- giant marketing typography in app screens
- noisy glow effects
- ambiguous status badges
- hiding raw protocol details when they matter for FAT/troubleshooting

Prefer:

- clear protocol facts
- bounded live buffers
- visible evidence paths
- restrained accent colors
- operator-readable labels
- stable long-session behavior

## Documentation Discipline

Public docs should stay user-oriented:

- README: product overview, screenshots, quick build/use path, validation status.
- ROADMAP: release and validation plan.
- docs landing page: download/source/validation positioning.
- migration notes: exact engineering state and remaining gates.

Historical AI continuity notes may be kept only if they are clearly separated from user-facing documentation.
