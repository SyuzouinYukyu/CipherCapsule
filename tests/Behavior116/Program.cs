using CipherCapsule.Builder;
using CipherCapsule.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    static readonly List<object> results=[];
    static string source="", root="";
    static void Check(bool ok){if(!ok)throw new Exception("assertion failed");}
    static void Reject(Action action){bool threw=false;try{action();}catch{threw=true;}Check(threw);}
    static void Test(string name,Action action){try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+" "+e.Message);}}
    static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    static int Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("source root and isolated work root required");
        source=Path.GetFullPath(args[0]);root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        // Installation and forced registration were retired in v1.1.10.
        // Restore-only coverage now lives in Privacy1110.
        Test("TEXT CCAP roundtrip remains unchanged",()=>Roundtrip(false));
        Test("HTML CCAP copy restriction attachment hash remains unchanged",()=>Roundtrip(true));
        foreach(var area in Enum.GetValues<FileDropArea>()){
            var captured=area;
            Test("CCAP routing wins in "+area,()=>Check(FileDropRouting.Decide([@"F:\日本語 空白=🔐\本文.ccap"],captured,false)==FileDropAction.OpenCcap));
            Test("busy or closing blocks drop in "+area,()=>Check(FileDropRouting.Decide([@"F:\本文.ccap"],captured,true)==FileDropAction.Ignore));
        }
        Test("uppercase CCAP extension accepted everywhere",()=>Check(FileDropRouting.Decide([@"F:\capsule.CCAP"],FileDropArea.Other,false)==FileDropAction.OpenCcap));
        Test("CCAP mixed with ordinary attachments stays a decryption request",()=>Check(FileDropRouting.Decide([@"F:\capsule.ccap",@"F:\file.txt"],FileDropArea.Attachments,false)==FileDropAction.OpenCcap));
        Test("multiple CCAPs are forwarded to existing one-file validation",()=>Check(FileDropRouting.Decide([@"F:\one.ccap",@"F:\two.ccap"],FileDropArea.TextBody,false)==FileDropAction.OpenCcap));
        Test("ordinary body drop is preserved",()=>Check(FileDropRouting.Decide([@"F:\body.txt"],FileDropArea.TextBody,false)==FileDropAction.ReadBody));
        Test("ordinary attachment drop is preserved",()=>Check(FileDropRouting.Decide([@"F:\one.txt",@"F:\two.pdf"],FileDropArea.Attachments,false)==FileDropAction.AddAttachments));
        Test("ordinary file elsewhere is not silently inserted",()=>Check(FileDropRouting.Decide([@"F:\file.txt"],FileDropArea.Other,false)==FileDropAction.Ignore));
        Test("multiple body files are ignored",()=>Check(FileDropRouting.Decide([@"F:\one.txt",@"F:\two.txt"],FileDropArea.TextBody,false)==FileDropAction.Ignore));
        Test("empty file drag is ignored",()=>Check(FileDropRouting.Decide([],FileDropArea.Other,false)==FileDropAction.Ignore));
        Test("invalid missing drag path is ignored",()=>Check(FileDropRouting.Decide(["",@"F:\a.ccap"],FileDropArea.Attachments,false)==FileDropAction.Ignore));
        Test("CCAP in the middle of filename is not a decryption request",()=>Check(FileDropRouting.Decide([@"F:\a.ccap.txt"],FileDropArea.Other,false)==FileDropAction.Ignore));
        int failed=results.Count(x=>!(bool)x.GetType().GetProperty("passed")!.GetValue(x)!);
        var report=new{version="1.1.10",passed=results.Count-failed,failed,headless=true,desktop_input=false,windows_file_association_modified=false,registry_backend="isolated in-memory fake",tests=results};
        File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"RESULT {results.Count-failed}/{failed}");return failed==0?0:1;
    }
    static void Roundtrip(bool html)
    {
        string area=Path.Combine(root,html?"HTML":"TEXT");Directory.CreateDirectory(area);
        string attached=Path.Combine(area,"添付.txt"),output=Path.Combine(area,"本文.ccap");File.WriteAllText(attached,"v115 dummy 日本語");
        string body=html?"<h1>v115</h1><table><tr><td>表</td></tr></table>":"v115 TEXT 日本語";byte[] password=Encoding.UTF8.GetBytes("Icon115-Dummy!2026");
        try{
            var package=PackageWriter.GenerateCcap(output,body,html?BodyKind.HTML:BodyKind.TEXT,[attached],password,true,false,default,allowCopy:!html);
            using var capsule=Container.Unlock(output,password,default,ccapOnly:true);
            Check(capsule.Manifest.Body==body&&capsule.Manifest.AllowCopy==!html&&capsule.Manifest.Attachments.Count==1&&package.Sha256==Hash(output));
            string saved=Path.Combine(area,"saved.txt");capsule.Save(0,saved,false,default);Check(Hash(saved)==Hash(attached));
            Check(File.ReadAllText(output+".sha512").StartsWith(package.Sha512!));
        }finally{Sodium.Wipe(password);}
    }
}
