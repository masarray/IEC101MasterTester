# Contributing

Thank you for helping improve IEC101 Master Tester.

## Contribution principles

- Preserve factual protocol evidence.
- Do not guess COT, ACD, DFC, CASDU, IOA, quality, or timestamps.
- Keep UI wording clear for field engineers.
- Prefer small, testable changes.
- Update documentation when behavior changes.
- Add validation traces or manual test notes for protocol changes.

## Development checklist

Before submitting a change:

1. Build the main application in Debug and Release.
2. Build the slave simulator in Debug and Release.
3. Run at least one simulator session.
4. Test GI, Class 1, Class 2, command feedback, and line monitor output when protocol code changes.
5. Test active/standby switchover when redundancy code changes.
6. Update README/docs when user-visible behavior changes.

## Pull request description

Please include:

- what changed;
- why it changed;
- how it was tested;
- screenshots if UI changed;
- trace excerpts if protocol behavior changed.
