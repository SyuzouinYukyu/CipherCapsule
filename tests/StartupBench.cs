using System.Diagnostics;
using System.Text.Json;
using CipherCapsule.Core;
static class StartupBench
{
    static string Root=FindRoot();static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(Directory.Exists(Path.Combine(d.FullName,"Source")))return d.FullName;d=d.Parent;}throw new Exception();}
    static int Main(string[] args)
    {
        string dir=Path.Combine(Root,"Work","comparison");Directory.CreateDirectory(dir);string old=Path.Combine(dir,"old-message.exe"),current=Path.Combine(dir,"new-message.exe");
        if(!args.Contains("--measure")){
            string attachment=Path.Combine(dir,"比較添付.bin");File.WriteAllBytes(attachment,Enumerable.Range(0,1229).Select(i=>(byte)(i%251)).ToArray());
            string body="<p>連絡事項です</p><p><a href='https://www.youtube.com/'>YouTube</a></p>";byte[] password=Limits.Utf8.GetBytes("比較テスト🔑");
            string oldTemplate=Path.GetFullPath(Path.Combine(Root,"../v1.1.0/Source/templates/CipherCapsule_Viewer.exe")),newTemplate=Path.Combine(Root,"Source/templates/CipherCapsule_Viewer.exe");
            var auth=new Dictionary<string,double>();
            foreach(var x in new[]{(oldTemplate,old),(newTemplate,current)}){using var t=File.OpenRead(x.Item1);PackageWriter.Generate(t,x.Item2,body,BodyKind.HTML,[attachment],password,true,true,default);var sw=Stopwatch.StartNew();using(var unlocked=Container.Unlock(x.Item2,password,default)){}auth[Path.GetFileName(x.Item2)]=sw.Elapsed.TotalMilliseconds;}
            Sodium.Wipe(password);long a=new FileInfo(old).Length,b=new FileInfo(current).Length;
            var report=new{old_viewer_bytes=new FileInfo(oldTemplate).Length,new_viewer_bytes=new FileInfo(newTemplate).Length,old_message_bytes=a,new_message_bytes=b,saving_bytes=a-b,saving_percent=(a-b)*100.0/a,authentication_ms=auth};File.WriteAllText(Path.Combine(dir,"sizes.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));return 0;
        }
        var samples=new List<object>();var timings=new Dictionary<string,List<double>>{{"old",[]},{"new",[]}};
        for(int i=0;i<3;i++)foreach(var sample in new[]{("old",old),("new",current)}){
            var sw=Stopwatch.StartNew();using var p=Process.Start(new ProcessStartInfo(sample.Item2){UseShellExecute=false})!;if(!p.WaitForInputIdle(30000))throw new Exception("UI startup timeout");
            while(sw.Elapsed.TotalSeconds<30){p.Refresh();if(p.MainWindowHandle!=IntPtr.Zero&&p.MainWindowTitle.Contains("パスワード認証"))break;Thread.Sleep(10);}
            if(!p.MainWindowTitle.Contains("パスワード認証"))throw new Exception("Password window not observed");double ms=sw.Elapsed.TotalMilliseconds;timings[sample.Item1].Add(ms);samples.Add(new{kind=sample.Item1,trial=i+1,pid=p.Id,milliseconds=ms});Console.WriteLine(JsonSerializer.Serialize(samples[^1]));
            if(!p.WaitForExit(120000))throw new Exception("Close the benchmark password dialog normally; timeout");
        }
        var result=new{samples,old_median_ms=timings["old"].Order().ElementAt(1),new_median_ms=timings["new"].Order().ElementAt(1)};File.WriteAllText(Path.Combine(dir,"startup.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));return 0;
    }
}
