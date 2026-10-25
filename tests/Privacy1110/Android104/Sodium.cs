using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CipherCapsule.Core;

public static class Sodium
{
    const string Dll = "libsodium";
    static Sodium() { if (sodium_init() < 0) throw new CryptographicException("暗号処理を初期化できません。"); }
    public static string Version => Marshal.PtrToStringAnsi(sodium_version_string())!;
    public static void Wipe(byte[]? b) { if (b != null) CryptographicOperations.ZeroMemory(b); }
    public static byte[] Random(int n) { var b = new byte[n]; randombytes_buf(b, (nuint)n); return b; }
    public static byte[] PasswordKey(byte[] password, byte[] salt)
    {
        byte[] k = new byte[32];
        if (crypto_pwhash(k, 32, password, (ulong)password.Length, salt, 3, 256u * 1024 * 1024, 2) != 0)
        { Wipe(k); throw new KeyMemoryException(); }
        return k;
    }
    public static byte[] Derive(byte[] master, ulong id, string context)
    {
        byte[] k = new byte[32];
        if (crypto_kdf_derive_from_key(k, 32, id, Encoding.ASCII.GetBytes(context), master) != 0)
        { Wipe(k); throw new CryptographicException(); }
        return k;
    }
    public static byte[] Encrypt(byte[] data, byte[] aad, byte[] nonce, byte[] key)
    {
        byte[] c = new byte[data.Length + 16];
        if (crypto_aead_xchacha20poly1305_ietf_encrypt(c, out ulong n, data, (ulong)data.Length, aad, (ulong)aad.Length, IntPtr.Zero, nonce, key) != 0 || n != (ulong)c.Length) throw new CryptographicException();
        return c;
    }
    public static byte[] Decrypt(byte[] c, byte[] aad, byte[] nonce, byte[] key)
    {
        if (c.Length < 16) throw new CryptographicException();
        byte[] p = new byte[c.Length - 16];
        if (crypto_aead_xchacha20poly1305_ietf_decrypt(p, out ulong n, IntPtr.Zero, c, (ulong)c.Length, aad, (ulong)aad.Length, nonce, key) != 0 || n != (ulong)p.Length)
        { Wipe(p); throw new CryptographicException(); }
        return p;
    }
    public static byte[] NewState() => new byte[(int)crypto_secretstream_xchacha20poly1305_statebytes()];
    public static byte[] InitPush(byte[] state, byte[] key)
    {
        byte[] h = new byte[24];
        if (crypto_secretstream_xchacha20poly1305_init_push(state, h, key) != 0) throw new CryptographicException();
        return h;
    }
    public static void InitPull(byte[] state, byte[] h, byte[] key)
    { if (crypto_secretstream_xchacha20poly1305_init_pull(state, h, key) != 0) throw new CryptographicException(); }
    public static int Push(byte[] state, byte[] output, byte[] input, int length, bool final)
    {
        if (crypto_secretstream_xchacha20poly1305_push(state, output, out ulong n, input, (ulong)length, IntPtr.Zero, 0, final ? (byte)3 : (byte)0) != 0) throw new CryptographicException();
        return checked((int)n);
    }
    public static int Pull(byte[] state, byte[] output, byte[] input, int length, bool final)
    {
        if (crypto_secretstream_xchacha20poly1305_pull(state, output, out ulong n, out byte tag, input, (ulong)length, IntPtr.Zero, 0) != 0 || tag != (final ? 3 : 0))
        { Wipe(output); throw new CryptographicException(); }
        return checked((int)n);
    }
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sodium_init();
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sodium_version_string();
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void randombytes_buf(byte[] b, nuint size);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_pwhash(byte[] output, ulong len, byte[] password, ulong plen, byte[] salt, ulong ops, nuint mem, int alg);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_kdf_derive_from_key(byte[] output, nuint len, ulong id, byte[] ctx, byte[] master);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_aead_xchacha20poly1305_ietf_encrypt(byte[] c, out ulong clen, byte[] m, ulong mlen, byte[] ad, ulong adlen, IntPtr nsec, byte[] nonce, byte[] key);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_aead_xchacha20poly1305_ietf_decrypt(byte[] m, out ulong mlen, IntPtr nsec, byte[] c, ulong clen, byte[] ad, ulong adlen, byte[] nonce, byte[] key);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern nuint crypto_secretstream_xchacha20poly1305_statebytes();
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_secretstream_xchacha20poly1305_init_push(byte[] state, byte[] header, byte[] key);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_secretstream_xchacha20poly1305_init_pull(byte[] state, byte[] header, byte[] key);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_secretstream_xchacha20poly1305_push(byte[] state, byte[] c, out ulong clen, byte[] m, ulong mlen, IntPtr ad, ulong adlen, byte tag);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int crypto_secretstream_xchacha20poly1305_pull(byte[] state, byte[] m, out ulong mlen, out byte tag, byte[] c, ulong clen, IntPtr ad, ulong adlen);
}
