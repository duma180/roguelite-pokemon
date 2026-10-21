using UnityEngine;

namespace PokemonRoguelite.NewBarkPilot
{
    // Diagnostic provenance only. No event, movement, or story behavior runs here.
    public sealed class PilotSourceRecord : MonoBehaviour
    {
        public string stableId;
        public string sourceStableId;
        public string sourceReference;
        public string kind;
        [TextArea(2, 12)] public string rawRecordJson;
        [TextArea(2, 5)] public string interpretation;
        public string geometryHash;
    }
}
