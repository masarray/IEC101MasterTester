# Native Clean-Room Pass 7 — Deterministic GI Drain & Application-Image Truth

Pass 7 hardens the IEC 60870-5-101 native clean-room NUC redundancy engine after field testing showed that startup could become link-responsive while the Value Viewer only contained cyclic/background Class 2 values.

## Problem fixed

Previous passes could make this misleading sequence possible:

1. Link A opened and became responsive.
2. Link B opened in standby supervision.
3. Startup GI was scheduled, but normal Class 2 polling could dominate the visible data flow.
4. Background analog values arrived, so the UI looked alive.
5. Digital/Class 1 GI snapshot was still missing.
6. Manual GI worked, proving the codec was not the main issue.

That made the master look operational before the IEC-101 application image was actually complete.

## Master-side changes

- GI is now an exclusive application sequence on the active channel.
- Normal polling pauses while the GI sequence is active.
- After C_IC_NA_1 activation is link-acknowledged, the master drains Class 1 deterministically.
- The Class 1 drain continues until GI data, GI activation termination, or a bounded no-data window is observed.
- Startup does not rely only on ACD from the activation ACK.
- Background/Class 2 values no longer mark the application image as fully ready.

## Slave-side changes

- Confirmed primary ASDUs are acknowledged after application handling, so the secondary ACD bit reflects newly queued Class 1 data.
- GI handling now queues GI confirmation, interrogated values, and activation termination before sending the link ACK.
- Class 2 polling no longer hides pending Class 1 traffic. If Class 1 is pending, the slave returns a no-data indication with ACD=1 so the master switches to FC10/Class 1.

## Application-image truth model

- Background scan / cyclic Class 2 values are treated as partial image only.
- The NUC controller considers the application image ready only after GI/interrogated values are observed.
- Optional GI policy no longer means “skip cold-start GI.” It only allows skipping post-switch GI when a real GI image is still fresh.

## Expected startup behavior

1. Start NUC session.
2. Link A becomes active, Link B becomes standby.
3. Startup GI dispatch appears in the line monitor.
4. C_IC_NA_1 is acknowledged.
5. Master immediately requests Class 1 data.
6. Digital and analog GI snapshot values populate the Value Viewer.
7. Normal Class 1/Class 2 polling resumes.

## Expected switchover behavior

If the active link fails while the application image is incomplete or stale, the promoted link performs post-switch GI and drains Class 1 before the tester claims that the application image is ready.
