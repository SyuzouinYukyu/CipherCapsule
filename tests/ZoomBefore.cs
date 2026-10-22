using CipherCapsule.Core;
using CipherCapsule.UI;
using CipherCapsule.Viewer;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text.Json;
internal static partial class Program
{
    static void RunZoomBefore()
    {
        string path=Path.Combine(Work,"zoom-before.exe");
        Gen(path,"<h1>倍率検証</h1>"+string.Concat(Enumerable.Range(1,300).Select(i=>$"<p>段落 {i} 日本語と長文表示</p>")),BodyKind.HTML,[]);
        using var capsule=Unlock(path);using var form=new ViewerForm(capsule);
        var view=(HtmlView)typeof(ViewerForm).GetField("html",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        var web=view.Controls.OfType<WebView2>().Single();var starts=new List<string>();var completions=new List<object>();
        form.Shown+=async(_,_)=>{try{
            for(int i=0;i<200&&!view.Rendered;i++)await Task.Delay(100);
            if(!view.Rendered)throw new Exception("Initial HTML did not render");
            web.CoreWebView2.NavigationStarting+=(_,e)=>starts.Add(e.Uri);
            web.CoreWebView2.NavigationCompleted+=(_,e)=>completions.Add(new{e.NavigationId,e.IsSuccess,status=e.WebErrorStatus.ToString()});
            for(int i=0;i<64;i++){form.SetSize(9+i%9);await Task.Delay(3);}
            await Task.Delay(2500);
            var record=new{source="Current ViewerForm / HtmlView zoom diagnostic harness",fontChanges=64,navigationStarts=starts.Count,completions,view.Rendered,errorLabels=view.Controls.OfType<Label>().Select(x=>x.Text).ToArray()};
            string json=JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(Root,"../Work/zoom-before.json"),json);Console.WriteLine(json);
        }catch(Exception e){Console.WriteLine(e);throw;}finally{form.Close();}};
        Application.Run(form);
    }
}
