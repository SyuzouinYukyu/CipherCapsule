extern alias BuilderApp;
using System.Reflection;
using System.Text.Json;
using BuilderType=BuilderApp::CipherCapsule.Builder.BuilderForm;
using EditorType=BuilderApp::CipherCapsule.Builder.HtmlEditor;
internal static partial class Program
{
    static void RunLayout()
    {
        using var form=new BuilderType();
        var ed=(EditorType)typeof(BuilderType).GetField("htmlEditor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        var mode=(ComboBox)typeof(BuilderType).GetField("mode",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        mode.SelectedIndex=1;form.ClientSize=new Size(1240,980);
        form.Shown+=async(_,_)=>{try{
            for(int i=0;i<200&&!ed.IsReady;i++)await Task.Delay(100);
            Test("layout editor initialization",()=>Check(ed.IsReady));
            if(!ed.IsReady)return;
            await ed.SetContent("<p>レイアウト検証</p>");
            foreach(int size in new[]{9,12,17}){
                form.SetSize(size);await Task.Delay(350);
                string geometry=await ed.EditorBrowser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({frame:tinymce.activeEditor.iframeElement.getBoundingClientRect().height,menu:[...document.querySelectorAll('.tox-menubar .tox-mbtn')].map(b=>({text:b.innerText,width:b.clientWidth,height:b.clientHeight,scrollHeight:b.scrollHeight,nowrap:getComputedStyle(b).whiteSpace})),tools:document.querySelector('.tox-editor-header').getBoundingClientRect().height})");
                string json=JsonSerializer.Deserialize<string>(geometry)!;using var doc=JsonDocument.Parse(json);Console.WriteLine("Layout "+size+"pt "+json);
                // Keep enough content height even when the host must scroll.
                Test("HTML editing area remains usable at "+size+"pt",()=>Check(doc.RootElement.GetProperty("frame").GetDouble()>=70));
                string bar=await ed.EditorBrowser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('.tox-statusbar__text-container');return e.clientHeight>=parseFloat(getComputedStyle(e).fontSize)*1.3})()");
                Test("status bar text fits at "+size+"pt",()=>Check(bar=="true"));
                Test("Japanese menu labels fit at "+size+"pt",()=>Check(doc.RootElement.GetProperty("menu").EnumerateArray().All(x=>x.GetProperty("scrollHeight").GetInt32()<=x.GetProperty("height").GetInt32())));
                form.ClientSize=new Size(1400,1200);await Task.Delay(250);string expanded=await ed.EditorBrowser.CoreWebView2.ExecuteScriptAsync("tinymce.activeEditor.iframeElement.getBoundingClientRect().height");
                Test("HTML editing area expands on window resize at "+size+"pt",()=>Check(double.Parse(expanded,System.Globalization.CultureInfo.InvariantCulture)>=70));
                form.ClientSize=new Size(1240,980);await Task.Delay(250);
            }
            await ed.Switch(1);await ed.Switch(0);string content=await ed.GetContent();Test("responsive toolbar keeps visual code contents",()=>Check(content.Contains("レイアウト検証")));
        }catch(Exception e){Test("layout runtime",()=>throw e);}finally{form.Close();}};
        Application.Run(form);
    }
}
