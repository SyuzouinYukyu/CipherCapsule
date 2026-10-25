namespace CipherCapsule.Core;
public static class Display
{
    public static string Size(long bytes) { string[] units=["B","KiB","MiB","GiB","TiB"];double n=bytes;int i=0;while(n>=1024&&i<4){n/=1024;i++;}return i==0?$"{bytes:N0} B":$"{n:N1} {units[i]}"; }
    public static string Time(TimeSpan t)=>t.TotalMinutes<1?$"{t.TotalSeconds:F1}秒":$"{(int)t.TotalMinutes}分{t.Seconds:D2}秒";
    public static string OutputCandidate(string path)
    {
        path=path.Trim().Trim('"'); if(!Path.IsPathFullyQualified(path))throw new ArgumentException("出力先は絶対パスで指定してください。");path=Path.GetFullPath(path);
        if(Directory.Exists(path))return Path.Combine(path,"SecureMessage.exe");
        if(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)){Limits.FileName(Path.GetFileName(path));return path;}
        throw new ArgumentException("フォルダーまたは.exeファイルを指定してください。");
    }
}
