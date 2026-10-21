extern alias BuilderApp;
using CipherCapsule.UI;
using CipherCapsule.Core;
using EditorType=BuilderApp::CipherCapsule.Builder.HtmlEditor;
internal static partial class Program
{
    static void RunLifecycle()
    {
        using var owner=new AppForm{Text="CipherCapsule lifecycle regression",Size=new Size(1100,800)};using var editor=new EditorType();owner.Controls.Add(editor);
        owner.Shown+=async(_,_)=>{try{
            for(int i=0;i<200&&!editor.IsReady;i++)await Task.Delay(100);Test("lifecycle editor initialized",()=>Check(editor.IsReady));
            string[] bodies=["<p>HTMLプレビューテスト</p>","<p><a href='https://www.youtube.com/'>YouTube</a></p>","<p>コード編集後 日本語🔑</p><table><tr><td>表</td></tr></table>"];
            for(int i=0;i<3;i++){
                await editor.SetContent(bodies[i]);if(i==2){await editor.Switch(1);await editor.Switch(0);}string body=await editor.GetContent();
                using var f=new AppForm{Text="CipherCapsule preview regression",Size=new Size(950,700)};using var view=new HtmlView();f.Controls.Add(view);bool closing=false;
                f.Shown+=async(_,_)=>{try{await view.ShowHtml(body,12);for(int j=0;j<100&&!view.Rendered;j++)await Task.Delay(100);Test("modal preview render cycle "+(i+1),()=>Check(view.Rendered));GC.Collect();GC.WaitForPendingFinalizers();}catch(Exception e){Test("modal preview "+(i+1),()=>throw e);}finally{f.Close();}};
                f.FormClosing+=async(_,e)=>{if(closing)return;e.Cancel=true;closing=true;bool ok=await view.CloseAsync();Test("modal preview profile cleanup "+(i+1),()=>Check(ok));f.Close();};
                await AppForm.PostModal(owner,()=>f.ShowDialog(owner));
            }
            GC.Collect();GC.WaitForPendingFinalizers();bool editorCleanup=await editor.CloseAsync();Test("editor profile cleanup after three previews",()=>Check(editorCleanup));
            var locked=new BrowserProfile();Directory.CreateDirectory(locked.PathName);string path=Path.Combine(locked.PathName,"dummy-lock.tmp");using(var held=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){bool ok=await locked.Cleanup();Test("profile lock reported as failure",()=>Check(!ok&&locked.FailureReason.Contains("ロック")));}File.Delete(path);Directory.Delete(locked.PathName);
            Test("specific initialization diagnostics contain no contents",()=>{var failure=new BrowserFailure("コントローラー初期化",new System.Runtime.InteropServices.COMException("",unchecked((int)0x80004004)));Check(failure.UserMessage.Contains("中断")&&failure.UserMessage.Contains("初期化"));});
        }catch(Exception e){Test("lifecycle runtime",()=>throw e);}finally{owner.Close();}};Application.Run(owner);
    }
}
