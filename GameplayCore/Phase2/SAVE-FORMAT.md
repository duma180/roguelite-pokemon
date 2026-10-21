# Save format and transactional publication

## Scope

The repository persists a project roster/profile, not original DS save files, world/story progress, inventory, a live battle, cloud saves or an entire HGSS adventure. It uses a statically typed binary codec with no BinaryFormatter, runtime type names, dynamic serializers or reflection-based object construction. It is source-owned and targets .NET Standard 2.1.

## Envelope 1

All numeric primitives are little-endian. GUIDs use the fixed .NET Guid.ToByteArray layout, tested across .NET and Unity's bundled Mono. Strings use a signed Int32 byte length followed by strict UTF-8. Booleans must be exactly 0 or 1; enums are validated explicit Int32 values.

| Offset | Bytes | Meaning |
|---:|---:|---|
| 0 | 8 | ASCII `PKRGSAVE` |
| 8 | 4 | Envelope version, currently 1 |
| 12 | 4 | Payload schema, currently 2 |
| 16 | 4 | Payload byte count |
| 20 | N | Typed payload |
| 20+N | 32 | SHA-256 of **all preceding header and payload bytes** |

Maximum payload is 8 MiB; envelope overhead is 52 bytes. The bounded output stream refuses oversize encoding before filesystem publication. Decoding checks overall length and checksum, then framing/version, strict fields, trailing bytes and full domain invariants. Variable-length counts are checked before allocating. Limits include 546 creatures, six party slots, eighteen thirty-slot boxes, four moves per creature, 100 evolution records per creature, and 64 extensions per scope with 64 KiB maximum per payload. Resource limits are not new game-progression policies.

SHA-256 detects accidental corruption; it is not authentication or protection against a person who can rewrite the file and its checksum. No encryption, signing or tamper resistance is claimed.

## Payload schemas

Both supported schemas contain all currently declared creature/roster fields. Schema 2 adds only the root Description string. Schema 1 is a **deliberately introduced predecessor fixture for this milestone**, not a claim that Phase 1 or native HGSS saves used this format. The explicit `1.0.0 -> 2.0.0` migration supplies empty descriptive text and preserves every gameplay/identity field. It is in memory until a caller explicitly commits a new revision. The next commit writes schema 2 and retains the original schema-1 generation as backup.

Legacy export is an explicitly named fixture API and refuses to discard a nonempty Description. Unknown envelope/payload schemas and unsupported rulesets fail without a downgrade. Catalog ID/version/hash mismatches require a separate reviewed catalog migration; they cannot use the schema migration as an implicit rebind.

Canonical payload order is fixed by SaveCodec.WriteSave/Creature and mirrored by static readers. Root: save ID, commit ID, revision, handler, player, catalog ID/version/hash, ruleset, optional description, creatures, party slots, boxes and root extensions. Creature groups follow the field table in ARCHITECTURE.md; native metadata precedes evolution history and extensions. Source code and the 2,925-byte frozen rich-save fixture pin exact ordering. Adding a field requires a new schema and explicit migration, not insertion into an accepted encoding.

Creature collections sort by individual GUID text; extension documents sort by key. Party/box/slot/move/history order is significant and retained. Definition data is not embedded as mutable per-creature copies: saves pin a separately available immutable catalog. Unsupported extra fields outside the extension mechanism reject; unknown extension schemas remain opaque bytes rather than disappearing.

## Commit protocol

All files reside in one explicitly supplied local directory, on the same volume:

- `roster.save`: published current generation.
- `roster.save.bak`: previous committed generation.
- `roster.save.tmp`: unpublished candidate, ignored by Load.
- `roster.lock`: cooperating clients hold an exclusive FileShare.None handle; file existence alone is not a stale lock.
- `roster.repair.tmp` and `quarantine-<recovery-id>.save`: explicit repair artifacts.

Commit validates/encodes the immutable proposal, acquires the exclusive lock, loads the committed state and compares revision/transaction identity. Initial revision 0 expects −1; later proposals must be exactly expected+1. An exact retry of the current commit and canonical payload returns AlreadyCommitted without writing. Reusing a commit ID with different data or using a stale revision rejects.

The candidate is written to the temporary file, flushed with Flush(true), closed and decoded again. Publication is a same-directory File.Move for the first save or File.Replace for an existing primary, preserving the old primary as backup. There is **no delete/copy fallback** if replacement is unsupported or fails. Windows/.NET documents replacement/backup behavior and the flush overload; actual publication/recovery paths were tested on this workstation. [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-7.0), [FileStream.Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).

An error before the publication call leaves the candidate uncommitted. Once native publication is attempted, any error or lost acknowledgement is reported as uncertain. Reload under the lock and reconcile the proposed CommitId/payload; never invent a second transaction to compensate blindly. Tests include both injected failures and a real native sharing violation.

## Load and recovery

Load validates the primary. Only an absent or **corrupt** primary permits trying the backup. A newer/unknown schema, mismatched catalog/ruleset or foreign profile is not classified as generic corruption and never silently rolls back to an older backup. If no committed primary/backup exists, Load returns no save, even when a valid temporary candidate is present.

Backup selection is explicitly reported. Load does not repair files, rewrite schemas, delete candidates or hide damage. Commit refuses to write while recovery is pending, preventing a corrupt primary from replacing the good backup.

RepairFromBackup requires a caller recovery ID and the expected backup CommitId. It revalidates the backup, writes/flushes a separate repair candidate, then atomically replaces the corrupt primary while preserving its bytes under a unique quarantine name. An existing quarantine is never overwritten. The good backup remains intact. If the primary is absent, repair publishes the verified copy by rename. A valid current primary cannot be rolled back through this method.

Recovery restores the previous generation; it cannot reconstruct changes contained only in the corrupt newest generation. If both committed files are corrupt, the repository fails explicitly. There is one backup, not an indefinite history or automatic journal reconstruction. Historical reward/transaction receipts remain a future domain ledger responsibility.

## Tested failure model and limits

Five diagnostic stages cover before writing, mid-write, after disk flush, immediately before publication and immediately after publication. Initial writes, updates and recovery are tested with injected I/O failures. Separately launched processes terminate with Environment.Exit(73) at all five update stages, bypassing normal disposal/acknowledgement. Readers observe revision 0 at all pre-publication stages and revision 1 after publication; no temporary file becomes committed on load.

This demonstrates application/process-interruption handling and actual local filesystem API behavior. It does not simulate loss of power inside the kernel call, controller-cache failure, disk loss, network/cloud filesystems, filesystem corruption or every out-of-space condition. There is no directory-fsync/hardware durability certification. The final Windows x64 IL2CPP player must repeat these tests on its real save path before production acceptance.
