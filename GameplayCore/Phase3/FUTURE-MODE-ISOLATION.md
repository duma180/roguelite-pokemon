# Future standard and Challenge Mode save isolation

This is an architectural constraint only; Phase 3.2 implements no Challenge Mode, permadeath, season, or reward-sharing system.

- Standard Normal, Hard, and Nightmare adventures each persist their own run state according to their approved progression policy. A post-Red Challenge Mode adventure is a distinct mode save with its own party/PC/run records.
- Challenge Mode Nuzlocke death may mark or remove an individual only from that Challenge Mode save's references and storage. It must never delete, mutate, or change the identity of that Pokémon in any Standard adventure save.
- Shared immutable catalog definitions and stable individual IDs may be read across modes. Mutable Pokémon state, party membership, PC storage, enhancements, run status, Gym achievements, and currency remain owned by an explicit save/mode boundary.
- Any intentionally shared future account-level value requires a separately versioned policy and explicit transaction contract. No cross-mode rewards are presumed.
- Timed seasons and season-driven deletion/expiry are out of scope. Standard saves and Challenge saves must remain recoverable independently.

When implemented, tests must prove that a permadeath transaction in a Challenge save leaves byte-equivalent Standard-save payloads and identities unchanged, including interruption/recovery cases.
