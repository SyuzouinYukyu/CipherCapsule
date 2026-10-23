using CipherCapsule.Core;
using System.Security.Cryptography;
using System.Text.Json;
internal static partial class Program
{
    static int InspectGui113()
    {
        string gui=Path.GetFullPath(Path.Combine(Root,"../Work/gui"));string folder=Path.Combine(gui,"出力 日本語=113");
        byte[] pw=Limits.Utf8.GetBytes("Gui113-Dummy!2026");var list=new List<object>();
        try{
            string template=Path.Combine(Root,"templates/CipherCapsule_Viewer.exe");long size=new FileInfo(template).Length;
            foreach(string kind in new[]{"TEXT","HTML"}){
                string path=Path.Combine(folder,kind+"-message.exe");string prefix;
                using(var f=File.OpenRead(path)){using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] b=new byte[1024*1024];long left=size;while(left>0){int n=f.Read(b,0,(int)Math.Min(left,b.Length));if(n<=0)throw new IOException("Truncated prefix");h.AppendData(b,0,n);left-=n;}prefix=Convert.ToHexString(h.GetHashAndReset());}
                using var capsule=Container.Unlock(path,pw,default);
                using var stream=File.OpenRead(path);string sha512=Convert.ToHexString(SHA512.HashData(stream));
                bool body=kind=="TEXT"?capsule.Manifest.Body.Replace("\r\n","\n")==File.ReadAllText(Path.Combine(gui,"本文.txt")).Replace("\r\n","\n"):capsule.Manifest.Body.Contains("CipherCapsule v1.1.3 HTML")&&capsule.Manifest.Body.Contains("<table")&&capsule.Manifest.Body.Contains("段落 60");
                list.Add(new{kind=capsule.Manifest.Kind.ToString(),bytes=new FileInfo(path).Length,embeddedViewerMatches=prefix==Hash(template),sha512Matches=File.ReadAllText(path+".sha512").TrimStart('\uFEFF').StartsWith(sha512,StringComparison.OrdinalIgnoreCase),bodyMatchesPreparedInput=body,attachments=capsule.Manifest.Attachments.Select(a=>new{a.Name,a.Size})});
            }
            string json=JsonSerializer.Serialize(list,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(Root,"../Work/gui-generated-exes-inspection.json"),json);Console.WriteLine(json);return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}finally{Sodium.Wipe(pw);}
    }
}
