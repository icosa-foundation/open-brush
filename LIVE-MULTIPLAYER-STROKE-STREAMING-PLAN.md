# Live Multiplayer Stroke Streaming Plan

## Objective

Allow ordinary freehand strokes to appear for other players while they are being drawn. Keep the existing completed-stroke multiplayer path as the fallback.

This work should not attempt to stream every kind of drawing operation. Parametric strokes, straightedge strokes, and high-pointer-count symmetry can continue to appear only when completed.

## Activation and compatibility invariant

1. Live stroke streaming is an optional room mode and is disabled by default.
2. When the mode is disabled, multiplayer must use the current completed-stroke implementation unchanged.
3. Adding streaming support must not alter stroke creation, transmission, timestamps, contributor layers, undo, saving, playback, command history, or scene synchronization while the mode is disabled.
4. Disabled mode must remain behaviourally equivalent to the completed-stroke implementation that existed before live streaming was added.
5. The room owner has authoritative control over the setting. Non-owner clients cannot enable, disable, or locally override it.
6. Every compatible client must receive and apply the room setting before it is allowed to create a multiplayer stroke.
7. When streaming is enabled, every compatible client follows the same eligibility and fallback rules. A resource-pressure or unsupported-stroke fallback is compliance with the enabled mode, not a local opt-out.
8. A client that does not support live streaming may still join an enabled room. It continues to send and receive strokes through the completed-stroke protocol.
9. Capability fallback is not a user preference: a compatible client cannot claim to be incompatible or locally select completed-stroke-only mode to override the room owner.

## Existing behaviour and foundations

1. `PointerScript` owns the mutable local line while painting. It updates its `BaseBrushScript` and calls `ApplyChangesToVisuals()` as control points change.
2. A stroke does not enter `SketchMemoryScript` or become a `BrushStrokeCommand` until it is detached and finalized.
3. Multiplayer listens for completed commands and sends the completed stroke through `PhotonManager` and `PhotonRPC`.
4. The existing Photon begin/continue/complete RPCs divide an already-completed stroke into packets. The receiver accumulates them but does not render anything until completion.
5. Current completed-stroke messages can carry the source stroke-session clock anchor, allowing the receiver to preserve source wall-clock time with millisecond accuracy.
6. Current completed-stroke messages can carry an ephemeral contributor token and nickname. Each receiver maps that token to a local layer named `Multiplayer - <nickname>`.
7. The contributor token exists only for the current application run. It is not written to `.tilt`, `PlayerPrefs`, logs, analytics, or account data. The ordinary human-readable layer name is saved normally.
8. Disconnecting does not remove the contributor-layer mapping. Reconnecting during the same application run therefore returns the player to the same layer. Restarting Open Brush creates a new token and may create a new layer.
9. Initial large-data scene synchronization carries temporary contributor metadata and an optional per-stroke clock-session trailer. Legacy payloads without that trailer remain readable.
10. Current receivers retain the legacy completed-stroke paths for strokes without clock or contributor metadata.
11. `SketchMemoryScript.MemoryListAdd()` expects stored control points to be immutable. A live remote preview must remain outside sketch memory and the undo stack.
12. `PhotonRPCBatcher` currently sends one queued RPC every 100 ms. It is suitable for an initial low-rate experiment but not a final multi-stream scheduler.

Relevant files:

1. `Assets/Scripts/PointerScript.cs`
2. `Assets/Scripts/PointerManager.cs`
3. `Assets/Scripts/SketchMemoryScript.cs`
4. `Assets/Scripts/StrokeData.cs`
5. `Assets/Scripts/Multiplayer/MultiplayerManager.cs`
6. `Assets/Scripts/Multiplayer/MultiplayerSceneSync.cs`
7. `Assets/Scripts/Multiplayer/MultiplayerStrokeSerialization.cs`
8. `Assets/Scripts/Multiplayer/Photon/PhotonManager.cs`
9. `Assets/Scripts/Multiplayer/Photon/PhotonRPC.cs`
10. `Assets/Scripts/Multiplayer/Photon/PhotonRPCBatcher.cs`
11. `Assets/Scripts/Multiplayer/Photon/PhotonStructs.cs`

---

## Phase 1: Proof of concept

The proof of concept exists only to establish that a remote `BaseBrushScript` can be updated incrementally and converted into a normal completed stroke without a visible duplicate or disappearance.

### Scope

1. Add a development-only, owner-controlled room flag for live stroke previews. It defaults to disabled and is propagated to both proof-of-concept clients.
2. Support the main pointer using an ordinary freehand stroke.
3. Disable preview streaming whenever any of these apply:
   1. A parametric creator is active.
   2. Straightedge drawing is active.
   3. More than one pointer is producing the stroke.
4. Use reliable, ordered Photon RPCs for authoritative data and an unreliable RPC for the
   replaceable provisional tail.
5. Send updates at a fixed low rate, initially 10 Hz.
6. Reuse the existing RPC batcher unless it prevents the experiment from functioning.
7. Assume both proof-of-concept clients run the same build.
8. Test with two players in one room.
9. Verify the non-owner client cannot override the room flag.
10. Verify the disabled flag leaves the completed-stroke path unchanged.

### Minimal protocol

`PreviewStart` contains:

1. A temporary stream ID.
2. The sender's ephemeral contributor token.
3. The sender's current nickname.
4. Brush GUID, colour, size, scale, and random seed.
5. Source `StartUtcMs`.
6. Source `StartSketchTimeMs`.
7. The first control point, retaining its source timestamp.

The live protocol carries these fields in a dedicated one-point start structure. It does not
reuse the completed-stroke `NetworkedStroke`, whose fixed-capacity control-point arrays are too
large for a start RPC once clock and contributor metadata are included. Live streaming has one
current wire format; there is no compatibility contract between revisions of this experimental
protocol.

`PreviewConfirmed` contains reliable authoritative data:

1. The stream ID.
2. The index of the first included control point.
3. New confirmed control points, retaining their source timestamps.

`PreviewTail` contains replaceable unreliable data:

1. The stream ID.
2. A monotonically increasing sequence number.
3. The confirmed-point count on which the tail is based.
4. The current provisional tail point.

`PreviewComplete` contains:

1. The stream ID.
2. The final control-point count.
3. The command GUID and command timestamp.
4. Any stroke flags needed to create the finished `BrushStrokeCommand`.

`PreviewCancel` contains:

1. The stream ID.

### Sender changes

1. Give an eligible local stroke a temporary stream ID when `PointerScript.CreateNewLine()` is called.
2. Obtain the active source stroke-session anchor when the stream starts. If no valid anchor is available, use the completed-stroke path instead of inventing one.
3. Attach the existing runtime-only contributor token and nickname to `PreviewStart`.
4. After `SetControlPoint()` changes the local list, expose enough state for multiplayer to distinguish confirmed points from the provisional tail.
5. At the fixed send interval, send newly confirmed points reliably and the latest provisional
   tail unreliably.
6. Preserve the original source timestamps in all preview messages.
7. On discard, send `PreviewCancel`.
8. On normal detach, send `PreviewComplete` instead of sending the complete control-point payload again.
9. Do not put preview messages through the command or undo system.

### Receiver changes

1. Maintain a dictionary from stream ID to transient remote preview.
2. On `PreviewStart`, resolve the contributor token through `MultiplayerManager.GetOrCreateContributorLayer()`.
3. Create the transient `BaseBrushScript` on that contributor layer using the supplied brush metadata.
4. Retain the contributor token, nickname, source clock anchor, and raw source timestamps with the transient preview.
5. Keep that brush instance alive and drive it through the same incremental `UpdatePosition_LS()` and `ApplyChangesToVisuals()` lifecycle used by a local in-progress stroke.
6. On `PreviewConfirmed`, feed only newly confirmed points into the existing brush.
7. On `PreviewTail`, retain only a newer tail whose confirmed-point count matches the receiver,
   and render it with a separate reusable preview brush.
8. Do not destroy, recreate, or replay the complete brush from its first control point for each network update.
9. Skip visual work when an update contains neither new confirmed points nor a changed provisional tail.
10. Keep the preview outside `SketchMemoryScript` and the undo stack.
11. On `PreviewComplete`, validate the point count, convert the assembled timestamps exactly once, and create the normal authoritative `Stroke` and `BrushStrokeCommand` on the same contributor layer.
12. Destroy the provisional-tail brush, then finalize the confirmed preview brush into the
    authoritative stroke rather than creating duplicate geometry.
13. Add the completed stroke to sketch memory using the existing network-command path.
14. On `PreviewCancel`, destroy the preview.

### Timestamp handling

Do not calculate a receipt-time offset from `CurrentSketchTime`. Network delay must not become part of the reconstructed painting time.

Keep the assembled preview points in their source timeline until completion. At finalization, use the same checked source-session conversion as completed multiplayer strokes:

```text
pointUtcMs =
    source.StartUtcMs
    + (sourcePointTimestampMs - source.StartSketchTimeMs)

receiverPointTimestampMs =
    receiver.StartSketchTimeMs
    + (pointUtcMs - receiver.StartUtcMs)
```

Conversion requirements:

1. Use signed 64-bit intermediate arithmetic.
2. Validate the complete stroke before mutating any point timestamps.
3. Convert every assembled point exactly once.
4. Record the completed stroke in the receiver's current `StrokeTimeSessions` range.
5. Do not run completed-stroke receipt-time tail rebasing after source-clock conversion.
6. If conversion fails, discard the preview and request or accept the authoritative completed stroke through the existing fallback path.

### Contributor-layer handling

1. The contributor token, not the nickname, selects the receiver-local layer.
2. The nickname supplies the initial human-readable layer name only.
3. A reconnect using the same in-memory token reuses the layer.
4. A user rename of an existing layer must not be overwritten on reconnect.
5. Disconnect removes active previews but retains the contributor-layer mapping.
6. The finalized stroke retains its runtime contributor token and nickname so command-history and initial scene synchronization can preserve grouping.
7. No contributor token is added to persistent `.tilt` metadata.

### Proof-of-concept acceptance criteria

1. A normal freehand stroke grows visibly on the remote client while it is drawn.
2. The preview and finalized stroke appear on the sender's receiver-local contributor layer.
3. The finalized stroke retains the source wall-clock time and millisecond point accuracy provided by the completed-stroke path.
4. The final remote stroke remains visible and participates in save and undo.
5. The completed control-point payload is not retransmitted after a successful stream.
6. Cancelling a local stroke removes its remote preview.
7. Disconnecting removes abandoned previews without removing the contributor layer.
8. Reconnecting during the same application run reuses the contributor layer.
9. Unsupported stroke types still use the existing completed-stroke path.

### Explicitly excluded

1. Automatic resource-pressure fallback.
2. Symmetry streaming.
3. Recovery requests, hashes, or checksums.
4. Adaptive update frequency.
5. Interpolation or prediction.
6. Per-recipient behaviour.
7. Mixed-build protocol compatibility.
8. Persistence of contributor identity across application restarts.
9. Performance optimization beyond avoiding obvious per-frame allocations.

---

## Phase 2: NVP (narrow viable product)

The NVP turns the proof of concept into an optional feature that fails safely back to completed strokes.

### Eligibility and configuration

1. Add an owner-controlled live-preview setting to the authoritative room state. It defaults to disabled.
2. Apply the received room setting before enabling multiplayer stroke generation on a joining compatible client.
3. Do not expose a local override to non-owner clients.
4. Continue to exclude parametric and straightedge strokes.
5. Add `MaxStreamedPointers` to `PlatformConfig`, with a default of `16` in `PlatformConfigPC` and `4` in `PlatformConfigMobile`.
6. Allow a positive `Multiplayer.MaxStreamedPointers` value in `Open Brush.cfg` to override the active platform default. An absent or non-positive value uses the platform default so this resource limit cannot silently disable an owner-enabled room mode.
7. Treat the pointer limit as a resource and network safety bound, not as compensation for inefficient preview rendering.
8. Decide eligibility for the entire logical drawing action.
9. If symmetry produces more pointers than the effective limit, stream none of the strokes in that group.
10. Never stream only a subset of a symmetry group.
11. Require a valid source clock anchor before streaming; otherwise use completed-stroke delivery.

### Room ownership and setting propagation

1. Keep the live-preview setting as current-client runtime state, synchronized only between
   clients that advertise live-streaming support. Do not change the pre-streaming room-settings
   wire structure.
2. Only the current room owner can change the setting.
3. Broadcast an accepted change to every joined client before it takes effect for new strokes.
4. Fix the delivery mode for an in-progress stroke when that stroke begins. A room-setting change affects the next stroke rather than switching transport halfway through an active stroke.
5. Preserve the setting when room ownership transfers. The new owner inherits the current value and may subsequently change it.
6. Send the current setting directly to a capable client after capability discovery and after
   ownership transfer.
7. Allow the owner to enable streaming while incompatible clients remain in the room. Those clients continue using completed-stroke delivery.
8. Require every compatible client to follow an enabled setting; compatible clients have no completed-stroke-only preference or local override.
9. If the owner disables streaming, all subsequent strokes immediately use the current completed-stroke path.
10. Resource-pressure, parametric, straightedge, clock-metadata, excessive-symmetry, and incompatible-client fallbacks remain permitted while the room mode is enabled.

### Capability fallback

1. Advertise live-preview support and the client's effective incoming-pointer capacity through
   the pre-existing generic command RPC. Clients without this feature safely ignore the unknown
   command name.
2. Treat capability as binary. The live-streaming wire format has no negotiated internal version.
3. Allow an incompatible client to join a streaming-enabled room and use completed-stroke delivery.
4. Stream between compatible participants according to the owner-selected room setting, routing a logical pointer group only to recipients that advertised enough capacity for the entire group.
5. Ensure compatible clients continue to receive completed strokes sent by incompatible participants.
6. Ensure incompatible clients receive authoritative completed strokes for drawing actions that compatible clients preview live. The live preview remains optional presentation data; the completed protocol remains the compatibility path.
7. Do not expose capability fallback as a local setting. A compatible client must obey the room mode.
8. Send clients without live-streaming capability only the unchanged pre-streaming completed-stroke
   and scene-sync formats. Send clock-, contributor-, preview-, room-state-, and related new RPCs
   only to clients that advertised capability.
9. If a receiver cannot create or retain any preview in a logical pointer group, have it decline that stream immediately. The sender then cancels the recipient's other previews in the group and sends the normal completed command tree instead.
10. The only supported mixed-build boundary is between builds with live stroke streaming and builds
    without it. Any future requirement to support multiple live-protocol revisions must be discussed
    and designed explicitly before implementation.

### Simple resource-pressure fallback

Use a small number of understandable limits:

1. Maximum simultaneous outgoing preview streams, derived from the effective `MaxStreamedPointers` value.
2. Maximum simultaneous incoming preview streams per remote painter, using the receiver's effective `MaxStreamedPointers` value. Separate painters have separate allowances so one painter cannot consume every receiver preview slot.
3. No unbounded application-level preview queue. The sender samples each active stream at 10 Hz and sends through Fusion directly, rather than feeding preview updates into the global one-RPC-per-100-ms batcher.

Choose the mode before the stroke starts whenever possible:

1. Under the limits: stream the stroke.
2. Over a limit: use the existing completed-stroke path.

If pressure appears during a stream:

1. The receiver declines the affected stream and discards its preview.
2. The sender stops live updates to that recipient for the logical pointer group.
3. At completion, the sender cancels any remaining previews from that group for the recipient and sends the existing authoritative completed command tree.
4. Retain completion-time repair for the race where completion crosses the receiver's decline response in flight.

Do not attempt complex frame-time, memory, or bandwidth prediction in the NVP.

### Lifecycle handling

1. Remove active previews when their sender disconnects, but retain the contributor-layer mapping.
2. Time out abandoned previews.
3. Handle cancellation and discarded local strokes.
4. Ignore duplicate updates.
5. Reject updates for unknown or already-completed stream IDs.
6. Apply sensible maximum point counts and stream counts.
7. Recreate a contributor layer if its previous layer was deleted and that contributor draws again.

### Completion validation and repair

1. Include the final control-point count in `PreviewComplete`.
2. If the receiver has the expected count, finalize without retransmitting the stroke.
3. If the count does not match, discard the preview and request the authoritative completed stroke using the existing full or chunked stroke transport.
4. Keep the completed sender-side stroke available long enough to answer that request.
5. Ensure the repair response carries the contributor metadata and source clock anchor already supported by the completed-stroke path.

This makes retransmission exceptional rather than routine without requiring a checksum system.

### RPC scheduling

Preview traffic does not use the single global 100 ms RPC batch queue:

1. The sender samples current pointer state once per active stream at 10 Hz. This naturally merges all newly confirmed points since the previous sample and uses only the latest provisional tail.
2. Capability, room state, start, cancel, completion, decline, and repair messages are sent directly rather than waiting behind preview updates.
3. Confirmed-point payloads respect `NetworkingConstants.MaxControlPointsPerChunk`.
4. Reliable Fusion RPC ordering is preserved within each stream without adding another application-level queue.
5. The receiver accumulates every message's confirmed points immediately but applies geometry at most once per preview per Unity frame.

### Preview rendering performance

1. Remote preview rendering must follow the local incremental brush lifecycle rather than rebuilding a stroke from all accumulated points on every update.
2. Keep one transient `BaseBrushScript` per active remote pointer.
3. Apply each confirmed control point to that brush once.
4. Mutate only the current provisional tail until it becomes confirmed.
5. Coalesce network input before touching geometry when multiple messages arrive in one frame.
6. Expect drawing cost to scale with the number and complexity of visible brushes, as it does for local symmetry. The configured pointer limit primarily bounds concurrent network streams and untrusted remote resource consumption.
7. Profile before adding brush-specific limits, caching, adaptive quality, or lower hard-coded caps.

### Late-join synchronization and clock accuracy

The contributor-aware scene-sync envelope preserves runtime contributor grouping and carries an optional source clock-session anchor for each stroke:

1. The source clock anchor is associated with each stroke in an optional transport trailer.
2. The receiver restores those clock sessions so the synchronized strokes retain the same wall-clock mapping used by live and completed RPC delivery.
3. Legacy scene-sync payloads without clock metadata remain supported.
4. Contributor tokens and clock anchors remain multiplayer transport data; contributor identity is not added to `.tilt` metadata.

### NVP acceptance criteria

1. Streaming is disabled by default.
2. With streaming disabled, multiplayer behaviour is equivalent to the current completed-stroke implementation.
3. Only the room owner can change the setting.
4. Every compatible client applies the owner-selected setting before it can draw.
5. Compatible clients cannot locally override an enabled room setting.
6. Incompatible clients can join an enabled room and continue using completed-stroke delivery.
7. Compatible clients receive completed strokes from incompatible clients.
8. Incompatible clients receive completed authoritative strokes from compatible clients without needing to understand preview messages.
9. Ordinary freehand and permitted symmetry strokes appear while drawing between compatible clients when the room mode is enabled.
10. Parametric, straightedge, excessive-symmetry, invalid-clock, resource-pressure, and incompatible-client cases use completed-stroke delivery as defined fallbacks within the enabled mode.
11. Resource limits cause a safe fallback rather than an ever-growing queue.
12. A successfully streamed stroke sends each confirmed control point only once to compatible recipients unless repair is required.
13. A missing-point-count condition recovers through an on-demand full-stroke transfer.
14. Finalized and repaired strokes retain their contributor layer and source clock accuracy.
15. Disconnects, cancellation, and abandoned streams do not leave permanent preview geometry.
16. Late joiners retain contributor grouping and wall-clock playback accuracy when the sender provides the scene-sync clock trailer; legacy senders continue to synchronize without it.
17. Remote previews update incrementally without recreating their brush object or replaying previously applied confirmed points.
18. The effective maximum streamed-pointer count defaults to `16` on PC and `4` on mobile and can be overridden with a positive `Multiplayer.MaxStreamedPointers` value in `Open Brush.cfg`.
19. A recipient either finalizes every streamed pointer in a logical symmetry group or receives the normal completed command tree; it never retains a partial streamed group.

---

## Optional stretch goals

Each item below is independent and should be justified by observed behaviour before implementation.

### Better validation

1. Add a checksum to `PreviewComplete`.
2. Request repair when the count matches but the checksum does not.
3. Add acknowledgements if completed-stroke retention becomes difficult to manage.

### Better visual quality

1. Interpolate the remote provisional tail between network updates.
2. Adapt update frequency to controller speed.
3. Reduce update frequency for distant or hidden players.

### More adaptive pressure handling

1. Use measured outgoing bandwidth.
2. Use preview geometry update time.
3. Let receivers advertise a preview budget or temporarily decline preview streams.
4. Allow a receiver to accumulate points without rendering them when only rendering is under pressure.

### Broader stroke support

1. Investigate revisioned snapshots for straightedge or parametric strokes.
2. Consider more specialized per-recipient delivery only if observed performance requires it. The required compatibility split is limited to preview-capable recipients versus completed-stroke-only recipients.

### Protocol and diagnostics

1. Do not add live-protocol revision negotiation unless a new compatibility requirement is agreed.
2. Add development metrics for active streams, update rate, bytes sent, queue age, fallbacks, and repairs.
3. Add rate limiting and stricter validation for hostile or malfunctioning clients.

---

## Recommended implementation order

1. Add the proof-of-concept lifecycle events and eligibility gate.
2. Carry the source clock anchor, contributor token, and nickname in `PreviewStart`.
3. Render one transient remote freehand preview on its contributor layer using the same incremental brush lifecycle as local drawing.
4. Finalize that preview through the existing checked clock conversion without retransmitting the complete control-point payload.
5. Add cancellation and confirm the existing completed-stroke fallback remains unchanged.
6. Stop and evaluate visual quality, bandwidth, queue behaviour, timestamp accuracy, and brush compatibility.
7. Add the authoritative room setting, owner-only mutation, propagation, ownership-transfer handling, and disabled-mode equivalence checks.
8. Add capability-aware routing so incompatible clients remain in the room using completed strokes while compatible clients cannot override the owner-selected setting.
9. Add the platform defaults and `Open Brush.cfg` override for `MaxStreamedPointers`.
10. Extend the incremental receiver to permitted symmetry groups without rebuilding existing preview geometry.
11. Implement the remaining NVP lifecycle limits only if the proof of concept demonstrates acceptable behaviour.
12. Preserve the existing late-join scene-sync clock trailer and its legacy fallback.
13. Select stretch goals only from measured problems.

## Design constraints to preserve

1. The transient preview is presentation state.
2. The completed `Stroke` and `BrushStrokeCommand` remain the authoritative persistent state used by undo, saving, playback, history synchronization, and late joiners.
3. Source wall-clock data remains authoritative; packet receipt time and Photon server time do not replace it.
4. Contributor identity remains ephemeral and receiver-local. The nickname is human-readable layer text, not an identity key.
5. Unsupported, invalid, or resource-constrained streams fall back to the completed-stroke path.
6. The room owner is authoritative for whether streaming is enabled, and non-owner clients have no local override.
7. Disabled mode preserves the current completed-stroke behaviour unchanged.
8. Enabled mode is authoritative for every compatible client. Incompatible clients remain supported through completed-stroke delivery.
9. The completed stroke remains authoritative and must reach incompatible clients even when compatible clients received a live preview first.
