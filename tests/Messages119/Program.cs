extern alias Android103;
using CipherCapsule.Core;
using A=Android103::CipherCapsule.Core;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
var tests=new List<object>();int failed=0,regression=0;bool reg=false;
void Check(string name,Action action){try{action();tests.Add(new{name,passed=true,category=reg?"regression":"armor"});if(reg)regression++;}catch(Exception e){failed++;tests.Add(new{name,passed=false,category=reg?"regression":"armor",error=e.GetType().Name});Console.WriteLine("FAILED: "+name+" "+e.GetType().Name);}}
void True(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject(Action action){try{action();}catch(Exception e)when(e is ArgumentException or InvalidDataException or CryptographicException or OperationCanceledException or IOException){return;}throw new Exception("Expected rejection");}
string temporary=Path.Combine(root,"temporary");
byte[] key=Limits.Utf8.GetBytes("Armor-Dummy!2026-日本語");
byte[] Fake(int size=256)
{
    byte[] b=new byte[size];"CCAPDAT1"u8.CopyTo(b);void U(int i,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i),v);void L(int i,long v)=>BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(i),v);
    U(8,1);U(12,1);L(16,3);L(24,256L*1024*1024);L(144,176);L(152,size-240);U(164,Limits.Chunk);
    int f=size-64;"CCAPEND1"u8.CopyTo(b.AsSpan(f));U(f+8,1);L(f+24,size-64);SHA256.HashData(b.AsSpan(f,32)).CopyTo(b,f+32);return b;
}
string Raw(byte[] b)=>AsciiArmor.Begin+"\nVersion: 1\n\n"+string.Join("\n",Convert.ToBase64String(b).Chunk(64).Select(c=>new string(c)))+"\n\n"+AsciiArmor.End;
byte[] fake=Fake();string sample=Raw(fake);
foreach(var os in new[]{"Windows","Android"})
{
    byte[] Decode(string s)=>os=="Windows"?AsciiArmor.Decode(s):A.AsciiArmor.Decode(s);
    string Encode(byte[] b)=>os=="Windows"?AsciiArmor.Encode(b):A.AsciiArmor.Encode(b);
    void Text(string s){if(os=="Windows")AsciiArmor.ValidateText(s);else A.AsciiArmor.ValidateText(s);}
    Check(os+" exact format vector",()=>True(Encode(fake)==sample&&Decode(sample).SequenceEqual(fake)));
    Check(os+" LF and 64-column wrapping",()=>{string s=Encode(fake);True(!s.Contains('\r'));var rows=s.Split('\n');True(rows[2]==""&&rows[^2]==""&&rows.Skip(3).Take(rows.Length-6).All(x=>x.Length==64));});
    Check(os+" CRLF and surrounding ASCII whitespace",()=>True(Decode(" \t\r\n"+sample.Replace("\n","\r\n")+"\r\n\t ").SequenceEqual(fake)));
    foreach(var (name,mutate) in new (string,Func<string,string>)[]{
        ("BEGIN case",s=>s.Replace("BEGIN CIPHERCAPSULE","Begin CIPHERCAPSULE")),("END case",s=>s.Replace("END CIPHERCAPSULE","End CIPHERCAPSULE")),
        ("unknown version",s=>s.Replace("Version: 1","Version: 2")),("duplicate version",s=>s.Replace("Version: 1","Version: 1\nVersion: 1")),
        ("unknown field",s=>s.Replace("Version: 1","Version: 1\nX-Field: value")),("missing separator",s=>s.Replace("Version: 1\n\n","Version: 1\n")),
        ("missing footer separator",s=>s.Replace("\n\n"+AsciiArmor.End,"\n"+AsciiArmor.End)),("truncated",s=>s[..^8]),
        ("duplicate block",s=>s+"\n"+s),("leading prose",s=>"message\n"+s),("trailing prose",s=>s+"\nmessage"),
        ("internal space",s=>s.Replace("Q0NB","Q0 N")),("invalid alphabet",s=>s.Replace("Q0NB","Q0?B")),
        ("bare CR",s=>s.Replace("\n","\r")),("Unicode outside whitespace",s=>"\u00a0"+s),
        ("base64 empty line",s=>s.Replace("Version: 1\n\n","Version: 1\n\n\n")),
        ("padding missing",s=>s.Replace("==\n\n","\n\n")),("padding excessive",s=>s.Replace("==\n\n","===\n\n")),
        ("noncanonical padding bits",s=>{int at=s.LastIndexOf("==\n\n",StringComparison.Ordinal);const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";return s[..(at-1)]+alphabet[alphabet.IndexOf(s[at-1])+1]+s[at..];}),
        ("short nonfinal base64 line",s=>s.Insert(s.IndexOf("Q0NB",StringComparison.Ordinal)+4,"\n"))})
        Check(os+" reject "+name,()=>Reject(()=>Decode(mutate(sample))));
    Check(os+" armor input bound before parsing",()=>Reject(()=>Decode(new string('A',AsciiArmor.MaxArmorLength+1))));
    Check(os+" binary bound",()=>Reject(()=>Encode(Fake(AsciiArmor.MaxBinaryBytes+1))));
    Check(os+" wrapper overhead bound",()=>Reject(()=>Encode(Fake(AsciiArmor.MaxBinaryBytes))));
    Check(os+" invalid footer hash",()=>{byte[] b=(byte[])fake.Clone();b[^1]^=1;Reject(()=>Decode(Raw(b)));});
    Check(os+" footer boundary",()=>{byte[] b=(byte[])fake.Clone();b[^40]^=1;Reject(()=>Decode(Raw(b)));});
    Check(os+" invalid KDF before authentication",()=>{byte[] b=(byte[])fake.Clone();b[16]=4;Reject(()=>Decode(Raw(b)));});
    Check(os+" count zero preflight",()=>{byte[] b=(byte[])fake.Clone();b[160]=1;Reject(()=>Decode(Raw(b)));});
    Check(os+" manifest offset bounds",()=>{byte[] b=(byte[])fake.Clone();b[144]=255;Reject(()=>Decode(Raw(b)));});
    Check(os+" plaintext empty",()=>Reject(()=>Text("")));
    Check(os+" plaintext 16384 bytes",()=>Text(new string('a',16384)));
    Check(os+" plaintext 16385 bytes",()=>Reject(()=>Text(new string('a',16385))));
    Check(os+" Japanese byte limit",()=>Reject(()=>Text(new string('語',5462))));
    Check(os+" unpaired surrogate",()=>Reject(()=>Text("\ud800")));
    Check(os+" Unicode preserved",()=>Text("日本語😀e\u0301\r\n記号 ! = + /\n"));
}
var state=new ArmorMessageState();var ast=new A.ArmorMessageState();
Check("copy unavailable initially",()=>True(!state.CanCopy(sample)&&!ast.CanCopy(sample)));
Check("copy returns full generated block",()=>{state.EncryptionSucceeded(sample);ast.EncryptionSucceeded(sample);True(state.CopyGenerated(sample)==sample&&ast.CopyGenerated(sample)==sample);});
Check("edited incoming cipher cannot be copied as generated",()=>{state.CipherChanged();ast.CipherChanged();True(!state.CanCopy(sample)&&!ast.CanCopy(sample));});
Check("new operation removes prior plaintext",()=>{state.DecryptionSucceeded("old");ast.DecryptionSucceeded("old");state.BeginOperation();ast.BeginOperation();True(state.PlainResult==""&&ast.PlainResult=="");});
Check("cipher edits remove prior plaintext",()=>{state.DecryptionSucceeded("old");ast.DecryptionSucceeded("old");state.CipherChanged();ast.CipherChanged();True(state.PlainResult==""&&ast.PlainResult=="");});
Check("paste validation rejects nonmessage",()=>{Reject(()=>ArmorMessageState.ValidatePaste("not armor"));Reject(()=>A.ArmorMessageState.ValidatePaste("not armor"));});
string combined="ASCII English !@#$%^&*() = + /\\ \"'\n日本語メッセージ\r\n😀 e\u0301 é\n末尾\n";
string win="",android="";
Check("Windows encrypt and self decrypt exact Unicode",()=>{win=ArmorMessages.Encrypt(combined,key,temporary,default);True(ArmorMessages.Decrypt(win,key,temporary,default)==combined);});
Check("Windows -> Android host core",()=>True(A.ArmorMessages.Decrypt(win,key,temporary,default)==combined));
Check("Android encrypt and self decrypt exact Unicode",()=>{android=A.ArmorMessages.Encrypt(combined,key,temporary,default);True(A.ArmorMessages.Decrypt(android,key,temporary,default)==combined);});
Check("Android -> Windows core",()=>True(ArmorMessages.Decrypt(android,key,temporary,default)==combined));
Check("randomized same text and password",()=>True(win!=android));
Check("16384 UTF8 boundary authenticated roundtrip",()=>{string text=new string('語',5460)+"😀";True(Limits.Utf8.GetByteCount(text)==16384);string s=ArmorMessages.Encrypt(text,key,temporary,default);True(A.ArmorMessages.Decrypt(s,key,temporary,default)==text);});
Check("Windows wrong password denied",()=>Reject(()=>ArmorMessages.Decrypt(android,"wrong"u8.ToArray(),temporary,default)));
Check("Android wrong password denied",()=>Reject(()=>A.ArmorMessages.Decrypt(win,"wrong"u8.ToArray(),temporary,default)));
Check("authenticated cipher tamper rejected both",()=>{var b=AsciiArmor.Decode(win);b[190]^=1;string bad=Raw(b);Reject(()=>ArmorMessages.Decrypt(bad,key,temporary,default));Reject(()=>A.ArmorMessages.Decrypt(bad,key,temporary,default));});
Check("public invalid container never enters native auth",()=>{string bad=sample.Replace("Version: 1","Version: 9");Reject(()=>ArmorMessages.Decrypt(bad,Array.Empty<byte>(),temporary,default));True(!Directory.EnumerateFileSystemEntries(temporary).Any());});
Check("HTML authenticated manifest rejected both",()=>{string p=Path.Combine(root,"html.ccap");Container.GenerateCcap(p,"<p>HTML</p>",BodyKind.HTML,[],key,false,default);string s=Raw(File.ReadAllBytes(p));Reject(()=>ArmorMessages.Decrypt(s,key,temporary,default));Reject(()=>A.ArmorMessages.Decrypt(s,key,temporary,default));});
Check("authenticated oversize text rejected both",()=>{string p=Path.Combine(root,"oversize.ccap");Container.GenerateCcap(p,new string('x',16385),BodyKind.TEXT,[],key,false,default);string s=Raw(File.ReadAllBytes(p));Reject(()=>ArmorMessages.Decrypt(s,key,temporary,default));Reject(()=>A.ArmorMessages.Decrypt(s,key,temporary,default));});
Check("authenticated empty body rejected both",()=>{string p=Path.Combine(root,"empty.ccap");Container.GenerateCcap(p,"",BodyKind.TEXT,[],key,false,default);string s=Raw(File.ReadAllBytes(p));Reject(()=>ArmorMessages.Decrypt(s,key,temporary,default));Reject(()=>A.ArmorMessages.Decrypt(s,key,temporary,default));});
Check("pre-canceled encryption both",()=>{using var c=new CancellationTokenSource();c.Cancel();Reject(()=>ArmorMessages.Encrypt(combined,key,temporary,c.Token));Reject(()=>A.ArmorMessages.Encrypt(combined,key,temporary,c.Token));});
Check("canceled encryption cleans encrypted temporaries",()=>{using var c=new CancellationTokenSource();c.CancelAfter(10);Reject(()=>ArmorMessages.Encrypt(combined,key,temporary,c.Token));True(!Directory.EnumerateFileSystemEntries(temporary).Any());});
Check("all success/failure decrypt temp files cleaned",()=>True(!Directory.EnumerateFileSystemEntries(temporary).Any()));
// Demonstrate a validly authenticated nonempty attachment manifest even when a
// sender forges a zero-count public header: the existing Core must reject it.
Check("authenticated manifest cannot bypass attachment restriction",()=>
{
    byte[] b=AsciiArmor.Decode(win),header=b[..176];byte[] kek=Sodium.PasswordKey(key,header[32..48]),dek=Sodium.Decrypt(header[96..144],header[..96],header[48..72],kek),mk=Sodium.Derive(dek,0,"CCMANIF1");
    try
    {
        var m=new Manifest{Body="text",Attachments=[new AttachmentInfo{Name="x.txt",Size=0,Offset=176,CipherLength=45,Sha256=new string('0',64)}]};byte[] p=JsonSerializer.SerializeToUtf8Bytes(m);BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(152),p.Length+16);
        byte[] encrypted=Sodium.Encrypt(p,header,header[72..96],mk);byte[] forged=Fake(176+encrypted.Length+64);header.CopyTo(forged,0);encrypted.CopyTo(forged,176);string s=Raw(forged);
        Reject(()=>ArmorMessages.Decrypt(s,key,temporary,default));Reject(()=>A.ArmorMessages.Decrypt(s,key,temporary,default));Sodium.Wipe(p);
    }finally{Sodium.Wipe(kek);Sodium.Wipe(dek);Sodium.Wipe(mk);}
});
reg=true;
string attachment=Path.Combine(root,"資料 日本語=.txt");byte[] content="dummy attachment 😀"u8.ToArray();File.WriteAllBytes(attachment,content);
string regular=Path.Combine(root,"regular.ccap");
Check("ordinary Windows TEXT CCAP with attachment -> Android",()=>{Container.GenerateCcap(regular,combined,BodyKind.TEXT,[attachment],key,false,default);using var c=A.Container.Unlock(regular,key,default,ccapOnly:true);using var m=new MemoryStream();c.SaveTo(0,m,default);True(c.Manifest.Body==combined&&m.ToArray().SequenceEqual(content));});
Check("ordinary Android HTML stream writer -> Windows attachment save",()=>{string p=Path.Combine(root,"android-html.ccap");A.Container.GenerateStreams(p,"<p>表</p>",A.BodyKind.HTML,[new A.AttachmentInput(Path.GetFileName(attachment),content.Length,()=>new MemoryStream(content))],key,true,default);using var c=Container.Unlock(p,key,default,ccapOnly:true);string saved=Path.Combine(root,"saved.txt");c.Save(0,saved,false,default);True(c.Manifest.Kind==BodyKind.HTML&&File.ReadAllBytes(saved).SequenceEqual(content));});
Check("ordinary attachment armor rejected before auth",()=>{string s=Raw(File.ReadAllBytes(regular));Reject(()=>AsciiArmor.Decode(s));Reject(()=>A.AsciiArmor.Decode(s));});
Check("ordinary container wrong password",()=>Reject(()=>Container.Unlock(regular,"wrong"u8.ToArray(),default,ccapOnly:true)));
Check("ordinary container tamper",()=>{var b=File.ReadAllBytes(regular);b[190]^=1;string p=Path.Combine(root,"tamper.ccap");File.WriteAllBytes(p,b);Reject(()=>A.Container.Unlock(p,key,default,ccapOnly:true));});
Check("Windows EXE creation and authenticated overlay",()=>{using var template=File.OpenRead(args[1]);string path=Path.Combine(root,"generated-test.exe");Container.Generate(template,path,combined,BodyKind.TEXT,[attachment],key,false,default);using var c=Container.Unlock(path,key,default);True(c.Manifest.Body==combined&&c.Manifest.Attachments.Count==1);});
Sodium.Wipe(key);
File.WriteAllText(Path.Combine(root,"windows-message.txt"),win,new UTF8Encoding(false));File.WriteAllText(Path.Combine(root,"android-host-message.txt"),android,new UTF8Encoding(false));
var report=new{passed=tests.Count-failed,failed,new_passed=tests.Count-failed-regression,regression_passed=regression,execution="Windows host, actual Windows v1.1.9 and portable Android v1.0.3 cores; NOT Android runtime or real clipboard/GUI",android_runtime_verified=false,physical_gui_verified=false,tests};
File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{tests.Count-failed} passed / {failed} failed; {regression} regression");return failed==0?0:1;
