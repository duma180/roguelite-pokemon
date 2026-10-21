using System;
using System.IO;
using System.Linq;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    // Unity's scene-template service can create a default project-settings file on scene save.
    // Restore its exact pre-operation bytes or absence without changing the user's configuration.
    public sealed class PilotSettingsSnapshot:IDisposable
    {
        readonly string path;
        readonly byte[] original;
        readonly DateTime writeTime;
        public PilotSettingsSnapshot(string path)
        {
            this.path=path;
            if(File.Exists(path)){original=File.ReadAllBytes(path);writeTime=File.GetLastWriteTimeUtc(path);}
        }
        public void Dispose()
        {
            if(original==null) {if(File.Exists(path))File.Delete(path);return;}
            if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(original))
            {
                File.WriteAllBytes(path,original);File.SetLastWriteTimeUtc(path,writeTime);
            }
        }
    }
}
