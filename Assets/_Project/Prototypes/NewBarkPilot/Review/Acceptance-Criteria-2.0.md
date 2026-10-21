# New Bark structural pilot acceptance criteria 2.0

These criteria supersede the blanket shared-edge continuity requirement for this pilot. They validate static structure and provenance; they do not approve visual fidelity or promote the Unity scene. The 384 existing diagnostic sample IDs and locations must remain available for comparison with Phase 1B.1.

## Input and transformations

Only the exact pinned 1.2.0 IR is accepted. Schemas 1.0.0, 1.1.0, unknown versions and hash mismatches must fail before scene mutation. All 6,144 permission positions must satisfy `index=x+32*z`, and all 12,288 bytes must round-trip with unchanged source-section hashes and stable IDs. All 18 complete building records must validate signed translation, rotation and scale at the documented offsets and retain every unknown byte. Source anchors and placeholder offsets must be separately inspectable.

The declared one-tile Unity coordinate convention is a structural convention. Model altitude is not added to BDHC traversal Y or outdoor prop Y. BDHC rectangles, coefficients and all references remain unmodified. No flattening, sidewalls, bridging or filling of source coverage gaps is permitted merely to obtain a passing diagnostic.

## Boundary categories and decisions

| Category | Acceptance rule |
|---|---|
| Traversable boundary candidate | Both adjacent static collision bits clear. Both surface sets must be present and unambiguous, with height difference at most0.0001 Unity units. Failure, missing coverage or ambiguous multilevel selection requires investigation. This checks static candidates, not all original gameplay permissions. |
| Documented blocked elevation discontinuity | A blocked bit alone is insufficient. Sample ID, source cells, all surface hits, delta and blocked flags must match an explicit evidence-register entry tied to the ROM and source-land hashes. A matched entry can exempt that sample from traversable continuity while retaining its rationale and unresolved visual status. |
| Source BDHC coverage gap | Preserve the missing hits. Known blocked gaps can receive a documented continuity exemption only when exact register evidence matches. An unblocked, unknown or changed gap remains unresolved and must not silently pass. No visual surface is inferred. |
| Blocked continuous boundary | Record the blocked state and measured equality. It is not a traversable-continuity test and does not prove correct visual topology. |
| Unresolved boundary | Changed, unknown or unregistered blocked steps, ambiguous traversable surface selection and other unsupported cases require investigation. They cannot receive a blanket exemption. |

The register contains 182 individually documented source steps and ten source gaps, based on the accepted Phase 1B.1 investigation. Its status is evidence-backed structural exemption, not proof of deliberate original visual cliffs. Every entry retains the reason and `visualStatus=unresolved`. Removing or changing the evidence must revoke the exemption. No new sample is automatically exempt just because it is blocked.

## Integrity and generation

Two independent extractor runs must be byte-identical. Independently compare the source bytes/IDs/hashes to the original ROM and historical IR. Two clean Unity preview-scene imports must have identical sorted manifests and geometry hashes. Reimport must reuse stable IDs, remove only stale generated records and create no duplicates. Preserve all 144 persistent saved GameObject identities across the schema update, and verify live instance reuse within a repeated-import session.

All earlier regression coverage must remain, with assertions depending on superseded interpretations explicitly updated for1.2.0. Non-default building transforms, permission-mesh positions and tampered data/register cases require targeted tests. Unity compilation and console must show zero errors and warnings. SampleScene, gameplay settings and baseline assets must remain unchanged relative to the start of the phase; any preexisting change from Git HEAD must be identified and preserved separately.

## Visual acceptance remains separate

Successful structural tests, one-unit reference rulers, explicit boundary exemptions and source-byte preservation do not prove faithful HGSS rendering. Building pivots, original visual topology, camera projection, apparent dimensions and source-model node transforms remain provisional until the reproducible visual-calibration protocol is reviewed and executed. No scene commit or development-baseline promotion is authorized by these structural criteria alone.
