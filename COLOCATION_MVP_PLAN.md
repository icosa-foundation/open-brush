# Meta Quest Colocation MVP Plan

## Purpose

Ship an explicitly experimental, supportable Meta Quest colocation feature for two people in the same physical space. One headset hosts an Open Brush session, a second headset joins it, both headsets localize the Open Brush scene against the same Meta spatial anchor, and normal multiplayer synchronizes sketch content.

This plan starts from the implementation on `feature/meta-colocation`. The current code proves the intended integration points exist, but it does not yet provide enough isolation, state management, user feedback, recovery, or diagnostics for release.

## MVP scope

The release target is deliberately narrow:

- Meta Quest Android builds only.
- Exactly two headsets for the supported workflow.
- One designated host and one designated joiner.
- Both users are physically present in the same mapped space.
- Both users have network access and valid Meta accounts entitled to run the application.
- Session creation and joining remain API/command driven.
- A user-supplied session code identifies the pair.
- Spatial alignment is temporary and lasts for the current application session.
- Existing Open Brush multiplayer transports sketch changes after alignment.

The following are not part of the MVP:

- A dedicated colocation settings panel.
- QR codes or nearby-headset discovery.
- More than two supported users.
- Persistent anchors reused across application launches.
- Host migration.
- Cross-platform colocation.
- Fully automatic recovery from arbitrary network interruptions.

## Current implementation

The current flow is:

1. `colocation.host` creates a local spatial anchor, saves it locally and to Meta cloud storage, localizes the host scene, loads the Meta scene model, and joins the Photon room `OculusMRRoom`.
2. `colocation.join` joins the same Photon room and waits without visible feedback.
3. Each player publishes its Meta user ID in multiplayer rig data.
4. The host detects a previously unseen remote Meta user ID and shares the anchor with it.
5. After Meta reports that sharing succeeded, the host broadcasts the anchor UUID using a Photon RPC.
6. The joiner loads the cloud anchor, binds and localizes it, then loads its Meta scene model.

Known release blockers in that implementation include:

- All users share the same public Photon room name.
- A join command can silently create or enter a hostless room.
- There is no user-visible progress, success, timeout, or failure state.
- Meta user ID initialization races against room joining and player-data transmission.
- Most operations use `async void`, so failures cannot be propagated or coordinated.
- Anchor creation and binding contain unbounded waits.
- Anchor sharing can be started repeatedly or overlap.
- The synchronization RPC is broadcast instead of being explicitly associated with the intended joiner.
- Voice connection failure can cause multiplayer room joining to report failure even though voice is not required for physically colocated users.
- A headset cannot leave, clean up, and retry colocation without restarting the application.
- The fixed `started` guard makes a second command appear to do nothing.
- Meta account, application entitlement, scene permission, cloud-anchor, and Photon failures are not differentiated for users.

## Phase 0: instrument and reproduce

### 0.1 Add a single diagnostic identity

Use `[Colocation]` as the prefix for all logs produced by this feature. Instrument:

- Bootstrap creation and platform support checks.
- Host/join command invocation.
- Scene permission state and permission callback result.
- Meta Platform logged-in-user request and returned user ID.
- Anchor creation, local save, cloud save, UUID, load result count, bind, localization, and share result.
- Photon room join result and multiplayer connection state.
- Local and remote player creation.
- Receipt of remote Meta user IDs.
- Anchor-share scheduling and completion.
- Anchor UUID RPC queueing and receipt.
- Remote anchor load and scene-model load.

Do not log once per frame. Log state transitions and one-time operation results. Meta user IDs are useful during development but are account identifiers; remove or redact them before the public release unless they are only present in development builds.

### 0.2 Capture a controlled two-headset reproduction

For both headsets, record:

- Headset model and OS version.
- Open Brush build identifier and commit.
- Meta account used; record only an internal label in test notes.
- Whether scene permission was already granted, newly granted, denied, or not requested.
- Whether a room setup/space scan exists on the headset.
- The exact time host and join commands were invoked.
- Complete `[Colocation]` log sequences from both devices.

Run the host command first and wait until cloud anchor save and Photon room join are logged. Then run join on the second headset. Classify the first missing transition rather than inferring the failure from the final visual result.

### 0.3 Validate external prerequisites independently

Confirm on the actual release application identity:

- Oculus Platform initialization succeeds.
- `Users.GetLoggedInUser` returns a nonzero user ID on both headsets.
- Both accounts are entitled to the application/release channel.
- Android contains the required scene and spatial-anchor permissions.
- Local spatial-anchor creation and localization succeed.
- Cloud anchor save succeeds.
- Sharing to the second account succeeds.
- The second account can load the shared UUID from cloud storage.
- Both devices connect to the same Photon application, region, and version namespace.

The result of Phase 0 is a timestamped trace showing the first failing operation on each device and a short issue for every confirmed failure.

## Phase 1: deterministic session lifecycle

### 1.1 Introduce a colocation state machine

Add a state owned by the colocation controller or a dedicated session coordinator:

- `Idle`
- `CheckingPrerequisites`
- `RequestingScenePermission`
- `CreatingAnchor`
- `SavingAnchorToCloud`
- `JoiningRoom`
- `WaitingForPeer`
- `SharingAnchor`
- `WaitingForAnchor`
- `Localizing`
- `Active`
- `Leaving`
- `Failed`

Every transition records a prefixed log and updates user-visible status. Store a structured failure reason rather than only a boolean. Reject commands that conflict with the current state and explain the current operation.

### 1.2 Make operations awaitable and bounded

- Replace colocation `async void` methods with `Task`-returning methods except at the outermost Unity/API event boundary.
- Add timeouts to anchor creation, binding, localization, room joining, peer discovery, anchor sharing, and remote anchor loading.
- Catch and classify exceptions at the session boundary.
- Add cancellation so leave/retry can stop an operation in progress.
- Ensure only one start, share, synchronize, or leave operation can run at a time.

### 1.3 Add explicit preflight checks

Before hosting or joining, verify:

- The build includes `OCULUS_COLOCATION_SUPPORTED` and is running on Android.
- The bootstrap and required prefab components are present.
- Multiplayer initialization has completed.
- Meta Platform initialization has completed.
- A nonzero logged-in Meta user ID is available.
- Scene permission is granted or can be requested.
- Required anchor and scene APIs are available on the device.

Do not enter the Photon room until prerequisites required for that role are ready.

## Phase 2: isolated two-person rendezvous

### 2.1 Replace `OculusMRRoom` with a session code

Change the API surface to accept one string:

- `colocation.host <session-code>`
- `colocation.join <session-code>`

Requirements:

- Normalize case and whitespace.
- Restrict input to a small unambiguous character set and sensible length.
- Construct a versioned internal Photon room name so incompatible builds do not meet.
- Set the maximum player count to two.
- Use a sufficiently hard-to-guess code for release instructions; short codes are suitable only for controlled testing.
- Display the normalized code back to the user.

### 2.2 Enforce roles

- The host creates the session and advertises host readiness only after its anchor is cloud-saved.
- A joiner must not silently become the room owner when the requested session does not exist.
- Reject a second host.
- Reject or clearly report a third user.
- Record host identity in room/session properties rather than inferring it only from join order.

## Phase 3: reliable anchor protocol

### 3.1 Define the protocol

Use an explicit sequence:

1. Host reports `AnchorReady` with its role and protocol version.
2. Joiner reports `JoinerReady` with its Meta user ID.
3. Host calls Meta anchor sharing exactly once for that joiner.
4. Host sends the anchor UUID to that joiner only after sharing succeeds.
5. Joiner loads, binds, and localizes the anchor.
6. Joiner sends `LocalizationSucceeded` or a structured failure.
7. Both devices enter `Active` only after acknowledgement.

### 3.2 Handle retries and duplicates

- Serialize share attempts.
- Retry only operations classified as transient, with a small bounded attempt count.
- Treat repeated ready/UUID/acknowledgement messages idempotently.
- Ignore UUIDs from an unexpected host or stale session.
- Do not reload the same anchor after successful localization.
- Report timeout separately from Meta API failure.

### 3.3 Clarify scene synchronization ordering

Determine whether sketch synchronization should occur before or after spatial localization. For the MVP, prefer completing spatial localization before displaying the session as active. Confirm that:

- The host's existing sketch is preserved.
- The joiner's existing sketch is not silently destroyed without warning.
- Remote rigs and strokes use the shared scene pose after localization.
- Loading the Meta scene model does not create duplicate synchronized widgets or unintended sketch commands.

## Phase 4: user feedback and recovery

### 4.1 In-headset feedback

Use existing Open Brush info cards and controller output. Minimum messages:

- `Creating colocation session <code>`
- `Waiting for second headset`
- `Joining colocation session <code>`
- `Sharing spatial anchor`
- `Aligning shared space`
- `Colocation active`
- A specific, actionable error message for every terminal failure state

Permission denial should explain how to grant the permission. A timeout should identify which operation timed out.

### 4.2 Leave and retry

Add `colocation.leave`. It must:

- Cancel in-flight colocation work.
- Leave the data and voice rooms as applicable.
- Clear cached remote Meta IDs and protocol state.
- Destroy transient anchor objects and scene-model objects where appropriate.
- Reset `started`, role, loaded-scene, and failure state.
- Permit a new host or join command without restarting Open Brush.

Define and test host departure. The MVP may end the colocation session for both users instead of supporting host migration.

### 4.3 Make voice non-blocking

Colocated users do not require network voice. Either skip joining the voice room for colocation or ensure voice connection failure does not fail the data/anchor session. Default colocation rooms to silent.

## Phase 5: verification and release preparation

### 5.1 Automated tests

Add tests where practical for:

- Session-code normalization and validation.
- State transition validity.
- Conflicting/repeated commands.
- Timeout and cancellation behavior.
- Duplicate peer and UUID messages.
- Host and joiner role enforcement.
- Failure classification and user-facing messages.

Abstract Meta anchor operations and multiplayer signalling behind small interfaces so protocol tests do not require a headset.

### 5.2 Physical-device test matrix

Test at least:

- Fresh host and join using two distinct Meta accounts.
- Every supported Quest model combination.
- Permission already granted, newly granted, denied, and denied permanently.
- No room/space setup or incomplete scene capture.
- Join before host and nonexistent session code.
- Wrong code and incompatible build version.
- No internet on either device.
- Network loss while waiting, sharing, and active.
- Meta user lookup failure.
- Cloud anchor save/share/load failure.
- Localization timeout.
- Leave and rejoin without restarting.
- Host departure.
- Application pause/resume and headset sleep/wake.
- Host sketch preservation, joiner warning, scene sync, strokes, undo, and transforms.
- Two unrelated session codes active concurrently without cross-talk.

### 5.3 Release configuration and documentation

- Verify the colocation define is enabled only for supported Quest builds.
- Verify manifest permissions and Meta application configuration in the release artifact.
- Verify store/release-channel entitlement with non-developer accounts.
- Mark the feature experimental for the first release.
- Document physical-space safety, room setup, account/network requirements, exact host/join steps, leave/retry, and known limitations.
- Remove or redact account identifiers from release logging.

## Release gates

The MVP is releasable only when:

- Two supported headsets on distinct entitled accounts can repeatedly establish a fresh session from a cold application launch.
- Both devices explicitly report `Colocation active`.
- A test mark or stroke created at the shared origin appears in the same physical location to both users within the agreed tolerance.
- Host sketch content and subsequent multiplayer edits synchronize as documented.
- Failures produce an in-headset reason and a complete `[Colocation]` diagnostic trace.
- Leaving and retrying does not require restarting Open Brush.
- Independent session codes do not share users, anchors, or sketch data.
- Voice failure does not prevent colocation.
- No unbounded waits remain in the colocation path.
- The physical-device matrix has no unresolved release-blocking failures.

## Suggested implementation order

1. Complete Phase 0 instrumentation and reproduce on the same two headsets that failed.
2. Fix the first confirmed platform/configuration failure before restructuring unrelated code.
3. Add state, error propagation, timeouts, and cancellation.
4. Add session codes and explicit roles.
5. Implement the acknowledged anchor protocol.
6. Add feedback, leave, cleanup, and retry.
7. Decouple voice.
8. Add automated protocol tests and run the physical-device matrix.
9. Complete release configuration, documentation, privacy review, and release gates.

