namespace CipherCapsule.UI;

// Check every existing ancestor; never follow junctions/symlinks for maintenance.
public static class ManagedFiles
{
    public static string CheckPath(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.Any(char.IsControl))throw new IOException("管理対象の絶対パスを検証できません。");
        string full=Path.GetFullPath(path);
        for(string? p=full;p!=null;p=Path.GetDirectoryName(p))
        {
            try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("リンク経由の管理対象は整理しません。");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        }
        return full;
    }
    public static void CheckTree(string path)
    {
        CheckPath(path);
        foreach(string p in Directory.EnumerateFileSystemEntries(path))
        {
            CheckPath(p);
            if((File.GetAttributes(p)&FileAttributes.Directory)!=0)CheckTree(p);
        }
    }
    public static void RemoveEmpty(string path)
    {
        CheckPath(path);if(!Directory.Exists(path))return;
        if(!Directory.EnumerateFileSystemEntries(path).Any())
            try{Directory.Delete(path,false);}catch(IOException)when(Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any()){}
    }
}
