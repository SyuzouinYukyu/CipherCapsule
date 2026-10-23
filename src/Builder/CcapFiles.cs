using CipherCapsule.Core;
namespace CipherCapsule.Builder;
public static class OutputSelection
{
    public static string Extension(OutputFormat format)=>format switch {OutputFormat.EXE=>".exe",OutputFormat.CCAP=>".ccap",_=>throw new ArgumentException("出力形式を確認してください。")};
    public static SaveFileDialog Dialog(OutputFormat format,string directory)
    {
        string ext=Extension(format);
        // Extension and overwrite checks are performed on the final path by the caller.
        return new(){Filter=format==OutputFormat.CCAP?"CipherCapsuleファイル (*.ccap)|*.ccap":"EXEファイル (*.exe)|*.exe",FileName="SecureMessage"+ext,InitialDirectory=Settings.OutputDirectory(directory),DefaultExt=ext[1..],AddExtension=false,OverwritePrompt=false,CheckPathExists=true};
    }
    public static string FinalPath(string path,OutputFormat format)
    {
        if(!Path.IsPathFullyQualified(path)||path.Any(char.IsControl)||path.StartsWith(@"\\?\")||path.StartsWith(@"\\.\"))throw new ArgumentException("保存先は通常の絶対パスで指定してください。");
        string full=Path.GetFullPath(path),ext=Extension(format);Limits.FileName(Path.GetFileName(full));string given=Path.GetExtension(full);
        if(given.Length==0)full+=ext;
        else if(!given.Equals(ext,StringComparison.OrdinalIgnoreCase))throw new ArgumentException($"選択した出力形式の拡張子は{ext}です。ファイル名を訂正してください。");
        Limits.FileName(Path.GetFileName(full));return full;
    }
}
public static class CcapFiles
{
    public static string ValidateRequest(IReadOnlyList<string> paths)
    {
        if(paths.Count!=1)throw new ArgumentException("CCAPファイルは1つずつ開いてください。複数ファイルの同時読込みはできません。");
        string path=paths[0];
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||path.Any(char.IsControl)||path.StartsWith(@"\\?\")||path.StartsWith(@"\\.\"))throw new ArgumentException("CCAPファイルの絶対パスを1つ指定してください。");
        path=Path.GetFullPath(path);
        if(!Path.GetExtension(path).Equals(".ccap",StringComparison.OrdinalIgnoreCase))throw new ArgumentException(".ccap形式のファイルを選択してください。自己復号EXEは直接起動してください。");
        if(!File.Exists(path))throw new ArgumentException("指定されたCCAPファイルが存在しません。");
        using var file=File.OpenRead(path);Container.LocateCcap(file);return path;
    }
}
