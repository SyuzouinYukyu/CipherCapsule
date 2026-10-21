using CipherCapsule.UI;
using CipherCapsule.Builder;
using System.Text.Json;
internal static class PreviewProbe
{
    static readonly List<object> results=[];
    static void Log(string stage,Exception? e=null,object? detail=null){var item=new{stage,type=e?.GetType().FullName,hresult=e?.HResult.ToString("X8"),stack=e?.StackTrace,trace=stage=="preview-show"?new System.Diagnostics.StackTrace().ToString():null,apartment=Thread.CurrentThread.GetApartmentState().ToString(),context=SynchronizationContext.Current?.GetType().FullName,detail};results.Add(item);Console.WriteLine(JsonSerializer.Serialize(item));}
    [STAThread]static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();using var owner=new AppForm{Text="CipherCapsule preview lifecycle probe",Size=new Size(1000,740)};using var editor=new HtmlEditor();owner.Controls.Add(editor);
        owner.Shown+=async(_,_)=>{try{for(int i=0;i<200&&!editor.IsReady;i++)await Task.Delay(100);Log("editor-ready",detail:editor.IsReady);await editor.SetContent("<p>HTMLプレビューテスト</p><p><a href='https://www.youtube.com/'>YouTube</a></p>");
            string body=await editor.GetContent();for(int count=0;count<3;count++){
                using var f=new AppForm{Text="CipherCapsule — HTMLプレビュー",Size=new Size(1000,740)};using var view=new HtmlView();f.Controls.Add(view);bool done=false;
                f.Shown+=async(_,_)=>{try{Log("preview-show");await view.ShowHtml(body,12);for(int i=0;i<100&&!view.Rendered;i++)await Task.Delay(100);Log("preview-render",detail:view.Rendered);}catch(Exception ex){Log("preview-error",ex);}finally{f.Close();}};
                f.FormClosing+=async(_,e)=>{if(done)return;e.Cancel=true;Log("preview-close-start");try{bool ok=await view.CloseAsync();Log("preview-cleanup",detail:ok);}catch(Exception ex){Log("preview-close-error",ex);}done=true;f.Close();};
                if(args.Contains("--after"))await AppForm.PostModal(owner,()=>f.ShowDialog(owner));else f.ShowDialog(owner);
            }
            Log("editor-close",detail:await editor.CloseAsync());
        }catch(Exception e){Log("outer-error",e);}finally{owner.Close();}};Application.Run(owner);
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../probe-results"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,args.Contains("--after")?"after.json":"before.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));return results.Any(x=>JsonSerializer.Serialize(x).Contains("error"))?1:0;
    }
}
