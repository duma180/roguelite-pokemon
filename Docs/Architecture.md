# Project architecture

Assets/_Project contains game-owned work. Assets/ThirdParty is reserved for separately licensed external packages and assets. Existing Unity template content remains in place.

## Folder ownership

- Core: small shared contracts and primitives; no feature or presentation dependencies.
- Features/Pokemon: Pokemon domain rules and data contracts.
- Features/Exploration: overworld exploration and interactions.
- Features/Battle: traditional battle rules and battle flow.
- Features/Progression: custom roguelite progression.
- Features/SaveLoad: save format, persistence, and migrations.
- Presentation/UI: screens, HUDs, and presentation adapters.
- Presentation/Rendering: camera and URP presentation integration.
- Content/Data, Prefabs, Scenes, Art, Audio: authored content grouped by type, then feature as needed.
- Editor: development tools excluded from players.
- Tests/EditMode and Tests/PlayMode: future tests in separate test assemblies.

## Dependency boundaries

Core must remain independent of features. Features may depend on Core and explicit domain contracts, avoiding circular references and direct dependencies on presentation. Presentation consumes feature contracts. Compose feature services at a future application entry point rather than through global scene searches. Keep authored data separate from runtime state and save data; persistent records should use stable identifiers rather than scene object references.

Create named assembly definitions when the first scripts enter each module, with explicit references and matching test assemblies. No assembly definitions or implementation scripts are added to this empty scaffold, avoiding empty-assembly warnings and premature package dependencies. Editor-only code belongs in Editor assemblies; runtime code must not reference UnityEditor.

## Current development boundary

No maps, gameplay systems, asset imports, package changes, render changes, or build-setting changes are part of initialization. Detailed generation-specific mechanics and roguelite rules still require design decisions before implementation. Use the MCP connection for scene and asset authoring, and preserve asset GUIDs. Configure a remote, binary asset storage policy, and automated validation when their concrete requirements are known.
