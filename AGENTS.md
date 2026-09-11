# AGENTS.md — IEC101 Master Tester Production Engineering Contract

This file is the operating contract for every coding agent, assistant, and maintainer modifying IEC101MasterTester.

Treat this repository as production engineering software for IEC 60870-5-101 FAT/SAT, commissioning, troubleshooting, and evidence collection. Do not treat it as a disposable serial-protocol demo.

## Product boundary

- Preserve IEC 60870-5-101 protocol correctness and operator evidence ahead of cosmetic work.
- Serial/FT1.2/link/application behavior must remain explicit and inspectable.
- Do not invent vendor-specific semantics, signal names, timestamps, causes of transmission, link state, or command outcomes.
- Treat malformed frames, noise, partial reads, timeouts, and disconnects as expected field conditions that must fail contained.
- Simulator behavior is test infrastructure and must not redefine production protocol semantics.

## Prime directive

Do not begin with a naive, synchronous, disposable, or knowingly temporary implementation merely to make a screen or scenario work.

Before editing code:
1. identify the current implementation and state owner;
2. reproduce or characterize the existing behavior;
3. determine the violated invariant/root cause;
4. identify callers, lifecycle, concurrency, and tests;
5. make the smallest coherent production-quality change;
6. add regression protection where practical;
7. validate build, protocol failure paths, UI responsiveness, and packaging impact.

If roughly three corrective patches have accumulated in the same subsystem without a stable result, stop stacking workarounds and reassess architecture, ownership, and assumptions.

## Architecture and state ownership

- Protocol parsing, link state, FCB/FCV handling, polling policy, command lifecycle, and transport state must not be owned by WPF controls.
- Keep one authoritative session/link state model.
- UI is a presentation surface; it must not infer protocol truth from button state or visual state.
- Serial/TCP/file/simulator boundaries must be explicit and disposable.
- Do not create parallel parser/state-machine implementations because editing the existing path is harder.

## Result / Try / exception policy

Expected runtime outcomes are not exceptional control flow.

Examples: timeout, no data, malformed frame, checksum failure, serial port unavailable, cancellation, unsupported ASDU, invalid config, partial frame, remote busy, command rejected.

For expected outcomes:
- prefer `Try...`, typed result/status records, discriminated enums, or another explicit non-throwing contract;
- validate before indexing/casting/converting;
- catch infrastructure exceptions at meaningful I/O/platform boundaries;
- convert them into structured failure/diagnostic outcomes;
- never swallow exceptions silently;
- one bad frame must not crash the whole session when isolation is possible.

Do not force a Result wrapper into pure helpers that cannot meaningfully fail.

## Parser and protocol defensive rules

Treat every inbound byte as untrusted.

Validate before use:
- FT1.2 start/end markers and length consistency;
- checksum/integrity fields;
- fixed vs variable frame shape;
- link address width and configured addressing mode;
- ASDU header sizes and cause/address fields;
- information-object count and payload bounds;
- CP24/CP56 time fields and legal ranges;
- sequence/index arithmetic and overflow;
- enum/function/type identifiers before casting.

Never read past the available span. Preserve raw evidence when semantic decoding is incomplete.

## Serial and transport lifecycle

- All reads/writes need finite timeout/cancellation behavior.
- Stop/Close must converge to a usable final state even when a driver blocks or disappears.
- Do not rely on arbitrary `Task.Delay` calls to fix races.
- Retries must be bounded and tied to protocol/transport policy.
- Dispose serial ports, streams, timers, cancellation sources, subscriptions, and background workers deterministically.
- Repeated Start/Stop cycles must not leak handles/tasks/event handlers.

## UI responsiveness and backpressure

The WPF dispatcher must never own blocking serial/network/file work or expensive parsing.

- Do not render one row synchronously for every byte/frame/event in high-volume sessions.
- Batch/coalesce monitor updates while preserving engineering meaning.
- Keep long-running grids virtualized/recycling-enabled and retained evidence bounded.
- Value/state views are snapshots; forensic traces may be append-oriented but still require retention policy.
- Expensive export/filter/sort/report work belongs off the UI thread when material.
- UI callbacks from background work must explicitly marshal to the dispatcher.

## Diagnostics

Diagnostics must help field troubleshooting without becoming the performance problem.

- High-rate paths emit compact status/counters/events, not expensive formatted logs.
- Use bounded async diagnostic delivery where volume can spike.
- Aggregate/deduplicate/rate-limit repeated timeout/noise storms.
- Formatting/persistence belongs off protocol hot paths.
- Diagnostic sink failure must not block communication or corrupt protocol state.
- Distinguish normal no-data/timeout conditions from real faults.

## Command and evidence integrity

- Command requested is not command accepted.
- Command accepted is not field-device state feedback.
- Keep select/execute/confirmation/feedback stages explicit where applicable.
- Never fabricate PASS from missing evidence.
- Use IED/source timestamps when valid and label fallback/capture time explicitly.
- Preserve raw frame evidence alongside operator-friendly interpretation.

## Simulator rules

The IEC slave simulator is a deterministic regression tool.

- Keep normal and negative behavior explicit.
- Do not silently make the simulator more forgiving than real protocol behavior.
- Use deterministic scenarios for timeout, malformed frame, busy/no-data, GI/event/command flows.
- Never hardcode vendor-specific signal truth unless the user supplies and validates it.

## Performance policy

Measure performance-sensitive changes when relevant:
- frame throughput;
- parsing latency;
- UI publication latency;
- queue depth/backlog;
- allocation/GC pressure;
- CPU/memory;
- cancellation convergence;
- repeated session resource counts.

Prefer bounded work and better algorithms over speculative pools/caches/workers.

## Regression discipline

Protect the exact failure mode at the highest stable seam available.

Examples:
- malformed frame bug → byte-level parser regression case;
- state-machine bug → deterministic transaction/scenario test;
- serial shutdown bug → repeated Start/Stop/cancel test;
- timestamp bug → valid/invalid/fallback timestamp cases;
- UI flood bug → bounded batching/state-coherence test;
- packaging bug → existing package verification path.

## Existing CI and definition of done

Preserve the repository's existing Windows build and release workflows. Do not add a second competing QA path when an authoritative one already exists.

A task is not complete because code compiles. Applicable evidence includes:
- root cause identified;
- build succeeds;
- targeted regression tests pass;
- malformed/timeout/cancel paths checked;
- UI remains responsive;
- protocol/evidence semantics preserved;
- performance measured when relevant;
- packaging validated when affected;
- real-device validation limits stated honestly.

## Required completion report

For substantial work, report:
1. Changed
2. Root cause
3. Architecture/ownership impact
4. Regression protection
5. Performance impact
6. Validation actually run
7. Remaining limitations / field validation still needed

Never claim a check passed if it was not run.

## Priority order

1. protocol correctness and evidence integrity
2. failure containment and safe lifecycle
3. regression compatibility
4. responsiveness and bounded concurrency
5. performance and memory
6. maintainability
7. implementation convenience
8. visual polish

## Final rule

Understand first. Fix root causes. Preserve protocol truth. Keep work bounded. Contain failures. Keep the UI responsive. Validate the exact failure mode. Ship only what has evidence.
