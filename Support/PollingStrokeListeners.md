# Polling stroke listeners

Register a listener with `listenfor.strokes.poll=<client-id>`, then retrieve its
completed-stroke commands with
`GET /api/v1?query.outgoing.poll=<url-encoded-client-id>`. Encode the client ID as
one URL parameter value. Each listener has its own queue; polling drains that queue.

Registrations expire after **300 seconds without a successful poll or
registration request**. Empty successful polls also renew the lease. Generating
new strokes does not renew it. Expiry releases the entire registration and queue;
Open Brush also checks for expiry once per second when no API requests arrive.

Set `Flags.PollingStrokeListenerTimeoutSeconds` in the user configuration to a
positive integer number of seconds to change the lease at application startup.
Missing, zero or negative values use the 300-second default. Poll comfortably
within that interval.

An expired ID returns the same response as an unknown ID:
`error: polling listener not registered`. Register the ID again before polling.
The new registration starts with an empty queue and receives only future commands.
Re-registering an existing, unexpired ID renews its lease without clearing its queue.

Use `listenfor.strokes.poll.unregister=<client-id>` when disconnecting normally.

Delivery is best-effort: polling destructively drains commands, so a lost response
is not replayed. The lease does not provide acknowledgements, durable history or
full-scene synchronization. Expiry drops the whole abandoned queue rather than
truncating individual commands from a stroke.
