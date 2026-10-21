using CipherCapsule.Core;
namespace CipherCapsule.Builder;
public sealed class Settings
{
    public int X { get; set; } = -1; public int Y { get; set; }=-1;
    public int Width { get; set; }=1240; public int Height { get; set; }=1000;
    public float Font { get; set; }=12; public int Kind { get; set; }
    static string PathName => Path.Combine(AppContext.BaseDirectory,"CipherCapsule.ini");
    public static Settings Load()
    {
        try {
            if(!File.Exists(PathName))return new(); if(new FileInfo(PathName).Length>4096)return new();
            var pairs=File.ReadAllLines(PathName).Where(x=>x.Contains('=')).Select(x=>x.Split('=',2)).ToDictionary(x=>x[0],x=>x[1]);
            var s=new Settings { X=int.Parse(pairs["X"]),Y=int.Parse(pairs["Y"]),Width=int.Parse(pairs["Width"]),Height=int.Parse(pairs["Height"]),Font=float.Parse(pairs["Font"],System.Globalization.CultureInfo.InvariantCulture),Kind=int.Parse(pairs["Kind"]) };
            if(s.Width<760 || s.Width>6000 || s.Height<600 || s.Height>4000 || !float.IsFinite(s.Font) || s.Font<9 || s.Font>17 || s.Kind is <0 or >1)return new(); return s;
        } catch { return new(); }
    }
    public void Save()
    {
        string temp=AtomicFile.Temporary(PathName);
        try { File.WriteAllText(temp,$"[Window]\nX={X}\nY={Y}\nWidth={Width}\nHeight={Height}\nFont={Font.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nKind={Kind}\n",Limits.Utf8); AtomicFile.Commit(temp,PathName,true); }
        finally { AtomicFile.Delete(temp); }
    }
}
