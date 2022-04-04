using DVRSDK.Log;
using DVRSDK.Utilities;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DVRSDK.Encrypt
{
    public class DotNetEncrypter : IEncrypter
    {
        private const string CONTAINER_NAME = "HenaCspKeyContainer";

        protected RSACryptoServiceProvider CreateProvider()
        {
            CspParameters cp = new CspParameters();
            cp.KeyContainerName = CONTAINER_NAME;

            return new RSACryptoServiceProvider(cp);
        }

        public string Decrypt(string encryptedString)
        {
            var decrypted = Decrypt(Convert.FromBase64String(encryptedString));

            return Encoding.UTF8.GetString(decrypted);
        }

        public byte[] Decrypt(byte[] src)
        {
            try
            {
                using (var rsa = CreateProvider())
                {
                    return rsa.Decrypt(src, false);
                }
            }
            catch
            {
                return EncryptedDataStorage.DecryptData(src);
            }
        }

        public async Task<string> DecryptAsync(string encryptedString)
        {
            var decryptedString = "";

            try
            {
                decryptedString = await Task.Run(() =>
                {
                    return Decrypt(encryptedString);
                });
            }
            catch (Exception e)
            {
                DebugLog.LogError(e.Message);
            }

            return decryptedString;
        }

        public string Encrypt(string plainString)
        {
            var encrypted = Encrypt(Encoding.UTF8.GetBytes(plainString));

            return Convert.ToBase64String(encrypted);
        }

        public byte[] Encrypt(byte[] src)
        {
            try
            {
                using (var rsa = CreateProvider())
                {
                    return rsa.Encrypt(src, false);
                }
            }
            catch
            {
                return EncryptedDataStorage.EncryptData(src);
            }
        }

        public async Task<string> EncryptAsync(string plainString)
        {
            var encryptedString = "";

            try
            {
                encryptedString = await Task.Run(() =>
                {
                    return Encrypt(plainString);
                });
            }
            catch (Exception e)
            {
                DebugLog.LogError(e.Message);
            }

            return encryptedString;
        }
    }
}
