using Microsoft.Web.WebView2.Core;
using System.Security.AccessControl;
using System.Security.Principal;
namespace CipherCapsule.UI;
public sealed class BrowserProfile
{
    readonly ProfileDirectory directory=new(Path.Combine(Path.GetTempPath(),"CipherCapsule-v1110"));
    public string PathName=>directory.PathName;
    public bool CleanupFailed{get;private set;}public string FailureReason{get;private set;}="";
    readonly TaskCompletionSource exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    CoreWebView2Environment? environment;Task<CoreWebView2Environment>? creation;Task<bool>? cleanup;bool controllerCreated;
    public Task<CoreWebView2Environment> Create()=>creation??=CreateCore();
    async Task<CoreWebView2Environment> CreateCore()
    {
        try{
            directory.Create();
            var d=new DirectoryInfo(PathName);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));d.SetAccessControl(acl);
            environment=await CoreWebView2Environment.CreateAsync(null,PathName,new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update --disable-sync --no-first-run"));
            if(!string.Equals(Path.GetFullPath(environment.UserDataFolder),Path.GetFullPath(PathName),StringComparison.OrdinalIgnoreCase))throw new IOException("一時領域が外部設定で上書きされています。");
            environment.BrowserProcessExited+=OnExited;return environment;
        }catch(Exception e){throw new BrowserFailure("WebView2環境の作成",e);}
    }
    void OnExited(object? sender,CoreWebView2BrowserProcessExitedEventArgs e)=>exited.TrySetResult();
    public void ControllerCreated()=>controllerCreated=true;
    public void InitializationTimedOut(){Fail("WebView2の初期化が完了せず、解放状態を確認できませんでした。");}
    public Task<bool> Cleanup()=>cleanup??=CleanupCore();
    async Task<bool> CleanupCore()
    {
        if(creation!=null)try{await creation.WaitAsync(TimeSpan.FromSeconds(12));}catch(TimeoutException){return Fail("WebView2環境の作成完了を確認できませんでした。");}catch{}
        if(!Directory.Exists(PathName)){ReleaseEnvironment();return true;}
        bool processStarted=controllerCreated;
        try{processStarted|=environment?.GetProcessInfos().Count>0;}catch{processStarted=true;}
        if(processStarted&&!exited.Task.IsCompleted)await Task.WhenAny(exited.Task,Task.Delay(8000));
        if(processStarted&&!exited.Task.IsCompleted)return Fail("ブラウザープロセスの終了を上限時間内に確認できませんでした。");
        try{if(environment?.GetProcessInfos().Count>0)return Fail("ブラウザープロセスが一時領域を使用中です。");}catch{return Fail("ブラウザープロセスの解放状態を確認できません。");}
        string last="";
        for(int i=0;i<3;i++){
            try{directory.DeleteAfterExit(true);ReleaseEnvironment();return true;}
            catch(UnauthorizedAccessException){last="一時領域の削除がアクセス拒否されました。";}
            catch(IOException e){last=(e.HResult&0xffff) is 32 or 33?"一時領域のファイルがロックされています。":"一時領域の安全性検査または削除を完了できません。";}
            catch{last="一時領域の削除を完了できません。";}
            await Task.Delay(250);
        }return Fail(last);
    }
    void ReleaseEnvironment(){if(environment!=null)environment.BrowserProcessExited-=OnExited;environment=null;}
    bool Fail(string message){CleanupFailed=true;FailureReason=message+"\n対象："+PathName;return false;}
}
