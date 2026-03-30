# ROADMAP.md

## Current Direction

The project should stay:
- protocol-correct
- operator-usable
- lightweight
- evidence-oriented

## Near-Term Priorities

### 1. NUC Link Trace trustworthiness

Status:
- recorder-style 60-second tape is in place
- plot click is now restricted to graph area
- inspect mode now freezes the selected time window

Next:
- tighten bucket-to-event anchoring
- validate that spike areas consistently resolve to matching GI/Class1/Class2 rows
- remove any remaining cursor/tape mistrust

### 2. Findings and rule refinement

Next:
- continue moving logic toward explicit rule codes
- keep analyzer verdicts tied to real IEC evidence

### 3. Point-profile adoption

Next:
- replace remaining scattered hardcoded IOA assumptions
- keep `OfficialPointProfile` as single metadata source

### 4. Redundancy workflow hardening

Next:
- continue refining exclusive NUC session behavior
- keep active/standby observation stable and operator-readable

### 5. SOE and availability depth

Next:
- deepen replay evidence analysis
- improve long-run health summaries without inventing pass/fail policy

## Medium-Term Goals

- stronger command lifecycle correlation
- richer NUC switchover evidence
- better export/report quality for FAT evidence
- tighter coupling between findings, evidence, and operator windows

## Guardrails

- do not invent protocol facts
- do not replace `lib60870.NET`
- do not turn operator windows into heavy visual experiments
- do not sacrifice wire correctness for UI polish
