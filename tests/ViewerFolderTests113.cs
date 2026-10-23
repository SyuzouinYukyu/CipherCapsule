using CipherCapsule.Core;
using CipherCapsule.UI;
using CipherCapsule.Viewer;
using System.Diagnostics;
using System.ComponentModel;
using Container=CipherCapsule.Core.Container;
internal static partial class Program
{
    static Button OpenSavedButton(ReadOnlyDialog f)=>AllControls(f).OfType<Button>().Single(b=>b.Text=="保存先フォルダを開く");
    static void RunViewerFolderDpi113()
    {
        string folder=Path.Combine(Work,"DPI 保存=113");Directory.CreateDirectory(folder);
        foreach(bool batch in new[]{false,true})Test($"Viewer {(batch?"batch":"single")} 200 percent layout scaling",()=>{
            using var f=batch?AttachmentResults.Batch(new(1,0,0,folder,"保存完了",false,TimeSpan.Zero,[])):AttachmentResults.Single("保存完了",Path.Combine(folder,"a.pdf"));
            f.SetSize(17);Exception? failure=null;f.Shown+=(_,_)=>{try{
                f.Scale(new SizeF(2,2));f.Width=1800;f.PerformLayout();var open=OpenSavedButton(f);var close=f.CloseButton;var group=open.Parent!;var row=group.Parent!;
                Check(open.Height>=40&&close.Height>=40&&!open.Bounds.IntersectsWith(close.Bounds)&&open.Width>=TextRenderer.MeasureText(open.Text,open.Font).Width+open.Padding.Horizontal&&group.Right<=row.ClientSize.Width&&Math.Abs(group.Left+group.Width/2.0-row.ClientSize.Width/2.0)<=2);
            }catch(Exception ex){failure=ex;}finally{f.Close();}};Application.Run(f);if(failure!=null)throw failure;
        });
    }
    static int PrepareViewerFolderGui113()
    {
        string gui=Path.GetFullPath(Path.Combine(Root,"../Work/viewer-folder-gui"));if(Directory.Exists(gui))throw new IOException("GUI fixture already exists");Directory.CreateDirectory(gui);
        string input=Path.GetFullPath(Path.Combine(Root,"../Work/gui/input"));byte[] pw=Limits.Utf8.GetBytes("Gui113-Dummy!2026");
        try{
            foreach(var kind in new[]{BodyKind.TEXT,BodyKind.HTML}){
                string output=Path.Combine(gui,kind+"-folder.exe");string[] files=kind==BodyKind.TEXT?[Path.Combine(input,"資料.pdf"),Path.Combine(input,"大容量A.bin"),Path.Combine(input,"大容量B.bin"),Path.Combine(input,"文章.txt"),Path.Combine(input,"別/資料.pdf")]:[Path.Combine(input,"資料.pdf"),Path.Combine(input,"文章.txt")];
                using var template=File.OpenRead(Path.Combine(Root,"templates/CipherCapsule_Viewer.exe"));Container.Generate(template,output,kind==BodyKind.TEXT?"保存先ボタン追加の限定GUI試験":"<h1>保存先ボタン追加</h1><table><tr><td>HTML本文・表</td></tr></table>",kind,files,pw,true,default);
            }
            Console.WriteLine(gui);return 0;
        }finally{Sodium.Wipe(pw);}
    }
    static void RunViewerFolder113()
    {
        string area=Path.Combine(Work,"添付 保存🔑=113 & literal");Directory.CreateDirectory(area);
        string input=Path.Combine(area,"資料.pdf"),other=Path.Combine(area,"文章.txt"),pack=Path.Combine(area,"input.exe");
        File.WriteAllText(input,"PDF dummy");File.WriteAllText(other,"TXT dummy");Gen(pack,"添付保存結果",BodyKind.TEXT,[input,other]);using var c=Unlock(pack);
        string single=AttachmentNames.SinglePath(Path.Combine(area,"別名.avi"),"資料.pdf");c.Save(0,single,false,default);
        Test("single completed extension path opens parent without running attachment",()=>{
            ProcessStartInfo? info=null;using var f=AttachmentResults.Single("表示上の偽パス C:\\wrong\\file.exe",single,p=>info=p);f.Show();Application.DoEvents();OpenSavedButton(f).PerformClick();
            Check(info!=null&&info.FileName==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")&&!info.UseShellExecute&&info.Arguments==""&&info.ArgumentList.Count==1&&info.ArgumentList[0]==area&&Hash(single)==Hash(input));f.Close();
        });
        Test("folder with Japanese spaces equals Unicode and shell metacharacters is literal",()=>{var p=SavedFolder.StartInfo(area);Check(p.ArgumentList.Single()==area&&p.Arguments==""&&!p.UseShellExecute);});
        Test("relative device and control paths rejected before dispatch",()=>{foreach(var path in new[]{"relative",@"\\?\C:\fake",@"\\.\pipe\fake",area+"\n"}){bool launched=false;Check(SavedFolder.TryOpen(path,_=>launched=true)!=null&&!launched);}});
        Test("single file parent normalizes confirmed full path",()=>{string sub=Path.Combine(area,"child");Directory.CreateDirectory(sub);Check(SavedFolder.Parent(Path.Combine(sub,"..","別名.avi.pdf"))==area);});
        Test("missing folder warning does not invalidate saved file",()=>{bool launched=false;var error=SavedFolder.TryOpen(Path.Combine(area,"missing"),_=>launched=true);Check(!launched&&error!=null&&error.Contains("存在しません")&&error.Contains("保存結果は変わりません")&&Hash(single)==Hash(input));});
        Test("access denied remains separate from save outcome",()=>{string? error=SavedFolder.TryOpen(area,_=>throw new UnauthorizedAccessException());Check(error!=null&&error.Contains("アクセスできません")&&Hash(single)==Hash(input));});
        Test("Explorer failure remains separate from save outcome",()=>{string? error=SavedFolder.TryOpen(area,_=>throw new Win32Exception(2));Check(error!=null&&error.Contains("開けません")&&Hash(single)==Hash(input));});
        Test("missing directory checked again at button click and dialog stays closable",()=>{string folder=Path.Combine(area,"move-away");Directory.CreateDirectory(folder);string? warning=null;using var f=AttachmentResults.Single("保存成功",Path.Combine(folder,"a.pdf"),_=>throw new Exception("must not start"),s=>warning=s);Directory.Delete(folder);f.Show();Application.DoEvents();OpenSavedButton(f).PerformClick();Check(warning!=null&&warning.Contains("存在しません")&&f.Visible);f.CloseButton.PerformClick();Check(!f.Visible);});
        string fullFolder=Path.Combine(area,"full");Directory.CreateDirectory(fullFolder);var full=AttachmentBatch.Save(c,fullFolder,default);
        Test("actual successful batch opens selected folder and keeps count text",()=>{ProcessStartInfo? p=null;using var f=AttachmentResults.Batch(full,x=>p=x);f.Show();Application.DoEvents();OpenSavedButton(f).PerformClick();Check(full.Succeeded==2&&full.Failed==0&&full.Unprocessed==0&&p?.ArgumentList.Single()==fullFolder&&f.Content.Text.Contains("保存成功：2件")&&f.Content.Text.Contains("保存失敗：0件")&&f.Content.Text.Contains("未処理：0件"));for(int i=0;i<2;i++)Check(Hash(full.Saved[i].Path)==Hash(i==0?input:other));f.Close();});
        string cancelFolder=Path.Combine(area,"cancel");Directory.CreateDirectory(cancelFolder);using var ct=new CancellationTokenSource();var canceled=AttachmentBatch.Save(c,cancelFolder,ct.Token,new InlineProgress<BatchProgress>(p=>{if(p.Completed==1)ct.Cancel();}));
        Test("actual cancel after success retains folder action and 1 0 1 counts",()=>{using var f=AttachmentResults.Batch(canceled);Check(canceled.Canceled&&canceled.Succeeded==1&&canceled.Failed==0&&canceled.Unprocessed==1&&OpenSavedButton(f)!=null&&f.Content.Text.Contains("未処理：1件")&&Hash(canceled.Saved[0].Path)==Hash(input));});
        string failFolder=Path.Combine(area,"partial-failure");Directory.CreateDirectory(failFolder);var partial=AttachmentBatch.Save(c,failFolder,default,new InlineProgress<BatchProgress>(p=>{if(p.Completed==1)File.WriteAllText(Path.Combine(failFolder,"文章.txt"),"EXTERNAL");}));
        Test("actual partial failure keeps folder action and 1 1 0 counts",()=>{using var f=AttachmentResults.Batch(partial);Check(partial.Succeeded==1&&partial.Failed==1&&partial.Unprocessed==0&&OpenSavedButton(f)!=null&&f.Content.Text.Contains("保存失敗：1件")&&Hash(partial.Saved[0].Path)==Hash(input));});
        foreach(var item in new[]{new BatchSaveResult(0,0,2,area,"キャンセル",true,TimeSpan.Zero,[]),new BatchSaveResult(0,1,1,area,"失敗",false,TimeSpan.Zero,[]),new BatchSaveResult(0,0,0,area,"添付なし",false,TimeSpan.Zero,[])})
            Test($"zero successes no action canceled={item.Canceled} failed={item.Failed} remaining={item.Unprocessed}",()=>{using var f=AttachmentResults.Batch(item);Check(AllControls(f).OfType<Button>().Single()==f.CloseButton);});
        Test("general and specification read-only dialogs unchanged",()=>{foreach(var title in new[]{"暗号アルゴリズム仕様","復号失敗","パスワード誤り","保存失敗","保存キャンセル"}){using var f=new ReadOnlyDialog(title,"一般結果",new Size(900,520));Check(AllControls(f).OfType<Button>().Single()==f.CloseButton);}});
        foreach(bool batch in new[]{false,true})foreach(int pt in new[]{9,12,17})foreach(int width in new[]{700,900,1200})
            Test($"Viewer {(batch?"batch":"single")} centered copy close {pt}pt width {width}",()=>{
                using var f=batch?AttachmentResults.Batch(full):AttachmentResults.Single(string.Join("\r\n",Enumerable.Range(0,80).Select(i=>$"先頭から表示 {i}")),single);
                f.SetSize(pt);f.Width=width;Exception? failure=null;
                f.Shown+=(_,_)=>{try{f.PerformLayout();var open=OpenSavedButton(f);var close=f.CloseButton;var group=open.Parent!;var row=group.Parent!;
                    Check(open.Parent==close.Parent&&open.Left<close.Left&&open.Top==close.Top&&close.Left-open.Right>=11&&open.Height>=40&&close.Height>=40&&!open.Bounds.IntersectsWith(close.Bounds));
                    Check(open.Width>=TextRenderer.MeasureText(open.Text,open.Font).Width+open.Padding.Horizontal&&close.Width>=TextRenderer.MeasureText(close.Text,close.Font).Width+close.Padding.Horizontal);
                    Check(Math.Abs(group.Left+group.Width/2.0-row.ClientSize.Width/2.0)<=2&&group.Right<=row.ClientSize.Width);
                    Check(f.Content.SelectionStart==0&&f.Content.SelectionLength==0&&f.ActiveControl==close&&f.Content.GetCharIndexFromPosition(Point.Empty)==0&&f.Content.ScrollBars==ScrollBars.Both);
                    f.Content.Focus();Check(f.Content.SelectionLength==0);f.Content.SelectAll();Check(f.Content.ShortcutsEnabled&&f.Content.SelectedText==f.Content.Text);
                    bool copied=false;for(int i=0;i<10;i++){f.Content.Copy();Application.DoEvents();Thread.Sleep(30);if(Clipboard.GetText()==f.Content.Text){copied=true;break;}}Check(copied);
                    close.PerformClick();Check(!f.Visible);
                }catch(Exception ex){failure=ex;}finally{f.Close();}};Application.Run(f);if(failure!=null)throw failure;
            });
    }
}
