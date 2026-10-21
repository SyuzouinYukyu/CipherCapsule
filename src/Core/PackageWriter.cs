using System.Security.Cryptography;
namespace CipherCapsule.Core;
public sealed record PackageResult(string Path,long Size,string Sha256,string? Sha512);
public static class PackageWriter
{
    public static PackageResult Generate(Stream template,string output,string body,BodyKind kind,IReadOnlyList<string> paths,byte[] password,bool checksum,bool overwrite,CancellationToken ct,IProgress<CapsuleProgress>? progress=null,bool allowCopy=true)
    {
        string dest=Path.GetFullPath(output),sum=dest+".sha512",exeTemp=AtomicFile.Temporary(dest),sumTemp=AtomicFile.Temporary(dest);
        if(paths.Any(p=>string.Equals(Path.GetFullPath(p),dest,StringComparison.OrdinalIgnoreCase)||checksum&&string.Equals(Path.GetFullPath(p),sum,StringComparison.OrdinalIgnoreCase)))throw new IOException("出力先と添付元が同じです。");
        if(Directory.Exists(dest)||checksum&&Directory.Exists(sum))throw new IOException("出力先に同名のフォルダーがあります。");
        var committed=new List<(string Target,string Backup,bool Existed)>();
        try
        {
            Container.Generate(template,exeTemp,body,kind,paths,password,false,ct,progress,allowCopy);
            ct.ThrowIfCancellationRequested();progress?.Report(new("最終EXEのチェックサムを計算しています",0,1));
            string h256,h512;using(var f=File.OpenRead(exeTemp)){h256=Convert.ToHexString(SHA256.HashData(f));f.Position=0;h512=Convert.ToHexString(SHA512.HashData(f)).ToLowerInvariant();}
            if(checksum)File.WriteAllText(sumTemp,h512+"  "+Path.GetFileName(dest)+"\n",Limits.Utf8);
            ct.ThrowIfCancellationRequested();
            if(checksum)Commit(sumTemp,sum,overwrite,committed);
            Commit(exeTemp,dest,overwrite,committed);
            // Both prepared files have been committed. Do not report success on a checksum failure.
            using(var final=File.OpenRead(dest))if(Convert.ToHexString(SHA512.HashData(final)).ToLowerInvariant()!=h512)throw new IOException("確定後のEXEが変更されました。");
            foreach(var c in committed)AtomicFile.Delete(c.Backup);
            return new(dest,new FileInfo(dest).Length,h256,checksum?h512:null);
        }
        catch(Exception failure)
        {
            var recovery=new List<string>();
            foreach(var c in committed.AsEnumerable().Reverse())
            {
                try { if(c.Existed) { if(File.Exists(c.Target))File.Replace(c.Backup,c.Target,null);else File.Move(c.Backup,c.Target); } else File.Delete(c.Target); }
                catch { recovery.Add(c.Existed?c.Backup:c.Target); }
            }
            if(recovery.Count>0)throw new IOException("出力の復元を完了できません。次の退避ファイルを保管して手動確認してください："+string.Join("、",recovery),failure);
            throw;
        }
        finally {AtomicFile.Delete(exeTemp);AtomicFile.Delete(sumTemp);}
    }
    static void Commit(string temp,string target,bool overwrite,List<(string Target,string Backup,bool Existed)> journal)
    {
        bool exists=File.Exists(target);string backup=AtomicFile.Temporary(target);
        if(exists){if(!overwrite)throw new IOException("出力先が既に存在します。");File.Replace(temp,target,backup);}
        else File.Move(temp,target);
        journal.Add((target,backup,exists));
    }
}
