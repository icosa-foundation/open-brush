# SAF File-Descriptor Probe

A standalone ~16 KB Android app that validates the provider-side assumptions
behind the fd-backed SAF storage design, without building Open Brush.

See `google-play-saf-fd-backed-storage-plan.md` for the design it gates.

## Why this exists

The Google Play storage design reads and writes `.tilt` archives through file
descriptors detached from `ContentResolver.openFileDescriptor`. That only works
if the Documents provider hands back a *seekable regular file* rather than a
pipe. Cloud-backed providers generally do not, and the behaviour varies by
device, OS version and provider.

Checking that with a Unity build takes roughly twenty minutes. This takes
seconds, so it is practical to re-run against a new device or Android release.

## Usage

With a device connected over adb:

```sh
Support/SafFdProbe/build-and-run.sh
```

It builds, installs, launches, waits for you to pick a folder on the device,
prints the results, and uninstalls itself. It cleans up the documents it
creates. Pass an SDK and JDK path as arguments to override autodetection.

## What it checks

| # | Check | Why it matters |
| --- | --- | --- |
| 1 | `createDocument` | Basic tree write access |
| 2 | Capability flags | `FLAG_SUPPORTS_WRITE` / `RENAME` / `DELETE` gate the commit sequence |
| 3 | `detachFd` + `fstat` | Must be `S_ISREG`; a pipe cannot be seeked |
| 3b | `lseek(SEEK_END)` | Seekability, the core assumption |
| 4 | 3 MB write through the detached fd | Realistic sketch payload |
| 5 | Random-access read at mid-file | What `ZipSubfileReader` needs |
| 6 | `/proc/self/fd/N` as a path | Would let path-only native libraries skip materialization |
| 7 | Reopen by URI, byte-compare | Writes actually land in the document |
| 8 | `rwt` truncate mode | Used by the replacement path |
| 9 | `renameDocument` round trip | The commit sequence depends on rename |
| 10 | Rename onto an existing name | Determines whether rename can ever replace |
| 12 | Large sequential write throughput | How long a multi-gigabyte sketch takes to save |
| 13 | Cost of a single `fsync` | Decides whether one fsync per save can replace recovery-time deep validation |
| 14 | Large sequential read throughput | The I/O floor for recovery deep validation |

| 15a | `MediaPlayer` on a `content://` video | Whether large video can render from SAF via a native plugin instead of being copied |
| 15b | `MediaPlayer` on a `content://` audio | Informational: audio still needs an `AudioClip` for the visualiser FFT |

Checks 15a and 15b need real media in the chosen folder and skip each kind
independently if absent. 15a is the decision-relevant one. Checks 12-14 size their payload against free space (a quarter of it, capped at
1 GiB) and skip if under 64 MiB is available.

## Results

### Nothing Phone (3a), Android 16 (API 36), `com.android.externalstorage`

Run 2026-09-16. Passed. Key lines:

```text
INFO 2  flags=0x146 write=true rename=true delete=true
PASS 3  detachFd -> 132
INFO 3a fstat mode=0100660 regular=true size=0
PASS 3b lseek(SEEK_END) = 0
PASS 4  wrote 3145728 bytes through detached fd
PASS 5  random-access read at 1572864 got=64 match=true
FAIL 6  /proc/self/fd path unusable: EACCES (Permission denied)
PASS 7  reopen 'r' size=3145728 read=3145728 identical=true
PASS 8  rwt accepted, size after truncate = 0
PASS 9  renameDocument -> primary:Documents/obfdprobe.tilt.ob-bak
INFO 10 rename onto existing name -> obfdprobe (1).tilt
```

Two results are load-bearing beyond the pass/fail:

**Check 6 failed, and that is the expected answer.** `/proc/self/fd/N` resolves
to the underlying FUSE path, which the app sandbox cannot open. There is no
shortcut by which a path-only library can consume a SAF document directly, so
on-demand materialization for models, SVG, fonts and Lua is required rather
than merely convenient.

**Check 10 deduplicated instead of replacing.** `renameDocument` onto an
existing display name produced `obfdprobe (1).tilt`. Rename can therefore never
be used to atomically replace a document. The commit sequence must free the
target name first — which is exactly why it renames the canonical document to
`.ob-bak` before renaming the temporary into place. Reordering those steps
would silently produce `Sketch (1).tilt` instead of overwriting.

### Still outstanding

This probe covers the provider. It does not cover the IL2CPP/.NET half —
`SafeFileHandle` over a detached descriptor under Unity's runtime. Run
`AndroidSafStorage.RunFileDescriptorProbe` from a real Google Play build for
that. A regular-file descriptor is the case `FileStream` handles natively, so
the residual risk is low, but it is not zero and remains a release gate.

No cloud-backed provider (Drive, Dropbox) has been probed. Those are expected
to fail check 3 and fall back to materialization.
