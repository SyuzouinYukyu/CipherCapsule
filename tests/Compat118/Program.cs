extern alias Old117;
using CipherCapsule.Core;
using Old=Old117::CipherCapsule.Core;
using System.Security.Cryptography;
using System.Text.Json;
string oldTemplate=args[0],newTemplate=args[1],root=Path.GetFullPath(args[2]);Directory.CreateDirectory(root);var results=new List<object>();int passed=0,failed=0;byte[] password=Limits.Utf8.GetBytes("Compat118-Dummy!2026");
string input=Path.Combine(root,"添付 日本語=118.txt");File.WriteAllText(input,"互換テスト 😀\n");
void Assert(bool ok){if(!ok)throw new Exception("Assertion failed");}
void Test(string name,Action action){try{action();passed++;results.Add(new{name,passed=true});}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.Message});}Console.WriteLine(name);}
foreach(bool exe in new[]{false,true})
{
 Test($"v117 {(exe?"EXE":"CCAP")} -> v118 authenticated body and attachment",()=>{string file=Path.Combine(root,exe?"old.exe":"old.ccap");using(var template=exe?File.OpenRead(oldTemplate):Stream.Null)Old.Container.Generate(template,file,"日本語旧版TEXT 😀",Old.BodyKind.TEXT,[input],password,false,CancellationToken.None,allowCopy:false);using var opened=Container.Unlock(file,password,CancellationToken.None);Assert(opened.Manifest.Body=="日本語旧版TEXT 😀"&&!opened.Manifest.AllowCopy&&opened.Manifest.Attachments.Single().Name==Path.GetFileName(input));string saved=Path.Combine(root,exe?"old-exe.saved":"old-ccap.saved");opened.Save(0,saved,false,CancellationToken.None);Assert(SHA256.HashData(File.ReadAllBytes(saved)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))));});
 Test($"v118 {(exe?"EXE":"CCAP")} -> v117 authenticated HTML and attachment",()=>{string file=Path.Combine(root,exe?"new.exe":"new.ccap");const string html="<p style=\"font-size:18pt\">新規 HTML 😀</p><table><tr><td>表</td></tr></table>";using(var template=exe?File.OpenRead(newTemplate):Stream.Null)Container.Generate(template,file,html,BodyKind.HTML,[input],password,false,CancellationToken.None);using var opened=Old.Container.Unlock(file,password,CancellationToken.None);Assert(opened.Manifest.Body==html&&opened.Manifest.AllowCopy&&opened.Manifest.Kind==Old.BodyKind.HTML);string saved=Path.Combine(root,exe?"new-exe.saved":"new-ccap.saved");opened.Save(0,saved,false,CancellationToken.None);Assert(SHA256.HashData(File.ReadAllBytes(saved)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))));});
}
Sodium.Wipe(password);File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{passed,failed,desktop_input=false,tests=results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{passed} passed / {failed} failed");return failed==0?0:1;
