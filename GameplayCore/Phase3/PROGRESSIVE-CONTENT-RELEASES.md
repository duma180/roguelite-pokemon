# Progressive Pokémon content releases

This is the content architecture contract for later catalog and release work. It does not populate game data or change the Phase 3.1 battle implementation.

## Separate master definitions from release availability

Maintain independent, immutable, versioned catalogs for:

- species/forms and their stable species/form identifiers;
- moves and their stable move identifiers;
- abilities and their stable ability identifiers;
- items and any other rules data.

Catalog identity is not the same as player availability. A release manifest references stable IDs and declares versioned availability, while regional encounter manifests declare where eligible species occur and in what encounter slots/rates. A content manifest must never rewrite a species definition to hide a species from an earlier release.

The initial game release targets the faithful HGSS adventure and its original encounter structure, with a selected, curated set of later-generation Pokémon introduced into authored existing encounters. Approximately 351 obtainable species is a planning estimate only. It is not a schema limit, mandatory count, or hardcoded generation cutoff. Hoenn or another future regional expansion may publish a later manifest that enables additional species, forms, moves, abilities, evolutions and encounters.

## Manifest requirements

Each published release manifest should include a schema version, manifest ID/version, parent manifest, catalog content hashes, included stable IDs, regional encounter-manifest hashes, compatibility-profile IDs, and an explicit migration policy. Encounter distributions remain separate, versioned datasets so changes to availability do not mutate the master species record or the map/extraction IR.

Catalog additions must be additive where possible. Existing stable IDs, save schema identifiers, individual Pokémon records, evolution history, enhancement tracks, and Gym achievement state survive later manifest versions. Removing a creature from new encounters must not invalidate a saved Pokémon or prevent viewing, party use, PC storage, evolution resolution, or battle loading for content already in a save. A save must pin the catalog/manifest identities needed to interpret saved records; a migration must preserve unknown fields and reject unresolved destructive changes.

Mechanics availability is separate again: the battle engine may implement an effect while a release manifest keeps its move or ability unavailable. Conversely, a release cannot publish a move/ability until its mechanic is registered for the selected versioned ruleset or explicitly marked unsupported for that release. No generation-wide allow/deny rule may substitute for per-ID manifest availability.

## HGSS preservation and expansion boundary

Johto, Kanto, all 16 Gyms, Mt. Silver, Red, and their progression remain the base-game structure. Curated later-generation species are additions to selected encounter slots, not a replacement for the original HGSS roster or encounter progression. Region expansion manifests can add encounters and content without changing existing stable IDs or requiring a save reset.

Engine support across Generations I–IX does not imply that every supported species, move, or ability is obtainable in the initial game. No species count, availability set, or encounter distribution is implemented in Phase 3.1.
