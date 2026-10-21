using CipherCapsule.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace CipherCapsule.UI;
public sealed class HtmlView : UserControl
{
    readonly WebView2 web=new(){Dock=DockStyle.Fill,DefaultBackgroundColor=Color.White};
    readonly BrowserProfile profile=new();readonly SemaphoreSlim update=new(1,1);
    readonly string documentBase="https://capsule.invalid/"+Guid.NewGuid().ToString("N")+"/";string documentUrl="";int revision;
    MemoryStream? document;CoreWebView2Environment? environment;bool ready,closed,allowCopy=true,linkPromptPending;ulong navigationId;
    public bool Rendered{get;private set;}public Task<bool>? CleanupTask{get;private set;}public string CleanupFailure=>profile.FailureReason;string stage="HTMLの安全性検証";
    public HtmlView(){Dock=DockStyle.Fill;Controls.Add(web);}
    public async Task ShowHtml(string html,float size,bool allowCopy=true)
    {
        if(closed)return;string clean=await Task.Run(()=>SafeHtml.Sanitize(html,size,allowCopy));await update.WaitAsync();
        try{
            if(closed)return;this.allowCopy=allowCopy;
            if(!ready)
            {
                try{_=CoreWebView2Environment.GetAvailableBrowserVersionString();}catch{ShowRuntimeHelp();return;}
                stage="WebView2環境の作成";environment=await profile.Create();if(closed)return;stage="WebView2コントローラーの初期化";await web.EnsureCoreWebView2Async(environment);profile.ControllerCreated();if(closed)return;stage="WebView2安全設定";
                var c=web.CoreWebView2;var s=c.Settings;s.IsScriptEnabled=false;s.IsWebMessageEnabled=false;s.AreHostObjectsAllowed=false;s.AreDevToolsEnabled=false;s.AreDefaultScriptDialogsEnabled=false;s.AreDefaultContextMenusEnabled=false;s.IsStatusBarEnabled=false;s.IsPasswordAutosaveEnabled=false;s.IsGeneralAutofillEnabled=false;s.AreBrowserAcceleratorKeysEnabled=false;s.IsBuiltInErrorPageEnabled=false;
                c.PermissionRequested+=(_,e)=>{e.State=CoreWebView2PermissionState.Deny;e.Handled=true;};c.NewWindowRequested+=(_,e)=>e.Handled=true;c.DownloadStarting+=(_,e)=>e.Cancel=true;c.FrameNavigationStarting+=(_,e)=>e.Cancel=true;c.LaunchingExternalUriScheme+=(_,e)=>e.Cancel=true;
                c.NavigationStarting+=(_,e)=>{
                    if(e.Uri==documentUrl){navigationId=e.NavigationId;return;}e.Cancel=true;
                    if(e.IsUserInitiated&&!e.IsRedirected&&!linkPromptPending&&SafeHtml.SafeUrl(e.Uri) is string url)
                    {
                        linkPromptPending=true;BeginInvoke(new Action(()=>{try{if(!closed&&MessageBox.Show(this,"次のURLを既定のブラウザーで開きますか？\n\n"+url,"外部リンクの確認",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){AppForm.Error(this,ex);}finally{linkPromptPending=false;}}));
                    }
                };
                web.AllowExternalDrop=false;web.KeyDown+=(_,e)=>{
                    Keys k=e.KeyCode;
                    if(!this.allowCopy&&(e.Control&&(k is Keys.C or Keys.X or Keys.A or Keys.Insert)||e.Shift&&k==Keys.Insert||k==Keys.Apps)){e.Handled=true;e.SuppressKeyPress=true;}
                    if(e.Control&&(k is Keys.Add or Keys.Oemplus or Keys.Subtract or Keys.OemMinus)){e.Handled=true;e.SuppressKeyPress=true;if(FindForm() is AppForm f)f.SetSize(f.UiSize+(k is Keys.Add or Keys.Oemplus?1:-1));}
                };
                c.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                c.WebResourceRequested+=(_,e)=>{
                    if(e.Request.Uri==documentUrl&&e.ResourceContext==CoreWebView2WebResourceContext.Document&&document!=null){document.Position=0;e.Response=environment.CreateWebResourceResponse(document,200,"OK","Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; sandbox allow-top-navigation-by-user-activation");}
                    else e.Response=environment.CreateWebResourceResponse(null,403,"Blocked","Cache-Control: no-store");
                };c.NavigationCompleted+=(_,e)=>{if(e.NavigationId==navigationId){Rendered=e.IsSuccess;if(!e.IsSuccess&&!closed)ShowFailure("HTMLを表示できません。ブラウザー状態："+e.WebErrorStatus);}};
                c.ProcessFailed+=(_,e)=>{if(!closed){Rendered=false;ShowFailure("WebView2のブラウザープロセスが異常終了しました。画面を閉じて開き直してください。状態："+e.ProcessFailedKind);}};ready=true;
            }
            document?.Dispose();document=new MemoryStream(Limits.Utf8.GetBytes(clean),false);documentUrl=documentBase+(++revision).ToString();Rendered=false;web.CoreWebView2.Navigate(documentUrl);
        }catch(Exception e){if(!closed){var failure=e as BrowserFailure??new BrowserFailure(stage,e);ShowFailure(failure.UserMessage);throw failure;}}finally{update.Release();}
    }
    void ShowRuntimeHelp()
    {
        Controls.Clear();var link=new LinkLabel{Text="Microsoft公式のWebView2 Runtime導入先",AutoSize=true};Controls.Add(AppForm.Flow(AppForm.Label("HTML表示にはMicrosoft Edge WebView2 Runtimeが必要です。"),link));link.LinkClicked+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/"){UseShellExecute=true});
    }
    void ShowFailure(string message){var label=new Label{Text=message,AutoSize=false,Dock=DockStyle.Fill,Padding=new Padding(18),BackColor=Color.White,ForeColor=Color.DarkRed};Controls.Add(label);label.BringToFront();}
    public Task<bool> CloseAsync()=>CleanupTask??=CloseCore();
    async Task<bool> CloseCore(){closed=true;try{await update.WaitAsync().WaitAsync(TimeSpan.FromSeconds(12));}catch{profile.InitializationTimedOut();return false;}try{web.Dispose();document?.Dispose();}finally{update.Release();}return await profile.Cleanup();}
    protected override void Dispose(bool disposing){if(disposing&&!closed){closed=true;web.Dispose();document?.Dispose();CleanupTask=profile.Cleanup();}base.Dispose(disposing);}
}
