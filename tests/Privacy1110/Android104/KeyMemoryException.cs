using System.Security.Cryptography;
namespace CipherCapsule.Core;
public sealed class KeyMemoryException():CryptographicException("Argon2idの鍵導出に必要な256MiBのメモリを確保できません。暗号強度は変更せず処理を中止しました。");
