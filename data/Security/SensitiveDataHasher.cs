using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;

namespace data.Security
{
    /// <summary>
    /// Veritabanındaki tüm kişisel verileri (PII: E-Posta, Kullanıcı Adı, Öğrenci No, Ad, Soyad, TC Kimlik No vb.)
    /// PostgreSQL'de düz metin (plaintext) yerine deterministik kriptografik hash/şifre ($h256$...) formatında saklar.
    /// Deterministik SIV (HMAC-SHA256 + AES-256) yapısı sayesinde hem veritabanında tüm kişisel sütunlar hash'lenmiş görünür
    /// hem de e-posta ile sorgulama (WHERE "Email" = '$h256$...') indeks üzerinden birebir çalışır.
    /// </summary>
    public static class SensitiveDataHasher
    {
        public const string Prefix = "$h256$";

        private static readonly byte[] MasterCipherKey = SHA256.HashData(
            Encoding.UTF8.GetBytes("OptiMatrix_Enterprise_OMR_PII_Vault_CipherKey_2026_v1"));

        private static readonly byte[] MasterHmacKey = SHA256.HashData(
            Encoding.UTF8.GetBytes("OptiMatrix_Enterprise_OMR_PII_Vault_HmacKey_2026_v1"));

        public static string Protect(string? plainText, bool normalizeLower = false)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
                return plainText;

            string normalized = normalizeLower
                ? plainText.Trim().ToLowerInvariant()
                : plainText;

            byte[] plainBytes = Encoding.UTF8.GetBytes(normalized);

            // Deterministik 16-byte SIV (Synthetic IV) = HMAC-SHA256(MasterHmacKey, plainBytes)[0..16]
            byte[] fullHmac;
            using (var hmac = new HMACSHA256(MasterHmacKey))
            {
                fullHmac = hmac.ComputeHash(plainBytes);
            }

            byte[] iv = new byte[16];
            Buffer.BlockCopy(fullHmac, 0, iv, 0, 16);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = MasterCipherKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // [16 byte SIV/Hash] + [CipherBytes] -> Hex string
            byte[] combined = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);

            return Prefix + Convert.ToHexString(combined).ToLowerInvariant();
        }

        public static string Unprotect(string? storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
                return string.Empty;

            if (!storedValue.StartsWith(Prefix, StringComparison.Ordinal))
                return storedValue;

            string hexPayload = storedValue.Substring(Prefix.Length);
            try
            {
                byte[] combined = Convert.FromHexString(hexPayload);
                if (combined.Length <= 16)
                    return storedValue;

                byte[] iv = new byte[16];
                byte[] cipherBytes = new byte[combined.Length - 16];
                Buffer.BlockCopy(combined, 0, iv, 0, 16);
                Buffer.BlockCopy(combined, 16, cipherBytes, 0, cipherBytes.Length);

                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = MasterCipherKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return storedValue;
            }
        }

        public static ValueConverter<string, string> CreatePersonalDataConverter() =>
            new ValueConverter<string, string>(
                v => Protect(v, false),
                v => Unprotect(v));

        public static ValueConverter<string, string> CreateNormalizedEmailConverter() =>
            new ValueConverter<string, string>(
                v => Protect(v, true),
                v => Unprotect(v));
    }
}
