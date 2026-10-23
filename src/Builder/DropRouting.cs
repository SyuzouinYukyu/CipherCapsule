namespace CipherCapsule.Builder;

public enum FileDropArea { Other, TextBody, Attachments }
public enum FileDropAction { Ignore, OpenCcap, ReadBody, AddAttachments }
public static class FileDropRouting
{
    public static FileDropAction Decide(IReadOnlyList<string> paths, FileDropArea area, bool blocked)
    {
        if(blocked || paths.Count==0 || paths.Any(string.IsNullOrWhiteSpace)) return FileDropAction.Ignore;
        // CCAP always wins over the previous body/attachment interpretation.
        if(paths.Any(p=>Path.GetExtension(p).Equals(".ccap",StringComparison.OrdinalIgnoreCase))) return FileDropAction.OpenCcap;
        if(area==FileDropArea.TextBody && paths.Count==1) return FileDropAction.ReadBody;
        if(area==FileDropArea.Attachments) return FileDropAction.AddAttachments;
        return FileDropAction.Ignore;
    }
}
public static class AutomaticCcapIcon
{
    public static string? Apply(Action install)
    {
        try{install();return null;}
        catch(UnauthorizedAccessException){return "CCAPアイコンを設定できませんでした。現在のユーザーの設定先へのアクセスが拒否されました。暗号化・復号は引き続き利用できます。";}
        catch(Exception){return "CCAPアイコンを設定できませんでした。設定先・復元情報・書込み権限を確認してください。暗号化・復号は引き続き利用できます。";}
    }
}
