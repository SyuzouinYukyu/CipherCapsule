using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
namespace CipherCapsule.Core;

public sealed record AttachmentInput(string Name,long? Size,Func<Stream> Open);
public static partial class Container
{
    // Exact v1.1.7 framing, KDF contexts and authenticated header; only input acquisition differs.
    public static void GenerateStreams(string output,string body,BodyKind kind,IReadOnlyList<AttachmentInput> inputs,byte[] password,bool allowCopy,CancellationToken ct,IProgress<CapsuleProgress>? progress=null)
    {
        Limits.Password(Limits.Utf8.GetString(password));Limits.BodyText(body);
        if(!Enum.IsDefined(kind)||inputs.Count>Limits.Count)throw new ArgumentException("本文形式または添付件数が不正です。");
        long known=0;
        foreach(var item in inputs){Limits.FileName(item.Name);if(item.Size is long n){if(n<0||n>Limits.File)throw new IOException("単一添付は256MiBまでです。");known=checked(known+n);}}
        if(known>Limits.Total)throw new IOException("添付合計は512MiBまでです。");
        string temp=AtomicFile.Temporary(output);byte[] dek=Sodium.Random(32),salt=Sodium.Random(16),wn=Sodium.Random(24),mn=Sodium.Random(24);byte[]? kek=null,mk=null,plain=null;
        try
        {
            ct.ThrowIfCancellationRequested();progress?.Report(new("鍵を導出しています（256MiB）",0,1));kek=Sodium.PasswordKey(password,salt);ct.ThrowIfCancellationRequested();
            byte[] h=new byte[HeaderSize];Magic.CopyTo(h,0);P32(h,8,1);P32(h,12,1);P64(h,16,3);P64(h,24,256L*1024*1024);salt.CopyTo(h,32);wn.CopyTo(h,48);mn.CopyTo(h,72);
            Sodium.Encrypt(dek,h[..96],wn,kek).CopyTo(h,96);Sodium.Wipe(kek);kek=null;
            using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {
                target.Write(h);var manifest=new Manifest{Body=body,Kind=kind,AllowCopy=allowCopy};long total=0;
                for(int i=0;i<inputs.Count;i++)
                {
                    ct.ThrowIfCancellationRequested();var item=inputs[i];using var source=item.Open();
                    if(!source.CanRead)throw new IOException("添付を読み込めません。");
                    var a=new AttachmentInfo{Name=item.Name,Offset=target.Position};byte[] key=Sodium.Derive(dek,(ulong)i+1,"CCATTCH1"),state=Sodium.NewState(),buffer=new byte[Limits.Chunk],enc=new byte[Limits.Chunk+17],frame=new byte[4];
                    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    try
                    {
                        var sh=Sodium.InitPush(state,key);target.Write(sh);hash.AppendData(sh);
                        int look=source.ReadByte();
                        do
                        {
                            ct.ThrowIfCancellationRequested();int count=0;
                            if(look>=0){buffer[count++]=(byte)look;while(count<buffer.Length){ct.ThrowIfCancellationRequested();int n=source.Read(buffer,count,buffer.Length-count);if(n==0)break;count+=n;}}
                            look=source.ReadByte();
                            a.Size=checked(a.Size+count);total=checked(total+count);
                            if(a.Size>Limits.File||total>Limits.Total)throw new IOException("添付容量の上限を超えました（単体256MiB・合計512MiB）。");
                            int frameLength=Sodium.Push(state,enc,buffer,count,look<0);BinaryPrimitives.WriteUInt32LittleEndian(frame,(uint)frameLength);
                            target.Write(frame);target.Write(enc,0,frameLength);hash.AppendData(frame);hash.AppendData(enc,0,frameLength);Sodium.Wipe(buffer);
                            progress?.Report(new($"添付を暗号化しています ({i+1}/{inputs.Count})",total,Math.Max(known,total)));
                        }while(look>=0);
                        if(item.Size is long size&&size!=a.Size)throw new IOException("処理中に添付サイズが変更されました。");
                        a.CipherLength=target.Position-a.Offset;a.Sha256=Convert.ToHexString(hash.GetHashAndReset());manifest.Attachments.Add(a);
                    }
                    finally{Sodium.Wipe(key);Sodium.Wipe(state);Sodium.Wipe(buffer);Sodium.Wipe(enc);}
                }
                long offset=target.Position;plain=JsonSerializer.SerializeToUtf8Bytes(manifest);mk=Sodium.Derive(dek,0,"CCMANIF1");P64(h,144,offset);P64(h,152,plain.Length+16);P32(h,160,(uint)inputs.Count);P32(h,164,Limits.Chunk);
                target.Write(Sodium.Encrypt(plain,h,mn,mk));long length=target.Position;byte[] footer=new byte[FooterSize];EndMagic.CopyTo(footer,0);P32(footer,8,1);P64(footer,16,0);P64(footer,24,length);SHA256.HashData(footer.AsSpan(0,32)).CopyTo(footer,32);target.Write(footer);target.Position=0;target.Write(h);target.Flush(true);
            }
            progress?.Report(new("生成内容を認証付き復号で自己検証しています",0,1));
            using(var verify=Unlock(temp,password,ct,ccapOnly:true))for(int i=0;i<verify.Manifest.Attachments.Count;i++)verify.SaveTo(i,Stream.Null,ct);
            ct.ThrowIfCancellationRequested();AtomicFile.Commit(temp,output,false);
        }
        finally{AtomicFile.Delete(temp);Sodium.Wipe(dek);Sodium.Wipe(kek);Sodium.Wipe(mk);Sodium.Wipe(plain);}
    }
    public sealed partial class UnlockedCapsule
    {
        // Caller owns destination and must delete an incomplete newly created document on failure.
        public void SaveTo(int index,Stream target,CancellationToken ct,IProgress<CapsuleProgress>? progress=null)
        {
            gate.Wait(ct);byte[]? key=null,state=null,plain=null,enc=null;
            try
            {
                ObjectDisposedException.ThrowIf(disposed,this);if(index<0||index>=Manifest.Attachments.Count)throw new ArgumentOutOfRangeException(nameof(index));
                var a=Manifest.Attachments[index];key=Sodium.Derive(dek,(ulong)index+1,"CCATTCH1");state=Sodium.NewState();plain=new byte[Limits.Chunk];enc=new byte[Limits.Chunk+17];source.Position=start+a.Offset;Sodium.InitPull(state,Read(source,24),key);long left=a.Size;
                do{ct.ThrowIfCancellationRequested();int n=(int)Math.Min(left,Limits.Chunk),cl=checked((int)U32(Read(source,4),0));if(cl!=n+17)throw new CryptographicException(AuthFailure);source.ReadExactly(enc.AsSpan(0,cl));left-=n;if(Sodium.Pull(state,plain,enc,cl,left==0)!=n)throw new CryptographicException(AuthFailure);target.Write(plain,0,n);Sodium.Wipe(plain);progress?.Report(new("添付を保存しています",a.Size-left,Math.Max(1,a.Size)));}while(left>0);
                if(source.Position!=start+a.Offset+a.CipherLength)throw new CryptographicException(AuthFailure);target.Flush();ct.ThrowIfCancellationRequested();
            }
            finally{Sodium.Wipe(key);Sodium.Wipe(state);Sodium.Wipe(plain);Sodium.Wipe(enc);gate.Release();}
        }
    }
}
