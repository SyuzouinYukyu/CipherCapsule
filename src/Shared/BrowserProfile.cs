using Microsoft.Web.WebView2.Core;
using System.Security.AccessControl;
using System.Security.Principal;
namespace CipherCapsule.UI;
public sealed class BrowserProfile
{
    static readonly string Root=Path.Combine(Path.GetTempPath(),"CipherCapsule-v112");
    public string PathName{get;}=Path.Combine(Root,Guid.NewGuid().ToString("N"));
    public bool CleanupFailed{get;private set;}public string FailureReason{get;private set;}="";
    readonly TaskCompletionSource exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    CoreWebView2Environment? environment;Task<bool>? cleanup;bool controllerCreated;
    public async Task<CoreWebView2Environment> Create()
    {
        try{
            Directory.CreateDirectory(Root);if((File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0)throw new IOException("不正な一時領域です。");
            var d=Directory.CreateDirectory(PathName);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));d.SetAccessControl(acl);
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
        if(!Directory.Exists(PathName))return true;
        bool processStarted=controllerCreated;
        try{processStarted|=environment?.GetProcessInfos().Count>0;}catch{processStarted=true;}
        if(processStarted&&!exited.Task.IsCompleted)await Task.WhenAny(exited.Task,Task.Delay(8000));
        if(processStarted&&!exited.Task.IsCompleted)return Fail("ブラウザープロセスの終了を上限時間内に確認できませんでした。");
        try{if(environment?.GetProcessInfos().Count>0)return Fail("ブラウザープロセスが一時領域を使用中です。");}catch{return Fail("ブラウザープロセスの解放状態を確認できません。");}
        string path=Path.GetFullPath(PathName),prefix=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(path),"N",out _))return Fail("削除対象の境界を検証できません。");
        string last="";
        for(int i=0;i<3;i++){
            try{CheckTree(Root,false);CheckTree(path,true);Directory.Delete(path,true);if(environment!=null)environment.BrowserProcessExited-=OnExited;environment=null;return true;}
            catch(DirectoryNotFoundException){return true;}
            catch(UnauthorizedAccessException){last="一時領域の削除がアクセス拒否されました。";}
            catch(IOException e){last=(e.HResult&0xffff) is 32 or 33?"一時領域のファイルがロックされています。":"一時領域の安全性検査または削除を完了できません。";}
            catch{last="一時領域の削除を完了できません。";}
            await Task.Delay(250);
        }return Fail(last);
    }
    bool Fail(string message){CleanupFailed=true;FailureReason=message;return false;}
    static void CheckTree(string path,bool descend)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("リンクは削除対象にできません。");
        if(!descend)return;foreach(string p in Directory.EnumerateFileSystemEntries(path)){var a=File.GetAttributes(p);if((a&FileAttributes.ReparsePoint)!=0)throw new IOException("リンクは削除対象にできません。");if((a&FileAttributes.Directory)!=0)CheckTree(p,true);}
    }
}
