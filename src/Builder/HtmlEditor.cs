using CipherCapsule.Core;
using CipherCapsule.UI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
namespace CipherCapsule.Builder;
public sealed class HtmlEditor : UserControl
{
    readonly WebView2 web=new(){Dock=DockStyle.Fill};readonly TextBox code=new(){Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,AcceptsReturn=true,AcceptsTab=true,MaxLength=Limits.Body,AccessibleName="HTMLコード"};
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};readonly BrowserProfile profile=new();
    readonly EditorSwitchQueue switches;readonly Queue<EditorSwitchTrace> switchTrace=new();long scriptRevision;
    readonly SemaphoreSlim uiFontSync=new(1,1);long uiFontRevision;
    readonly Dictionary<string,byte[]> assets=[];readonly List<MemoryStream> responses=[];
    readonly string origin="https://editor.invalid";bool ready,closed,switching;string pending="";Task? initialization;Task<bool>? cleanup;
    [System.ComponentModel.Browsable(false),System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string[],Task>? FileDropRequested{get;set;}
    public string CleanupFailure=>profile.FailureReason;
    public bool IsReady=>ready;public WebView2 EditorBrowser=>web;public bool IsCode=>tabs.SelectedIndex==1;
    public HtmlEditor()
    {
        Dock=DockStyle.Fill;var visual=new TabPage("ビジュアル");var source=new TabPage("コード");visual.Controls.Add(web);source.Controls.Add(code);tabs.TabPages.AddRange([visual,source]);Controls.Add(tabs);
        switches=new(()=>tabs.SelectedIndex,PrepareSwitch,CommitSwitch,SwitchBusy,SwitchFailed,RememberSwitch);
        tabs.Selecting+=async(_,e)=>{if(switching)return;e.Cancel=true;await Switch(e.TabPageIndex);};
        // Clicking the currently visible tab while a request is pending must also cancel that request.
        tabs.MouseDown+=async(_,e)=>{if(e.Button!=MouseButtons.Left||switching)return;for(int i=0;i<tabs.TabCount;i++)if(tabs.GetTabRect(i).Contains(e.Location)){await Switch(i);break;}};
        Load+=async(_,_)=>{try{initialization??=Initialize();await initialization;}catch(Exception ex){AppForm.Error(this,ex);}};
    }
    async Task Initialize()
    {
        if(ready||closed)return;
        try{_=CoreWebView2Environment.GetAvailableBrowserVersionString();}catch{throw new ArgumentException("HTML編集にはMicrosoft Edge WebView2 Runtimeが必要です。公式導入先：https://developer.microsoft.com/microsoft-edge/webview2/");}
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("EditorAssets")!)using(var zip=new ZipArchive(resource,ZipArchiveMode.Read))foreach(var e in zip.Entries){using var s=e.Open();using var m=new MemoryStream();s.CopyTo(m);assets["/"+e.FullName]=m.ToArray();}
        var env=await profile.Create();if(closed)return;try{await web.EnsureCoreWebView2Async(env);}catch(Exception error){throw new BrowserFailure("HTMLエディターの初期化",error);}profile.ControllerCreated();if(closed)return;var c=web.CoreWebView2;var s0=c.Settings;
        s0.IsWebMessageEnabled=true;s0.AreHostObjectsAllowed=false;s0.AreDevToolsEnabled=false;s0.AreDefaultScriptDialogsEnabled=false;s0.IsPasswordAutosaveEnabled=false;s0.IsGeneralAutofillEnabled=false;s0.IsStatusBarEnabled=false;
        c.PermissionRequested+=(_,e)=>{e.State=CoreWebView2PermissionState.Deny;e.Handled=true;};c.NewWindowRequested+=(_,e)=>e.Handled=true;c.DownloadStarting+=(_,e)=>e.Cancel=true;c.LaunchingExternalUriScheme+=(_,e)=>e.Cancel=true;
        c.NavigationStarting+=(_,e)=>{if(e.Uri!=origin+"/index.html")e.Cancel=true;};
        c.FrameNavigationStarting+=(_,e)=>{if(e.Uri is not("about:blank" or "about:srcdoc"))e.Cancel=true;};
        c.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
        c.WebResourceRequested+=(_,e)=>{
            if(Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var u)&&u.GetLeftPart(UriPartial.Authority)==origin&&assets.TryGetValue(u.AbsolutePath,out var bytes)){
                var m=new MemoryStream(bytes,false);responses.Add(m);string type=Path.GetExtension(u.AbsolutePath) switch{".js"=>"text/javascript",".css"=>"text/css",".svg"=>"image/svg+xml",".woff"=>"font/woff",".woff2"=>"font/woff2",_=>"text/html"};
                e.Response=env.CreateWebResourceResponse(m,200,"OK","Content-Type: "+type+"; charset=utf-8\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src data:; font-src 'self'; frame-src 'self' about:; connect-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'");
            }else e.Response=env.CreateWebResourceResponse(null,403,"Blocked","Cache-Control: no-store");
        };
        c.WebMessageReceived+=ReceiveWebMessage;
        web.AllowExternalDrop=false;web.KeyDown+=(_,e)=>{Keys k=e.KeyCode;if(e.Control&&k is Keys.Add or Keys.Oemplus or Keys.Subtract or Keys.OemMinus){e.Handled=true;e.SuppressKeyPress=true;if(FindForm() is AppForm f)f.SetSize(f.UiSize+(k is Keys.Add or Keys.Oemplus?1:-1));}};
        c.Navigate(origin+"/index.html");
        for(int i=0;i<150;i++){if(closed)return;try{if(await c.ExecuteScriptAsync("window.ccReady === true")=="true"){ready=true;await BindFileDrops();await SetVisual(pending);await ApplyUiFont();return;}}catch{}await Task.Delay(100);}
        throw new IOException("オフラインHTMLエディターを初期化できませんでした。");
    }
    void ReceiveWebMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(e.Source!=origin+"/index.html"||closed)return;
        try{
            using var message=JsonDocument.Parse(e.WebMessageAsJson);
            string? type=message.RootElement.GetProperty("type").GetString();
            if(type=="ccapDrop"){
                var objects=e.AdditionalObjects;
                if(FileDropRequested==null||objects==null||objects.Count==0||objects.Count>100||objects.Any(o=>o is not CoreWebView2File))return;
                string[] paths=objects.Cast<CoreWebView2File>().Select(f=>f.Path).ToArray();
                BeginInvoke(new Action(async()=>{if(!closed&&FileDropRequested!=null)try{await FileDropRequested(paths);}catch(Exception ex){AppForm.Error(this,ex);}}));
                return;
            }
            string action=type=="clipboard"?message.RootElement.GetProperty("action").GetString()??"":"";
            BeginInvoke(new Action(async()=>{if(closed)return;try{if(type=="pickImage")await PickImage();else if(type=="clipboard")await ClipboardAction(action);}catch(Exception ex){AppForm.Error(this,ex);}}));
        }catch{}
    }
    async Task BindFileDrops()
    {
        if(FileDropRequested==null)return;
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("CcapDropScript")??throw new IOException("CCAPドロップ処理を読み込めません。");
        using var reader=new StreamReader(resource);
        await web.CoreWebView2.ExecuteScriptAsync(await reader.ReadToEndAsync());
        web.AllowExternalDrop=true;
    }
    async Task PickImage()
    {
        using var d=new OpenFileDialog{Filter="画像 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",Title="小容量のローカル画像を挿入（256KiB以下）"};if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(new FileInfo(d.FileName).Length>256*1024)throw new ArgumentException("画像は256KiB以下にしてください。");
        string prefix=Path.GetExtension(d.FileName).Equals(".png",StringComparison.OrdinalIgnoreCase)?"data:image/png;base64,":"data:image/jpeg;base64,";
        string? data=SafeHtml.ImageData(prefix+Convert.ToBase64String(File.ReadAllBytes(d.FileName)));if(data==null)throw new ArgumentException("画像を検証できません。PNG/JPEG、最大4096px、総画素数4Mi以内で指定してください。");
        await web.CoreWebView2.ExecuteScriptAsync("window.ccImageCallback?.("+JsonSerializer.Serialize(data)+",{alt:''});window.ccImageCallback=null;");
    }
    async Task<string> EditorScript(string command,string? html=null,CancellationToken ct=default)
    {
        if(closed||web.CoreWebView2==null)throw new IOException("HTMLエディターを使用できません。");
        ct.ThrowIfCancellationRequested();long revision=Interlocked.Increment(ref scriptRevision);
        long expires=DateTimeOffset.UtcNow.AddSeconds(6).ToUnixTimeMilliseconds();
        string script="window.ccEditorBridge.run("+JsonSerializer.Serialize(command)+","+JsonSerializer.Serialize(revision)+","+JsonSerializer.Serialize(expires)+","+JsonSerializer.Serialize(html)+");";
        string reply=await web.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(TimeSpan.FromSeconds(6),ct);
        ct.ThrowIfCancellationRequested();
        using var json=JsonDocument.Parse(reply);var result=json.RootElement;
        if(result.ValueKind!=JsonValueKind.Object||!result.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True||!result.TryGetProperty("value",out var value)||value.ValueKind!=JsonValueKind.String)throw new IOException("HTMLエディターの応答を検証できません。内容は変更せず再試行できます。");
        return value.GetString()!;
    }
    async Task SetVisual(string value,CancellationToken ct=default){await EditorScript("write",value,ct);}
    async Task ClipboardAction(string action)
    {
        if(!ready)return;
        if(action is "copy" or "cut")
        {
            string text=JsonSerializer.Deserialize<string>(await web.CoreWebView2.ExecuteScriptAsync("tinymce.activeEditor.selection.getContent({format:'text'})"))??"";
            string html=JsonSerializer.Deserialize<string>(await web.CoreWebView2.ExecuteScriptAsync("tinymce.activeEditor.selection.getContent({format:'html'})"))??"";
            if(text.Length==0&&html.Length==0)return;var data=new DataObject();data.SetText(text,TextDataFormat.UnicodeText);data.SetText(ClipboardHtml.Encode(SafeHtml.Fragment(html)),TextDataFormat.Html);Clipboard.SetDataObject(data,true);
            if(action=="cut")await web.CoreWebView2.ExecuteScriptAsync("(()=>{const e=tinymce.activeEditor;e.undoManager.transact(()=>e.execCommand('Delete'));})()");return;
        }
        if(action is not("paste" or "plain"))return;
        string plain=Clipboard.ContainsText()?Clipboard.GetText():"";Limits.BodyText(plain);
        string markup=action=="paste"&&Clipboard.ContainsText(TextDataFormat.Html)?SafeHtml.Fragment(ClipboardHtml.Decode(Clipboard.GetText(TextDataFormat.Html))):System.Net.WebUtility.HtmlEncode(plain).Replace("\r\n","\n").Replace("\r","\n").Replace("\n","<br>");
        string? link=action=="paste"?SafeHtml.SafeUrl(plain):null;
        await web.CoreWebView2.ExecuteScriptAsync("(()=>{const e=tinymce.activeEditor;const url="+JsonSerializer.Serialize(link)+";e.focus();e.undoManager.transact(()=>{if(url&&!e.selection.isCollapsed())e.execCommand('mceInsertLink',false,{href:url});else e.insertContent("+JsonSerializer.Serialize(markup)+");});})()");
    }
    async Task ApplyUiFont()
    {
        if(!ready||closed)return;
        long revision=Interlocked.Increment(ref uiFontRevision);
        await uiFontSync.WaitAsync();
        try
        {
            if(!ready||closed||revision!=uiFontRevision)return;
            float size=FindForm() is AppForm f?f.UiSize:12;
            if(!float.IsFinite(size)||size<9||size>17)return;
            await web.CoreWebView2.ExecuteScriptAsync("window.ccUiScale.apply("+JsonSerializer.Serialize(size)+","+JsonSerializer.Serialize(revision)+");").WaitAsync(TimeSpan.FromSeconds(6));
        }
        finally{uiFontSync.Release();}
    }
    protected override async void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(ready&&!closed)try{await ApplyUiFont();}catch{}}
    async Task<string> VisualContent(CancellationToken ct=default)=>await EditorScript("read",ct:ct);
    public async Task<string> GetContent(){await switches.Idle;return IsCode?code.Text:ready?await VisualContent():pending;}
    public async Task SetContent(string value)
    {
        Limits.BodyText(value);await switches.Idle;pending=value;code.Text=value;
        if(ready){string clean=SafeHtml.ValidateEditor(value,out bool warning);if(warning)MessageBox.Show(this,"HTMLに不正な構造または危険な要素があります。ビジュアル表示では安全に正規化します。コード原文はコードタブで確認できます。","HTML安全性",MessageBoxButtons.OK,MessageBoxIcon.Warning);await SetVisual(clean);}
    }
    public Task Switch(int index)=>switches.Request(index);
    void SwitchBusy(bool value){if(closed||IsDisposed)return;web.Enabled=!value;code.ReadOnly=value;}
    async Task<EditorSwitchDraft> PrepareSwitch(int index,CancellationToken ct)
    {
        if(closed)throw new OperationCanceledException(ct);
        if(!ready){initialization??=Initialize();await initialization.WaitAsync(TimeSpan.FromSeconds(6),ct);}
        ct.ThrowIfCancellationRequested();if(!ready)throw new IOException("HTMLエディターの初期化をお待ちください。");
        if(index==1)return new(true,await VisualContent(ct));
        string original=code.Text;string clean=SafeHtml.ValidateEditor(original,out bool warning);
        if(warning&&MessageBox.Show(this,"HTMLに不正な構造または危険な要素があります。安全に正規化してビジュアルへ切り替えますか？\n「いいえ」ならコードを保持します。","HTML安全性",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return new(false,original);
        ct.ThrowIfCancellationRequested();await SetVisual(clean,ct);return new(true,clean);
    }
    void CommitSwitch(int index,EditorSwitchDraft draft)
    {
        int old=tabs.SelectedIndex;string oldCode=code.Text,oldPending=pending;switching=true;
        try{tabs.SelectedIndex=index;if(tabs.SelectedIndex!=index)throw new IOException("タブを切り替えられませんでした。");if(index==1)code.Text=draft.Html;pending=draft.Html;}
        catch{code.Text=oldCode;pending=oldPending;tabs.SelectedIndex=old;throw;}
        finally{switching=false;}
        if(index==0)_=RefreshSwitchFont();
    }
    async Task RefreshSwitchFont(){try{await ApplyUiFont();}catch{}}
    void RememberSwitch(EditorSwitchTrace value){switchTrace.Enqueue(value);while(switchTrace.Count>24)switchTrace.Dequeue();}
    void SwitchFailed(Exception error)
    {
        if(closed||IsDisposed)return;
        try{
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CipherCapsule","Diagnostics");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"editor-switch118.json"),JsonSerializer.Serialize(new{version="1.1.9",utc=DateTime.UtcNow,ready,selected=tabs.SelectedIndex,errorType=error.GetType().Name,hresult=error.HResult,events=switchTrace.ToArray()}));
        }catch{}
        MessageBox.Show(this,"HTML表示を切り替えられませんでした。編集中の内容は保持しています。初期化・応答を待って、もう一度タブを選択してください。","HTML切替",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    public Task<bool> CloseAsync()=>cleanup??=CloseCore();
    async Task<bool> CloseCore(){closed=true;switches.Stop();if(initialization!=null)try{await initialization.WaitAsync(TimeSpan.FromSeconds(12));}catch(TimeoutException){profile.InitializationTimedOut();return false;}catch{}web.Dispose();foreach(var r in responses)r.Dispose();return await profile.Cleanup();}
    protected override void Dispose(bool disposing){if(disposing&&!closed){closed=true;switches.Stop();web.Dispose();foreach(var r in responses)r.Dispose();_=profile.Cleanup();}base.Dispose(disposing);}
}
