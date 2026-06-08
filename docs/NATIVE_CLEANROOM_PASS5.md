# Native clean-room pass 5 — Smart startup bootstrap and application image state

Pass 5 hardens the NUC redundancy startup sequence so the tester no longer treats a responsive link-layer as a complete application session.

## What changed

- Added an explicit application-image state model:
  - `Empty`
  - `Bootstrapping`
  - `Partial`
  - `Ready`
  - `Stale`
  - `Failed`
- NUC cold start now triggers startup GI when the application image is empty, even when the NUC GI policy is `Optional`.
- GI policy `Optional` now means: skip post-failover GI only when the existing application image is still fresh.
- The redundancy controller can report `Bootstrapping` instead of presenting the session as fully ready while the Value Viewer is still empty.
- Startup bootstrap publishes system evidence rows such as startup election, GI dispatch, first application value, image ready, partial image, or bootstrap failure.
- Low-level per-channel auto-GI is disabled in NUC mode so GI is orchestrated by the redundancy controller instead of being duplicated by individual channel services.
- NUC Event Log now shows bootstrap/system evidence, not only process values and commands.
- UI startup switching noise is reduced by clearing switchover timestamps when a new NUC session starts.

## Expected behavior

Cold start should now look like this:

1. Link A and Link B open.
2. Controller elects the active link.
3. Controller reports `Application Bootstrapping`.
4. Startup GI is dispatched through the active link.
5. First application value changes image state from `Empty/Bootstrapping` to `Partial/Ready`.
6. Value Viewer is populated.
7. Controller can move to healthy/degraded based on link and standby health.

If no application value arrives after startup GI retries, the controller reports a bootstrap failure while keeping normal class polling and recovery active.

## Why this matters

Before pass 5, the tester could display `COMM RESPONSIVE` and `Active` while the Value Viewer remained empty. That was technically true at link-layer level, but misleading for a master tester. Pass 5 separates:

- transport readiness
- link-layer responsiveness
- application image readiness
- operational readiness
