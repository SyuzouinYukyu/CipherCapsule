using System.Diagnostics;
namespace CipherCapsule.Builder;

public static class OutputFolder
{
    public static ProcessStartInfo StartInfo(string completedOutputPath)
    {
        if(!Path.IsPathFullyQualified(completedOutputPath)||completedOutputPath.Any(char.IsControl)||completedOutputPath.StartsWith(@"\\?\")||completedOutputPath.StartsWith(@"\\.\"))
            throw new ArgumentException("保存先の絶対パスを確認できません。");
        string fullPath=Path.GetFullPath(completedOutputPath);
        string directory=Path.GetDirectoryName(fullPath)??throw new ArgumentException("保存先フォルダを確認できません。");
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException("保存先フォルダが移動または削除されています。");
        using(var entries=Directory.EnumerateFileSystemEntries(directory).GetEnumerator())_ = entries.MoveNext();
        var info=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),UseShellExecute=false};
        info.ArgumentList.Add(directory);return info;
    }
    public static string? TryOpen(string completedOutputPath)=>TryOpen(completedOutputPath,info=>{using var process=Process.Start(info)??throw new IOException();});
    public static string? OpenAndClose(string completedOutputPath,Action close,Action<ProcessStartInfo>? start=null)
    {
        string? warning=start==null?TryOpen(completedOutputPath):TryOpen(completedOutputPath,start);
        if(warning==null)close();
        return warning;
    }
    public static string? TryOpen(string completedOutputPath,Action<ProcessStartInfo> start)
    {
        try{start(StartInfo(completedOutputPath));return null;}
        catch(DirectoryNotFoundException){return "保存先フォルダが存在しません。移動・削除されていないか確認してください。暗号化は完了しています。";}
        catch(UnauthorizedAccessException){return "保存先フォルダにアクセスできません。アクセス権を確認してください。暗号化は完了しています。";}
        catch{return "保存先フォルダを開けませんでした。フォルダの場所・アクセス権・エクスプローラーの状態を確認してください。暗号化は完了しています。";}
    }
}
