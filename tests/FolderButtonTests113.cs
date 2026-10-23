extern alias BuilderApp;
using System.Reflection;
using System.ComponentModel;
using System.Diagnostics;
using Folder113=BuilderApp::CipherCapsule.Builder.OutputFolder;
using Builder113Type=BuilderApp::CipherCapsule.Builder.BuilderForm;
using Dialog113=BuilderApp::CipherCapsule.UI.ReadOnlyDialog;

internal static partial class Program
{
    static Dialog113 Result113(string title,string? path=null)=>
        (Dialog113)typeof(Builder113Type).GetMethod("CreateResultDialog",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,["表示文中の別パス C:\\not-the-output\\wrong.exe",title,path])!;
    static void RunFolder113()
    {
        string area=Path.Combine(Work,"フォルダを開く 日本語🔑=113");Directory.CreateDirectory(area);
        string output=Path.Combine(area,"SecureMessage.exe");File.WriteAllText(output,"COMPLETED-EXE-SENTINEL");
        Test("Explorer uses one absolute Unicode folder argument and never runs output EXE",()=>{
            var p=Folder113.StartInfo(output);Check(p.FileName==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")&&!p.UseShellExecute&&p.ArgumentList.Count==1&&p.ArgumentList[0]==area&&p.ArgumentList[0]!=output&&p.Arguments=="");
        });
        Test("confirmed output path is normalized before parent selection",()=>{string nested=Path.Combine(area,"child");Directory.CreateDirectory(nested);var p=Folder113.StartInfo(Path.Combine(nested,"..","SecureMessage.exe"));Check(p.ArgumentList.Single()==area);});
        Test("shell metacharacters remain a literal directory argument",()=>{string dir=Path.Combine(area,"空白 & % ; ' = folder");Directory.CreateDirectory(dir);var p=Folder113.StartInfo(Path.Combine(dir,"dummy.exe"));Check(p.ArgumentList.Single()==dir&&p.Arguments=="");});
        Test("successful process dispatch keeps completed EXE unchanged",()=>{ProcessStartInfo? info=null;string? error=Folder113.TryOpen(output,p=>info=p);Check(error==null&&info?.ArgumentList.Single()==area&&File.ReadAllText(output)=="COMPLETED-EXE-SENTINEL");});
        Test("missing folder returns Japanese warning without launch",()=>{bool launched=false;string? error=Folder113.TryOpen(Path.Combine(area,"missing","done.exe"),_=>launched=true);Check(!launched&&error!=null&&error.Contains("存在しません")&&error.Contains("暗号化は完了"));});
        Test("deleted output folder is detected at click time",()=>{string dir=Path.Combine(area,"removed");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"done.exe");Directory.Delete(dir);string? error=Folder113.TryOpen(path,_=>throw new Exception("must not launch"));Check(error!=null&&error.Contains("存在しません"));});
        Test("access denied becomes separate Japanese warning",()=>{var error=Folder113.TryOpen(output,_=>throw new UnauthorizedAccessException());Check(error!=null&&error.Contains("アクセスできません")&&File.ReadAllText(output)=="COMPLETED-EXE-SENTINEL");});
        Test("Explorer launch failure cannot invalidate completed output",()=>{var error=Folder113.TryOpen(output,_=>throw new Win32Exception(2));Check(error!=null&&error.Contains("暗号化は完了")&&File.ReadAllText(output)=="COMPLETED-EXE-SENTINEL");});
        Test("relative and device paths never reach process launch",()=>{foreach(string path in new[]{"relative.exe",@"\\?\C:\fake\a.exe",@"\\.\pipe\fake"}){bool launched=false;Check(Folder113.TryOpen(path,_=>launched=true)!=null&&!launched);}});
        foreach(string title in new[]{"暗号化結果","暗号化失敗","キャンセル","CipherCapsule — ライセンス・注意事項","暗号アルゴリズム仕様","一般結果"})
            Test("no folder action for "+title,()=>{using var f=Result113(title);Check(AllControls(f).OfType<Button>().Count()==1&&f.CloseButton.Text=="閉じる");});
        foreach(int pt in new[]{9,12,17})foreach(int width in new[]{700,900,1200}){
            Test($"success action group centered and readable at {pt}pt width {width}",()=>{
                using var f=Result113("暗号化成功",output);f.Content.AppendText($" {pt}/{width}");f.SetSize(pt);f.Width=width;Exception? failure=null;string stage="initial layout";
                f.Shown+=(_,_)=>{try{f.PerformLayout();
                var buttons=AllControls(f).OfType<Button>().ToArray();Check(buttons.Length==2);
                var open=buttons.Single(b=>b.Text=="保存先フォルダを開く");var close=f.CloseButton;
                Console.WriteLine($"LAYOUT {pt}/{width} open={open.Bounds} close={close.Bounds} group={open.Parent!.Bounds} row={open.Parent.Parent!.ClientSize} selected={f.Content.SelectionStart}/{f.Content.SelectionLength} focus={f.ActiveControl?.Text} measured={TextRenderer.MeasureText(open.Text,open.Font).Width} padding={open.Padding.Horizontal}");
                Check(open.Parent==close.Parent&&open.Left<close.Left&&open.Top==close.Top&&close.Left-open.Right>=11&&open.Height>=40&&close.Height>=40&&!open.Bounds.IntersectsWith(close.Bounds));stage="text fits";
                Check(open.Width>=TextRenderer.MeasureText(open.Text,open.Font).Width+open.Padding.Horizontal);
                var group=open.Parent!;var row=group.Parent!;Check(Math.Abs(group.Left+group.Width/2.0-row.ClientSize.Width/2.0)<=2&&group.Right<=row.ClientSize.Width);stage="initial selection and focus";
                Check(f.Content.SelectionStart==0&&f.Content.SelectionLength==0&&f.ActiveControl==close);
                stage="first body focus";f.Content.Focus();Check(f.Content.SelectionLength==0);stage="select all";f.Content.SelectAll();Check(f.Content.SelectedText==f.Content.Text&&f.Content.ShortcutsEnabled);
                stage="clipboard copy";bool copied=false;int attempts=0;for(;attempts<10;attempts++){f.Content.Copy();Application.DoEvents();Thread.Sleep(30);if(Clipboard.GetText()==f.Content.Text){copied=true;break;}}Console.WriteLine($"Clipboard settled: {copied}, attempts={attempts+1}");Check(copied);stage="close click";close.PerformClick();Check(!f.Visible);
                }catch(Exception e){failure=new Exception(stage+": "+e.Message,e);}finally{f.Close();}};Application.Run(f);if(failure!=null)throw failure;
            });
        }
    }
}
