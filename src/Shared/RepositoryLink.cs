using System.Diagnostics;
namespace CipherCapsule.UI;

public static class RepositoryLink
{
    public const string Url="https://github.com/SyuzouinYukyu/CipherCapsule";
    public static string? TryOpen(string? target,Action<ProcessStartInfo>? launch=null)
    {
        if(!string.Equals(target,Url,StringComparison.Ordinal))return "指定されたリンクは開けません。";
        try{
            var start=new ProcessStartInfo(Url){UseShellExecute=true,Verb="open"};
            if(launch==null)Process.Start(start);else launch(start);
            return null;
        }catch{return "既定のブラウザーでGitHubを開けませんでした。ブラウザーの設定を確認してください。";}
    }
}
