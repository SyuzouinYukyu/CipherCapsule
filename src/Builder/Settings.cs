using CipherCapsule.Core;
namespace CipherCapsule.Builder;
public sealed class Settings
{
    public int X { get; set; } = -1; public int Y { get; set; }=-1;
    public int Width { get; set; }=1240; public int Height { get; set; }=1000;
    public float Font { get; set; }=12; public int Kind { get; set; }
    public bool AllowCopy { get; set; }=true;
    public bool ChecksumSHA512 { get; set; }
    public bool ChecksumSHA256 { get; set; }
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
            var entries=File.ReadAllLines(PathName).Where(x=>x.Contains('=')).Select(x=>x.Split('=',2)).GroupBy(x=>x[0].Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.Count()==1?x.Single()[1]:"",StringComparer.OrdinalIgnoreCase);
            string V(string key)=>entries.GetValueOrDefault(key,"").Trim();
            int I(string key,int fallback,int min,int max)=>int.TryParse(V(key),out int n)&&n>=min&&n<=max?n:fallback;
            bool B(string key,bool fallback)=>V(key)=="1"?true:V(key)=="0"?false:fallback;
            var s=new Settings();s.X=I("X",-1,-100000,100000);s.Y=I("Y",-1,-100000,100000);s.Width=I("Width",s.Width,760,6000);s.Height=I("Height",s.Height,600,4000);s.Kind=I("Kind",0,0,1);
            if(float.TryParse(V("Font"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float font)&&float.IsFinite(font)&&font>=9&&font<=17)s.Font=font;
            s.PasswordRevealed=B("PasswordRevealed",false);s.AllowCopy=B("AllowCopy",true);s.ChecksumSHA512=B("ChecksumSHA512",false);s.ChecksumSHA256=B("ChecksumSHA256",false);
            s.LastOutputDirectory=entries.TryGetValue("LastOutputDirectory",out var output)?OutputDirectory(output):Documents;s.OutputFormat=V("OutputFormat")=="CCAP"?OutputFormat.CCAP:OutputFormat.EXE;return s;
        } catch { return new(); }
    }
    public void Save()
    {
        string temp=AtomicFile.Temporary(PathName);
        try { File.WriteAllText(temp,$"[Window]\nX={X}\nY={Y}\nWidth={Width}\nHeight={Height}\nFont={Font.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nKind={Kind}\nPasswordRevealed={(PasswordRevealed?1:0)}\nLastOutputDirectory={OutputDirectory(LastOutputDirectory)}\nAllowCopy={(AllowCopy?1:0)}\nChecksumSHA512={(ChecksumSHA512?1:0)}\nChecksumSHA256={(ChecksumSHA256?1:0)}\nOutputFormat={(OutputFormat==OutputFormat.CCAP?"CCAP":"EXE")}\n",Limits.Utf8); AtomicFile.Commit(temp,PathName,true); }
        finally { AtomicFile.Delete(temp); }
    }
}
