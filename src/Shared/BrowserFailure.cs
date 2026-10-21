namespace CipherCapsule.UI;
public sealed class BrowserFailure : Exception
{
    public string Stage{get;}
    public BrowserFailure(string stage,Exception error):base("WebView2処理を完了できません。",error){Stage=stage;HResult=error.HResult;}
    public string UserMessage=>HResult switch{
        unchecked((int)0x80070005)=>$"{Stage}：アクセスが拒否されました。一時領域の書込み権限を確認してください。",
        unchecked((int)0x80004004)=>$"{Stage}：初期化が中断されました。画面を閉じて開き直してください。",
        _=>$"{Stage}を完了できません。WebView2 Runtimeと一時領域の権限を確認してください。\n診断：{InnerException?.GetType().Name} / 0x{HResult:X8}"};
}
