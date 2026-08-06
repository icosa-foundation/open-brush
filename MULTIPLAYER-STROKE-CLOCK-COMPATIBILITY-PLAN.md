# Multiplayer Stroke Clock Compatibility Plan

## Objective

Multiplayer strokes created by current clients must retain the same timing data and millisecond accuracy as local strokes. A current client must also continue to accept legacy multiplayer strokes that do not carry a source session clock anchor.

The source device clock remains authoritative. Multiplayer must not silently replace it with Photon server time or the receiving device clock.

## Implementation status

1. Phase 1 is implemented in `SketchMemoryScript`.
2. Phase 2 is implemented in `PhotonRPC`, while retaining the legacy receive methods.
3. Phase 3 is implemented in `PhotonManager` for full, chunked, broadcast, and targeted command paths.
4. Additive load support is implemented, including timestamp-range shifting and undo/redo session lifecycle.
5. Runtime and editor C# project builds pass. Unity/Fusion Weaver validation still requires the Editor to import and compile these changes.
6. End-to-end two-client validation and save/reload inspection remain to be done.

## Timing model

Each current multiplayer stroke carries the source session anchor that local `.tilt` metadata uses:

1. `StartUtcMs`: signed 64-bit Unix UTC milliseconds.
2. `StartSketchTimeMs`: unsigned 32-bit source sketch milliseconds.

Each existing control point continues to carry its unsigned 32-bit `m_TimestampMs`.

The source UTC of a point is therefore:

```text
pointUtcMs = StartUtcMs + (pointTimestampMs - StartSketchTimeMs)
```

The receiver converts that UTC into its current sketch timeline using its own active session anchor:

```text
receiverPointTimestampMs =
    receiverSession.StartSketchTimeMs
    + (pointUtcMs - receiverSession.StartUtcMs)
```

All intermediate arithmetic must use signed 64-bit integers. Conversion to `uint` occurs only after checking the complete stroke for underflow or overflow.

## Compatibility contract

Compatibility has three separate requirements:

1. Current receiver, current anchored packet:
   1. Reconstruct every point's source UTC.
   2. Rewrite every point into the receiver's sketch timeline.
   3. Store the resulting stroke under the receiver's current `StrokeTimeSessions` anchor.
2. Current receiver, legacy packet without an anchor:
   1. Preserve the existing receipt-time tail rebasing behaviour.
   2. Do not reject or reinterpret the stroke as anchored.
   3. Record it using the receiver's clock, as the current implementation does.
3. Current sender, stroke without clock metadata:
   1. Do not change the existing legacy RPC signatures or `NetworkedStroke` layout.
   2. Use legacy stroke RPCs when no valid source session anchor is available.
   3. Do not invent a UTC anchor for historical or imported stroke data that never contained one.

This compatibility contract is about multiplayer data: a current receiver accepts both packet forms. Running old and current executable versions in the same room is a separate protocol-negotiation problem because an old executable cannot understand a new RPC method.

## Protocol design

Keep all existing legacy stroke RPCs unchanged. Add versioned anchored equivalents rather than adding parameters to existing RPCs:

1. `BrushStrokeFullClockV1`
   1. Existing full `NetworkedStroke` payload.
   2. Existing command and grouping data.
   3. `sourceStartUtcMs`.
   4. `sourceStartSketchTimeMs`.
2. `BrushStrokeCompleteClockV1`
   1. Existing chunk-transfer ID and completion data.
   2. `sourceStartUtcMs`.
   3. `sourceStartSketchTimeMs`.
3. Chunk begin and continue packets remain unchanged because the clock anchor is needed only when the completed stroke is materialized.

The additional timing payload is 12 bytes per stroke before protocol alignment and RPC overhead.

## Protocol selection

1. A stroke with a valid source session anchor uses the clock V1 RPC.
2. A stroke without a valid source session anchor uses the unchanged legacy RPC.
3. Both receive paths remain present in current clients.
4. No field is added to `NetworkedStroke`, so legacy stroke data retains its existing wire layout.
5. This milestone assumes a room runs one executable protocol version, matching Fusion's existing RPC compatibility expectations.

## Sender changes

Add lookup methods to `SketchMemoryScript`:

1. Find the `StrokeTimeSessionMetadata` whose inclusive timestamp range contains the stroke timestamp.
2. Prefer the current in-memory session for newly completed strokes.
3. Return failure for an old stroke that has no matching session.
4. Return a copy or immutable values rather than the mutable metadata object.

When sending a stroke:

1. Look up the source session using the stroke's control-point timestamps.
2. If it is available, use the anchored RPC.
3. Otherwise use the unchanged legacy RPC.

This applies to:

1. Single-packet live strokes.
2. Chunked live strokes.
3. Command-history synchronization.
4. Any future historical stroke transport that uses the Photon stroke RPCs.

The current large-data scene synchronization format remains legacy unless it is separately extended to carry session metadata.

## Receiver changes

Create one stroke-materialization function that accepts an optional source anchor:

1. If the source anchor exists:
   1. Convert every source point to UTC using the source anchor.
   2. Ensure a receiver session mapping exists.
   3. Convert every point UTC into receiver sketch milliseconds.
   4. Validate every converted timestamp before mutating the stroke.
2. If the source anchor is absent:
   1. Run the existing receipt-time tail rebasing function unchanged.
3. Recreate the stroke, add it to sketch memory, and record its receiver-side session range as today.

The anchored path must never apply receipt-time tail rebasing after UTC conversion.

## Receiver session establishment

If the receiver already has a current editing session, use that session's anchor.

If the first newly received stroke creates the receiver session:

1. Use the receiver's current sketch time and current UTC as a temporary mapping.
2. Convert the source point UTC values into that mapping.
3. Commit the receiver session using the rewritten first and last timestamps.

This preserves both local and remote strokes in one receiver timeline without storing author-specific session metadata in the `.tilt` file.

## Invalid or unavailable timing data

Use the legacy path when:

1. No source session contains the stroke timestamp.
2. The packet is the legacy protocol version.
3. The source UTC value is outside a defensible Unix millisecond range.
4. The receiver has no valid clock mapping.
5. Any rewritten point would fall outside the `uint` timestamp range.

Log an invalid anchored packet fallback with one specific prefix and enough information to distinguish missing chunks, invalid metadata, and numeric overflow. Missing source metadata on the sender is expected for legacy sketches and does not need a warning per stroke.

## Historical and re-saved sketches

1. A stroke loaded from a current `.tilt` file can be sent with its original session anchor.
2. A stroke loaded from an older `.tilt` file has no recoverable source UTC and uses legacy receipt-time rebasing.
3. A receiver stores rewritten current-protocol strokes in its own timeline and metadata.
4. When that receiver later resends the stroke, it can derive UTC from its saved receiver session without retaining the original sender session.
5. Additive imports shift each imported session's sketch-time range by the same offset applied to its control points, while retaining `StartUtcMs` unchanged.
6. Undoable additive imports remove their shifted sessions on undo and restore the same sessions on redo.
7. Historical multiplayer strokes without associated session metadata remain legacy.

## Implementation phases

### Phase 1: Data and conversion foundations

1. Add source-session lookup by stroke timestamp.
2. Add a receiver-session mapping accessor.
3. Add a pure checked timestamp conversion helper.
4. Keep existing network behaviour unchanged while these foundations compile.

### Phase 2: Versioned receive paths

1. Add versioned full and chunk-complete RPC methods.
2. Route anchored packets through the checked conversion helper.
3. Leave existing RPC methods and receipt-time fallback unchanged.
4. Verify both paths produce ordinary persisted `Stroke` objects.

### Phase 3: Metadata-aware send selection

1. Send anchored packets when the stroke has valid clock metadata.
2. Send legacy packets when that metadata is absent or invalid.
3. Cover targeted command-history synchronization as well as live broadcast strokes.

### Phase 4: Validation

1. Compile runtime and editor projects.
2. Inspect the Unity Editor log using a unique clock-protocol prefix.
3. Verify a current sender and current receiver preserve source UTC to the millisecond.
4. Verify a current receiver accepts an unmodified legacy stroke packet.
5. Verify a stroke without session metadata uses receipt-time rebasing.
6. Verify full and chunked strokes behave identically.
7. Verify save, reload, and resave retain the rewritten wall time.
8. Verify additive load, undo, redo, save, and reload retain imported wall time.

## Non-goals

1. Correcting an inaccurate source system clock.
2. Synchronizing devices against Photon server time.
3. Recovering UTC for old strokes that never had session metadata.
4. Changing control-point spatial precision or brush serialization.
5. Changing the `.tilt` control-point binary format.
6. Implementing live in-progress stroke streaming.

## Completion criteria

1. Current anchored strokes reconstruct the same UTC values on sender and receiver.
2. Legacy multiplayer strokes continue to load and use their prior receipt-time semantics.
3. Existing legacy RPC signatures and `NetworkedStroke` layout remain unchanged.
4. Strokes without source clock metadata are always sent using the legacy protocol.
5. Both timing paths save valid, reloadable `.tilt` files.
6. Additively imported clock metadata preserves its original UTC after timestamp rebasing.
