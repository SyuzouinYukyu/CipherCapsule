namespace CipherCapsule.UI;

// An instance owns only the fresh GUID directory it actually created.
// It cannot adopt or clean a previous session's profile.
public sealed class ProfileDirectory
{
    readonly string root;bool owned;
    public string PathName{get;}
    public ProfileDirectory(string root)
    {
        this.root=Path.GetFullPath(root);PathName=Path.Combine(this.root,Guid.NewGuid().ToString("N"));
    }
    public void Create()
    {
        ManagedFiles.CheckPath(PathName);
        if(owned)throw new IOException("一時領域はすでに作成されています。");
        if(Directory.Exists(PathName)||File.Exists(PathName))throw new IOException("一時領域の名前が競合しました。");
        Directory.CreateDirectory(root);ManagedFiles.CheckPath(root);
        Directory.CreateDirectory(PathName);owned=true;ManagedFiles.CheckPath(PathName);
    }
    public void DeleteAfterExit(bool processExitConfirmed)
    {
        if(!owned)return;
        if(!processExitConfirmed)throw new IOException("使用中または終了未確認の一時領域は削除しません。");
        ManagedFiles.CheckPath(PathName);
        if(Directory.Exists(PathName)){
            ManagedFiles.CheckTree(PathName);Directory.Delete(PathName,true);
        }
        owned=false;ManagedFiles.RemoveEmpty(root);
    }
}
