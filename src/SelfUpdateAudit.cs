using System;
using System.IO;
namespace Astera {
static class SelfUpdateAudit {
    internal static int Verify(string file,string candidate=null){try{var release=SelfUpdate.Verify(File.ReadAllBytes(file),SelfUpdate.Embedded().PublicKeyXml);if(candidate!=null)SelfUpdate.ValidateCandidate(candidate,release);SelfUpdate.Save(file+".verified-app.json",new{Passed=true,Version=release.Version,Sequence=release.Sequence});return 0;}catch(Exception error){File.WriteAllText(file+".failure.txt",error.Message);return 1;}}
}
}
