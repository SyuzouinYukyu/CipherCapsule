extern alias Old;
extern alias Android;
using CipherCapsule.Core;
using CipherCapsule.Builder;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
var results=new List<object>();int failed=0;
string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);byte[] pw=Limits.Utf8.GetBytes("Public1111-Dummy!2026");
void Assert(bool b){if(!b)throw new Exception("assertion failed");}
void Test(string n,Action action){if(args.Length>2&&!n.Contains(args[2]))return;try{action();results.Add(new{name=n,passed=true});}catch(Exception ex){failed++;results.Add(new{name=n,passed=false,error=ex.ToString()});}}
void Reject(Action a){try{a();}catch(Exception e) when(e is not OutOfMemoryException){return;}throw new Exception("accepted invalid input");}
byte[] Make(string? message,BodyKind kind=BodyKind.TEXT,bool copy=true){using var m=new MemoryStream();Container.GenerateCcap(m,"秘密本文🔑",kind,[],pw,default,allowCopy:copy,publicMessage:message);return m.ToArray();}
uint Version(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8));
string Write(byte[] b,string name="probe.ccap"){var path=Path.Combine(root,name);File.WriteAllBytes(path,b);return path;}
void Open(byte[] b){using var m=new MemoryStream(b);using var c=Container.Unlock(m,pw,default);Assert(c.Manifest.Body=="秘密本文🔑");}
var v1=Make(null);var v2=Make("日本語😀\n行2\t<script>https://example.com</script>");
Test("empty emits v1 and absent manifest property",()=>{Assert(Version(v1)==1);using var m=new MemoryStream(v1);using var c=Container.Unlock(m,pw,default);Assert(!JsonSerializer.Serialize(c.Manifest).Contains("PublicMessageSha256"));});
Test("whitespace emits v1",()=>Assert(Version(Make(" \r\n\t"))==1));
Test("v2 text roundtrip and preread",()=>{Assert(Version(v2)==2);string path=Write(v2);Assert(Container.ReadPublicMessage(path,true)=="日本語😀\n行2\t<script>https://example.com</script>");Open(v2);});
Test("LF normalization",()=>Assert(PublicMessage.Normalize("a\r\nb\rc")=="a\nb\nc"));
Test("4096 boundary",()=>{var b=Make(new string('x',4096));Open(b);Assert(BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(168))==4096);});
Test("UTF8 byte boundary Unicode",()=>{Assert(Limits.Utf8.GetByteCount(PublicMessage.Normalize(new string('あ',1365)+"x"))==4096);Reject(()=>Make(new string('あ',1366)));});
foreach(var value in new[]{new string('a',4097),"a\0b","a\u0001b","a\u007fb","a\ud800","a\u202eb"})Test("invalid input "+results.Count,()=>Reject(()=>Make(value)));
foreach(long n in new[]{-1L,0L,4097L,long.MaxValue,long.MinValue})Test("bad length "+n,()=>{var b=(byte[])v2.Clone();BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(168),n);Reject(()=>Container.ReadPublicMessage(Write(b),true));Reject(()=>Open(b));});
Test("unknown version",()=>{var b=(byte[])v2.Clone();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8),3);Reject(()=>Container.ReadPublicMessage(Write(b)));Reject(()=>Open(b));});
Test("v1 reservation strict",()=>{var b=(byte[])v1.Clone();b[168]=1;Reject(()=>Container.ReadPublicMessage(Write(b)));Reject(()=>Open(b));});
Test("invalid UTF8 preread",()=>{var b=(byte[])v2.Clone();b[176]=255;Reject(()=>Container.ReadPublicMessage(Write(b)));Reject(()=>Open(b));});
Test("control character preread",()=>{var b=(byte[])v2.Clone();b[176]=0;Reject(()=>Container.ReadPublicMessage(Write(b)));});
Test("valid text tamper rejected after authentication",()=>{var b=(byte[])v2.Clone();b[176]=(byte)'x';b[177]=(byte)'x';b[178]=(byte)'x';Assert(Container.ReadPublicMessage(Write(b)).StartsWith("xxx"));Reject(()=>Open(b));});
Test("truncation and offset",()=>{Reject(()=>Container.ReadPublicMessage(Write(v2[..^1])));var b=(byte[])v2.Clone();BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(144),long.MaxValue);Reject(()=>Container.ReadPublicMessage(Write(b)));});
Test("wrong password",()=>{using var m=new MemoryStream(v2);Reject(()=>Container.Unlock(m,Limits.Utf8.GetBytes("wrong"),default));});
// Re-encrypt an intentionally malformed manifest with the correct key, to test authenticated schema/hash checks.
foreach(string? hash in new string?[]{null,"bad",new string('0',64)})Test("authenticated manifest hash "+(hash??"missing"),()=>{
var b=(byte[])v2.Clone();var h=b[..176];long mo=Container.I64(h,144),ml=Container.I64(h,152);var kek=Sodium.PasswordKey(pw,h[32..48]);var dek=Sodium.Decrypt(h[96..144],h[..96],h[48..72],kek);var key=Sodium.Derive(dek,0,"CCMANIF1");var plain=Sodium.Decrypt(b.AsSpan((int)mo,(int)ml).ToArray(),h,h[72..96],key);var manifest=JsonSerializer.Deserialize<Manifest>(plain)!;manifest.PublicMessageSha256=hash;var updated=JsonSerializer.SerializeToUtf8Bytes(manifest);BinaryPrimitives.WriteInt64LittleEndian(h.AsSpan(152),updated.Length+16);var cipher=Sodium.Encrypt(updated,h,h[72..96],key);using var stream=new MemoryStream();stream.Write(h);stream.Write(b,176,(int)mo-176);stream.Write(cipher);var footer=b[^64..];BinaryPrimitives.WriteInt64LittleEndian(footer.AsSpan(24),stream.Length);SHA256.HashData(footer.AsSpan(0,32)).CopyTo(footer,32);stream.Write(footer);Reject(()=>Open(stream.ToArray()));Sodium.Wipe(kek);Sodium.Wipe(dek);Sodium.Wipe(key);Sodium.Wipe(plain);});
Test("old Windows and Android accept new v1",()=>{string path=Write(v1);using var old=Old::CipherCapsule.Core.Container.Unlock(path,pw,default,ccapOnly:true);using var android=Android::CipherCapsule.Core.Container.Unlock(path,pw,default);Assert(old.Manifest.Body=="秘密本文🔑"&&android.Manifest.Body=="秘密本文🔑");});
Test("old Windows and Android reject v2",()=>{string path=Write(v2);Reject(()=>Old::CipherCapsule.Core.Container.Unlock(path,pw,default,ccapOnly:true));Reject(()=>Android::CipherCapsule.Core.Container.Unlock(path,pw,default));});
Test("legacy v1 generated by old Windows reads",()=>{string path=Path.Combine(root,"old.ccap");Old::CipherCapsule.Core.Container.GenerateCcap(path,"旧版",Old::CipherCapsule.Core.BodyKind.TEXT,[],pw,true,default);using var c=Container.Unlock(path,pw,default);Assert(c.Manifest.Body=="旧版");});
Test("Armor stays v1, Android bidirectional",()=>{string armor=ArmorMessages.Encrypt("日本語😀",pw,default);Assert(Version(AsciiArmor.Decode(armor))==1);Assert(Android::CipherCapsule.Core.ArmorMessages.Decrypt(armor,pw,root,default)=="日本語😀");var a=Android::CipherCapsule.Core.ArmorMessages.Encrypt("Android",pw,root,default);Assert(ArmorMessages.Decrypt(a,pw,default)=="Android");});
Test("HTML attachment encrypted and saved with v2",()=>{string file=Path.Combine(root,"秘密添付.txt"),outp=Path.Combine(root,"html.ccap"),save=Path.Combine(root,"saved.txt");File.WriteAllText(file,"添付秘密");PackageWriter.GenerateCcap(outp,"<p>secret</p>",BodyKind.HTML,[file],pw,true,true,default,allowCopy:false,checksum256:true,publicMessage:"お知らせ");using var c=Container.Unlock(outp,pw,default);Assert(!c.Manifest.AllowCopy&&c.Manifest.Kind==BodyKind.HTML);c.Save(0,save,true,default);Assert(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(save)));Assert(File.Exists(outp+".sha512")&&File.Exists(outp+".sha256"));Assert(File.ReadAllBytes(outp).AsSpan().IndexOf(Limits.Utf8.GetBytes("秘密添付"))<0);});
Test("EXE overlay v2 and preread",()=>{string path=Path.Combine(root,"message.exe");using var template=File.OpenRead(args[1]);Container.Generate(template,path,"秘密本文🔑",BodyKind.TEXT,[],pw,true,default,publicMessage:"EXE通知");Assert(Container.ReadPublicMessage(path)=="EXE通知");using var c=Container.Unlock(path,pw,default,ccapOnly:false);Assert(c.PublicMessageText=="EXE通知");});
Test("cancel no destination",()=>{string path=Path.Combine(root,"cancel.ccap");using var ct=new CancellationTokenSource();ct.Cancel();Reject(()=>Container.GenerateCcap(path,"body",BodyKind.TEXT,[],pw,true,ct.Token,publicMessage:"test"));Assert(!File.Exists(path));});
string ini=Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");Assert(!File.Exists(ini));
Test("INI defaults",()=>{var c=Settings.Load();Assert(c.OutputFormat==OutputFormat.EXE&&c.AllowCopy&&!c.ChecksumSHA512&&!c.ChecksumSHA256);});
for(int bits=0;bits<16;bits++){int b=bits;Test("INI independent roundtrip "+b,()=>{var c=new Settings{OutputFormat=(b&1)==0?OutputFormat.EXE:OutputFormat.CCAP,AllowCopy=(b&2)!=0,ChecksumSHA512=(b&4)!=0,ChecksumSHA256=(b&8)!=0,Font=17,Kind=1,PasswordRevealed=true};c.Save();var d=Settings.Load();Assert(c.OutputFormat==d.OutputFormat&&c.AllowCopy==d.AllowCopy&&c.ChecksumSHA512==d.ChecksumSHA512&&c.ChecksumSHA256==d.ChecksumSHA256&&d.Font==17&&d.Kind==1&&d.PasswordRevealed);});}
Test("INI old and invalid field isolation",()=>{File.WriteAllText(ini,"X=2\nY=3\nWidth=1200\nHeight=980\nFont=12\nKind=1\nOutputFormat=CCAP\nAllowCopy=bad\nChecksumSHA512=1\nChecksumSHA256=bad\n");var c=Settings.Load();Assert(c.OutputFormat==OutputFormat.CCAP&&c.AllowCopy&&c.ChecksumSHA512&&!c.ChecksumSHA256&&c.Kind==1&&c.X==2);c.Save();Assert(!File.ReadAllText(ini).Contains("秘密"));});File.Delete(ini);
File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{passed=results.Count-failed,failed,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Passed {results.Count-failed}; failed {failed}");return failed==0?0:1;
