using UnityEngine;

namespace PokemonRoguelite.NewBarkPilot
{
    public sealed class PilotImportSession : MonoBehaviour
    {
        public TextAsset sourceIr;
        public string schemaVersion;
        public string sourceIrSha256;
        public string sourceRomSha256;
        public string importerVersion;
        [TextArea(3, 12)] public string coordinateProfileJson;
        public string manifestSha256;
        public bool acceptedBaseline; // Remains false until review; importer never sets true.
    }
}
