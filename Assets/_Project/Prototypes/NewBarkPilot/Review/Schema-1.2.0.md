# HGSS structural schema 1.2.0

Schema identifier: `org.pokemon-roguelite.hgss-world-structure`. Canonical entry point: `Phase1A-Research/extract_new_bark.py`. Authoritative output: `output/new_bark_town_v1_2.json`. This is a fresh extraction from original USA HeartGold IPKE revision 0, not an in-place rewrite or automatic migration of an old JSON file.

Input SHA-256: `6bfd469abf6f7f440836bfdf96e4855a6a7907276260e0846bc3b2e8e77b23e6`.

The importer accepts only 1.2.0 and this exact hash. Schemas 1.0.0, 1.1.0 and unknown versions are rejected before scene mutation. No implicit migration exists. `extract_new_bark_v1_1.py` and the existing 1.1.0 output remain historical reproduction tools; they must not supply a current importer. The Phase 1B.1 investigator now explicitly imports that archived parser so its historical checks remain reproducible.

## Permission records

Each unique land record contains exactly 1,024 byte pairs, in unchanged physical byte order. Pair `index = x + 32*z`, with X increasing fastest; `x=index%32`, `z=index//32`. Each pair has unsigned `typeRaw` and `collisionRaw` bytes. A pair's stable ID remains based on its physical index, and its source reference is unchanged. Only the incorrect X/Z labels and order description change from 1.1.0.

`permissionRawSha256` hashes the 2,048-byte sequence `[type0,collision0,type1,collision1,...]`. All six blocks reproduce all 12,288 source bytes exactly. The six whole-land `rawSha256` values, BGS hashes, BDHC hashes, skipped-model hashes and every existing source ID/reference remain unchanged.

`structuralContract` records the original ROM lookup address and code-window hash. `validate_structure.py` independently reads the NARC section bytes and checks every `(x,z)` against the source offset `2*(x+32*z)`. The Unity loader separately validates every label and source-section hash before generation; both byte visualizations use those labels. No unknown terrain-byte behavior is inferred by the grid renderer.

## Building records

| Offset | Length | Field | Meaning |
|---:|---:|---|---|
| 0 | 4 | `modelIdRaw` | Unsigned little-endian model ID |
| 4 | 12 | `translationRawFx32` / `translationRawHex` | Three signed little-endian int32 values, 12 fractional bits in source model units |
| 16 | 12 | `rotationRaw32` / `rotationRawHex` | Three signed little-endian int32 values; preserved as vectors, not reinterpreted as generic Unity Euler angles |
| 28 | 12 | `scaleRawFx32` / `scaleRawHex` | Three signed little-endian int32 values; divide by4096 for scale |
| 40 | 8 | `unknownPaddingHex` | All eight unknown/padding bytes preserved unchanged |
| 0 | 48 | `rawHex` / `rawSha256` | Complete source record and its SHA-256 |

The redundant historical `positionRaw` signed-high16/unsigned-low16 representation and `positionCandidateFx16` alias are retained for provenance compatibility. The alias is in Unity tile-sized units, not a claim that the ROM's native encoding is fx16. Raw translation is authoritative fx32. At one Unity unit per movement tile, divide translation integers by65536 to obtain source anchor offsets in Unity units. Add the map-center X/Z shift of16. **Do not add a placeholder offset to the source anchor.**

`PilotCoordinates.BuildingAnchor` returns this map-local source anchor. `BuildingPlaceholderOffset=(0,1,0)` is a separate diagnostic cuboid offset. Scene interpretation metadata records both, plus the deliberately arbitrary cuboid size `(1.5,2,1.5)`. These sizes are not decoded building dimensions or visual footprints. Identity rotation in the outdoor runtime render path does not discard the source rotation vector. Placeholder sizes do not establish original scale, pivot or model-node topology.

The loader validates every vector, hex slice, signed translation split and record hash against `rawHex`. The independent validator reconstructs every 48-byte record from the decoded fields plus all unknown bytes and compares it with the ROM. Synthetic regressions include negative/fractional translation, nonzero signed 32-bit rotation vectors, non-unit/negative scale values, and nonzero unknown bytes. These validate preservation and offsets; they do not invent visual behavior for unsupported rotation paths.

## Unchanged records and hashes

Matrix cells, BDHC point/normal/constant/plate/strip/access records, event records, script references and ARM9 header records are unchanged. All 7,339 existing unique source stable IDs survive. Complete raw bytes remain authoritative, and unknown/additive fields must be retained. The JSON Schema file describes the changed envelope, permissions and building fields; semantic raw-byte/ROM equality checks are mandatory alongside it. It intentionally permits additional fields and does not replace the semantic validator.

The new whole-IR hash differs because spatial labels and building field interpretations changed and new explicit hashes/vectors were added. That is distinct from original source-section hashes, which did not change. Before/after Unity manifests record the new schema/hash/profile and boundary-review-register hash. Stable generated object IDs remain unchanged; diagnostic permission mesh hashes change because their spatial labels are corrected.
