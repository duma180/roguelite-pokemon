# Phase 3.3 mechanic coverage update (source-derived; cartridge conformance pending)

| System | Status | Evidence or remaining work |
|---|---|---|
| Leech Seed application | Implemented, source-derived | HGSS effect script targets the defender, rejects Grass types and duplicate seed. Accuracy still needs a runtime fixture. |
| Leech Seed turn residual | Implemented, source-derived | Stores source battle side, drains one eighth of target max HP (minimum 1), transfers actual damage after weather and before major-status damage. Source repository order and scripts reviewed; original-ROM event timing is pending. |
| Leech Seed abilities | Partially implemented | Magic Guard suppresses seeded damage; Liquid Ooze reverses healing; source-side Magic Guard prevents indirect Liquid Ooze damage. Heal Block and Big Root are unsupported. |
| Leech Seed switching | Implemented for singles, source-derived | Seeded target switch clears the volatile. Source-side switch preserves the target's effect and routes healing to the currently active battler on that side. Double-battle identity mapping and Baton Pass remain unsupported. |
| Rapid Spin | Leech Seed cleanup added | A successful damaging Rapid Spin clears seed on its user. Binding and all hazard cleanup paths remain their existing partial implementation. |
| Action lifecycle / priority edge matrix | Partial | Current action resolution remains in place. Runtime validation for modifiers, switching priority, and tie RNG remains pending. |
| Binding moves | Unsupported | Duration, random end checks, target identity, switching, and interruption require implementation and tests. |
| Entry-ability/hazard chains | Partial/unsupported | Existing hazards remain; weather-setting entry abilities and full chained replacements are not covered. |
| Cartridge conformance | Pending | New responsive-state replay did not consume injected input; emulator investigation stopped per bounded plan. |

The Phase 3 mechanics tests cover these implemented rows. This table does not imply broad Generation I–IX move or ability coverage.