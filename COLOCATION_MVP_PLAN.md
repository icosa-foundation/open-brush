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
- Session creation and joining are exposed through the existing in-headset VR multiplayer panel.
- The API commands remain available as a development and diagnostic path.
- The existing multiplayer room-name field supplies the user-entered session code.
- Spatial alignment is temporary and lasts for the current application session.
- Existing Open Brush multiplayer transports sketch changes after alignment.

The following are not part of the MVP:

- A separate colocation settings panel; the MVP extends the existing multiplayer panel instead.
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

## Existing VR multiplayer UI reuse

The MVP should extend `Assets/Prefabs/Panels/MultiplayerPanel.prefab` and
`Assets/Scripts/GUI/MultiplayerPanel.cs`, not introduce a second VR panel or a separate input
system. The existing multiplayer panel already provides most of the required interaction and
presentation infrastructure:

- `RoomName`, `MultiplayerPanelButton`, and `KeyboardPopUpWindow` provide controller-driven room
  name entry, initial-value prefill, accepted-value dispatch, and persistence through
  `PlayerPrefsDataStore`. Colocation uses this value as the session code and applies colocation
  normalization and validation before constructing the internal Photon room name.
- `m_State` and the existing `StateUpdated` subscription provide the status area. While a
  colocation operation is active, this area displays the more specific colocation state rather
  than only the underlying Photon connection state.
- `m_AlertsErrors` and `Alerts()` provide the error/warning area. Structured colocation failures
  feed into this list so permission, Meta Platform, anchor, timeout, and Photon failures appear in
  the same place as existing multiplayer errors.
- The existing nickname entry remains the multiplayer identity entry. The max-player control is
  not used for colocation because a colocation room always has a maximum of two users.
- Existing room ownership UI can represent the host role after the explicit protocol has recorded
  it. Ownership alone must not be used as the protocol's source of truth.
- Existing panel open/close behavior, localization, hover states, buttons, and popup interaction
  remain unchanged unless a colocation state requires a control to be disabled.

Add explicit `Host Colocation` and `Join Colocation` actions to the existing panel. Both actions
read the same room-name/session-code field. Do not overload the existing normal multiplayer Join
button with role-dependent behavior that is invisible to the user. While an operation is running,
disable conflicting multiplayer and colocation actions; leave/cancel remains available when it can
safely stop the current state.

The panel already connects to the multiplayer service when opened. Colocation actions should use
that lifecycle, wait for the required initialized/lobby state, and avoid starting a second
connection attempt. The current `CheckIfRoomExist()` warning must become operation-aware: an
existing room is an error for Host but expected for Join, while a missing room is an error for Join.
This UI check is advisory only; the Photon create/join operation must enforce the same rule
atomically because lobby room lists can be stale or may omit private rooms.

## Phase 0: instrument and reproduce

Status: the initial `[Colocation]` instrumentation was added in commit `0ed8df58a` across the API
entry points, bootstrap, Meta user lookup, spatial-anchor operations, multiplayer player discovery,
anchor sharing, and Photon RPC send/receive path. Phase 0 is not complete until a two-headset run
has produced and correlated both device traces. Remaining instrumentation work should fill observed
gaps rather than adding speculative per-frame logging.

Pre-test instrumentation status as of 2026-07-22:

- [x] Existing `[Colocation]` transition and result logging is present from `0ed8df58a`.
- [x] New pre-test diagnostics use the distinct `[ColocationDiag]` prefix so this test pass can be
  isolated from older entries.
- [x] The start trace records role, application version, build stamp, headset model, OS, Meta
  Platform initialization, bootstrap availability, controller availability, and multiplayer
  availability.
- [x] Scene-permission, host anchor create/save, Photon join, remote-anchor synchronization, local
  and cloud saves, cloud load, bind, localization, and share operations record elapsed time without
  adding timeouts or changing their sequence.
- [x] The `StartMRExperience`, remote-anchor synchronization, and anchor-sharing `async void`
  boundaries catch and log exceptions with their active operation context.
- [x] Multiplayer state transitions are logged while a colocation operation is active.
- [x] The first local rig transmission records whether the Meta user ID was available, the Meta
  user callback records whether transmission had already begun, and the first packet carrying a
  nonzero Meta user ID is recorded.
- [x] Every anchor-share entry records how many share operations were already in flight; this pass
  observes overlap but deliberately does not prevent it.
- [ ] Capture complete `[Colocation]` and `[ColocationDiag]` traces from both headsets using the CI
  build.
- [ ] Add further instrumentation only for a transition that the first trace shows is ambiguous.
- [ ] Add a scene-model completion/failure callback trace if the first run reaches scene-model load
  but does not provide enough evidence to classify its result.

The instrumentation changes in this pass deliberately do not add the state machine, timeouts,
cancellation, session codes, role enforcement, overlap prevention, or UI changes. Those would alter
the execution being diagnosed.

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

For the CI test build:

1. Record both serials with `adb devices` and assign the stable labels `host` and `joiner` in the
   test notes.
2. Clear each device log immediately before launching the test with
   `adb -s <serial> logcat -c`.
3. Start timestamped `threadtime` log capture for each serial before invoking either colocation
   command. Retain the complete device log, not only the filtered view.
4. During triage, search each complete log for both `[Colocation]` and `[ColocationDiag]` and align
   the two traces by timestamp.
5. Preserve the CI run/build identifier with the logs. Do not put Meta account IDs in filenames or
   test-note labels.

The expected first-test checkpoints are:

- Both devices print a `[ColocationDiag] Start context` line with the intended role and CI build
  stamp.
- Both devices report Meta Platform initialized and later report a nonzero logged-in Meta user ID.
- The host completes local save, cloud save, local scene localization, and Photon room join.
- The joiner completes Photon room join without silently becoming a host in a separate room.
- Each device creates its local multiplayer player and transmits a nonzero Meta user ID.
- The host detects the joiner's Meta user ID, enters exactly one anchor-share operation, completes
  the Meta share, and queues the UUID RPC.
- The joiner receives the UUID RPC, loads one cloud anchor, binds it, localizes it, and requests the
  Meta scene model.

Stop classification at the first missing or failed checkpoint on each device. Later failures may
be consequences of that first divergence.

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

Every transition records a prefixed log and updates `MultiplayerPanel.m_State`. Store a structured
failure reason rather than only a boolean. Expose that reason to the panel's existing alert list.
Reject commands that conflict with the current state, keep the current state visible, and explain
why the requested action is unavailable.

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
- Read and edit the code through the existing multiplayer room-name display, edit button, and
  keyboard popup.
- Persist the last entered code through the existing multiplayer `PlayerPrefsDataStore`; do not
  add a second colocation-specific keyboard or raw `PlayerPrefs` path.
- Keep the normal multiplayer room name unchanged until the user confirms the keyboard popup, then
  show the normalized value so the exact shared code is visible on both headsets.

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

Use the existing multiplayer panel state and alert fields as the primary feedback surface. Info
cards or controller output may mirror terminal failures when the panel is closed, but they are not
the primary state UI. Minimum status messages:

- `Creating colocation session <code>`
- `Waiting for second headset`
- `Joining colocation session <code>`
- `Sharing spatial anchor`
- `Aligning shared space`
- `Colocation active`
- A specific, actionable error message for every terminal failure state

Permission denial should explain how to grant the permission. A timeout should identify which
operation timed out. When the panel is reopened, it must reconstruct the current status and failure
from the coordinator rather than relying only on events emitted while the panel was visible.

### 4.2 Existing multiplayer panel behavior

- Show the stored/generated session code before Host or Join is pressed.
- Disable the room-name edit action after session startup begins and re-enable it after leave or a
  recoverable failure.
- Show Host and Join only in states where starting that role is valid.
- Replace the panel's generic Photon state text with the colocation state while hosting, joining,
  sharing, or localizing; retain the underlying Photon state in diagnostic logs.
- Route structured terminal failures through `Alerts()` and keep them visible until retry, leave,
  or a new successful start clears them.
- Fix room-availability messaging so it reflects the selected Host or Join operation rather than
  always treating an existing room as an alert.
- Keep normal multiplayer behavior intact when no colocation operation is active.

### 4.3 Leave and retry

Add `colocation.leave`. It must:

- Cancel in-flight colocation work.
- Leave the data and voice rooms as applicable.
- Clear cached remote Meta IDs and protocol state.
- Destroy transient anchor objects and scene-model objects where appropriate.
- Reset `started`, role, loaded-scene, and failure state.
- Permit a new host or join command without restarting Open Brush.

Define and test host departure. The MVP may end the colocation session for both users instead of supporting host migration.

### 4.4 Make voice non-blocking

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
- Projection of coordinator state and structured failures into the existing multiplayer panel.
- Host/join action availability and operation-aware room-existence messaging.
- Session-code editing through the existing room-name value without changing normal multiplayer
  behavior.

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
3. Add state, structured error propagation, timeouts, and cancellation in a coordinator that the
   existing multiplayer panel can query at any time.
4. Wire coordinator state and failures into the existing panel's state and alert fields.
5. Add Host Colocation and Join Colocation actions that reuse the existing room-name keyboard flow.
6. Add session-code isolation and explicit, atomic host/join role enforcement.
7. Implement the acknowledged anchor protocol.
8. Add leave, cleanup, and retry through the existing panel and API command path.
9. Decouple voice.
10. Add automated protocol/UI-state tests and run the physical-device matrix.
11. Complete release configuration, documentation, privacy review, and release gates.
