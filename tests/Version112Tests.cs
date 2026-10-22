extern alias BuilderApp;
using CipherCapsule.Core;
using CipherCapsule.UI;
using CipherCapsule.Viewer;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text.Json;
using BuilderType=BuilderApp::CipherCapsule.Builder.BuilderForm;
using SettingsType=BuilderApp::CipherCapsule.Builder.Settings;
using BuilderPassword=BuilderApp::CipherCapsule.UI.PasswordPanel;
internal static partial class Program
{
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    static async Task WaitRendered(HtmlView view){for(int i=0;i<150&&!view.Rendered;i++)await Task.Delay(100);Check(view.Rendered);}
    static void RunV112()
    {
        using(var f=new AppForm()){
            Test("font changes by one point with main and keypad keys",()=>{foreach(var plus in new[]{Keys.Oemplus,Keys.Add}){f.SetSize(12);Check(f.HandleZoomKey(Keys.Control|plus)&&f.UiSize==13);Check(f.HandleZoomKey(Keys.Control|Keys.Shift|plus)&&f.UiSize==14);}foreach(var minus in new[]{Keys.OemMinus,Keys.Subtract}){f.SetSize(12);Check(f.HandleZoomKey(Keys.Control|minus)&&f.UiSize==11);}});
            Test("font lower and upper bounds are 9 and 17",()=>{for(int i=0;i<30;i++)f.HandleZoomKey(Keys.Control|Keys.Add);Check(f.UiSize==17);var same=f.Font;f.HandleZoomKey(Keys.Control|Keys.Add);Check(ReferenceEquals(same,f.Font));for(int i=0;i<30;i++)f.HandleZoomKey(Keys.Control|Keys.Subtract);Check(f.UiSize==9);same=f.Font;f.HandleZoomKey(Keys.Control|Keys.Subtract);Check(ReferenceEquals(same,f.Font));});
        }
        string ini=Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");byte[]? prior=File.Exists(ini)?File.ReadAllBytes(ini):null;
        const string legacy="[Window]\nX=151\nY=171\nWidth=1280\nHeight=1080\nFont=17\nKind=1\n";
        try{
            Test("legacy INI retains every old setting and masks by default",()=>{File.WriteAllText(ini,legacy);var s=SettingsType.Load();Check(s.X==151&&s.Y==171&&s.Width==1280&&s.Height==1080&&s.Font==17&&s.Kind==1&&!s.PasswordRevealed);});
            Test("malformed or duplicate reveal key never resets old settings",()=>{foreach(string value in new[]{"2","-1","true","garbage","", "1\nPasswordRevealed=0"}){File.WriteAllText(ini,legacy+"PasswordRevealed="+value+"\n");var s=SettingsType.Load();Check(!s.PasswordRevealed&&s.X==151&&s.Font==17&&s.Kind==1);}});
            Test("revealed state atomic save and restore",()=>{new SettingsType{PasswordRevealed=true}.Save();Check(SettingsType.Load().PasswordRevealed&&File.ReadAllText(ini).Contains("PasswordRevealed=1"));});
            Test("masked state atomic save and restore",()=>{new SettingsType{PasswordRevealed=false}.Save();Check(!SettingsType.Load().PasswordRevealed&&File.ReadAllText(ini).Contains("PasswordRevealed=0"));});
            Test("INI has only public window settings and one visibility boolean",()=>{var keys=File.ReadAllLines(ini).Where(x=>x.Contains('=')).Select(x=>x.Split('=')[0]).ToHashSet();Check(keys.SetEquals(new[]{"X","Y","Width","Height","Font","Kind","PasswordRevealed"}));});
            using var builder=new BuilderType();var pw=Field<BuilderPassword>(builder,"password");var generate=Field<Button>(builder,"generate");var cancel=Field<Button>(builder,"cancel");
            builder.Shown+=(_,_)=>{try{
                Test("Builder default masked confirmation and generate disabled",()=>Check(!pw.Revealed&&pw.Primary.UseSystemPasswordChar&&pw.Confirm.Visible&&!generate.Enabled&&!cancel.Enabled));
                pw.Primary.Text="Visibility-only dummy 112";pw.SetRevealed(true);
                Test("Builder reveal preserves primary and hides confirmation",()=>Check(pw.Revealed&&!pw.Primary.UseSystemPasswordChar&&!pw.Confirm.Visible&&pw.Primary.Text=="Visibility-only dummy 112"&&generate.Enabled));
                pw.SetRevealed(false);
                Test("Builder remask requires fresh confirmation without clearing primary",()=>Check(!pw.Revealed&&pw.Confirm.Visible&&pw.Confirm.Text==""&&pw.Primary.Text=="Visibility-only dummy 112"&&!generate.Enabled));
                pw.Confirm.Text=pw.Primary.Text;
                Test("only attachment paste remains and candidate widgets are absent",()=>Check(AllControls(builder).Count(c=>c.Text=="貼り付け")==1&&!AllControls(builder).Any(c=>c.Text=="EXE出力先候補"||c.AccessibleName=="出力EXE保存先候補")));
                Test("body and attachment drag targets remain",()=>Check(Field<TextBox>(builder,"editor").AllowDrop&&Field<ListView>(builder,"files").AllowDrop));
                foreach(int size in new[]{9,12,17}){builder.SetSize(size);builder.PerformLayout();generate.Parent!.PerformLayout();Test("large action buttons fit without overlap at "+size+"pt",()=>Check(generate.Width>=180*size/12f&&generate.Height>=52*size/12f&&cancel.Width>=150*size/12f&&generate.Right+8<=cancel.Left&&generate.Width>=TextRenderer.MeasureText(generate.Text,generate.Font).Width+generate.Padding.Horizontal));}
                var busy=typeof(BuilderType).GetMethod("SetBusy",BindingFlags.NonPublic|BindingFlags.Instance)!;busy.Invoke(builder,[true]);
                Test("busy state disables generate and enables cancel",()=>Check(!generate.Enabled&&cancel.Enabled&&!pw.Enabled));busy.Invoke(builder,[false]);
                Test("idle state restores validity and retains inputs",()=>Check(generate.Enabled&&!cancel.Enabled&&pw.Primary.Text=="Visibility-only dummy 112"));
                using var dialog=(SaveFileDialog)typeof(BuilderType).GetMethod("CreateSaveDialog",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
                Test("SaveFileDialog uses documents and exe defaults with overwrite prompt",()=>Check(dialog.FileName=="SecureMessage.exe"&&dialog.InitialDirectory==Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)&&dialog.DefaultExt=="exe"&&dialog.AddExtension&&dialog.OverwritePrompt));
                pw.SetRevealed(true);
            }catch(Exception e){Test("Builder v112 runtime",()=>throw e);}finally{builder.Close();}};
            Application.Run(builder);
            Test("normal Builder close stores visibility but never password",()=>Check(SettingsType.Load().PasswordRevealed&&!File.ReadAllText(ini).Contains("Visibility-only dummy 112")));
        }finally{if(prior!=null)File.WriteAllBytes(ini,prior);else File.Delete(ini);}
        RunZoomAfter();
    }
    static void RunZoomAfter()
    {
        string file=Path.Combine(Work,"zoom-v112.exe");if(!File.Exists(file))Gen(file,"<h1>倍率検証</h1>"+string.Concat(Enumerable.Range(1,300).Select(i=>$"<p>段落 {i} 日本語と長文表示</p>")),BodyKind.HTML,[]);using var capsule=Unlock(file);using var form=new ViewerForm(capsule);
        var view=Field<HtmlView>(form,"html");var web=view.Controls.OfType<WebView2>().Single();var events=new List<string>();int starts=0;string original="";
        form.Shown+=async(_,_)=>{try{
            form.SetSize(17);await WaitRendered(view);var c=web.CoreWebView2;original=c.Source;
            Test("latest requested pre-initialization zoom is applied",()=>Check(Math.Abs(web.ZoomFactor-17/12.0)<0.00001));
            Test("Viewer user zoom disabled with script and resource protections intact",()=>Check(!c.Settings.IsZoomControlEnabled&&!c.Settings.IsScriptEnabled&&!c.Settings.IsWebMessageEnabled&&!c.Settings.AreHostObjectsAllowed&&!c.Settings.AreBrowserAcceleratorKeysEnabled));
            string baseFont=await c.ExecuteScriptAsync("getComputedStyle(document.body).fontSize");Test("HTML base font remains 12pt",()=>Check(baseFont=="\"16px\""));
            c.NavigationStarting+=(_,_)=>starts++;c.NavigationCompleted+=(_,e)=>events.Add(e.WebErrorStatus.ToString());
            for(int i=0;i<64;i++){form.SetSize(9+i%9);await Task.Delay(3);}await Task.Delay(300);
            Test("64 size changes perform zero HTML navigation",()=>Check(starts==0&&c.Source==original));
            Test("64 size changes keep rendered HTML without failure overlays",()=>Check(view.Rendered&&!view.Controls.OfType<Label>().Any()&&events.Count==0));
            Test("font and browser zoom agree after rapid changes",()=>Check(Math.Abs(web.ZoomFactor-form.UiSize/12.0)<0.00001));
            Test("renamed algorithm link present",()=>Check(AllControls(form).OfType<LinkLabel>().Any(l=>l.Text=="暗号アルゴリズム仕様")));
            await c.ExecuteScriptAsync("window.scrollTo(0,document.body.scrollHeight)");string bottom=await c.ExecuteScriptAsync("window.scrollY>0 && window.scrollY+innerHeight>=document.documentElement.scrollHeight-3");Test("zoomed long document remains reachable at end",()=>Check(bottom=="true"));
            // Genuine latest failures still notify, while repeated failures reuse one overlay.
            bool reject=true;c.NavigationStarting+=(_,e)=>{if(reject)e.Cancel=true;};
            await view.ShowHtml("<p>latest failure one</p>",12);for(int i=0;i<60&&!view.Controls.OfType<Label>().Any();i++)await Task.Delay(50);
            Test("latest navigation failure is reported",()=>Check(!view.Rendered&&view.Controls.OfType<Label>().Single().Text.Contains("HTMLを表示できません")));
            await view.ShowHtml("<p>latest failure two</p>",12);await Task.Delay(250);Test("failure notifications do not accumulate",()=>Check(view.Controls.OfType<Label>().Count()==1));
            reject=false;await view.ShowHtml("<p>recovered latest document</p>",12);await WaitRendered(view);Test("successful latest navigation removes prior error overlay",()=>Check(!view.Controls.OfType<Label>().Any()));
            await Task.WhenAll(Enumerable.Range(0,8).Select(i=>view.ShowHtml("<p>latest document "+i+"</p>",12)));await WaitRendered(view);await Task.Delay(250);
            Test("stale canceled navigation never covers latest success",()=>Check(view.Rendered&&!view.Controls.OfType<Label>().Any()));
            File.WriteAllText(Path.Combine(Root,"../Work/zoom-after.json"),JsonSerializer.Serialize(new{zoomChanges=64,zoomNavigationStarts=0,zoomErrors=0,latestFailureNotification=true,duplicateOverlays=false,staleFailureOverlay=false}));
        }catch(Exception e){Test("zoom v112 runtime",()=>throw e);}finally{double last=web.ZoomFactor;var close=view.CloseAsync();view.SetDisplaySize(9);await close;view.SetDisplaySize(17);Test("closing and closed browser ignore zoom safely",()=>Check(web.IsDisposed));form.Close();}};
        Application.Run(form);
    }
}
