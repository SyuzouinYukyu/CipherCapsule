extern alias Legacy119;
extern alias Android104;
using CipherCapsule.Core;
using CipherCapsule.Builder;
using CipherCapsule.UI;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Old=Legacy119::CipherCapsule.Core;
using Android=Android104::CipherCapsule.Core;

string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
var results=new List<object>();int failed=0,newPassed=0,regPassed=0;bool regression=false;
void True(bool b){if(!b)throw new Exception("Assertion failed");}
void Reject(Action a){try{a();}catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or CryptographicException or OperationCanceledException){return;}throw new Exception("Expected rejection");}
void Test(string name,Action a){if(args.Length>6&&!args[6].Split('|').Contains(name))return;try{a();results.Add(new{name,passed=true,category=regression?"regression":"privacy"});if(regression)regPassed++;else newPassed++;}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAILED "+name+": "+e.Message);}}
string Next(string name){string p=Path.Combine(root,name);Directory.CreateDirectory(p);return p;}
byte[] icon=File.ReadAllBytes(args[1]);
CcapIconValue original=new(false,false,null,RegistryValueKind.String);
string StatePath(string f)=>Path.Combine(f,"ccap-icon-registration.json");
void State(string f,CcapIconRegistration migration,CcapIconValue value)
{
    File.WriteAllBytes(migration.IconPath,icon);
    File.WriteAllText(StatePath(f),JsonSerializer.Serialize(new{Version=1,Original=value,InstalledValue=$"\"{migration.IconPath}\",0"}));
}
(FakeStore store,CcapIconRegistration migration,string folder) Fixture(string name,CcapIconValue? value=null)
{
    string f=Next(name);var store=new FakeStore();var m=new CcapIconRegistration(store,f,()=>store.Notifications++);
    State(f,m,value??original);store.Current=new(true,true,$"\"{m.IconPath}\",0",RegistryValueKind.String);return(store,m,f);
}
Test("migration absent state creates no files and never reads registry",()=>{
    string p=Path.Combine(root,"missing-icons");var store=new FakeStore();var m=new CcapIconRegistration(store,p,()=>{});
    True(!m.Restore()&&!Directory.Exists(p)&&store.Reads==0&&store.Writes==0);
});
foreach(var prior in new[]{original,new CcapIconValue(true,false,null,RegistryValueKind.String),new CcapIconValue(true,true,"original.ico,2",RegistryValueKind.String),new CcapIconValue(true,true,@"%SystemRoot%\system32\shell32.dll,0",RegistryValueKind.ExpandString)})
Test("restore recorded original "+prior,()=>{var f=Fixture("restore-"+Guid.NewGuid().ToString("N"),prior);True(f.migration.Restore());True(f.store.Current==prior&&f.store.Writes==1&&f.store.Notifications==1&&!Directory.Exists(f.folder));True(!f.migration.Restore()&&f.store.Writes==1);});
Test("already restored original needs no registry write",()=>{var f=Fixture("already");f.store.Current=original;True(f.migration.Restore()&&f.store.Writes==0&&f.store.Notifications==0);});
Test("third party current icon and recovery files preserved",()=>{var f=Fixture("foreign");f.store.Current=new(true,true,"third-party.ico,0",RegistryValueKind.String);Reject(()=>f.migration.Restore());True(f.store.Writes==0&&File.Exists(f.migration.IconPath)&&File.Exists(StatePath(f.folder)));});
Test("changed current value just before restore preserved",()=>{var f=Fixture("race");f.store.ChangeOnSecondRead=true;Reject(()=>f.migration.Restore());True(f.store.Writes==0&&File.Exists(StatePath(f.folder)));});
Test("missing recovery state retains known ICO",()=>{var f=Fixture("missing-state");File.Delete(StatePath(f.folder));Reject(()=>f.migration.Restore());True(f.store.Writes==0&&File.Exists(f.migration.IconPath));});
Test("corrupt state preserves registry and files",()=>{var f=Fixture("corrupt");File.WriteAllText(StatePath(f.folder),"{");Reject(()=>f.migration.Restore());True(f.store.Writes==0&&File.Exists(f.migration.IconPath));});
Test("missing state fields rejected",()=>{var f=Fixture("fields");File.WriteAllText(StatePath(f.folder),"{\"Version\":1,\"Original\":{},\"InstalledValue\":\"x\"}");Reject(()=>f.migration.Restore());True(f.store.Writes==0);});
Test("oversize recovery state rejected",()=>{var f=Fixture("large-state");File.WriteAllText(StatePath(f.folder),new string(' ',32769));Reject(()=>f.migration.Restore());True(f.store.Writes==0);});
Test("unexpected installed path rejected",()=>{var f=Fixture("path-mismatch");string text=File.ReadAllText(StatePath(f.folder));File.WriteAllText(StatePath(f.folder),text.Replace("ccap-","foreign-"));Reject(()=>f.migration.Restore());True(f.store.Writes==0);});
Test("tampered icon never deleted or restored",()=>{var f=Fixture("tampered-icon");var b=(byte[])icon.Clone();b[50]^=1;File.WriteAllBytes(f.migration.IconPath,b);Reject(()=>f.migration.Restore());True(f.store.Writes==0&&File.Exists(f.migration.IconPath));});
Test("restore write failure retains recovery files",()=>{var f=Fixture("write-failure");f.store.FailWrite=true;Reject(()=>f.migration.Restore());True(File.Exists(f.migration.IconPath)&&File.Exists(StatePath(f.folder)));});
Test("restore verification failure retains recovery files",()=>{var f=Fixture("verify-failure");f.store.IgnoreWrite=true;Reject(()=>f.migration.Restore());True(File.Exists(f.migration.IconPath)&&File.Exists(StatePath(f.folder)));});
Test("other class referencing ICO prevents asset deletion",()=>{var f=Fixture("referenced");f.store.Referenced=true;Reject(()=>f.migration.Restore());True(f.store.Current==original&&File.Exists(f.migration.IconPath)&&File.Exists(StatePath(f.folder)));});
Test("icon change during reference check retains ICO and state",()=>{var f=Fixture("late-change");f.store.ChangeDuringReferenceCheck=true;Reject(()=>f.migration.Restore());True(File.Exists(f.migration.IconPath)&&File.Exists(StatePath(f.folder))&&f.store.Current.Value=="late user change");});
Test("registry adapter never deletes a registry key",()=>{string t=File.ReadAllText(Path.Combine(args[2],"src/Builder/CcapIconRegistration.cs"));True(!t.Contains("DeleteSubKey")&&t.Contains("DeleteValue(\"\",false)"));});
Test("unknown files in Icons retained",()=>{var f=Fixture("unknown-file");string p=Path.Combine(f.folder,"user.txt");File.WriteAllText(p,"keep");True(f.migration.Restore()&&File.ReadAllText(p)=="keep"&&Directory.Exists(f.folder));});
Test("missing legacy ICO can restore verified snapshot",()=>{var f=Fixture("no-ico");File.Delete(f.migration.IconPath);True(f.migration.Restore()&&f.store.Current==original);});
void Junction(string link,string target)
{
    var info=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    info.ArgumentList.Add("/c");info.ArgumentList.Add("mklink");info.ArgumentList.Add("/J");info.ArgumentList.Add(link);info.ArgumentList.Add(target);
    using var p=Process.Start(info)!;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Junction fixture creation failed: "+p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd());
}
Test("icon ancestor junction rejected; target preserved",()=>{var f=Fixture("linked-icons-target");string parent=Next("links"),link=Path.Combine(parent,"icons");Junction(link,f.folder);try{Reject(()=>new CcapIconRegistration(f.store,link,()=>{}).Restore());True(f.store.Writes==0&&File.Exists(StatePath(f.folder)));}finally{Directory.Delete(link,false);}});
string LegacyLog(string local)
{
    string folder=Path.Combine(local,"Diagnostics");Directory.CreateDirectory(folder);string p=Path.Combine(folder,"editor-switch118.json");
    File.WriteAllText(p,JsonSerializer.Serialize(new{version="1.1.9",utc=DateTime.UtcNow,ready=true,selected=1,errorType="IOException",hresult=-1,events=Array.Empty<object>()}));return p;
}
Test("exact validated legacy diagnostic removed and empty folder removed",()=>{string f=Next("diagnostic");string p=LegacyLog(f);PrivacyMigration.RemoveLegacyDiagnostic(f);True(!File.Exists(p)&&!Directory.Exists(Path.GetDirectoryName(p)));});
Test("unknown diagnostic files retained",()=>{string f=Next("diagnostic-unknown");string p=LegacyLog(f),other=Path.Combine(Path.GetDirectoryName(p)!,"other.json");File.WriteAllText(other,"keep");PrivacyMigration.RemoveLegacyDiagnostic(f);True(!File.Exists(p)&&File.ReadAllText(other)=="keep");});
Test("unrecognized named diagnostic preserved",()=>{string f=Next("diagnostic-invalid");string p=LegacyLog(f);File.WriteAllText(p,"{\"personal\":true}");Reject(()=>PrivacyMigration.RemoveLegacyDiagnostic(f));True(File.Exists(p));});
Test("no diagnostic state creates no directories",()=>{string f=Path.Combine(root,"diagnostic-missing");PrivacyMigration.RemoveLegacyDiagnostic(f);True(!Directory.Exists(f));});
Test("empty ArmorMessages removed, populated folder preserved",()=>{string f=Next("old-armor-empty");ManagedFiles.RemoveEmpty(f);True(!Directory.Exists(f));f=Next("old-armor-nonempty");File.WriteAllText(Path.Combine(f,"unknown.ccap"),"keep");ManagedFiles.RemoveEmpty(f);True(File.Exists(Path.Combine(f,"unknown.ccap")));});
Test("profile success removes only own directory and empty root",()=>{string r=Path.Combine(root,"profiles-ok");var p=new ProfileDirectory(r);p.Create();File.WriteAllText(Path.Combine(p.PathName,"dummy"),"test");p.DeleteAfterExit(true);True(!Directory.Exists(p.PathName)&&!Directory.Exists(r));});
Test("active profile cleanup refused",()=>{var p=new ProfileDirectory(Path.Combine(root,"profiles-active"));p.Create();Reject(()=>p.DeleteAfterExit(false));True(Directory.Exists(p.PathName));p.DeleteAfterExit(true);});
Test("other instance profile remains intact",()=>{string r=Path.Combine(root,"profiles-multiple");var a=new ProfileDirectory(r);var b=new ProfileDirectory(r);a.Create();b.Create();string keep=Path.Combine(b.PathName,"keep");File.WriteAllText(keep,"other");a.DeleteAfterExit(true);True(File.ReadAllText(keep)=="other"&&Directory.Exists(r));b.DeleteAfterExit(true);True(!Directory.Exists(r));});
Test("unowned directory cannot be adopted for cleanup",()=>{var p=new ProfileDirectory(Path.Combine(root,"profiles-unowned"));Directory.CreateDirectory(p.PathName);p.DeleteAfterExit(true);True(Directory.Exists(p.PathName));});
Test("profile lock fails without force; retry after release succeeds",()=>{var p=new ProfileDirectory(Path.Combine(root,"profiles-lock"));p.Create();string file=Path.Combine(p.PathName,"locked");using(var hold=new FileStream(file,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){Reject(()=>p.DeleteAfterExit(true));True(File.Exists(file));}p.DeleteAfterExit(true);True(!Directory.Exists(p.PathName));});
Test("profile nested junction cannot delete target",()=>{var p=new ProfileDirectory(Path.Combine(root,"profiles-link"));p.Create();string target=Next("profile-external-target"),keep=Path.Combine(target,"keep"),link=Path.Combine(p.PathName,"link");File.WriteAllText(keep,"keep");Junction(link,target);try{Reject(()=>p.DeleteAfterExit(true));True(File.ReadAllText(keep)=="keep");}finally{Directory.Delete(link,false);}p.DeleteAfterExit(true);});
Test("profile ancestor junction cannot be created",()=>{string target=Next("profile-root-target"),link=Path.Combine(Next("profile-root-link"),"alias");Junction(link,target);try{var p=new ProfileDirectory(link);Reject(p.Create);True(!Directory.EnumerateFileSystemEntries(target).Any());}finally{Directory.Delete(link,false);}});
Test("HTML switch error retains content and permits explicit retry",()=>{
    int current=0,errors=0;bool broken=true;string contents="未保存本文";
    var queue=new EditorSwitchQueue(()=>current,(i,ct)=>broken?Task.FromException<EditorSwitchDraft>(new IOException("test")):Task.FromResult(new EditorSwitchDraft(true,contents)),(i,d)=>{current=i;contents=d.Html;},_=>{},_=>errors++);
    queue.Request(1).GetAwaiter().GetResult();True(current==0&&contents=="未保存本文"&&errors==1);broken=false;queue.Request(1).GetAwaiter().GetResult();True(current==1&&contents=="未保存本文");
});
string source=args[2];
Test("HTML failure path contains no diagnostic file writes",()=>{string text=File.ReadAllText(Path.Combine(source,"src/Builder/HtmlEditor.cs"));True(!text.Contains("File.Write")&&!text.Contains("Directory.CreateDirectory")&&!text.Contains("editor-switch118.json")&&text.Contains("編集中の内容は保持"));});
Test("startup contains no icon install or icon resource",()=>{string program=File.ReadAllText(Path.Combine(source,"src/Builder/Program.cs")),project=File.ReadAllText(Path.Combine(source,"src/Builder/Builder.csproj")),implementation=File.ReadAllText(Path.Combine(source,"src/Builder/CcapIconRegistration.cs"));True(!program.Contains("Install")&&!project.Contains("LogicalName=\"CcapIcon\"")&&!implementation.Contains("void Install")&&!implementation.Contains("CreateNew"));});
Test("Armor service contains no filesystem APIs",()=>{string t=File.ReadAllText(Path.Combine(source,"src/Core/AsciiArmor.cs"));t=t[t.IndexOf("public static class ArmorMessages")..t.IndexOf("public sealed class ArmorMessageState")];True(!t.Contains("File.")&&!t.Contains("FileStream")&&!t.Contains("Directory.")&&!t.Contains("TemporaryCipher"));});
byte[] key=Limits.Utf8.GetBytes("Armor-Dummy!2026-日本語");string text="日本語😀 e\u0301 = /\r\n2行目\n末尾\n";
string armor=File.ReadAllText(args[3]);
Test("memory encrypt/decrypt preserves Unicode and multiline",()=>{armor=ArmorMessages.Encrypt(text,key,default);True(ArmorMessages.Decrypt(armor,key,default)==text);});
Test("memory Armor format accepted by old Windows and Android",()=>{byte[] b=AsciiArmor.Decode(armor);try{True(Old.AsciiArmor.Decode(armor).SequenceEqual(b)&&Android.AsciiArmor.Decode(armor).SequenceEqual(b));True(armor==Old.AsciiArmor.Encode(b)&&armor==Android.AsciiArmor.Encode(b));}finally{Sodium.Wipe(b);}});
Test("new message decrypts in old Windows host Core",()=>{string temp=Next("old119-compat-temp");True(Old.ArmorMessages.Decrypt(armor,key,temp,default)==text);});
Test("new message decrypts in unchanged Android host Core",()=>{string temp=Next("android104-compat-temp");True(Android.ArmorMessages.Decrypt(armor,key,temp,default)==text);});
Test("old Windows v119 fixture decrypts entirely in memory",()=>{string old=File.ReadAllText(args[3]);True(ArmorMessages.Decrypt(old,key,default).Contains("日本語メッセージ"));});
Test("old Android host fixture decrypts entirely in memory",()=>{string old=File.ReadAllText(args[4]);True(ArmorMessages.Decrypt(old,key,default).Contains("日本語メッセージ"));});
Test("compat signature never accesses path even if path is invalid",()=>{string p="\0not-a-path";string a=ArmorMessages.Encrypt(text,key,p,default);True(ArmorMessages.Decrypt(a,key,p,default)==text);});
Test("wrong password is rejected",()=>Reject(()=>ArmorMessages.Decrypt(armor,"wrong"u8.ToArray(),default)));
Test("authenticated payload tamper rejected",()=>{byte[] b=AsciiArmor.Decode(armor);b[190]^=1;string bad=AsciiArmor.Encode(b);Reject(()=>ArmorMessages.Decrypt(bad,key,default));Sodium.Wipe(b);});
Test("invalid format rejected before native authentication",()=>Reject(()=>ArmorMessages.Decrypt(armor.Replace("Version: 1","Version: 2"),Array.Empty<byte>(),default)));
Test("pre-canceled memory operations rejected",()=>{using var c=new CancellationTokenSource();c.Cancel();Reject(()=>ArmorMessages.Encrypt(text,key,c.Token));Reject(()=>ArmorMessages.Decrypt(armor,key,c.Token));});
Test("cancellation during KDF returns no partial result",()=>{using var c=new CancellationTokenSource();c.CancelAfter(10);Reject(()=>ArmorMessages.Encrypt(text,key,c.Token));});
Test("UTF8 16384-byte boundary compatible with Android",()=>{string t=new string('語',5460)+"😀";True(Limits.Utf8.GetByteCount(t)==16384);string a=ArmorMessages.Encrypt(t,key,default);True(Android.ArmorMessages.Decrypt(a,key,Next("android-boundary"),default)==t);});
Test("oversize input rejected",()=>Reject(()=>ArmorMessages.Encrypt(new string('x',16385),key,default)));
Test("failed operation clears old displayed plain state",()=>{var state=new ArmorMessageState();state.DecryptionSucceeded("previous");state.BeginOperation();Reject(()=>ArmorMessages.Decrypt("bad",key,default));True(state.PlainResult=="");});
Test("copy and paste state preserve full generated armor",()=>{var state=new ArmorMessageState();state.EncryptionSucceeded(armor);ArmorMessageState.ValidatePaste(armor);True(state.CopyGenerated(armor)==armor);state.CipherChanged();True(!state.CanCopy(armor));});
Test("stream leaveOpen and manifest disposal semantics",()=>{byte[] bytes=AsciiArmor.Decode(armor);using var m=new MemoryStream(bytes);var c=Container.Unlock(m,key,default,leaveOpen:true);c.Dispose();True(m.CanRead&&c.Manifest.Body=="");Sodium.Wipe(bytes);});
Test("failed authentication respects stream ownership",()=>{byte[] bytes=AsciiArmor.Decode(armor);using var m=new MemoryStream(bytes);Reject(()=>Container.Unlock(m,"wrong"u8.ToArray(),default,leaveOpen:true));True(m.CanRead);using var n=new MemoryStream(bytes);Reject(()=>Container.Unlock(n,"wrong"u8.ToArray(),default));True(!n.CanRead);Sodium.Wipe(bytes);});
regression=true;
string attachment=Path.Combine(Next("regular"),"添付 日本語=.txt");byte[] contents="dummy attachment 😀"u8.ToArray();File.WriteAllBytes(attachment,contents);
string file=Path.Combine(Path.GetDirectoryName(attachment)!,"regular.ccap");
Test("regular TEXT CCAP with streamed attachment",()=>{Container.GenerateCcap(file,text,BodyKind.TEXT,[attachment],key,false,default);using var c=Container.Unlock(file,key,default,ccapOnly:true);string dest=Path.Combine(Path.GetDirectoryName(attachment)!,"saved.txt");c.Save(0,dest,false,default);True(c.Manifest.Body==text&&SHA256.HashData(File.ReadAllBytes(dest)).SequenceEqual(SHA256.HashData(contents)));});
Test("regular new TEXT CCAP opens in old Windows",()=>{using var c=Old.Container.Unlock(file,key,default,ccapOnly:true);True(c.Manifest.Body==text&&c.Manifest.Attachments.Count==1);});
Test("regular HTML CCAP opens in Android host Core",()=>{string html=Path.Combine(root,"html.ccap");Container.GenerateCcap(html,"<p>表</p><table><tr><td>text</td></tr></table>",BodyKind.HTML,[attachment],key,false,default,allowCopy:false);using var c=Android.Container.Unlock(html,key,default,ccapOnly:true);using var m=new MemoryStream();c.SaveTo(0,m,default);True(c.Manifest.Kind==Android.BodyKind.HTML&&!c.Manifest.AllowCopy&&m.ToArray().SequenceEqual(contents));});
Test("memory stream with attachments uses same writer and reader",()=>{using var m=new MemoryStream();Container.GenerateCcap(m,text,BodyKind.TEXT,[attachment],key,default);using var c=Container.Unlock(m,key,default,leaveOpen:true);string dest=Path.Combine(root,"stream-saved.txt");c.Save(0,dest,false,default);True(File.ReadAllBytes(dest).SequenceEqual(contents));});
Test("self-decrypting EXE authenticated overlay",()=>{using var template=File.OpenRead(args[5]);string output=Path.Combine(root,"sample.exe");Container.Generate(template,output,text,BodyKind.TEXT,[attachment],key,false,default);using var c=Container.Unlock(output,key,default);using var legacy=Old.Container.Unlock(output,key,default);True(c.Manifest.Body==text&&legacy.Manifest.Attachments.Count==1);});
Test("normal file cancellation preserves existing file and no temp",()=>{string f=Path.Combine(Next("cancel-file"),"existing.ccap");File.WriteAllText(f,"user original");using var c=new CancellationTokenSource();c.Cancel();Reject(()=>Container.GenerateCcap(f,text,BodyKind.TEXT,[],key,true,c.Token));True(File.ReadAllText(f)=="user original"&&Directory.GetFiles(Path.GetDirectoryName(f)!).Length==1);});
Test("file overwrite refusal preserves original",()=>{string f=Path.Combine(Next("no-overwrite"),"existing.ccap");File.WriteAllText(f,"original");Reject(()=>Container.GenerateCcap(f,text,BodyKind.TEXT,[],key,false,default));True(File.ReadAllText(f)=="original"&&Directory.GetFiles(Path.GetDirectoryName(f)!).Length==1);});
Test("regular container tamper rejected",()=>{byte[] b=File.ReadAllBytes(file);b[190]^=1;string bad=Path.Combine(root,"tampered.ccap");File.WriteAllBytes(bad,b);Reject(()=>Container.Unlock(bad,key,default,ccapOnly:true));});
Test("SHA256 SHA512 package output",()=>{string f=Path.Combine(root,"checksums.ccap");var r=PackageWriter.GenerateCcap(f,text,BodyKind.TEXT,[],key,true,false,default,checksum256:true);True(r.Sha256==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))&&File.ReadAllText(f+".sha512").Contains(Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(f))),StringComparison.OrdinalIgnoreCase)&&File.ReadAllText(f+".sha256").Contains(r.Sha256,StringComparison.OrdinalIgnoreCase));});
Test("INI compatibility and settings never contain secrets",()=>{string ini=Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");True(!File.Exists(ini));var settings=new Settings{Font=17,Kind=1,Width=1250,Height=1010,PasswordRevealed=true,OutputFormat=OutputFormat.CCAP,LastOutputDirectory=root};try{settings.Save();var loaded=Settings.Load();string raw=File.ReadAllText(ini);True(loaded.Font==17&&loaded.Kind==1&&loaded.PasswordRevealed&&loaded.OutputFormat==OutputFormat.CCAP&&loaded.LastOutputDirectory==root&&!raw.Contains(text)&&!raw.Contains("Armor-Dummy"));}finally{File.Delete(ini);}});
Sodium.Wipe(key);
var report=new{version="1.1.10",passed=newPassed+regPassed,failed,new_passed=newPassed,regression_passed=regPassed,physical_gui_verified=false,production_registry_modified=false,android_runtime_verified=false,tests=results};
File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{newPassed} privacy + {regPassed} regression passed / {failed} failed");return failed==0?0:1;

sealed class FakeStore:ICcapIconStore
{
    public CcapIconValue Current=new(false,false,null,RegistryValueKind.String);public int Reads,Writes,Notifications;public bool FailWrite,IgnoreWrite,Referenced,ChangeOnSecondRead,ChangeDuringReferenceCheck;
    public CcapIconValue Read(){Reads++;if(ChangeOnSecondRead&&Reads==2)Current=new(true,true,"user changed",RegistryValueKind.String);return Current;}
    public void Restore(CcapIconValue value){Writes++;if(FailWrite)throw new IOException("write failure");if(!IgnoreWrite)Current=value;}
    public bool IsIconReferenced(string path){if(ChangeDuringReferenceCheck)Current=new(true,true,"late user change",RegistryValueKind.String);return Referenced;}
}
