using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers.Binary;
using CipherCapsule.Core;
using CipherCapsule.UI;
using CipherCapsule.Viewer;
using Microsoft.Web.WebView2.WinForms;

internal static partial class Program
{
    static readonly string Root=FindRoot();
    static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(Directory.Exists(Path.Combine(d.FullName,"Source")))return Path.Combine(d.FullName,"Source");d=d.Parent;}throw new Exception("Source folder not found");}
    static string Work="", Template="", ResultFolder=""; static readonly List<object> Results=[]; static int passed,failed;
    static readonly string Password="  日本語🔑 Capsule! 2026  ";
    static readonly byte[] Pw=Limits.Utf8.GetBytes(Password);
    [STAThread] static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("--verify-gui113"))return VerifyGui113();
        if(args.Contains("--inspect-gui113"))return InspectGui113();
        if(args.Contains("--prepare-viewer-folder-gui113"))return PrepareViewerFolderGui113();
        if(args.Contains("--exit-trace"))return TraceUnexpectedExit();
        Work=Path.GetFullPath(Path.Combine(Root,"../Work/selftests")); Directory.CreateDirectory(Work); Template=Path.Combine(Root,"templates","CipherCapsule_Viewer.exe");
        string? runRoot=args.FirstOrDefault(x=>x.StartsWith("--run-root="))?[11..];
        if(runRoot!=null){string path=Path.GetFullPath(runRoot),boundary=Path.GetFullPath(Path.Combine(Root,"../Work"))+Path.DirectorySeparatorChar;if(!path.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Test run must be inside this version's Work folder");Work=Path.Combine(path,"selftests");ResultFolder=Path.Combine(path,"results");Directory.CreateDirectory(Work);}
        if(args.Contains("--zoom-before")) { RunZoomBefore(); return 0; }
        if(args.Contains("--v113")) { RunV113(); Finish("v113-results.json"); return failed==0?0:1; }
        if(args.Contains("--ccap114")) { RunCcap114(); Finish("ccap114-results.json"); return failed==0?0:1; }
        if(args.Contains("--ccap-compat-recheck114")) { RunCcapCompatRecheck114(); Finish("ccap-compat-recheck114-results.json"); return failed==0?0:1; }
        if(args.Contains("--ccap-gui114")) { RunCcapGui114(); Finish("ccap-gui114-results.json"); return failed==0?0:1; }
        if(args.Contains("--viewer-folder113")) { RunViewerFolder113(); Finish("viewer-folder113-results.json"); return failed==0?0:1; }
        if(args.Contains("--viewer-folder-dpi113")) { RunViewerFolderDpi113(); Finish("viewer-folder-dpi113-results.json"); return failed==0?0:1; }
        if(args.Contains("--folder113")) { RunFolder113(); Finish("folder113-results.json"); return failed==0?0:1; }
        if(args.Contains("--v112")) { RunV112(); Finish("v112-results.json"); return failed==0?0:1; }
        if(args.Contains("--layout")) { RunLayout(); Finish("layout-results.json"); return failed==0?0:1; }
        if(args.Contains("--html")) { RunHtml(); Finish("html-results.json"); return failed==0?0:1; }
        if(args.Contains("--editor")) { RunEditor(); Finish("editor-results.json"); return failed==0?0:1; }
        if(args.Contains("--extra")) { ExtraTests(); Finish("extra-results.json"); return failed==0?0:1; }
        if(args.Contains("--lifecycle")) { RunLifecycle(); Finish("lifecycle-results.json"); return failed==0?0:1; }
        Console.WriteLine("libsodium "+Sodium.Version);
        var source=Path.Combine(Work,"元添付.txt"); File.WriteAllText(source,"ダミー添付\r\n添付の内容🔒",Limits.Utf8); var zero=Path.Combine(Work,"empty.bin");File.WriteAllBytes(zero,[]);
        var sub=Path.Combine(Work,"別");Directory.CreateDirectory(sub);var same=Path.Combine(sub,"元添付.txt");File.WriteAllText(same,"同名の異なる添付",Limits.Utf8);
        string original=Hash(source),sameOriginal=Hash(same),body="日本語の秘密本文🔒\r\n2行目\nUnicode e\u0301 記号 <&>";
        string capsule=Path.Combine(Work,"TEXT検証.exe"),html=Path.Combine(Work,"HTML検証.exe"),none=Path.Combine(Work,"添付なし.exe");
        Test("password empty rejected",()=>Reject(()=>Limits.Password("")));
        Test("password 1 and 13 accepted",()=>{Limits.Password("鍵");Limits.Password(new string('a',13));});
        Test("password 14 accepted",()=>Limits.Password(new string('a',14)));
        Test("password 256 Unicode scalars accepted",()=>Limits.Password(string.Concat(Enumerable.Repeat("🔑",256))));
        Test("password 257 rejected",()=>Reject(()=>Limits.Password(new string('a',257))));
        Test("TEXT generate with duplicate names and empty file",()=>Gen(capsule,body,BodyKind.TEXT,[source,same,zero]));
        Test("correct password TEXT Unicode newline exact",()=>{using var c=Unlock(capsule);Check(c.Manifest.Body==body&&c.Manifest.Kind==BodyKind.TEXT);Check(c.Manifest.Attachments.Count==3);});
        Test("HTML generate",()=>Gen(html,"<h1>日本語 HTML🔐</h1><p style='color:blue'>静的表示</p><table><tr><td>表</td></tr></table><script>fetch('http://127.0.0.1:43821/leak')</script>",BodyKind.HTML,[source]));
        Test("HTML decrypt exact",()=>{using var c=Unlock(html);Check(c.Manifest.Kind==BodyKind.HTML&&c.Manifest.Body.Contains("日本語 HTML"));});
        Test("no attachment generate/decrypt",()=>{Gen(none,"添付なし",BodyKind.TEXT,[]);using var c=Unlock(none);Check(c.Manifest.Attachments.Count==0);});
        Test("wrong password rejected",()=>Reject(()=>{using var c=Container.Unlock(capsule,Limits.Utf8.GetBytes("Wrong password 123"),default);}));
        Test("trimmed password rejected",()=>Reject(()=>{using var c=Container.Unlock(capsule,Limits.Utf8.GetBytes(Password.Trim()),default);}));
        Test("empty unlock rejected",()=>Reject(()=>{using var c=Container.Unlock(capsule,[],default);}));
        Test("attachment save matches original SHA256",()=>{using var c=Unlock(capsule);var d=Path.Combine(Work,"saved.txt");c.Save(0,d,true,default);Check(Hash(d)==original);});
        Test("duplicate name independent save",()=>{using var c=Unlock(capsule);var d=Path.Combine(Work,"saved2.txt");c.Save(1,d,true,default);Check(Hash(d)==sameOriginal);});
        Test("zero attachment FINAL tag/save",()=>{using var c=Unlock(capsule);var d=Path.Combine(Work,"saved-empty.bin");c.Save(2,d,true,default);Check(new FileInfo(d).Length==0);});
        Test("save cancelled preserves existing",()=>{using var c=Unlock(capsule);var d=Path.Combine(Work,"preserve.txt");File.WriteAllText(d,"KEEP");using var t=new CancellationTokenSource();t.Cancel();Reject(()=>c.Save(0,d,true,t.Token));Check(File.ReadAllText(d)=="KEEP");});
        Test("save no overwrite preserves existing",()=>{using var c=Unlock(capsule);var d=Path.Combine(Work,"preserve2.txt");File.WriteAllText(d,"KEEP");Reject(()=>c.Save(0,d,false,default));Check(File.ReadAllText(d)=="KEEP");});
        Test("generation cancelled preserves existing",()=>{var d=Path.Combine(Work,"preserve.exe");File.WriteAllText(d,"KEEP");using var t=new CancellationTokenSource();t.Cancel();using var template=File.OpenRead(Template);Reject(()=>Container.Generate(template,d,body,BodyKind.TEXT,[source],Pw,true,t.Token));Check(File.ReadAllText(d)=="KEEP");});
        Test("generation missing input preserves existing",()=>{var d=Path.Combine(Work,"preserve.exe");Reject(()=>Gen(d,body,BodyKind.TEXT,[source+"missing"]));Check(File.ReadAllText(d)=="KEEP");});
        Test("output cannot replace attachment source",()=>{Reject(()=>Gen(source,body,BodyKind.TEXT,[source]));Check(Hash(source)==original);});
        Test("random salt and nonces independent",()=>{var d=Path.Combine(Work,"random.exe");Gen(d,body,BodyKind.TEXT,[source,same,zero]);var a=Header(capsule);var b=Header(d);foreach(var r in new[]{(32,16),(48,24),(72,24),(96,48)})Check(!a.AsSpan(r.Item1,r.Item2).SequenceEqual(b.AsSpan(r.Item1,r.Item2)));});
        foreach(var mutation in new[]{("header bit",12), ("salt bit",32),("nonce bit",48),("wrapped DEK bit",96),("manifest nonce bit",72),("KDF excessive",24),("unknown format",8),("bad chunk size",164),("reserved field",168)})
            Test(mutation.Item1+" rejected",()=>Mutate(capsule,mutation.Item1,(f,loc)=>{f.Position=loc.Offset+mutation.Item2;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        Test("manifest 1bit rejected",()=>Mutate(capsule,"manifest",(f,l)=>{var h=Header(capsule);f.Position=l.Offset+Container.I64(h,144)+5;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        Test("attachment 1bit rejected before body",()=>Mutate(capsule,"attachment",(f,l)=>{f.Position=l.Offset+Container.HeaderSize+29;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        Test("container truncated rejected",()=>Mutate(capsule,"truncate",(f,l)=>f.SetLength(f.Length-1)));
        Test("footer offset overflow rejected",()=>Mutate(capsule,"overflow",(f,l)=>{var b=new byte[64];f.Position=f.Length-64;f.ReadExactly(b);BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(16),long.MaxValue);SHA256.HashData(b.AsSpan(0,32)).CopyTo(b,32);f.Position=f.Length-64;f.Write(b);}));
        Test("manifest length overflow rejected",()=>Mutate(capsule,"badlength",(f,l)=>{f.Position=l.Offset+152;f.Write(BitConverter.GetBytes(long.MaxValue));}));
        Test("synthetic PE certificate table locator only",()=>{var d=Path.Combine(Work,"synthetic-certificate.exe");File.Copy(capsule,d,true);using(var f=new FileStream(d,FileMode.Open,FileAccess.ReadWrite)){long end=f.Length;int pad=(int)((8-end%8)%8);f.Position=end;f.Write(new byte[pad]);long cert=f.Position;f.Write(BitConverter.GetBytes(8u));f.Write(BitConverter.GetBytes((ushort)0x200));f.Write(BitConverter.GetBytes((ushort)2));f.Position=60;var b=new byte[4];f.ReadExactly(b);f.Position=BitConverter.ToUInt32(b)+24+112+32;f.Write(BitConverter.GetBytes((uint)cert));f.Write(BitConverter.GetBytes(8u));}using var c=Unlock(d);Check(c.Manifest.Body==body);});
        Test("authenticated malformed attachment metadata rejected",()=>{var d=Repack(capsule,"bad-metadata",(m,f,s)=>m.Attachments[0].Offset=long.MaxValue);Reject(()=>{using var c=Unlock(d);});});
        Test("FINAL tag missing prevents commit despite trusted cipher hash",()=>{
            var d=Repack(capsule,"missing-final",(m,f,s)=>{
                var a=m.Attachments[2];byte[] h=Header(capsule),kek=Sodium.PasswordKey(Pw,h[32..48]),dek=Sodium.Decrypt(h[96..144],h[..96],h[48..72],kek),key=Sodium.Derive(dek,3,"CCATTCH1"),state=Sodium.NewState();
                try {var sh=Sodium.InitPush(state,key);var outb=new byte[17];Sodium.Push(state,outb,[],0,false);f.Position=s+a.Offset;f.Write(sh);f.Write(BitConverter.GetBytes(17));f.Write(outb);f.Position=s+a.Offset;var raw=Container.Read(f,(int)a.CipherLength);a.Sha256=Convert.ToHexString(SHA256.HashData(raw));}
                finally{Sodium.Wipe(kek);Sodium.Wipe(dek);Sodium.Wipe(key);Sodium.Wipe(state);}
            });using var c=Unlock(d);var dest=Path.Combine(Work,"final-preserve.bin");File.WriteAllText(dest,"KEEP");Reject(()=>c.Save(2,dest,true,default));Check(File.ReadAllText(dest)=="KEEP");
        });
        Test("source inputs unchanged",()=>Check(Hash(source)==original&&Hash(same)==sameOriginal));
        foreach(var name in new[]{"../a","..\\a","C:\\a", "a:b","CON.txt","COM1","LPT².txt","a.","a ","NUL","conin$","a\0b"})Test("unsafe filename "+name.Replace('\0','?'),()=>Reject(()=>Limits.FileName(name)));
        Test("safe Japanese filename",()=>Limits.FileName("添付 資料🔑.txt"));
        Test("8MiB exact body boundary",()=>Limits.BodyText(new string('a',Limits.Body)));
        Test("8MiB exceeded rejected",()=>Reject(()=>Limits.BodyText(new string('a',Limits.Body+1))));
        Test("101 attachments rejected",()=>Reject(()=>Limits.Inputs(Enumerable.Repeat(source,101).ToArray())));
        Test("folder input rejected",()=>Reject(()=>Limits.Inputs([sub])));
        Test("HTML allowlist and CSS url stripped",()=>{string s=SafeHtml.Sanitize("<h1>保持</h1><script>alert(1)</script><svg><script>alert(2)</script></svg><form><input></form><iframe src='http://bad'></iframe><p style='color:red;background:url(http://bad)' onclick='evil()'>本文</p><img src='http://bad'><a href='file:///c:/'>link</a>");Check(s.Contains("保持")&&s.Contains("color:red")&&!s.Contains("alert(")&&!s.Contains("onclick")&&!s.Contains("http://bad")&&!s.Contains("file:///")&&!s.Contains("<form")&&!s.Contains("<iframe"));});
        Test("HTML invalid data:image refused",()=>Check(!SafeHtml.Sanitize("<img src='data:image/png;base64,aGVsbG8='>").Contains("<img")));
        Test("HTML SVG data:image refused",()=>Check(!SafeHtml.Sanitize("<img src='data:image/svg+xml;base64,PHN2Zz4='>").Contains("<img")));
        Test("HTML valid tiny PNG allowed",()=>{using var b=new Bitmap(2,2);using var ms=new MemoryStream();b.Save(ms,System.Drawing.Imaging.ImageFormat.Png);Check(SafeHtml.Sanitize("<img src='data:image/png;base64,"+Convert.ToBase64String(ms.ToArray())+"'>").Contains("<img"));});
        Test("password panel masked two rows, mismatch and toggle",()=>{using var p=new PasswordPanel();Check(!p.Revealed&&!p.CanRun);p.Primary.Text=Password;p.Confirm.Text="wrong";Reject(()=>p.Take(true));p.Confirm.Text=Password;Check(p.CanRun);p.Toggle();Check(p.Revealed&&p.CanRun&&p.Confirm.Text==""&&p.Primary.Text==Password);p.Toggle();Check(!p.Revealed&&!p.CanRun&&p.Primary.Text==Password);});
        Test("font sizes 9 12 17 clamp",()=>{using var f=new AppForm();foreach(int s in new[]{9,12,17}){f.SetSize(s);Check(f.Font.Size==s);}f.SetSize(30);Check(f.Font.Size==17);});
        Test("Viewer TEXT form body exact",()=>{using var c=Unlock(capsule);using var f=new ViewerForm(c);Check(AllControls(f).OfType<TextBox>().Any(t=>t.ReadOnly&&t.Text==body));});
        if(!args.Contains("--skip-verified-large"))Test("256MiB streaming, bounded managed allocation, hash roundtrip",()=>{
            var big=Path.Combine(Work,"large.bin");using(var f=File.Create(big)){byte[] b=new byte[Limits.Chunk];new Random(9876).NextBytes(b);for(int i=0;i<256;i++)f.Write(b);}
            var exe=Path.Combine(Work,"large.exe");long a=GC.GetTotalAllocatedBytes(true);Gen(exe,"大容量ダミー",BodyKind.TEXT,[big]);var dest=Path.Combine(Work,"large-saved.bin");using(var c=Unlock(exe))c.Save(0,dest,true,default);long alloc=GC.GetTotalAllocatedBytes(true)-a;Check(alloc<96L*1024*1024);Check(Hash(big)==Hash(dest));Console.WriteLine("Streaming managed allocation: "+alloc);File.Delete(exe);File.Delete(dest);File.Delete(big);
        });
        else Console.WriteLine("RETAINED v1.1.1 verified 256MiB streaming test; Core is unchanged (not counted as executed)");
        Test("no incomplete output files",()=>Check(!Directory.EnumerateFiles(Work,".cc-*.tmp").Any()));
        File.WriteAllText(Path.Combine(Work,"dummy-password.txt"),Password,Limits.Utf8);
        NewCoreTests(capsule,source,args.Contains("--skip-verified-legacy"));Finish("core-results.json"); return failed==0?0:1;
    }
    static IEnumerable<Control> AllControls(Control c) { foreach(Control x in c.Controls) {yield return x;foreach(var y in AllControls(x))yield return y;} }
    static void Test(string name,Action action){try{action();passed++;Results.Add(new{name,pass=true});Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Results.Add(new{name,pass=false,error=e.GetType().Name+": "+e.Message});Console.WriteLine("FAIL "+name+" "+e);}}
    static void Check(bool ok){if(!ok)throw new Exception("Assertion failed");}
    static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected);}
    static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
    static void Gen(string output,string body,BodyKind k,string[] files){using var t=File.OpenRead(Template);Container.Generate(t,output,body,k,files,Pw,true,default);}
    static Container.UnlockedCapsule Unlock(string path)=>Container.Unlock(path,Pw,default);
    static byte[] Header(string path){using var f=File.OpenRead(path);var l=Container.Locate(f);f.Position=l.Offset;return Container.Read(f,Container.HeaderSize);}
    static void Mutate(string path,string name,Action<FileStream,(long Offset,long Length)> action){string d=Path.Combine(Work,"mutation-"+name+".exe");File.Copy(path,d,true);using(var f=new FileStream(d,FileMode.Open,FileAccess.ReadWrite)){var l=Container.Locate(f);action(f,l);}Reject(()=>{using var c=Unlock(d);});File.Delete(d);}
    static string Repack(string path,string name,Action<Manifest,FileStream,long> mutate)
    {
        string d=Path.Combine(Work,name+".exe");File.Copy(path,d,true);using var f=new FileStream(d,FileMode.Open,FileAccess.ReadWrite);var l=Container.Locate(f);f.Position=l.Offset;var h=Container.Read(f,Container.HeaderSize);
        byte[] kek=Sodium.PasswordKey(Pw,h[32..48]),dek=Sodium.Decrypt(h[96..144],h[..96],h[48..72],kek),mk=Sodium.Derive(dek,0,"CCMANIF1");
        try{long mo=Container.I64(h,144);f.Position=l.Offset+mo;var m=JsonSerializer.Deserialize<Manifest>(Sodium.Decrypt(Container.Read(f,(int)Container.I64(h,152)),h,h[72..96],mk))!;mutate(m,f,l.Offset);var plain=JsonSerializer.SerializeToUtf8Bytes(m);BinaryPrimitives.WriteInt64LittleEndian(h.AsSpan(152),plain.Length+16);f.Position=l.Offset;f.Write(h);f.Position=l.Offset+mo;f.Write(Sodium.Encrypt(plain,h,h[72..96],mk));long end=f.Position;f.SetLength(end+64);byte[] footer=new byte[64];"CCAPEND1"u8.CopyTo(footer);BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(8),1);BinaryPrimitives.WriteInt64LittleEndian(footer.AsSpan(16),l.Offset);BinaryPrimitives.WriteInt64LittleEndian(footer.AsSpan(24),end-l.Offset);SHA256.HashData(footer.AsSpan(0,32)).CopyTo(footer,32);f.Write(footer);}
        finally{Sodium.Wipe(kek);Sodium.Wipe(dek);Sodium.Wipe(mk);}return d;
    }
    static void Finish(string name){string folder=ResultFolder.Length>0?ResultFolder:Path.GetFullPath(Path.Combine(Root,"../Work/test-results"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,name),JsonSerializer.Serialize(new{passed,failed,tests=Results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"TOTAL PASS={passed} FAIL={failed}");}
    static void RunHtml()
    {
        using var form=new AppForm{Text="CipherCapsule HTML security test",Size=new Size(1000,740)};using var view=new HtmlView();form.Controls.Add(view);
        form.Shown+=async(_,_)=>{
            try{
                string html="<h1>HTML実表示 日本語🔐</h1><p style='color:blue'>青色の本文</p><table><tr><td>表のセル</td></tr></table><script>window.__evil=1;fetch('http://127.0.0.1:43821/leak')</script><iframe src='http://127.0.0.1:43821/frame'></iframe><img src='http://127.0.0.1:43821/image'><a href='https://example.com'>無効なリンク</a>";
                await view.ShowHtml(html,12);for(int i=0;i<100&&!view.Rendered;i++)await Task.Delay(100);
                Test("WebView2 HTML actual render",()=>Check(view.Rendered));
                var w=(WebView2)typeof(HtmlView).GetField("web",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(view)!;var c=w.CoreWebView2;
                Test("WebView2 script messages host objects devtools disabled",()=>Check(!c.Settings.IsScriptEnabled&&!c.Settings.IsWebMessageEnabled&&!c.Settings.AreHostObjectsAllowed&&!c.Settings.AreDevToolsEnabled));
                string dom=await c.ExecuteScriptAsync("document.body.innerText");Test("HTML actual Japanese body and table",()=>Check(dom.Contains("HTML")&&dom.Contains("セル")));
                string evil=await c.ExecuteScriptAsync("String(window.__evil)");Test("HTML payload script never ran",()=>Check(evil=="\"undefined\""));
                string bad=await c.ExecuteScriptAsync("document.querySelectorAll('script,iframe,form,img[src^=http]').length");Test("HTML unsafe DOM removed",()=>Check(bad=="0"));
                string links=await c.ExecuteScriptAsync("document.querySelector('a').getAttribute('href')");Test("safe HTTP link retained",()=>Check(links.Contains("https://example.com")));
                c.NavigationCompleted+=(_,e)=>Console.WriteLine("HTML navigation completed: "+e.IsSuccess+" "+e.WebErrorStatus);
                c.NavigationStarting+=(_,e)=>Console.WriteLine("HTML starting: "+e.Uri+" cancelled="+e.Cancel+" user="+e.IsUserInitiated);
                c.WebResourceRequested+=(_,e)=>Console.WriteLine("HTML resource: "+e.Request.Uri+" "+e.ResourceContext);
                c.ProcessFailed+=(_,e)=>Console.WriteLine("HTML process failed: "+e.ProcessFailedKind);
                await view.ShowHtml("<p>"+string.Concat(Enumerable.Repeat("dummy text ",300000))+"</p>",12);for(int i=0;i<200&&!view.Rendered;i++)await Task.Delay(100);Test("HTML >2MiB memory response renders",()=>Check(view.Rendered));
                if(!view.Rendered)Console.WriteLine(await c.ExecuteScriptAsync("JSON.stringify({url:location.href,len:document.body.innerText.length,state:document.readyState})").WaitAsync(TimeSpan.FromSeconds(5)));
            }catch(Exception e){Test("HTML runtime",()=>throw e);}finally{form.Close();}
        };Application.Run(form);
    }
}
