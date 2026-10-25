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
