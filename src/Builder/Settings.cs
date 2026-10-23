using CipherCapsule.Core;
namespace CipherCapsule.Builder;
public sealed class Settings
{
    public int X { get; set; } = -1; public int Y { get; set; }=-1;
    public int Width { get; set; }=1240; public int Height { get; set; }=1000;
    public float Font { get; set; }=12; public int Kind { get; set; }
    public bool PasswordRevealed { get; set; }
    public OutputFormat OutputFormat { get; set; } = OutputFormat.EXE;
    public string LastOutputDirectory { get; set; } = Documents;
    public static string Documents=>Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public static string OutputDirectory(string? value)
    {
        try{
            if(string.IsNullOrWhiteSpace(value)||value.Length>2048||value.Any(char.IsControl)||!Path.IsPathFullyQualified(value)||value.StartsWith(@"\\?\")||value.StartsWith(@"\\.\"))return Documents;
            string path=Path.GetFullPath(value);if(!Directory.Exists(path))return Documents;
            using var access=Directory.EnumerateFileSystemEntries(path).GetEnumerator();_ = access.MoveNext();return path;
        }catch{return Documents;}
    }
    static string PathName => Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");
    public static Settings Load()
    {
        try {
            if(!File.Exists(PathName))return new(); if(new FileInfo(PathName).Length>4096)return new();
            var entries=File.ReadAllLines(PathName).Where(x=>x.Contains('=')).Select(x=>x.Split('=',2)).ToArray();
            bool IsReveal(string[] entry)=>entry[0].Trim().Equals("PasswordRevealed",StringComparison.OrdinalIgnoreCase);
            var reveal=entries.Where(IsReveal).ToArray();
            bool IsOutput(string[] entry)=>entry[0].Trim().Equals("LastOutputDirectory",StringComparison.OrdinalIgnoreCase);
            var output=entries.Where(IsOutput).ToArray();
            bool IsFormat(string[] entry)=>entry[0].Trim().Equals("OutputFormat",StringComparison.OrdinalIgnoreCase);
            var format=entries.Where(IsFormat).ToArray();
            var pairs=entries.Where(x=>!IsReveal(x)&&!IsOutput(x)&&!IsFormat(x)).ToDictionary(x=>x[0],x=>x[1]);
            var s=new Settings { X=int.Parse(pairs["X"]),Y=int.Parse(pairs["Y"]),Width=int.Parse(pairs["Width"]),Height=int.Parse(pairs["Height"]),Font=float.Parse(pairs["Font"],System.Globalization.CultureInfo.InvariantCulture),Kind=int.Parse(pairs["Kind"]) };
            if(s.Width<760 || s.Width>6000 || s.Height<600 || s.Height>4000 || !float.IsFinite(s.Font) || s.Font<9 || s.Font>17 || s.Kind is <0 or >1)return new();s.PasswordRevealed=reveal.Length==1&&reveal[0][1].Trim()=="1";s.LastOutputDirectory=output.Length==1?OutputDirectory(output[0][1]):Documents;s.OutputFormat=format.Length==1&&format[0][1].Trim()=="CCAP"?OutputFormat.CCAP:OutputFormat.EXE;return s;
        } catch { return new(); }
    }
    public void Save()
    {
        string temp=AtomicFile.Temporary(PathName);
        try { File.WriteAllText(temp,$"[Window]\nX={X}\nY={Y}\nWidth={Width}\nHeight={Height}\nFont={Font.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nKind={Kind}\nPasswordRevealed={(PasswordRevealed?1:0)}\nLastOutputDirectory={OutputDirectory(LastOutputDirectory)}\nOutputFormat={(OutputFormat==OutputFormat.CCAP?"CCAP":"EXE")}\n",Limits.Utf8); AtomicFile.Commit(temp,PathName,true); }
        finally { AtomicFile.Delete(temp); }
    }
}
