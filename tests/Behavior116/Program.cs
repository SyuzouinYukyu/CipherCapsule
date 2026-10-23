using CipherCapsule.Builder;
using CipherCapsule.Core;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    sealed class Store : ICcapIconStore
    {
        public CcapIconValue Value = new(false,false,null,RegistryValueKind.String);
        public bool FailSet, FailAfterSet, FailRestore;
        public int Writes;
        public CcapIconValue Read()=>Value;
        public void Set(string value){if(FailSet)throw new IOException("simulated write failure");Value=new(true,true,value,RegistryValueKind.String);Writes++;if(FailAfterSet)throw new IOException("simulated post-write failure");}
        public void Restore(CcapIconValue value){if(FailRestore)throw new IOException("simulated restore failure");Value=value;Writes++;}
    }
    static readonly List<object> results=[];
    static string source="", root=""; static byte[] icon=[];
    static int next;
    static void Check(bool ok){if(!ok)throw new Exception("assertion failed");}
    static void Reject(Action action){bool threw=false;try{action();}catch{threw=true;}Check(threw);}
    static void Test(string name,Action action){try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+" "+e.Message);}}
    static (Store store,CcapIconRegistration reg,string dir) Fresh(CcapIconValue? original=null,Action? notify=null)
    {
        string dir=Path.Combine(root,(++next).ToString(),"日本語 空白=🔐");var store=new Store();if(original!=null)store.Value=original;
        return(store,new(store,dir,icon,notify??(()=>{})),dir);
    }
    static string State(string dir)=>Path.Combine(dir,"ccap-icon-registration.json");
    static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    static int Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("source root and isolated work root required");
        source=Path.GetFullPath(args[0]);root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        icon=File.ReadAllBytes(Path.Combine(source,"assets/viewer.ico"));
        Test("construction has no filesystem, registry or notification side effects",()=>{int notifications=0;var x=Fresh(notify:()=>notifications++);Check(!Directory.Exists(x.dir)&&x.store.Writes==0&&notifications==0);});
        Test("ICO is the exact generated Viewer asset",()=>{var x=Fresh();x.reg.Install();Check(File.ReadAllBytes(x.reg.IconPath).AsSpan().SequenceEqual(icon));});
        Test("Japanese spaces equals Unicode icon path is quoted as one resource",()=>{var x=Fresh();x.reg.Install();Check(x.store.Value.Value==$"\"{x.reg.IconPath}\",0"&&x.reg.IconPath.Contains("日本語 空白=🔐"));});
        Test("registration only sets a string icon resource",()=>{var x=Fresh();x.reg.Install();Check(x.store.Value.Kind==RegistryValueKind.String&&x.store.Writes==1);});
        Test("restore removes initially absent value and key",()=>{var x=Fresh();var old=x.store.Read();x.reg.Install();Check(x.reg.Restore()&&x.store.Read()==old&&!File.Exists(State(x.dir)));});
        Test("restore retains an initially empty key",()=>{var x=Fresh(new(true,false,null,RegistryValueKind.String));var old=x.store.Read();x.reg.Install();x.reg.Restore();Check(x.store.Read()==old);});
        Test("restore preserves pre-existing empty string",()=>{var x=Fresh(new(true,true,"",RegistryValueKind.String));var old=x.store.Read();x.reg.Install();x.reg.Restore();Check(x.store.Read()==old);});
        Test("restore preserves prior application icon",()=>{var x=Fresh(new(true,true,"\"C:\\Other App\\original.exe\",3",RegistryValueKind.String));var old=x.store.Read();x.reg.Install();x.reg.Restore();Check(x.store.Read()==old);});
        Test("restore preserves unexpanded environment reference and kind",()=>{var x=Fresh(new(true,true,"%SystemRoot%\\system32\\shell32.dll,-1",RegistryValueKind.ExpandString));var old=x.store.Read();x.reg.Install();x.reg.Restore();Check(x.store.Read()==old);});
        Test("repeated install preserves the first recovery snapshot",()=>{var x=Fresh(new(true,true,"old.ico",RegistryValueKind.String));var old=x.store.Read();x.reg.Install();byte[] before=File.ReadAllBytes(State(x.dir));x.reg.Install();Check(before.AsSpan().SequenceEqual(File.ReadAllBytes(State(x.dir))));x.reg.Restore();Check(x.store.Read()==old);});
        Test("new process instance can restore persistent recovery state",()=>{var x=Fresh();var old=x.store.Read();x.reg.Install();var later=new CcapIconRegistration(x.store,x.dir,icon,()=>{});Check(later.Restore()&&x.store.Read()==old);});
        Test("external icon change blocks reinstall without overwriting it",()=>{var x=Fresh();x.reg.Install();x.store.Value=new(true,true,"external.ico",RegistryValueKind.String);var old=x.store.Read();Reject(x.reg.Install);Check(x.store.Read()==old);});
        Test("external icon change blocks restore and preserves backup",()=>{var x=Fresh();x.reg.Install();x.store.Value=new(true,true,"external.ico",RegistryValueKind.String);var old=x.store.Read();Reject(()=>x.reg.Restore());Check(x.store.Read()==old&&File.Exists(State(x.dir)));});
        Test("failure before registry write retains recoverable state",()=>{var x=Fresh();var old=x.store.Read();x.store.FailSet=true;Reject(x.reg.Install);Check(x.store.Read()==old&&File.Exists(State(x.dir)));Check(x.reg.Restore());});
        Test("failure after registry write is recoverable",()=>{var x=Fresh();var old=x.store.Read();x.store.FailAfterSet=true;Reject(x.reg.Install);Check(x.store.Read()!=old&&File.Exists(State(x.dir)));x.reg.Restore();Check(x.store.Read()==old);});
        Test("failed restore retains recovery record",()=>{var x=Fresh();x.reg.Install();x.store.FailRestore=true;Reject(()=>x.reg.Restore());Check(File.Exists(State(x.dir)));x.store.FailRestore=false;Check(x.reg.Restore());});
        Test("shell refresh failure does not discard recovery state",()=>{var x=Fresh(notify:()=>throw new IOException("notification failure"));Reject(x.reg.Install);Check(File.Exists(State(x.dir)));var later=new CcapIconRegistration(x.store,x.dir,icon,()=>{});Check(later.Restore());});
        Test("restore with no record does not write registry",()=>{var x=Fresh();Check(!x.reg.Restore()&&x.store.Writes==0);});
        Test("tampered icon cache is not overwritten",()=>{var x=Fresh();Directory.CreateDirectory(x.dir);File.WriteAllText(x.reg.IconPath,"user file");Reject(x.reg.Install);Check(File.ReadAllText(x.reg.IconPath)=="user file"&&x.store.Writes==0);});
        Test("malformed recovery JSON is fail-closed",()=>{var x=Fresh();Directory.CreateDirectory(x.dir);File.WriteAllText(State(x.dir),"{");Reject(x.reg.Install);Check(x.store.Writes==0);});
        Test("oversized recovery record is fail-closed",()=>{var x=Fresh();Directory.CreateDirectory(x.dir);File.WriteAllText(State(x.dir),new string('x',32769));Reject(x.reg.Install);Check(x.store.Writes==0);});
        Test("changed installed path in recovery record is rejected",()=>{var x=Fresh();x.reg.Install();File.WriteAllText(State(x.dir),File.ReadAllText(State(x.dir)).Replace("ccap-","other-"));int before=x.store.Writes;Reject(()=>x.reg.Restore());Check(x.store.Writes==before);});
        Test("caller mutation of source ICO cannot change cached asset",()=>{var x=Fresh();byte[] bytes=icon.ToArray();var reg=new CcapIconRegistration(x.store,x.dir,bytes,()=>{});bytes[^1]^=1;reg.Install();Check(File.ReadAllBytes(reg.IconPath).AsSpan().SequenceEqual(icon));});
        Test("truncated icon rejected before any side effect",()=>{var x=Fresh();Reject(()=>new CcapIconRegistration(x.store,x.dir,[0,0],()=>{}));Check(!Directory.Exists(x.dir)&&x.store.Writes==0);});
        Test("invalid image offset rejected",()=>{var x=Fresh();var bytes=icon.ToArray();for(int i=18;i<22;i++)bytes[i]=255;Reject(()=>new CcapIconRegistration(x.store,x.dir,bytes,()=>{}));});
        Test("zero image count rejected",()=>{var x=Fresh();var bytes=icon.ToArray();bytes[4]=bytes[5]=0;Reject(()=>new CcapIconRegistration(x.store,x.dir,bytes,()=>{}));});
        Test("relative icon cache folder rejected",()=>{var x=Fresh();Reject(()=>new CcapIconRegistration(x.store,"relative",icon,()=>{}));});
        Test("notification runs only after verified install and restore",()=>{int n=0;var x=Fresh(notify:()=>n++);Check(n==0);x.reg.Install();Check(n==1);x.reg.Restore();Check(n==2);});
        Test("TEXT CCAP roundtrip remains unchanged",()=>Roundtrip(false));
        Test("HTML CCAP copy restriction attachment hash remains unchanged",()=>Roundtrip(true));
        Test("automatic startup invokes registration without a choice",()=>{int calls=0;Check(AutomaticCcapIcon.Apply(()=>calls++)==null&&calls==1);});
        Test("denied startup icon registration warns and returns normally",()=>{Check(AutomaticCcapIcon.Apply(()=>throw new UnauthorizedAccessException())!.Contains("暗号化・復号は引き続き"));});
        Test("unexpected startup registration failure is contained",()=>{Check(AutomaticCcapIcon.Apply(()=>throw new InvalidDataException())!.Contains("設定できません"));});
        Test("forced startup replaces a changed icon without replacing first backup",()=>{var x=Fresh(new(true,true,"original.ico",RegistryValueKind.String));var original=x.store.Read();x.reg.Install();byte[] saved=File.ReadAllBytes(State(x.dir));x.store.Value=new(true,true,"changed.ico",RegistryValueKind.String);x.reg.Install(force:true);Check(x.store.Read().Value==x.reg.IconValue&&saved.AsSpan().SequenceEqual(File.ReadAllBytes(State(x.dir))));x.reg.Restore();Check(x.store.Read()==original);});
        Test("forced first install preserves existing user icon for recovery",()=>{var x=Fresh(new(true,true,"other.ico",RegistryValueKind.String));var original=x.store.Read();x.reg.Install(force:true);Check(x.store.Read().Value==x.reg.IconValue);x.reg.Restore();Check(x.store.Read()==original);});
        Test("unchanged forced startup avoids redundant registry and shell refresh",()=>{int calls=0;var x=Fresh(notify:()=>calls++);x.reg.Install(force:true);int writes=x.store.Writes;x.reg.Install(force:true);Check(x.store.Writes==writes&&calls==1);});
        Test("forced write failure keeps recovery snapshot",()=>{var x=Fresh();x.reg.Install();byte[] saved=File.ReadAllBytes(State(x.dir));x.store.Value=new(true,true,"external.ico",RegistryValueKind.String);x.store.FailSet=true;Reject(()=>x.reg.Install(force:true));Check(saved.AsSpan().SequenceEqual(File.ReadAllBytes(State(x.dir)))&&x.store.Value.Value=="external.ico");});
        Test("forced startup regenerates a missing cached icon",()=>{int calls=0;var x=Fresh(notify:()=>calls++);x.reg.Install(force:true);File.Delete(x.reg.IconPath);x.reg.Install(force:true);Check(File.ReadAllBytes(x.reg.IconPath).AsSpan().SequenceEqual(icon)&&calls==2);});
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
        var report=new{version="1.1.6",passed=results.Count-failed,failed,headless=true,desktop_input=false,windows_file_association_modified=false,registry_backend="isolated in-memory fake",tests=results};
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
