using CipherCapsule.Core;
using System.Security.Cryptography;
using System.Text.Json;

internal static partial class Program
{
    // Read-only provenance/decryption checks for the manually generated GUI fixtures.
    // This branch does not automate windows or replace any real GUI confirmations.
    static int VerifyGui113()
    {
        string gui=Path.GetFullPath(Path.Combine(Root,"../Work/gui")),outFolder=Path.Combine(gui,"出力 日本語=113");
        byte[] pw=Limits.Utf8.GetBytes("Gui113-Dummy!2026");var report=new Dictionary<string,object>();
        try{
            using var inputDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(gui,"manifest.json")));
            // Duplicate PDF names are checked independently below against the two source hashes.
            var expectedHashes=inputDoc.RootElement.GetProperty("inputs").EnumerateObject().Select(p=>p.Value.GetString()!.ToUpperInvariant()).Order().ToArray();
            string template=Path.Combine(Root,"templates/CipherCapsule_Viewer.exe");long templateLength=new FileInfo(template).Length;
            foreach(string kind in new[]{"TEXT","HTML"}){
                string path=Path.Combine(outFolder,kind+"-message.exe");
                using(var f=File.OpenRead(path)){using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] b=new byte[1024*1024];long left=templateLength;while(left>0){int n=f.Read(b,0,(int)Math.Min(left,b.Length));if(n<=0)throw new IOException("Viewer prefix truncated");h.AppendData(b,0,n);left-=n;}Check(Convert.ToHexString(h.GetHashAndReset())==Hash(template));}
                using var capsule=Container.Unlock(path,pw,default);
                Check(capsule.Manifest.Kind==(kind=="TEXT"?BodyKind.TEXT:BodyKind.HTML));
                var known=inputDoc.RootElement.GetProperty("inputs").EnumerateObject().Select(p=>(Name:Path.GetFileName(p.Name),Size:new FileInfo(Path.Combine(gui,p.Name)).Length)).ToArray();
                var actual=capsule.Manifest.Attachments.Select(a=>(a.Name,a.Size)).OrderBy(a=>a.Name,StringComparer.Ordinal).ThenBy(a=>a.Size).ToArray();
                var all=known.OrderBy(a=>a.Name,StringComparer.Ordinal).ThenBy(a=>a.Size).ToArray();
                var small=known.Where(a=>!a.Name.EndsWith(".bin",StringComparison.OrdinalIgnoreCase)).OrderBy(a=>a.Name,StringComparer.Ordinal).ThenBy(a=>a.Size).ToArray();
                Check(actual.SequenceEqual(all)||(kind=="HTML"&&actual.SequenceEqual(small)));
                report[kind.ToLowerInvariant()+"_attachment_count"]=capsule.Manifest.Attachments.Count;
                if(kind=="TEXT")Check(capsule.Manifest.Body.Replace("\r\n","\n")==File.ReadAllText(Path.Combine(gui,"本文.txt")).Replace("\r\n","\n"));
                else Check(capsule.Manifest.Body.Contains("CipherCapsule v1.1.3 HTML")&&capsule.Manifest.Body.Contains("<table")&&capsule.Manifest.Body.Contains("段落 60"));
                using var hashFile=File.OpenRead(path);string hash=Convert.ToHexString(SHA512.HashData(hashFile));Check(File.ReadAllText(path+".sha512").TrimStart('\uFEFF').StartsWith(hash,StringComparison.OrdinalIgnoreCase));
                report[kind.ToLowerInvariant()+"_decryption"]=true;report[kind.ToLowerInvariant()+"_embedded_viewer_matches"]=true;report[kind.ToLowerInvariant()+"_sha512"]=true;
                if(kind=="TEXT"){
                    int index=capsule.Manifest.Attachments.FindIndex(a=>a.Name=="資料.pdf");string verification=Path.Combine(gui,"expected-first.pdf");
                    Check(index>=0);if(File.Exists(verification))Check(new FileInfo(verification).Length<1024);capsule.Save(index,verification,File.Exists(verification),default);
                    Check(Hash(Path.Combine(gui,"単体保存/別名.avi.pdf"))==Hash(verification));
                }
            }
            Check(File.ReadAllText(Path.Combine(gui,"単体保存/変更.avi.pdf"))=="KEEP FINAL TARGET");
            Check(!File.Exists(Path.Combine(gui,"単体保存/変更.avi")));
            Check(Hash(Path.Combine(gui,"単体保存/メモ.foo.txt"))==inputDoc.RootElement.GetProperty("inputs").EnumerateObject().Single(p=>Path.GetFileName(p.Name)=="文章.txt").Value.GetString()!.ToUpperInvariant());
            string bulk=Path.Combine(gui,"一括保存");Check(File.ReadAllText(Path.Combine(bulk,"資料.pdf"))=="KEEP EXISTING"&&Directory.Exists(Path.Combine(bulk,"資料 (2).pdf")));
            string[] names=["資料 (3).pdf","文章.txt","資料 (4).pdf","大容量A.bin","大容量B.bin"];
            Check(names.Select(n=>Hash(Path.Combine(bulk,n))).Order().SequenceEqual(expectedHashes));
            Check(Directory.EnumerateFiles(bulk).Count()==6&&!Directory.EnumerateFiles(bulk,".cc-*.tmp").Any());
            string cancelled=Path.Combine(gui,"キャンセル保存");Check(!Directory.EnumerateFiles(cancelled,".cc-*.tmp").Any());
            var inputHashes=expectedHashes.ToHashSet();var savedCancelled=new List<object>();foreach(string path in Directory.EnumerateFiles(cancelled)){string hash=Hash(path);Check(inputHashes.Contains(hash));savedCancelled.Add(new{name=Path.GetFileName(path),sha256=hash});}
            string ini=Path.Combine(Root,"../Work/stage-folder-button/Builder/CipherCapsule.ini");string iniText=File.ReadAllText(ini);Check(iniText.Contains("LastOutputDirectory="+outFolder)&&!iniText.Contains("Gui113-Dummy!2026")&&!iniText.Contains("本文"));
            report["single_saved_hashes"]=true;report["final_target_overwrite_refused_and_preserved"]=true;report["bulk_five_hashes_match_originals"]=true;report["preexisting_file_and_directory_unchanged"]=true;report["cancelled_outputs_are_complete"]=savedCancelled;report["ini_directory_correct_no_secret"]=true;
            File.WriteAllText(Path.Combine(Root,"../Work/gui-artifact-verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}finally{Sodium.Wipe(pw);}
    }
}
