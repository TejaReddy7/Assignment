using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace IplStore.Infrastructure.Identity;

public class AesPasswordHasher : IPasswordHasher<ApplicationUser>
{
    // A fixed key for demo purposes. In a real app, use a secure key from config.
    private readonly byte[] _key = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJKLMNOPQRSTUV"); // 32 bytes

    public string HashPassword(ApplicationUser user, string password)
    {
        return Encrypt(password);
    }

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        var decrypted = Decrypt(hashedPassword);
        if (decrypted == providedPassword)
            return PasswordVerificationResult.Success;

        // Fallback for previously hashed passwords (if any exist)
        try 
        {
            // We can't easily fallback to IdentityV3 hasher without injecting it, 
            // but we can just fail if it doesn't match decrypted.
        }
        catch {}
        
        return PasswordVerificationResult.Failed;
    }

    private string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        
        return Convert.ToBase64String(result);
    }

    private string Decrypt(string cipherText)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = _key;
            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];
            
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
