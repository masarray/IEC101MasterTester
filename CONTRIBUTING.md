# Contributing

Thank you for helping improve IEC101 Master Tester.

## Ground Rules

- Keep the IEC-101 stack clean-room and project-owned.
- Do not add GPL/commercial protocol source trees to the repository.
- Preserve raw protocol facts; do not guess COT, ACD, DFC, CASDU, IOA, or timestamps.
- Prefer small, testable changes.
- Update documentation when behavior changes.

## Development Checklist

Before submitting a change:

1. Build Debug and Release on Windows.
2. Confirm no `lib60870` source/build dependency is reintroduced.
3. Run simulator tests or document the manual validation performed.
4. Add or update golden trace tests when changing protocol encoding/decoding.
5. Keep UI changes readable and evidence-oriented.
