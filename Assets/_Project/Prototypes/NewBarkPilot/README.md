# New Bark structural pilot â€” UNREVIEWED

Current input is schema **1.2.0**, `Data/new_bark_town_v1_2.json`, SHA-256 `6bfd469abf6f7f440836bfdf96e4855a6a7907276260e0846bc3b2e8e77b23e6`. The importer rejects 1.0.0, 1.1.0, unknown versions and altered bytes before scene/asset modification. There is no implicit migration. Historical1.1.0 data remains immutable and is not loaded by the current importer.

Open `Scenes/NewBark_StructuralPilot_UNREVIEWED.unity`. Run `Tools > New Bark Pilot > Import approved structural IR (UNREVIEWED)` to reconcile generated objects by stable ID. SampleScene and unrelated settings are preserved. The scene is not a development baseline and must not be committed or promoted without review.

Permissions retain their physical byte sequence and IDs; corrected spatial labels use `index=x+32*z`. Both raw-byte panels now follow the engine's spatial order. Source-section SHA-256 and every one of the6,144 positions are validated. `PilotPermissionBlock.ToSourceBytes()` reproduces all12,288 original bytes.

The54 unique BDHC plates produce57 instances over9 cells. Terrain vertices and planes are unchanged from Phase1B.1. Unity +X=east,+Z=south,+Y=up, one unit per tile. Matrix altitude affects omitted map models; it is not added to BDHC or outdoor prop Y. Static boundary policy2.0 retains384 samples:12 continuous static-traversable candidates,180 blocked continuous samples,182 explicitly documented blocked steps and10 documented source gaps. `Data/boundary-review-register.json` has per-sample source evidence. Blocked status alone never validates a step or gap. Visual topology remains unresolved.

All18 building proxies retain complete48-byte records. Signed translation starts at4, rotation at16, scale at28, and unknown bytes at40. Each decoded vector/slice/hash is checked against rawHex. Source anchors are distinct from the temporary `(0,1,0)` cuboid offset and arbitrary diagnostic size `(1.5,2,1.5)`; neither defines a source building footprint or pivot. Raw rotation/scale are retained without inventing model behavior.

The24 event markers retain10 NPC,5 warp,4 trigger and5 background records. Their Y=7 remains a diagnostic display lift. No story, battle, event or movement behavior is implemented.

`PilotImporter.RunReviewEvidence()` captures manifests and verifies idempotence/live instance reuse. `PilotCalibration.WriteEvidence()` writes all boundary decisions. `PilotTestRunner.Run()` starts the asynchronous24-test EditMode suite; inspect completed JSON/XML results. `PilotCalibrationReference.Capture()` renders independently authored unit rulers in a disposable preview scene. Evidence goes to `PilotEvidence/NewBark/Phase1B2`, outside Assets. Earlier evidence remains intact.

Detailed schema and revised acceptance criteria are in `Review/Schema-1.2.0.md` and `Review/Acceptance-Criteria-2.0.md`. A unit-scale reference is not evidence of faithful HGSS visual reconstruction. Building pivots, camera calibration, visual topology and source-model transforms remain provisional. No copyrighted game media is imported.
