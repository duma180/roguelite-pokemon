# Pokemon Roguelite

Standalone Windows game targeting Unity 6.3 LTS and URP. The project currently uses Unity 6000.3.26f1 and URP 17.3.0. Open this directory through Unity Hub with the version in ProjectSettings/ProjectVersion.txt.

The intended scope recreates the HeartGold/SoulSilver adventure across Johto and Kanto, all 16 Gyms, Mt. Silver, and Red, with modern 2.5D presentation inspired by Brilliant Diamond/Shining Pearl. Traditional exploration, battles, and Pokemon mechanics remain the foundation. Newer-generation Pokemon and custom roguelite progression are the major gameplay additions.

Only project scaffolding exists. Maps, gameplay systems, and imported content have not been implemented.

## Repository

Track Assets (including .meta files), Packages, and ProjectSettings. Generated caches, local settings, IDE projects, and builds are ignored. Preserve .meta files when moving assets; use Unity asset operations to preserve GUIDs. Empty scaffold folders use .gitkeep files because Git does not store empty directories.

The repository starts at this Unity project, excluding the sibling Essentials source, backup, and resource-pack directories. No external assets have been imported. No remote repository or Git LFS policy has been configured.

See Docs/Architecture.md for module ownership and dependency rules. Existing template assets remain in their original locations; SampleScene remains the integration verification scene.
