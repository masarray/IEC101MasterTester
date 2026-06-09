# AGENTS.md

Development guidance for automated and human contributors.

## Product standard

This repository is a user-facing engineering tool. Public wording should explain:

- what the application does;
- who should use it;
- how to download and run it;
- how to build it;
- how to validate it;
- what is stable and what still needs testing.

Avoid private notes, local machine paths, stale branch references, or non-user-facing implementation notes in public documentation.

## Protocol discipline

- COT must come from decoded ASDU data when present.
- ACD and DFC must come from secondary frame control bits.
- Class 1/Class 2 may be analyzer metadata, but must not overwrite protocol facts.
- Raw TX/RX evidence must remain exportable.
- Unsupported frames should be preserved and visible, not silently discarded.
- Transport state, protocol responsiveness, and application image readiness must remain separate concepts.

## UI discipline

The UI is an engineering cockpit. Keep it calm, readable, modern, and evidence-first.

Prefer:

- clear state badges;
- bounded live buffers;
- visible evidence paths;
- restrained accent colors;
- operator-readable labels;
- stable long-session behavior.

Avoid:

- noisy effects;
- ambiguous status labels;
- hiding raw protocol details;
- oversized marketing typography inside app screens;
- default-looking controls where a more readable field UI is needed.

## Documentation discipline

Documentation should be user-oriented and release-ready:

- README: overview, screenshots, download, quick start, build, docs map.
- User Manual: how to operate the app.
- FAQ: practical answers for new users.
- Troubleshooting: symptoms and field checks.
- Build docs: clear Windows build instructions.
- Validation docs: repeatable test checklist.
