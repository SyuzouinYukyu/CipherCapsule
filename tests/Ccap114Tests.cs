extern alias BuilderApp;
using CipherCapsule.Core;
using CipherCapsule.UI;
using CipherCapsule.Viewer;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using CcapInput=BuilderApp::CipherCapsule.Builder.CcapFiles;
using Output114=BuilderApp::CipherCapsule.Builder.OutputSelection;
using Format114=BuilderApp::CipherCapsule.Builder.OutputFormat;
using Settings114=BuilderApp::CipherCapsule.Builder.Settings;
using Builder114=BuilderApp::CipherCapsule.Builder.BuilderForm;
using Password114=BuilderApp::CipherCapsule.UI.PasswordPanel;
using HtmlEditor114=BuilderApp::CipherCapsule.Builder.HtmlEditor;
using Viewer114=BuilderApp::CipherCapsule.Viewer.ViewerForm;
internal static partial class Program
{
    static void RunCcap114()
    {
        string area=Path.Combine(Work,"CCAP 日本語🔐 =114");Directory.CreateDirectory(area);
        string txt=Path.Combine(area,"文章.txt"),pdf=Path.Combine(area,"資料.pdf"),otherDir=Path.Combine(area,"別");Directory.CreateDirectory(otherDir);string pdf2=Path.Combine(otherDir,"資料.pdf");
        File.WriteAllText(txt,"CCAP dummy attachment 日本語",Limits.Utf8);File.WriteAllText(pdf,"PDF-ONE");File.WriteAllText(pdf2,"PDF-TWO");
        string text=Path.Combine(area,"本文.ccap"),html=Path.Combine(area,"HTML.ccap"),many=Path.Combine(area,"複数.ccap"),exe=Path.Combine(area,"比較.exe");string body="CCAP本文 日本語 🔑\r\nUnicode e\u0301 =114";
        Test("CCAP TEXT no attachments authenticated roundtrip without checksum",()=>{var r=PackageWriter.GenerateCcap(text,body,BodyKind.TEXT,[],Pw,false,false,default);using var c=Container.Unlock(text,Pw,default,ccapOnly:true);Check(c.Manifest.Body==body&&c.Manifest.Kind==BodyKind.TEXT&&c.Manifest.Attachments.Count==0&&c.Manifest.AllowCopy&&r.Sha512==null&&!File.Exists(text+".sha512")&&r.Sha256==Hash(text));});
        Test("CCAP HTML copy prohibited checksum roundtrip",()=>{var r=PackageWriter.GenerateCcap(html,"<h1>CCAP HTML</h1><table><tr><td>表</td></tr></table>",BodyKind.HTML,[],Pw,true,false,default,allowCopy:false);using var c=Container.Unlock(html,Pw,default,ccapOnly:true);Check(c.Manifest.Kind==BodyKind.HTML&&c.Manifest.Body.Contains("<table")&&!c.Manifest.AllowCopy&&r.Sha512!=null&&File.ReadAllText(html+".sha512").StartsWith(r.Sha512)&&r.Sha256==Hash(html));});
        Test("CCAP header footer zero offset unchanged crypto constants and exact EOF",()=>{byte[] bytes=File.ReadAllBytes(text);Check(bytes.AsSpan(0,8).SequenceEqual("CCAPDAT1"u8)&&!bytes.AsSpan(0,2).SequenceEqual("MZ"u8));var f=bytes[^64..];using var stream=File.OpenRead(text);var loc=Container.Locate(stream);Check(loc.Offset==0&&loc.Length==bytes.Length-64&&Container.I64(f,16)==0&&Container.I64(f,24)==loc.Length&&f.AsSpan(0,8).SequenceEqual("CCAPEND1"u8));var h=Header(text);Check(BitConverter.ToUInt32(h,8)==1&&BitConverter.ToUInt32(h,12)==1&&Container.I64(h,16)==3&&Container.I64(h,24)==256L*1024*1024&&BitConverter.ToUInt32(h,164)==Limits.Chunk);});
        Test("CCAP never consumes or embeds viewer template and size delta equals template",()=>{Gen(exe,body,BodyKind.TEXT,[]);Check(new FileInfo(exe).Length-new FileInfo(text).Length==new FileInfo(Template).Length&&new FileInfo(text).Length<4096);using var c=Unlock(exe);Check(c.Manifest.Body==body);});
        Test("CCAP multiple attachments checksum and EXE capacity comparison",()=>{PackageWriter.GenerateCcap(many,body,BodyKind.TEXT,[pdf,txt,pdf2],Pw,true,false,default);string packed=Path.Combine(area,"複数.exe");Gen(packed,body,BodyKind.TEXT,[pdf,txt,pdf2]);using var c=Unlock(many);Check(c.Manifest.Attachments.Count==3&&new FileInfo(packed).Length-new FileInfo(many).Length==new FileInfo(Template).Length);File.WriteAllText(Path.Combine(Root,"../Work/ccap-size-comparison.json"),JsonSerializer.Serialize(new[]{new{kind="body-only",exe_bytes=new FileInfo(exe).Length,ccap_bytes=new FileInfo(text).Length,template_bytes=new FileInfo(Template).Length},new{kind="multiple-attachments",exe_bytes=new FileInfo(packed).Length,ccap_bytes=new FileInfo(many).Length,template_bytes=new FileInfo(Template).Length}},new JsonSerializerOptions{WriteIndented=true}));});
        Test("CCAP checksum may be removed without changing decryption",()=>{File.Delete(html+".sha512");using var c=Unlock(html);Check(c.Manifest.Kind==BodyKind.HTML);});
        Test("CCAP single attachment final extension and SHA256",()=>{using var c=Unlock(many);string output=AttachmentNames.SinglePath(Path.Combine(area,"別名.avi"),c.Manifest.Attachments[0].Name);c.Save(0,output,false,default);Check(Path.GetFileName(output)=="別名.avi.pdf"&&Hash(output)==Hash(pdf));});
        Test("CCAP bulk collisions preserve file and directory with exact saved hashes",()=>{using var c=Unlock(many);string folder=Path.Combine(area,"一括");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"資料.pdf"),"KEEP");Directory.CreateDirectory(Path.Combine(folder,"資料 (2).pdf"));var r=AttachmentBatch.Save(c,folder,default);Check(r.Succeeded==3&&r.Failed==0&&r.Unprocessed==0&&Path.GetFileName(r.Saved[0].Path)=="資料 (3).pdf"&&Path.GetFileName(r.Saved[2].Path)=="資料 (4).pdf"&&Hash(r.Saved[0].Path)==Hash(pdf)&&Hash(r.Saved[1].Path)==Hash(txt)&&Hash(r.Saved[2].Path)==Hash(pdf2)&&File.ReadAllText(Path.Combine(folder,"資料.pdf"))=="KEEP");});
        Test("CCAP wrong password never returns manifest",()=>Reject(()=>{using var c=Container.Unlock(text,Limits.Utf8.GetBytes("Wrong114"),default,ccapOnly:true);}));
        Test("CCAP parser recognizes content independently of filename",()=>{string renamed=Path.Combine(area,"opaque.bin");File.Copy(text,renamed);using var c=Unlock(renamed);Check(c.Manifest.Body==body);});
        Test("CCAP opening UI rejects a PE renamed to ccap",()=>{string renamed=Path.Combine(area,"renamed.ccap");File.Copy(exe,renamed);Reject(()=>CcapInput.ValidateRequest([renamed]));Reject(()=>{using var c=Container.Unlock(renamed,Pw,default,ccapOnly:true);});using var old=Unlock(renamed);Check(old.Manifest.Body==body);});
        foreach(var mutation in new[]{("header-magic",0),("header-version",8),("kdf-algorithm",12),("kdf-ops",16),("kdf-memory",24),("salt",32),("nonce",48),("wrapped-key",96),("manifest-offset",144),("manifest-length",152),("attachment-count",160),("chunk-size",164),("header-reserved",168)})
            Test("CCAP rejects "+mutation.Item1,()=>Mutate(text,"ccap-"+mutation.Item1,(f,l)=>{f.Position=mutation.Item2;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        foreach(var mutation in new[]{("footer-magic",0),("footer-version",8),("footer-reserved",12),("footer-hash",32)})
            Test("CCAP rejects "+mutation.Item1,()=>Mutate(text,"ccap-"+mutation.Item1,(f,l)=>{f.Position=f.Length-64+mutation.Item2;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        foreach(long offset in new[]{1L,-1,long.MaxValue})Test("CCAP rejects rehashed nonzero offset "+offset,()=>Mutate(text,"ccap-offset-"+offset,(f,l)=>{f.Position=f.Length-64;var b=Container.Read(f,64);BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(16),offset);SHA256.HashData(b.AsSpan(0,32)).CopyTo(b,32);f.Position=f.Length-64;f.Write(b);}));
        foreach(long length in new[]{0L,191,-1,long.MaxValue})Test("CCAP rejects rehashed invalid length "+length,()=>Mutate(text,"ccap-length-"+length,(f,l)=>{f.Position=f.Length-64;var b=Container.Read(f,64);BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(24),length);SHA256.HashData(b.AsSpan(0,32)).CopyTo(b,32);f.Position=f.Length-64;f.Write(b);}));
        Test("CCAP truncation fails closed",()=>Mutate(text,"ccap-truncated",(f,l)=>f.SetLength(f.Length-1)));
        Test("CCAP excess trailing byte fails closed",()=>Mutate(text,"ccap-excess",(f,l)=>{f.Position=f.Length;f.WriteByte(0);}));
        Test("CCAP shifted header and preserved footer rejected",()=>{string d=Path.Combine(area,"prefix.ccap");File.WriteAllBytes(d,[0,..File.ReadAllBytes(text)]);Reject(()=>CcapInput.ValidateRequest([d]));});
        Test("CCAP modified authenticated manifest never exposes body",()=>Mutate(text,"ccap-manifest",(f,l)=>{f.Position=Container.I64(Header(text),144)+5;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        Test("CCAP modified attachment ciphertext rejected before display",()=>Mutate(many,"ccap-attachment",(f,l)=>{f.Position=Container.HeaderSize+30;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}));
        Test("CCAP authenticated traversal filename rejected",()=>{string d=Repack(many,"ccap-unsafe-name",(m,f,start)=>m.Attachments[0].Name="../outside.txt");Reject(()=>{using var c=Unlock(d);});});
        Test("CCAP partial generation canceled before commit preserves existing output",()=>{string d=Path.Combine(area,"cancel.ccap");File.WriteAllText(d,"KEEP");using var ct=new CancellationTokenSource();ct.Cancel();Reject(()=>PackageWriter.GenerateCcap(d,body,BodyKind.TEXT,[],Pw,true,true,ct.Token));Check(File.ReadAllText(d)=="KEEP"&&!File.Exists(d+".sha512"));});
        Test("CCAP self-validation failure preserves destination and deletes incomplete temp",()=>{string folder=Path.Combine(area,"self-verify");Directory.CreateDirectory(folder);string d=Path.Combine(folder,"preserve.ccap");File.WriteAllText(d,"KEEP");bool changed=false;Reject(()=>Container.GenerateCcap(d,body,BodyKind.TEXT,[],Pw,true,default,new InlineProgress<CapsuleProgress>(p=>{if(p.Stage=="生成データを再検証しています"){string temp=Directory.EnumerateFiles(folder,".cc-*.tmp").Single();using var f=new FileStream(temp,FileMode.Open,FileAccess.ReadWrite);f.Position=96;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));changed=true;}})));Check(changed&&File.ReadAllText(d)=="KEEP"&&!Directory.EnumerateFiles(folder,".cc-*.tmp").Any());});
        Test("CCAP pair rollback after final output lock restores prior checksum",()=>{string d=Path.Combine(area,"locked.ccap");File.WriteAllText(d,"OLD CAPSULE");File.WriteAllText(d+".sha512","OLD SUM");using(var held=new FileStream(d,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject(()=>PackageWriter.GenerateCcap(d,body,BodyKind.TEXT,[],Pw,true,true,default));Check(File.ReadAllText(d)=="OLD CAPSULE"&&File.ReadAllText(d+".sha512")=="OLD SUM");});
        Test("CCAP cannot replace an attachment input",()=>{Reject(()=>PackageWriter.GenerateCcap(txt,body,BodyKind.TEXT,[txt],Pw,true,true,default));Check(File.ReadAllText(txt,Limits.Utf8)=="CCAP dummy attachment 日本語");});
        Test("CCAP no-overwrite preserves existing output",()=>{string d=Path.Combine(area,"no-overwrite.ccap");File.WriteAllText(d,"KEEP");Reject(()=>PackageWriter.GenerateCcap(d,body,BodyKind.TEXT,[],Pw,true,false,default));Check(File.ReadAllText(d)=="KEEP"&&!File.Exists(d+".sha512"));});
        Test("CCAP destination directory collision fails without artifacts",()=>Reject(()=>PackageWriter.GenerateCcap(area,body,BodyKind.TEXT,[],Pw,false,false,default)));
        Test("CCAP 64MiB streaming allocation cancellation and exact hash",()=>{
            string large=Path.Combine(area,"large.bin"),d=Path.Combine(area,"large.ccap"),saved=Path.Combine(area,"large-saved.bin");using(var f=File.Create(large)){byte[] block=new byte[Limits.Chunk];new Random(114).NextBytes(block);for(int i=0;i<64;i++)f.Write(block);}
            long start=GC.GetTotalAllocatedBytes(true);Container.GenerateCcap(d,body,BodyKind.TEXT,[large],Pw,false,default);using(var c=Unlock(d))c.Save(0,saved,false,default);long allocation=GC.GetTotalAllocatedBytes(true)-start;Check(allocation<96L*1024*1024&&Hash(saved)==Hash(large));
            using var ct=new CancellationTokenSource();string canceled=Path.Combine(area,"large-canceled.ccap");Reject(()=>Container.GenerateCcap(canceled,body,BodyKind.TEXT,[large],Pw,false,ct.Token,new InlineProgress<CapsuleProgress>(p=>{if(p.Stage.StartsWith("添付を暗号化")&&p.Done>Limits.Chunk)ct.Cancel();})));Check(!File.Exists(canceled));
            File.WriteAllText(Path.Combine(Root,"../Work/ccap-streaming.json"),JsonSerializer.Serialize(new{input_bytes=64L*1024*1024,managed_allocation_bytes=allocation,hash_matches=true,canceled_during_chunks=true}));
        });
        foreach(var format in new[]{Format114.EXE,Format114.CCAP})Test("output dialog and explicit extension validation "+format,()=>{string ext=format==Format114.CCAP?".ccap":".exe";using var d=Output114.Dialog(format,area);Check(d.FileName=="SecureMessage"+ext&&d.DefaultExt==ext[1..]&&!d.AddExtension&&!d.OverwritePrompt&&d.Filter.Contains("*"+ext));Check(Output114.FinalPath(Path.Combine(area,"名前"),format)==Path.Combine(area,"名前"+ext)&&Output114.FinalPath(Path.Combine(area,"名前"+ext.ToUpperInvariant()),format).EndsWith(ext.ToUpperInvariant()));Reject(()=>Output114.FinalPath(Path.Combine(area,"名前.wrong"),format));});
        Test("CCAP opening accepts only one existing absolute genuine CCAP",()=>{Check(CcapInput.ValidateRequest([text])==text);foreach(var paths in new[]{Array.Empty<string>(),new[]{text,html},new[]{"relative.ccap"},new[]{Path.Combine(area,"missing.ccap")},new[]{exe}})Reject(()=>CcapInput.ValidateRequest(paths));string fake=Path.Combine(area,"fake.ccap");File.WriteAllText(fake,"Not a capsule");Reject(()=>CcapInput.ValidateRequest([fake]));});
        string ini=Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");byte[]? prior=File.Exists(ini)?File.ReadAllBytes(ini):null;
        const string legacy="[Window]\nX=151\nY=171\nWidth=1280\nHeight=1080\nFont=12\nKind=1\nPasswordRevealed=1\n";
        try{
            Test("old INI defaults EXE without altering HTML kind or existing values",()=>{File.WriteAllText(ini,legacy);var s=Settings114.Load();Check(s.OutputFormat==Format114.EXE&&s.Kind==1&&s.X==151&&s.PasswordRevealed);});
            Test("CCAP INI saved atomically independently of TEXT HTML kind",()=>{var s=Settings114.Load();s.OutputFormat=Format114.CCAP;s.Kind=0;s.LastOutputDirectory=area;s.Save();var r=Settings114.Load();Check(r.OutputFormat==Format114.CCAP&&r.Kind==0&&r.LastOutputDirectory==area&&!File.ReadAllText(ini).Contains(Password));});
            Test("invalid duplicate OutputFormat only falls back EXE",()=>{foreach(string v in new[]{"BAD","","1","ccap","CCAP\nOutputFormat=EXE"}){File.WriteAllText(ini,legacy+"OutputFormat="+v+"\n");var s=Settings114.Load();Check(s.OutputFormat==Format114.EXE&&s.Kind==1&&s.X==151&&s.PasswordRevealed);}});
            Test("dedicated CCAP drop target and busy state preserve existing targets",()=>{File.WriteAllText(ini,legacy);using var b=new Builder114();Check(Field<Label>(b,"ccapDrop").AllowDrop&&Field<TextBox>(b,"editor").AllowDrop&&Field<ListView>(b,"files").AllowDrop);var busy=typeof(Builder114).GetMethod("SetBusy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;busy.Invoke(b,[true]);Check(!Field<Button>(b,"openCcap").Enabled&&!Field<ComboBox>(b,"outputFormat").Enabled);busy.Invoke(b,[false]);Check(Field<Button>(b,"openCcap").Enabled&&Field<ComboBox>(b,"outputFormat").Enabled);});
        }finally{if(prior!=null)File.WriteAllBytes(ini,prior);else File.Delete(ini);}
        Test("CCAP post-tests no incomplete temporary output",()=>Check(!Directory.EnumerateFiles(area,".cc-*.tmp",SearchOption.AllDirectories).Any()));
        Test("new reader authenticates genuine unchanged v113 generated EXE",()=>{string legacy=Path.GetFullPath(Path.Combine(Root,"../Work/compat113/legacy113.exe"));using var c=Unlock(legacy);Check(c.Manifest.Body=="Legacy v1.1.3 fixture 日本語"&&c.Manifest.Kind==BodyKind.TEXT);});
        Test("unchanged v113 reader authenticates newly generated EXE",()=>{string legacy=Path.GetFullPath(Path.Combine(Root,"../Work/compat113"));var p=new System.Diagnostics.ProcessStartInfo{FileName="dotnet",WorkingDirectory=legacy,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8};p.ArgumentList.Add(Path.Combine(legacy,"bin/Release/net10.0-windows/Legacy.dll"));p.ArgumentList.Add(exe);using var process=System.Diagnostics.Process.Start(p)!;string output=process.StandardOutput.ReadToEnd();process.WaitForExit();Check(process.ExitCode==0&&output.Trim()==Convert.ToHexString(SHA256.HashData(Limits.Utf8.GetBytes(body))));});
    }
    static void RunCcapCompatRecheck114()
    {
        Test("unchanged v113 reader authenticates newly generated EXE",()=>{
            string legacy=Path.GetFullPath(Path.Combine(Root,"../Work/compat113"));
            string exe=Path.GetFullPath(Path.Combine(Root,"../Work/regression-final/selftests/CCAP 日本語🔐 =114/比較.exe"));
            string body="CCAP本文 日本語 🔑\r\nUnicode e\u0301 =114";
            var p=new System.Diagnostics.ProcessStartInfo{FileName="dotnet",WorkingDirectory=legacy,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};p.ArgumentList.Add(Path.Combine(legacy,"bin/Release/net10.0-windows/Legacy.dll"));p.ArgumentList.Add(exe);
            using var process=System.Diagnostics.Process.Start(p)!;string output=process.StandardOutput.ReadToEnd();string error=process.StandardError.ReadToEnd();process.WaitForExit();
            Check(process.ExitCode==0&&output.Trim()==Convert.ToHexString(SHA256.HashData(Limits.Utf8.GetBytes(body))));
        });
    }
    static void RunCcapGui114()
    {
        string area=Path.Combine(Work,"Builder CCAP session");Directory.CreateDirectory(area);string attachment=Path.Combine(area,"attached.txt");File.WriteAllText(attachment,"PUBLIC DUMMY");
        string text=Path.Combine(area,"TEXT.ccap"),html=Path.Combine(area,"HTML.ccap");Container.GenerateCcap(text,"CCAP body distinct from editor",BodyKind.TEXT,[attachment],Pw,false,default,allowCopy:false);Container.GenerateCcap(html,"<h1>CCAP HTML</h1><table><tr><td>表</td></tr></table>",BodyKind.HTML,[attachment],Pw,false,default);
        string ini=Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");byte[]? prior=File.Exists(ini)?File.ReadAllBytes(ini):null;File.WriteAllText(ini,"[Window]\nX=-1\nY=-1\nWidth=1400\nHeight=1200\nFont=12\nKind=1\nOutputFormat=CCAP\n");
        try{
            using var b=new Builder114();var editor=Field<TextBox>(b,"editor");var rich=Field<HtmlEditor114>(b,"htmlEditor");var password=Field<Password114>(b,"password");var format=Field<ComboBox>(b,"outputFormat");var mode=Field<ComboBox>(b,"mode");
            b.Shown+=async(_,_)=>{try{
                for(int i=0;i<200&&!rich.IsReady;i++)await Task.Delay(100);Test("CCAP Builder editor ready for retained-session verification",()=>Check(rich.IsReady));if(!rich.IsReady)return;
                editor.Text="BUILDER TEXT MUST REMAIN";await rich.SetContent("<p>BUILDER HTML MUST REMAIN</p>");string originalHtml=await rich.GetContent();mode.SelectedIndex=0;password.Primary.Text="Builder-only password 114";password.Confirm.Text=password.Primary.Text;typeof(Builder114).GetMethod("AddFiles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(b,[new[]{attachment}]);
                foreach(int pt in new[]{9,12,17}){b.SetSize(pt);b.PerformLayout();var output=Field<ComboBox>(b,"outputFormat");var open=Field<Button>(b,"openCcap");var drop=Field<Label>(b,"ccapDrop");Test("CCAP new controls fit at "+pt+"pt",()=>Check(output.Width>=TextRenderer.MeasureText("CipherCapsule形式 (.ccap)",output.Font).Width+20&&open.Width>=TextRenderer.MeasureText(open.Text,open.Font).Width+open.Padding.Horizontal&&drop.Width>=TextRenderer.MeasureText(drop.Text,drop.Font).Width+drop.Padding.Horizontal&&!open.Bounds.IntersectsWith(drop.Bounds)));}
                b.SetSize(12);
                foreach(var path in new[]{text,html}){
                    int phase=0;bool rendered=false,authCleared=false;Exception? failure=null;var watch=System.Diagnostics.Stopwatch.StartNew();
                    using var timer=new System.Windows.Forms.Timer{Interval=60};
                    timer.Tick+=async(_,_)=>{try{
                        if(watch.Elapsed>TimeSpan.FromSeconds(35))throw new TimeoutException("CCAP modal session timed out");
                        var auth=Application.OpenForms.OfType<BuilderApp::CipherCapsule.UI.PasswordDialog>().FirstOrDefault();
                        if(phase==0&&auth!=null){phase=1;Check(auth.Password.Primary.Text.Length==0&&auth.Password.Confirm.Text.Length==0);auth.Password.Primary.Text=Password;auth.Password.Confirm.Text=Password;((Button)auth.AcceptButton!).PerformClick();}
                        var viewer=Application.OpenForms.OfType<Viewer114>().FirstOrDefault();
                        if(phase==1&&viewer!=null){phase=2;authCleared=auth==null||auth.Password.Primary.Text.Length==0;
                            if(path==html){var view=Field<BuilderApp::CipherCapsule.UI.HtmlView>(viewer,"html");for(int i=0;i<150&&!view.Rendered;i++)await Task.Delay(100);rendered=view.Rendered;Check(rendered);}else{Check(!Field<BuilderApp::CipherCapsule.Viewer.CopyTextBox>(viewer,"text").AllowCopy);rendered=true;}
                            Check(AllControls(viewer).OfType<Button>().Any(x=>x.Text=="選択した添付を保存…")&&AllControls(viewer).OfType<Button>().Any(x=>x.Text=="すべての添付を保存…"));viewer.Close();phase=3;
                        }
                    }catch(Exception ex){failure=ex;timer.Stop();foreach(var viewer in Application.OpenForms.OfType<Viewer114>().ToArray())viewer.Close();foreach(var auth in Application.OpenForms.OfType<BuilderApp::CipherCapsule.UI.PasswordDialog>().ToArray())auth.Close();}};
                    timer.Start();await b.OpenCcap([path]);timer.Stop();Test("CCAP shared Viewer auth render normal-close "+Path.GetFileName(path),()=>{if(failure!=null)throw failure;Check(phase==3&&rendered&&authCleared);});
                    string afterHtml=await rich.GetContent();Test("Builder TEXT HTML password attachments format remain after "+Path.GetFileName(path),()=>Check(editor.Text=="BUILDER TEXT MUST REMAIN"&&afterHtml==originalHtml&&password.Primary.Text=="Builder-only password 114"&&password.Confirm.Text==password.Primary.Text&&Field<List<string>>(b,"paths").SequenceEqual(new[]{attachment})&&format.SelectedIndex==1&&mode.SelectedIndex==0));
                }
            }catch(Exception ex){Test("CCAP modal runtime",()=>throw ex);}finally{b.Close();}};Application.Run(b);
        }finally{if(prior!=null)File.WriteAllBytes(ini,prior);else File.Delete(ini);}
    }
}
