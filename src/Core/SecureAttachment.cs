using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
namespace CipherCapsule.Core;

public sealed class PlaintextResidueException(string path,Exception cause,Exception cleanup) : IOException(
    "警告：復号済みデータを含む一時ファイルを削除できませんでした。機密情報が残存している可能性があります。\r\n平文の残留パス："+path+
    "\r\n削除失敗："+cleanup.Message+"\r\n元の処理結果："+cause.GetType().Name+"："+cause.Message+
    "\r\n移動・共有を避け、使用中のプロセスを終了してから安全に削除してください。次回起動時にも回復を試みます。",cause);

// Only attachment plaintext uses this lease. Encrypted package cleanup is unchanged.
public sealed class SecureAttachment : IDisposable
{
    static readonly SecurityIdentifier User=WindowsIdentity.GetCurrent().User!;
    static readonly SecurityIdentifier SystemSid=new(WellKnownSidType.LocalSystemSid,null);
    static readonly string Suffix="CipherCapsule-Plaintext-"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(User.Value)))[..20];
    internal static string? TestRoot {get;set;}
    bool cleaned; readonly string job,root; readonly FileStream lease; readonly Receipt receipt;
    public string PathName { get; }
    sealed record Receipt(int Version,string Job,string Id,string State);
    SecureAttachment(string root,string job,FileStream lease,Receipt receipt){this.root=root;this.job=job;this.lease=lease;this.receipt=receipt;PathName=Path.Combine(job,"payload");}
    static string Root(string destination)
    {
        if(!Path.IsPathFullyQualified(destination)||destination.StartsWith(@"\\")||destination.StartsWith(@"\\?\"))throw new IOException("安全なローカルNTFSフォルダーへ保存してください。ネットワーク保存は対応していません。");
        string volume=Path.GetPathRoot(Path.GetFullPath(destination))!;var d=new DriveInfo(volume);
        if(d.DriveType!=DriveType.Fixed||d.DriveFormat!="NTFS")throw new IOException("同一ボリュームのアクセス制限領域を確保できません。ローカルNTFSフォルダーへ保存してください。");
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return TestRoot??(string.Equals(Path.GetPathRoot(local),volume,StringComparison.OrdinalIgnoreCase)?Path.Combine(local,Suffix):Path.Combine(volume,Suffix));
    }
    static void Ancestry(string path)
    {
        for(string? p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p)){
            try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("リンク経由の一時領域・保存先は使用できません。");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        }
    }
    static DirectorySecurity Acl(){var a=new DirectorySecurity();a.SetOwner(User);a.SetAccessRuleProtection(true,false);foreach(var sid in new[]{User,SystemSid})a.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));return a;}
    static void Verify(string path,bool directory)
    {
        Ancestry(path);FileSystemSecurity a=directory?new DirectoryInfo(path).GetAccessControl():new FileInfo(path).GetAccessControl();
        if(!User.Equals(a.GetOwner(typeof(SecurityIdentifier))))throw new IOException("一時領域の所有者を確認できません。");
        bool own=false,system=false;
        foreach(FileSystemAccessRule r in a.GetAccessRules(true,true,typeof(SecurityIdentifier))){
            if(r.AccessControlType!=AccessControlType.Allow||(!r.IdentityReference.Equals(User)&&!r.IdentityReference.Equals(SystemSid)))throw new IOException("一時領域のアクセス権が安全ではありません。");
            if((r.FileSystemRights&FileSystemRights.FullControl)!=FileSystemRights.FullControl)throw new IOException("一時領域の権限が不足しています。");
            own|=r.IdentityReference.Equals(User);system|=r.IdentityReference.Equals(SystemSid);
        }
        if(!own||!system||directory&&!a.AreAccessRulesProtected)throw new IOException("一時領域のACLを検証できません。");
    }
    static byte[] RootKey(string root,bool create)
    {
        Ancestry(root);bool exists=Directory.Exists(root);
        if(!exists){if(!create)throw new IOException("管理領域が存在しません。");new DirectoryInfo(root).Create(Acl());}
        Verify(root,true);string key=Path.Combine(root,"owner.key");
        if(!exists){using var f=new FileStream(key,FileMode.CreateNew,FileAccess.Write,FileShare.None);f.Write(RandomNumberGenerator.GetBytes(32));f.Flush(true);}
        Verify(key,false);if(new FileInfo(key).Length!=32)throw new IOException("管理鍵の長さが不正です。");byte[] b=File.ReadAllBytes(key);if(b.Length!=32)throw new IOException("管理領域の所有記録が不正です。");return b;
    }
    static string Id(SafeFileHandle handle){if(!GetFileInformationByHandle(handle,out var i))throw new Win32Exception();if((i.Attributes&0x410)!=0||i.Links!=1)throw new IOException("通常の単一ファイルではありません。");return $"{i.Serial:X8}:{i.IndexHigh:X8}{i.IndexLow:X8}:{i.CreationHigh:X8}{i.CreationLow:X8}";}
    static void Store(string job,Receipt r,byte[] key){byte[] json=JsonSerializer.SerializeToUtf8Bytes(r);using var f=new FileStream(Path.Combine(job,"state"),FileMode.CreateNew,FileAccess.Write,FileShare.None);f.Write(HMACSHA256.HashData(key,json));f.Write(json);f.Flush(true);}
    static Receipt Load(string job,byte[] key){string path=Path.Combine(job,"state");Verify(path,false);if(new FileInfo(path).Length>4096)throw new IOException("管理記録が大きすぎます。");byte[] b=File.ReadAllBytes(path);if(b.Length<33||!CryptographicOperations.FixedTimeEquals(b.AsSpan(0,32),HMACSHA256.HashData(key,b.AsSpan(32))))throw new IOException("管理記録の認証に失敗しました。");var r=JsonSerializer.Deserialize<Receipt>(b.AsSpan(32))??throw new IOException();if(r.Version!=1||r.Job!=job||r.State!="writing")throw new IOException("管理記録の一致を確認できません。");return r;}
    public static SecureAttachment Create(string destination)
    {
        Ancestry(destination);string root=Root(destination);if(!string.Equals(Path.GetPathRoot(root),Path.GetPathRoot(destination),StringComparison.OrdinalIgnoreCase))throw new IOException("一時領域と保存先のボリュームが異なります。");
        byte[] key=RootKey(root,true);string job=Path.Combine(root,Guid.NewGuid().ToString("N"));new DirectoryInfo(job).Create(Acl());Verify(job,true);
        var lease=new FileStream(Path.Combine(job,"lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        try{using var f=new FileStream(Path.Combine(job,"payload"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);Verify(Path.Combine(job,"payload"),false);var r=new Receipt(1,job,Id(f.SafeFileHandle),"writing");Store(job,r,key);return new(root,job,lease,r);}catch{lease.Dispose();throw;}finally{CryptographicOperations.ZeroMemory(key);}
    }
    static void DeletePayload(string job,Receipt receipt)
    {
        string path=Path.Combine(job,"payload");try{_ = File.GetAttributes(path);}catch(FileNotFoundException){return;}Verify(path,false);
        using var h=CreateFile(path,0x80000000|0x10000,0,IntPtr.Zero,3,0x00200000,IntPtr.Zero);
        if(h.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        if(Id(h)!=receipt.Id)throw new IOException("一時ファイルの同一性が変わっています。自動削除しません。");
        int yes=1;if(!SetFileInformationByHandle(h,4,ref yes,4))throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Finish(Exception? original)
    {
        try{DeletePayload(job,receipt);cleaned=true;}catch(Exception e){throw new PlaintextResidueException(PathName,original??new IOException("保存後の後始末"),e);}
    }
    public void Dispose(){lease.Dispose();if(cleaned) {try{File.Delete(Path.Combine(job,"state"));File.Delete(Path.Combine(job,"lease"));Directory.Delete(job,false);}catch{/* No plaintext remains; retain metadata for a later warning. */}}}
    public static IReadOnlyList<string> Recover()
    {
        var warnings=new List<string>();var roots=TestRoot!=null?new[]{TestRoot}:DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed&&d.DriveFormat=="NTFS").Select(d=>Root(Path.Combine(d.RootDirectory.FullName,"dummy"))).Distinct().ToArray();
        foreach(string root in roots){if(!Directory.Exists(root))continue;try{byte[] key=RootKey(root,false);try{foreach(string job in Directory.EnumerateFileSystemEntries(root).Where(p=>Path.GetFileName(p)!="owner.key")){
            try{if(!Guid.TryParseExact(Path.GetFileName(job),"N",out _))throw new IOException("不明なファイルです。保持します。");Verify(job,true);
                string lp=Path.Combine(job,"lease");Verify(lp,false);FileStream active;try{active=new(lp,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}catch(IOException e)when((e.HResult&0xffff) is 32 or 33){continue;}
                using(active){if(Directory.EnumerateFileSystemEntries(job).Any(p=>Path.GetFileName(p) is not("lease" or "state" or "payload")))throw new IOException("不明な内容を保持します。");var r=Load(job,key);DeletePayload(job,r);}
                File.Delete(Path.Combine(job,"state"));File.Delete(lp);Directory.Delete(job,false);
            }catch(Exception e){warnings.Add("警告：復号済みデータを含む一時領域を安全に回復できません。機密情報が残存している可能性があります。自動削除せず保持しました。\r\n"+Path.Combine(job,"payload")+"\r\n"+e.Message+"\r\n移動・共有を避け、使用プロセス終了後に確認してください。");}
        }}finally{CryptographicOperations.ZeroMemory(key);}}catch(Exception e){warnings.Add("一時領域の検証・回復失敗："+root+"\r\n"+e.Message);}}
        return warnings;
    }
    [StructLayout(LayoutKind.Sequential)]struct Info{public uint Attributes,CreationLow,CreationHigh,AccessLow,AccessHigh,WriteLow,WriteHigh,Serial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle handle,int kind,ref int data,int size);
}
