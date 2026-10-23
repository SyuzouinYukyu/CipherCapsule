using CipherCapsule.Core;
using System.Diagnostics;
namespace CipherCapsule.Viewer;

public static class AttachmentNames
{
    public static string Extension(string name)
    {
        Limits.FileName(name);
        foreach(string suffix in new[]{".tar.gz",".tar.bz2",".tar.xz",".tar.zst"})
            if(name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return name[^suffix.Length..];
        return Path.GetExtension(name);
    }
    public static SaveFileDialog Dialog(string name)
    {
        string ext=Extension(name),kind=ext.ToLowerInvariant() switch {
            ".pdf"=>"PDF", ".txt"=>"テキスト", ".jpg" or ".jpeg"=>"JPEG", ".png"=>"PNG",
            _=>ext.TrimStart('.').ToUpperInvariant() };
        return new SaveFileDialog{FileName=name,Filter=ext.Length==0?"すべてのファイル (*.*)|*.*":$"{kind}ファイル (*{ext})|*{ext}",
            DefaultExt=ext.TrimStart('.'),AddExtension=ext.Length>0,OverwritePrompt=false,CheckPathExists=true};
    }
    public static string SinglePath(string input,string original)
    {
        if(!Path.IsPathFullyQualified(input)||input.StartsWith(@"\\?\")||input.StartsWith(@"\\.\"))throw new ArgumentException("通常の絶対パスを指定してください。");
        string name=Path.GetFileName(input);Limits.FileName(name);string ext=Extension(original);
        if(ext.Length>0&&!name.EndsWith(ext,StringComparison.OrdinalIgnoreCase))name+=ext;
        Limits.FileName(name);string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(input)!,name));
        ValidateDirectory(Path.GetDirectoryName(path)!);ValidateTarget(path);return path;
    }
    public static void ValidateDirectory(string directory)
    {
        var d=new DirectoryInfo(Path.GetFullPath(directory));
        while(d!=null){if(!d.Exists||(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("保存先フォルダーが存在しないか、通常のフォルダーではありません。");d=d.Parent;}
    }
    public static void ValidateTarget(string path)
    {
        try{var attr=File.GetAttributes(path);if((attr&(FileAttributes.Directory|FileAttributes.ReparsePoint|FileAttributes.Device))!=0)throw new IOException("通常のファイル以外には保存できません。");}
        catch(FileNotFoundException){} // An absent file is a valid, non-overwriting destination.
    }
    public static string Available(string folder,string original,HashSet<string> occupied)
    {
        Limits.FileName(original);string ext=Extension(original),stem=original[..(original.Length-ext.Length)];
        string candidate=original;
        for(int n=2;occupied.Contains(candidate);n++)candidate=$"{stem} ({n}){ext}";
        if(candidate.Length>255)throw new ArgumentException("連番を付けるとファイル名が長すぎます。保存先の重複を解消してください。");
        Limits.FileName(candidate);occupied.Add(candidate);
        string dest=Path.GetFullPath(Path.Combine(folder,candidate));
        if(!string.Equals(Path.GetDirectoryName(dest),Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),StringComparison.OrdinalIgnoreCase))throw new IOException("保存先フォルダー外には保存できません。");
        return dest;
    }
}
public sealed record BatchProgress(int Total,int Completed,string Current,double Fraction,long BytesDone,long BytesTotal);
public sealed record SavedAttachment(int Index,string Path);
public sealed record BatchSaveResult(int Succeeded,int Failed,int Unprocessed,string Folder,string Reason,bool Canceled,TimeSpan Elapsed,IReadOnlyList<SavedAttachment> Saved);
public static class AttachmentBatch
{
    sealed class Forward(Action<CapsuleProgress> report):IProgress<CapsuleProgress>{public void Report(CapsuleProgress p)=>report(p);}
    public static BatchSaveResult Save(Container.UnlockedCapsule capsule,string folder,CancellationToken ct,IProgress<BatchProgress>? progress=null)
    {
        var watch=Stopwatch.StartNew();var saved=new List<SavedAttachment>();int count=capsule.Manifest.Attachments.Count,failed=0;bool canceled=false;string reason="すべての添付を保存しました。";
        long total=capsule.Manifest.Attachments.Sum(a=>a.Size),done=0;
        try{
            ct.ThrowIfCancellationRequested();folder=Path.GetFullPath(folder);AttachmentNames.ValidateDirectory(folder);
            var occupied=Directory.EnumerateFileSystemEntries(folder).Select(p=>Path.GetFileName(p)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<count;i++){
                ct.ThrowIfCancellationRequested();AttachmentNames.ValidateDirectory(folder);
                var a=capsule.Manifest.Attachments[i];string dest=AttachmentNames.Available(folder,a.Name,occupied);AttachmentNames.ValidateTarget(dest);
                void Report(long current){double part=a.Size==0?0:(double)current/a.Size;progress?.Report(new(count,saved.Count,a.Name,(saved.Count+part)/Math.Max(1,count),done+current,total));}
                Report(0);
                capsule.Save(i,dest,false,ct,new Forward(p=>Report(Math.Min(a.Size,p.Done))));
                saved.Add(new(i,dest));done+=a.Size;
                progress?.Report(new(count,saved.Count,a.Name,(double)saved.Count/Math.Max(1,count),done,total));
            }
        }
        catch(OperationCanceledException){canceled=true;reason="保存をキャンセルしました。正常保存済みのファイルは保持しています。";}
        catch(Exception e){failed=count>saved.Count?1:0;reason=e is System.Security.Cryptography.CryptographicException?Container.AuthFailure:e is ArgumentException or InvalidDataException?e.Message:e is UnauthorizedAccessException?"保存先へのアクセスが拒否されました。":"保存を中断しました。保存先の使用状況・空き容量・名前の衝突を確認してください。";}
        return new(saved.Count,failed,count-saved.Count-failed,folder,reason,canceled,watch.Elapsed,saved);
    }
}
