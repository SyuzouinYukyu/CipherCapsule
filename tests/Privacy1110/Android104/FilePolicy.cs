namespace CipherCapsule.Core;
public static class FilePolicy
{
    public static string Extension(string name,string original){Limits.FileName(name);Limits.FileName(original);string ext=Path.GetExtension(original);return ext.Length==0||name.EndsWith(ext,StringComparison.OrdinalIgnoreCase)?name:name+ext;}
    public static string UniqueName(string name,ISet<string> existing)
    {Limits.FileName(name);string stem=Path.GetFileNameWithoutExtension(name),ext=Path.GetExtension(name);if(!existing.Contains(name))return name;for(int i=2;i<100000;i++){string candidate=$"{stem} ({i}){ext}";if(!existing.Contains(candidate)){Limits.FileName(candidate);return candidate;}}throw new IOException("同名ファイルが多すぎます。");}
}
