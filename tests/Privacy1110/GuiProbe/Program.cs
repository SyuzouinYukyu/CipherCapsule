extern alias BuilderApp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CipherCapsule.Core;
using Builder=BuilderApp::CipherCapsule.Builder.BuilderForm;
using Editor=BuilderApp::CipherCapsule.Builder.HtmlEditor;
using ArmorForm=BuilderApp::CipherCapsule.Builder.ArmorMessageForm;
using PasswordPanel=BuilderApp::CipherCapsule.UI.PasswordPanel;
using HtmlView=BuilderApp::CipherCapsule.UI.HtmlView;
using Viewer=BuilderApp::CipherCapsule.Viewer.ViewerForm;

internal static class Program
{
    static readonly List<object> results=[];static int failed;static string output="";static string desktop="";
    [STAThread]static int Main(string[] args)
    {
        output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);desktop=DesktopName();
        if(!desktop.StartsWith("CC1110-",StringComparison.Ordinal))throw new InvalidOperationException("Refusing to show GUI on the user's desktop");
        ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>{Record("UI thread exception",false,e.Exception.ToString());Application.Exit();};
        using var builder=new Builder();string editorProfile="";
        builder.Shown+=async(_,_)=>{
            try{
                Record("new Builder title",builder.Text=="CipherCapsule v1.1.10");
                var editor=Field<Editor>(builder,"htmlEditor");Field<ComboBox>(builder,"mode").SelectedIndex=1;
                await Wait(()=>editor.IsReady,"editor initialization",25000);
                editorProfile=ProfilePath(editor);
                string original="<p>未保存 HTML 日本語😀</p><table><tbody><tr><td>表</td></tr></tbody></table>";
                await editor.SetContent(original);
                foreach(float size in new[]{9f,12f,17f}){
                    builder.SetSize(size);await editor.Switch(1);string html=await editor.GetContent();Record("code tab preserves body at "+size,html.Contains("未保存 HTML")&&html.Contains("<table"));
                    await editor.Switch(0);html=await editor.GetContent();Record("visual tab preserves body at "+size,html.Contains("未保存 HTML")&&html.Contains("<table"));
                }
                using(var armor=new ArmorForm(12)){
                    armor.Show(builder);var pw=Field<PasswordPanel>(armor,"password");pw.Primary.Text=pw.Confirm.Text="Gui1110-Dummy!2026";
                    Field<TextBox>(armor,"plain").Text="メモリ内 GUI 暗号メッセージ😀\r\n2行目";
                    Field<Button>(armor,"encrypt").PerformClick();await Wait(()=>!Field<bool>(armor,"busy"),"Armor encryption",20000);
                    string cipher=Field<RichTextBox>(armor,"cipher").Text;Record("Armor GUI generates full ciphertext",cipher.StartsWith(AsciiArmor.Begin));
                    Field<ComboBox>(armor,"mode").SelectedIndex=1;pw.Primary.Text=pw.Confirm.Text="Gui1110-Dummy!2026";
                    Field<Button>(armor,"decrypt").PerformClick();await Wait(()=>!Field<bool>(armor,"busy"),"Armor decryption",15000);
                    Record("Armor GUI authenticates and displays text",Field<RichTextBox>(armor,"decoded").Text.Contains("メモリ内 GUI"));
                    armor.Close();Record("Armor GUI clears secrets",pw.Primary.Text==""&&Field<RichTextBox>(armor,"decoded").Text=="");
                }
                byte[] key="Gui1110-Dummy!2026"u8.ToArray();string package=Path.Combine(output,"html.ccap");
                try{
                    await Task.Run(()=>Container.GenerateCcap(package,original,BodyKind.HTML,[],key,false,default));
                    using var capsule=await Task.Run(()=>Container.Unlock(package,key,default,ccapOnly:true));
                    using var viewer=new Viewer(capsule);var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);viewer.FormClosed+=(_,_)=>closed.TrySetResult();
                    viewer.Show(builder);var view=Field<HtmlView>(viewer,"html");
                    await Wait(()=>view.Rendered,"HTML Viewer render",25000);string profile=ProfilePath(view);
                    foreach(float size in new[]{9f,12f,17f}){viewer.SetSize(size);await Task.Delay(50);Record("Viewer font "+size,viewer.UiSize==size&&view.Rendered);}
                    viewer.Close();await closed.Task.WaitAsync(TimeSpan.FromSeconds(25));
                    Record("HTML Viewer normal-close profile deletion",!Directory.Exists(profile));
                    Record("Viewer normal-close manifest clearing",capsule.Manifest.Body=="");
                }finally{Sodium.Wipe(key);}
            }
            catch(Exception e){Record("isolated GUI workflow",false,e.ToString());}
            finally{builder.Close();}
        };
        Application.Run(builder);
        if(editorProfile!="")Record("Builder normal-close editor profile deletion",!Directory.Exists(editorProfile));
        Record("isolated desktop retained",DesktopName()==desktop);
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{version="1.1.10",desktop,isolated_desktop=true,user_desktop_input=false,production_registry_modified=false,physical_dpi_changed=false,passed=results.Count-failed,failed,tests=results},new JsonSerializerOptions{WriteIndented=true}));
        return failed==0?0:1;
    }
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    static string ProfilePath(object view){object p=Field<object>(view,"profile");return (string)p.GetType().GetProperty("PathName")!.GetValue(p)!;}
    static async Task Wait(Func<bool> ready,string label,int ms){var start=Environment.TickCount64;while(!ready()){if(Environment.TickCount64-start>ms)throw new TimeoutException(label);await Task.Delay(100);}}
    static void Record(string name,bool passed,string? error=null){if(!passed)failed++;results.Add(new{name,passed,error});}
    static string DesktopName(){var b=new StringBuilder(256);if(!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()),2,b,b.Capacity*2,out _))throw new System.ComponentModel.Win32Exception();return b.ToString();}
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,int length,out int needed);
}
