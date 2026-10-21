using System;
using UnityEngine;

namespace PokemonRoguelite.NewBarkPilot
{
    public sealed class PilotPermissionBlock : MonoBehaviour
    {
        public int landDataId;
        public string sourceReference;
        public string sourcePermissionSha256;
        public int[] typeBytes;
        public int[] collisionBytes;

        public byte[] ToSourceBytes()
        {
            if (typeBytes == null || collisionBytes == null || typeBytes.Length != 1024 || collisionBytes.Length != 1024)
                throw new InvalidOperationException("Expected 1024 runtime-order permission pairs (x+32*z).");
            var bytes = new byte[2048];
            for (int z = 0; z < 32; z++)
                for (int x = 0; x < 32; x++)
                {
                    int i = x + 32 * z;
                    bytes[i * 2] = checked((byte)typeBytes[i]);
                    bytes[i * 2 + 1] = checked((byte)collisionBytes[i]);
                }
            return bytes;
        }
    }
}
