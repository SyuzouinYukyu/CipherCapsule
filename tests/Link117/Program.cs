using CipherCapsule.UI;
using System.Text.Json;
using System.ComponentModel;
using CipherCapsule.Builder;
var tests=new List<object>();
void Check(bool value){if(!value)throw new Exception("assertion failed");}
void Test(string name,Action action){try{action();tests.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception ex){tests.Add(new{name,passed=false,error=ex.Message});Console.WriteLine("FAIL "+name);}}
Test("click dispatches exactly once to the OS default handler",()=>{int calls=0;Check(RepositoryLink.TryOpen(RepositoryLink.Url,p=>{calls++;Check(p.UseShellExecute&&p.Verb=="open"&&p.FileName==RepositoryLink.Url);})==null&&calls==1);});
Test("URL is fixed HTTPS without command arguments or elevation",()=>Check(RepositoryLink.TryOpen(RepositoryLink.Url,p=>Check(p.Arguments==""&&p.ArgumentList.Count==0&&p.Verb!="runas"&&new Uri(p.FileName).Scheme=="https"))==null));
foreach(string? invalid in new[]{null,"","http://github.com/SyuzouinYukyu/CipherCapsule","https://github.com/other/repo","javascript:alert(1)",RepositoryLink.Url+" & calc.exe"}){
    var captured=invalid;Test("unexpected target is rejected: "+(captured??"null"),()=>{int calls=0;Check(RepositoryLink.TryOpen(captured,_=>calls++)!=null&&calls==0);});
}
Test("browser start error returns Japanese warning without crashing",()=>Check(RepositoryLink.TryOpen(RepositoryLink.Url,_=>throw new Win32Exception())!.Contains("既定のブラウザー")));
Test("subsequent clicks work after a failed browser start",()=>{RepositoryLink.TryOpen(RepositoryLink.Url,_=>throw new InvalidOperationException());int calls=0;Check(RepositoryLink.TryOpen(RepositoryLink.Url,_=>calls++)==null&&calls==1);});
var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
string directory=Path.Combine(Path.GetDirectoryName(output)!,"日本語 保存=🔐");Directory.CreateDirectory(directory);
string completed=Path.Combine(directory,"message.ccap");
Test("successful folder start closes success dialog exactly once",()=>{int opened=0,closed=0;Check(OutputFolder.OpenAndClose(completed,()=>closed++,_=>opened++)==null&&opened==1&&closed==1);});
Test("folder is requested before dialog closure",()=>{var order=new List<string>();OutputFolder.OpenAndClose(completed,()=>order.Add("close"),_=>order.Add("open"));Check(string.Join(",",order)=="open,close");});
Test("only Explorer receives the parent folder as one argument",()=>{Check(OutputFolder.OpenAndClose(completed,()=>{},p=>Check(Path.GetFileName(p.FileName)=="explorer.exe"&&!p.UseShellExecute&&p.ArgumentList.Count==1&&p.ArgumentList[0]==directory))==null);});
Test("missing folder warns without closing or launching",()=>{int closed=0,opened=0;Check(OutputFolder.OpenAndClose(Path.Combine(directory,"missing","file.exe"),()=>closed++,_=>opened++)!.Contains("存在しません")&&closed==0&&opened==0);});
Test("Explorer launch failure leaves dialog available",()=>{int closed=0;Check(OutputFolder.OpenAndClose(completed,()=>closed++,_=>throw new Win32Exception())!=null&&closed==0);});
Test("relative path rejection does not close result dialog",()=>{int closed=0;Check(OutputFolder.OpenAndClose("relative.exe",()=>closed++,_=>throw new Exception())!=null&&closed==0);});
Test("a retry can close after an earlier folder-open failure",()=>{int closed=0;OutputFolder.OpenAndClose(completed,()=>closed++,_=>throw new Win32Exception());Check(OutputFolder.OpenAndClose(completed,()=>closed++,_=>{})==null&&closed==1);});
int failed=tests.Count(t=>!(bool)t.GetType().GetProperty("passed")!.GetValue(t)!);
File.WriteAllText(output,JsonSerializer.Serialize(new{version="1.1.7",passed=tests.Count-failed,failed,headless=true,actual_browser_started=false,desktop_input=false,tests},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"RESULT {tests.Count-failed}/{failed}");return failed==0?0:1;
