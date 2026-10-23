using CipherCapsule.Core;
using CipherCapsule.Builder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    sealed class Inline(Action<CapsuleProgress> action):IProgress<CapsuleProgress>{public void Report(CapsuleProgress value)=>action(value);}
    static readonly List<object> results=[];
    static string work="",template="";static int index;
    static readonly byte[] password=Encoding.UTF8.GetBytes("Checksum116-Dummy!2026");
    static void Check(bool ok){if(!ok)throw new Exception("assertion failed");}
    static void Reject(Action action){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected);}
    static string Area(){string p=Path.Combine(work,(++index).ToString());Directory.CreateDirectory(p);return p;}
    static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
    static void Test(string name,Action action){try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception ex){results.Add(new{name,passed=false,error=ex.Message});Console.WriteLine("FAIL "+name+" "+ex.Message);}}
    static PackageResult Generate(string p,bool a,bool b,bool overwrite=false,CancellationToken ct=default,IProgress<CapsuleProgress>? progress=null,IReadOnlyList<string>? inputs=null)=>PackageWriter.GenerateCcap(p,"checksum test 日本語",BodyKind.TEXT,inputs??[],password,a,overwrite,ct,progress,checksum256:b);
    static void Seed(string p){File.WriteAllText(p,"ORIGINAL PACKAGE");File.WriteAllText(p+".sha512","ORIGINAL 512");File.WriteAllText(p+".sha256","ORIGINAL 256");}
    static void Original(string p){Check(File.ReadAllText(p)=="ORIGINAL PACKAGE"&&File.ReadAllText(p+".sha512")=="ORIGINAL 512"&&File.ReadAllText(p+".sha256")=="ORIGINAL 256");}
    static int Main(string[] args)
    {
        template=Path.GetFullPath(args[0]);work=Path.GetFullPath(args[1]);Directory.CreateDirectory(work);
        Test("self-extracting checkbox ON selects EXE",()=>Check(ArchiveOutputSelection.FromSelfExtracting(true)==OutputFormat.EXE));
        Test("self-extracting checkbox OFF selects CCAP",()=>Check(ArchiveOutputSelection.FromSelfExtracting(false)==OutputFormat.CCAP));
        Test("existing INI enum values map back to the checkbox",()=>Check(ArchiveOutputSelection.IsSelfExtracting(OutputFormat.EXE)&&!ArchiveOutputSelection.IsSelfExtracting(OutputFormat.CCAP)&&(int)OutputFormat.EXE==0&&(int)OutputFormat.CCAP==1));
        foreach(bool exe in new[]{false,true})foreach(bool a in new[]{false,true})foreach(bool b in new[]{false,true}){
            bool selectedExe=exe,sha512=a,sha256=b;
            Test($"{(exe?"EXE":"CCAP")} SHA512={a} SHA256={b} whole-file digest and roundtrip",()=>{
                string area=Area(),p=Path.Combine(area,"日本語 空白=🔐"+(selectedExe?".exe":".ccap")),input=Path.Combine(area,"添付.txt");File.WriteAllText(input,"attachment content 日本語");
                using var stream=selectedExe?File.OpenRead(template):Stream.Null;
                var package=PackageWriter.Generate(stream,p,"<p>checksum 日本語</p>",BodyKind.HTML,[input],password,sha512,false,default,allowCopy:false,checksum256:sha256);
                Check(File.Exists(p+".sha512")==sha512&&File.Exists(p+".sha256")==sha256&&package.Sha256FileWritten==sha256&&package.Sha256==Hash(p));
                if(sha256)Check(File.ReadAllText(p+".sha256")==Hash(p).ToLowerInvariant()+"  "+Path.GetFileName(p)+"\n"&&!File.ReadAllBytes(p+".sha256").AsSpan().StartsWith(new byte[]{239,187,191}));
                if(sha512){using var f=File.OpenRead(p);string h=Convert.ToHexString(SHA512.HashData(f)).ToLowerInvariant();Check(package.Sha512==h&&File.ReadAllText(p+".sha512")==h+"  "+Path.GetFileName(p)+"\n");}else Check(package.Sha512==null);
                using var capsule=Container.Unlock(p,password,default,ccapOnly:!selectedExe);Check(capsule.Manifest.Body=="<p>checksum 日本語</p>"&&!capsule.Manifest.AllowCopy);string saved=Path.Combine(area,"saved.txt");capsule.Save(0,saved,false,default);Check(Hash(input)==Hash(saved));
            });
        }
        Test("legacy API defaults to no SHA256 file",()=>{string p=Path.Combine(Area(),"legacy.ccap");var package=PackageWriter.GenerateCcap(p,"legacy",BodyKind.TEXT,[],password,true,false,default);Check(!package.Sha256FileWritten&&!File.Exists(p+".sha256")&&File.Exists(p+".sha512"));});
        Test("SHA256 input collision is rejected without altering input",()=>{string p=Path.Combine(Area(),"input.ccap");File.WriteAllText(p+".sha256","INPUT");Reject(()=>Generate(p,true,true,inputs:[p+".sha256"]));Check(File.ReadAllText(p+".sha256")=="INPUT"&&!File.Exists(p));});
        Test("SHA256 directory collision has no output side effects",()=>{string p=Path.Combine(Area(),"dir.ccap");Directory.CreateDirectory(p+".sha256");Reject(()=>Generate(p,true,true));Check(!File.Exists(p)&&!File.Exists(p+".sha512"));});
        Test("locked SHA256 restores committed SHA512 and keeps output",()=>{string p=Path.Combine(Area(),"locked256.ccap");Seed(p);using(var held=new FileStream(p+".sha256",FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject(()=>Generate(p,true,true,true));Original(p);});
        Test("locked final package restores both old checksum files",()=>{string p=Path.Combine(Area(),"locked.ccap");Seed(p);using(var held=new FileStream(p,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject(()=>Generate(p,true,true,true));Original(p);});
        Test("locked SHA512 keeps SHA256 and package intact",()=>{string p=Path.Combine(Area(),"locked512.ccap");Seed(p);using(var held=new FileStream(p+".sha512",FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject(()=>Generate(p,true,true,true));Original(p);});
        Test("no-overwrite rolls back newly created checksum files",()=>{string p=Path.Combine(Area(),"existing.ccap");File.WriteAllText(p,"KEEP");Reject(()=>Generate(p,true,true));Check(File.ReadAllText(p)=="KEEP"&&!File.Exists(p+".sha512")&&!File.Exists(p+".sha256"));});
        Test("pre-canceled generation keeps all old files",()=>{string p=Path.Combine(Area(),"cancel.ccap");Seed(p);using var ct=new CancellationTokenSource();ct.Cancel();Reject(()=>Generate(p,true,true,true,ct.Token));Original(p);});
        Test("cancellation before checksum commit preserves old three-file set",()=>{string p=Path.Combine(Area(),"prepare.ccap");Seed(p);using var ct=new CancellationTokenSource();bool reached=false;Reject(()=>Generate(p,true,true,true,ct.Token,new Inline(x=>{if(x.Stage=="完成ファイルのチェックサムを計算しています"){reached=true;ct.Cancel();}})));Check(reached);Original(p);});
        Test("disabled checksums preserve existing sidecars",()=>{string p=Path.Combine(Area(),"disabled.ccap");Seed(p);var package=Generate(p,false,false,true);Check(package.Sha512==null&&!package.Sha256FileWritten&&File.ReadAllText(p+".sha512")=="ORIGINAL 512"&&File.ReadAllText(p+".sha256")=="ORIGINAL 256");});
        Test("all success and failure paths leave no atomic temporary files",()=>Check(!Directory.EnumerateFiles(work,".cc-*.tmp",SearchOption.AllDirectories).Any()));
        int failed=results.Count(x=>!(bool)x.GetType().GetProperty("passed")!.GetValue(x)!);
        File.WriteAllText(Path.Combine(work,"results.json"),JsonSerializer.Serialize(new{version="1.1.6",revision="ui-checksum",passed=results.Count-failed,failed,headless=true,desktop_input=false,tests=results},new JsonSerializerOptions{WriteIndented=true}));
        Sodium.Wipe(password);Console.WriteLine($"RESULT {results.Count-failed}/{failed}");return failed==0?0:1;
    }
}
