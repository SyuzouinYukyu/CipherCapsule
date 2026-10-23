using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace CipherCapsule.Core;
public static class Container
{
    public const int HeaderSize = 176, FooterSize = 64;
    public const string AuthFailure = "パスワードが正しくないか、暗号化データを検証できません";
    static readonly byte[] Magic = "CCAPDAT1"u8.ToArray(), EndMagic = "CCAPEND1"u8.ToArray();
    public static long I64(byte[] b, int i) => BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(i));
    static uint U32(byte[] b, int i) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(i));
    static void P64(byte[] b, int i, long v) => BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(i), v);
    static void P32(byte[] b, int i, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i), v);
    public static byte[] Read(Stream s, int n) { byte[] b = new byte[n]; s.ReadExactly(b); return b; }
    static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, Limits.Chunk, FileOptions.SequentialScan);
    public static (long Offset, long Length) Locate(FileStream s)
    {
        if (s.Length < 256) throw new InvalidDataException("有効な暗号化EXEではありません。");
        s.Position=0;if(Read(s,8).AsSpan().SequenceEqual(Magic))return LocateCcap(s);
        s.Position = 0; byte[] dos = Read(s, 64);
        if (dos[0] != 'M' || dos[1] != 'Z') throw new InvalidDataException();
        long pe = U32(dos, 60);
        if (pe < 64 || pe > s.Length - 264) throw new InvalidDataException();
        s.Position = pe; byte[] h = Read(s, 264);
        int sections = BinaryPrimitives.ReadUInt16LittleEndian(h.AsSpan(6));
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(h.AsSpan(20));
        if (!h.AsSpan(0,4).SequenceEqual("PE\0\0"u8) || BinaryPrimitives.ReadUInt16LittleEndian(h.AsSpan(24)) != 0x20b || optionalSize < 240 || sections is < 1 or > 96 || U32(h, 24 + 108) < 5) throw new InvalidDataException();
        long cert = U32(h, 24 + 112 + 32), certSize = U32(h, 24 + 112 + 36);
        long minOverlay = pe + 24 + optionalSize + sections * 40;
        if (minOverlay > s.Length) throw new InvalidDataException();
        s.Position = pe + 24 + optionalSize;
        for (int i = 0; i < sections; i++) { byte[] sh = Read(s,40); long end = (long)U32(sh,20) + U32(sh,16); if (end > s.Length) throw new InvalidDataException(); minOverlay = Math.Max(minOverlay, end); }
        long boundary = s.Length;
        if (cert != 0 || certSize != 0)
        {
            if (cert < minOverlay || certSize < 8 || cert % 8 != 0 || certSize > s.Length - cert || cert + certSize != s.Length) throw new InvalidDataException("証明書テーブルの境界が不正です。");
            long p = cert;
            while (p < s.Length) { s.Position = p; byte[] ch = Read(s, 8); long n = U32(ch,0); if (n < 8 || n > s.Length-p || BinaryPrimitives.ReadUInt16LittleEndian(ch.AsSpan(4)) != 0x200 || BinaryPrimitives.ReadUInt16LittleEndian(ch.AsSpan(6)) != 2) throw new InvalidDataException(); p = checked(p + ((n + 7) & ~7L)); }
            if (p != s.Length) throw new InvalidDataException(); boundary = cert;
        }
        for (int pad = 0; pad <= (cert != 0 ? 7 : 0); pad++)
        {
            long at = boundary - pad - FooterSize;
            if (at < minOverlay) continue;
            s.Position = at; byte[] f = Read(s, FooterSize);
            if (!f.AsSpan(0,8).SequenceEqual(EndMagic)) continue;
            if (U32(f,8) != 1 || U32(f,12) != 0 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(f.AsSpan(0,32)), f.AsSpan(32))) throw new InvalidDataException();
            long off = I64(f,16), len = I64(f,24);
            if (off < minOverlay || len < HeaderSize + 16 || len > Limits.Total + 60L * 1024 * 1024 || off > at || len != at-off) throw new InvalidDataException();
            if (pad != 0 && Read(s,pad).Any(x => x != 0)) throw new InvalidDataException();
            return (off,len);
        }
        throw new InvalidDataException("暗号化コンテナーを検出できません。");
    }
    public static (long Offset,long Length) LocateCcap(FileStream s)
    {
        if(s.Length<HeaderSize+16+FooterSize)throw new InvalidDataException("有効なCCAPファイルではありません。");
        s.Position=0;if(!Read(s,8).AsSpan().SequenceEqual(Magic))throw new InvalidDataException("CCAP形式ではありません。自己復号EXEは直接起動してください。");
        s.Position=s.Length-FooterSize;byte[] f=Read(s,FooterSize);long length=I64(f,24);
        if(!f.AsSpan(0,8).SequenceEqual(EndMagic)||U32(f,8)!=1||U32(f,12)!=0||I64(f,16)!=0||length<HeaderSize+16||length>Limits.Total+60L*1024*1024||length!=s.Length-FooterSize||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(f.AsSpan(0,32)),f.AsSpan(32)))throw new InvalidDataException("CCAPの末尾・長さを検証できません。");
        return (0,length);
    }
    public static void GenerateCcap(string output,string body,BodyKind kind,IReadOnlyList<string> paths,byte[] password,bool overwrite,CancellationToken ct,IProgress<CapsuleProgress>? progress=null,bool allowCopy=true)
        =>Generate(Stream.Null,output,body,kind,paths,password,overwrite,ct,progress,allowCopy);
    public static void Generate(Stream template, string output, string body, BodyKind kind, IReadOnlyList<string> paths, byte[] password, bool overwrite, CancellationToken ct, IProgress<CapsuleProgress>? progress = null, bool allowCopy = true)
    {
        Limits.Password(Limits.Utf8.GetString(password)); Limits.BodyText(body); Limits.Inputs(paths);
        if (!Enum.IsDefined(kind)) throw new ArgumentException();
        string dest = Path.GetFullPath(output);
        if (paths.Any(p => string.Equals(Path.GetFullPath(p), dest, StringComparison.OrdinalIgnoreCase))) throw new IOException("添付元と同じパスには出力できません。");
        string temp = AtomicFile.Temporary(dest);
        byte[] dek = Sodium.Random(32), salt = Sodium.Random(16), wn = Sodium.Random(24), mn = Sodium.Random(24);
        byte[]? kek = null, mk = null, plain = null;
        try
        {
            ct.ThrowIfCancellationRequested(); progress?.Report(new("鍵を導出しています（Argon2id）",0,1));
            kek = Sodium.PasswordKey(password,salt); ct.ThrowIfCancellationRequested();
            byte[] header = new byte[HeaderSize]; Magic.CopyTo(header,0); P32(header,8,1); P32(header,12,1); P64(header,16,3); P64(header,24,256L*1024*1024);
            salt.CopyTo(header,32); wn.CopyTo(header,48); mn.CopyTo(header,72);
            Sodium.Encrypt(dek,header[..96],wn,kek).CopyTo(header,96); Sodium.Wipe(kek); kek=null;
            using (var target = new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,Limits.Chunk,FileOptions.SequentialScan))
            {
                template.CopyTo(target); long start = target.Position; target.Write(header);
                var manifest = new Manifest { Body=body, Kind=kind, AllowCopy=allowCopy };
                long total = paths.Sum(p => new FileInfo(p).Length), done = 0;
                for (int i = 0; i < paths.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    using var input = Open(paths[i]);
                    var before = new FileInfo(paths[i]); long size=input.Length; DateTime modified=before.LastWriteTimeUtc;
                    if (size > Limits.File || done + size > Limits.Total) throw new IOException("登録後に添付容量が変更されました。");
                    var item = new AttachmentInfo { Name=before.Name, Size=size, Offset=target.Position-start };
                    byte[] key=Sodium.Derive(dek,(ulong)i+1,"CCATTCH1"), state=Sodium.NewState(), buf=new byte[Limits.Chunk], enc=new byte[Limits.Chunk+17];
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    try
                    {
                        byte[] sh=Sodium.InitPush(state,key); target.Write(sh); hash.AppendData(sh);
                        long left=size;
                        do {
                            ct.ThrowIfCancellationRequested(); int n=(int)Math.Min(left,Limits.Chunk); input.ReadExactly(buf.AsSpan(0,n)); left-=n;
                            int count=Sodium.Push(state,enc,buf,n,left==0); byte[] length=new byte[4]; P32(length,0,(uint)count);
                            target.Write(length); target.Write(enc,0,count); hash.AppendData(length); hash.AppendData(enc,0,count);
                            done+=n; progress?.Report(new($"添付を暗号化しています ({i+1}/{paths.Count})",done,Math.Max(1,total)));
                        } while(left>0);
                        before.Refresh(); if (input.Length!=size || input.ReadByte()!=-1 || before.Length!=size || before.LastWriteTimeUtc!=modified) throw new IOException("暗号化中に入力ファイルが変更されました。");
                        item.CipherLength=target.Position-start-item.Offset; item.Sha256=Convert.ToHexString(hash.GetHashAndReset()); manifest.Attachments.Add(item);
                    }
                    finally { Sodium.Wipe(key); Sodium.Wipe(state); Sodium.Wipe(buf); Sodium.Wipe(enc); }
                }
                long mo=target.Position-start;
                plain=JsonSerializer.SerializeToUtf8Bytes(manifest); mk=Sodium.Derive(dek,0,"CCMANIF1");
                P64(header,144,mo); P64(header,152,plain.Length+16); P32(header,160,(uint)paths.Count); P32(header,164,Limits.Chunk);
                byte[] cipher=Sodium.Encrypt(plain,header,mn,mk); target.Write(cipher);
                long len=target.Position-start;
                byte[] footer=new byte[FooterSize]; EndMagic.CopyTo(footer,0); P32(footer,8,1); P64(footer,16,start); P64(footer,24,len); SHA256.HashData(footer.AsSpan(0,32)).CopyTo(footer,32); target.Write(footer);
                target.Position=start; target.Write(header); target.Flush(true);
            }
            progress?.Report(new("生成データを再検証しています",0,1));
            using (var verify=Unlock(temp,password,ct)) { }
            ct.ThrowIfCancellationRequested(); AtomicFile.Commit(temp,dest,overwrite);
            progress?.Report(new("完了",1,1));
        }
        finally { AtomicFile.Delete(temp); Sodium.Wipe(dek); Sodium.Wipe(kek); Sodium.Wipe(mk); Sodium.Wipe(plain); }
    }
    public static UnlockedCapsule Unlock(string path, byte[] password, CancellationToken ct, IProgress<CapsuleProgress>? progress=null,bool ccapOnly=false)
    {
        FileStream? s=null; byte[]? kek=null,dek=null,mk=null,plain=null;
        try
        {
            Limits.Password(Limits.Utf8.GetString(password),false); s=Open(path); var loc=ccapOnly?LocateCcap(s):Locate(s); s.Position=loc.Offset;
            byte[] h=Read(s,HeaderSize);
            if (!h.AsSpan(0,8).SequenceEqual(Magic) || U32(h,8)!=1 || U32(h,12)!=1 || I64(h,16)!=3 || I64(h,24)!=256L*1024*1024 || U32(h,160)>Limits.Count || U32(h,164)!=Limits.Chunk || I64(h,168)!=0) throw new InvalidDataException();
            long mo=I64(h,144),ml=I64(h,152);
            if (mo<HeaderSize || ml<16 || ml>52L*1024*1024 || mo>loc.Length || ml!=loc.Length-mo) throw new InvalidDataException();
            ct.ThrowIfCancellationRequested(); progress?.Report(new("パスワードを検証しています",0,1));
            kek=Sodium.PasswordKey(password,h[32..48]); ct.ThrowIfCancellationRequested();
            dek=Sodium.Decrypt(h[96..144],h[..96],h[48..72],kek); Sodium.Wipe(kek); kek=null;
            mk=Sodium.Derive(dek,0,"CCMANIF1"); s.Position=loc.Offset+mo;
            plain=Sodium.Decrypt(Read(s,(int)ml),h,h[72..96],mk);
            var m=JsonSerializer.Deserialize<Manifest>(plain) ?? throw new InvalidDataException();
            if (!Enum.IsDefined(m.Kind) || m.Body==null || m.Attachments==null || m.Attachments.Count!=U32(h,160)) throw new InvalidDataException();
            Limits.BodyText(m.Body); long expected=HeaderSize,total=0;
            for(int i=0;i<m.Attachments.Count;i++)
            {
                var a=m.Attachments[i]; if(a==null) throw new InvalidDataException(); Limits.FileName(a.Name);
                if(a.Size<0 || a.Size>Limits.File) throw new InvalidDataException();
                long chunks=Math.Max(1,(a.Size+Limits.Chunk-1)/Limits.Chunk),cl=24+a.Size+chunks*21;
                if(a.Offset!=expected || a.CipherLength!=cl || cl>mo-expected || a.Sha256==null || a.Sha256.Length!=64) throw new InvalidDataException();
                s.Position=loc.Offset+expected; using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                HashRange(s,cl,hash,ct,progress,$"添付を検証しています ({i+1}/{m.Attachments.Count})");
                if(!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),Convert.FromHexString(a.Sha256))) throw new CryptographicException();
                // Check every public frame boundary before displaying the message.
                s.Position=loc.Offset+expected+24; long left=a.Size;
                do { int n=(int)Math.Min(left,Limits.Chunk); if(U32(Read(s,4),0)!=n+17) throw new InvalidDataException(); s.Position+=n+17; left-=n; } while(left>0);
                expected=checked(expected+cl); total=checked(total+a.Size); if(total>Limits.Total) throw new InvalidDataException();
            }
            if(expected!=mo) throw new InvalidDataException();
            var result=new UnlockedCapsule(s,loc.Offset,m,dek); s=null; dek=null; return result;
        }
        catch(OperationCanceledException) { throw; }
        catch { throw new CryptographicException(AuthFailure); }
        finally { s?.Dispose(); Sodium.Wipe(kek); Sodium.Wipe(dek); Sodium.Wipe(mk); Sodium.Wipe(plain); }
    }
    static void HashRange(Stream s,long length,IncrementalHash hash,CancellationToken ct,IProgress<CapsuleProgress>? p,string stage)
    {
        byte[] b=new byte[Limits.Chunk]; long left=length;
        while(left>0) { ct.ThrowIfCancellationRequested(); int n=(int)Math.Min(left,b.Length); s.ReadExactly(b.AsSpan(0,n)); hash.AppendData(b,0,n); left-=n; p?.Report(new(stage,length-left,length)); }
    }
    public sealed class UnlockedCapsule : IDisposable
    {
        readonly FileStream source; readonly long start; readonly byte[] dek; readonly SemaphoreSlim gate=new(1,1); bool disposed;
        public Manifest Manifest { get; }
        internal UnlockedCapsule(FileStream source,long start,Manifest m,byte[] dek) { this.source=source; this.start=start; Manifest=m; this.dek=dek; }
        public void Save(int index,string output,bool overwrite,CancellationToken ct,IProgress<CapsuleProgress>? progress=null)
        {
            gate.Wait(ct); string? temp=null; byte[]? key=null,state=null,plain=null,enc=null;
            try
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                if(index<0 || index>=Manifest.Attachments.Count) throw new ArgumentOutOfRangeException(nameof(index));
                string dest=Path.GetFullPath(output); Limits.FileName(Path.GetFileName(dest));
                if(string.Equals(dest,source.Name,StringComparison.OrdinalIgnoreCase)) throw new IOException("実行中の暗号化EXEには保存できません。");
                temp=AtomicFile.Temporary(dest); var a=Manifest.Attachments[index];
                key=Sodium.Derive(dek,(ulong)index+1,"CCATTCH1"); state=Sodium.NewState(); plain=new byte[Limits.Chunk]; enc=new byte[Limits.Chunk+17];
                source.Position=start+a.Offset; Sodium.InitPull(state,Read(source,24),key); long left=a.Size;
                using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,Limits.Chunk,FileOptions.SequentialScan))
                {
                    do {
                        ct.ThrowIfCancellationRequested(); int n=(int)Math.Min(left,Limits.Chunk),cl=checked((int)U32(Read(source,4),0));
                        if(cl!=n+17) throw new CryptographicException(AuthFailure);
                        source.ReadExactly(enc.AsSpan(0,cl)); left-=n;
                        if(Sodium.Pull(state,plain,enc,cl,left==0)!=n) throw new CryptographicException(AuthFailure);
                        target.Write(plain,0,n); Sodium.Wipe(plain); progress?.Report(new("添付を保存しています",a.Size-left,Math.Max(1,a.Size)));
                    } while(left>0);
                    if(source.Position!=start+a.Offset+a.CipherLength) throw new CryptographicException(AuthFailure);
                    target.Flush(true);
                }
                ct.ThrowIfCancellationRequested(); AtomicFile.Commit(temp,dest,overwrite);
            }
            finally { AtomicFile.Delete(temp); Sodium.Wipe(key); Sodium.Wipe(state); Sodium.Wipe(plain); Sodium.Wipe(enc); gate.Release(); }
        }
        public void Dispose() { gate.Wait(); try { if(disposed)return; disposed=true; Sodium.Wipe(dek); Manifest.Body=""; source.Dispose(); } finally { gate.Release(); } }
    }
}
