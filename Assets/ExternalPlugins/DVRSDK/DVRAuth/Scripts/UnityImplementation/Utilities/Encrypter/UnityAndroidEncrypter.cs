using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRSDK.Encrypt
{
    public class UnityAndroidEncrypter : IEncrypter
    {
        protected const string CLASS_NAME = "com.dmm.vrlab.hena.security.AndroidKeyStoreEncrypter";

        public string Encrypt(string plainString)
        {
            string encryptedString = "";

            using (AndroidJavaObject androidKeyStoreEncrypter = new AndroidJavaObject(CLASS_NAME))
            {
                encryptedString = androidKeyStoreEncrypter.Call<string>("encrypt", plainString);
            }

            return encryptedString;
        }

        public async Task<string> EncryptAsync(string plainString)
        {
            var encryptedString = "";

            try
            {
                using (AndroidJavaObject androidKeyStoreEncrypter = new AndroidJavaObject(CLASS_NAME))
                {
                    await Task.Run(() =>
                    {
                        AndroidJNI.AttachCurrentThread();
                        encryptedString = androidKeyStoreEncrypter.Call<string>("encrypt", plainString);
                        AndroidJNI.DetachCurrentThread();
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            return encryptedString;
        }

        public string Decrypt(string encryptedString)
        {
            string decryptedString = "";

            using (AndroidJavaObject androidKeyStoreEncrypter = new AndroidJavaObject(CLASS_NAME))
            {
                decryptedString = androidKeyStoreEncrypter.Call<string>("decrypt", encryptedString);
            }

            return decryptedString;
        }

        public async Task<string> DecryptAsync(string encryptedString)
        {
            var decryptedString = "";

            try
            {
                using (AndroidJavaObject androidKeyStoreEncrypter = new AndroidJavaObject(CLASS_NAME))
                {
                    await Task.Run(() =>
                    {
                        AndroidJNI.AttachCurrentThread();
                        decryptedString = androidKeyStoreEncrypter.Call<string>("decrypt", encryptedString);
                        AndroidJNI.DetachCurrentThread();
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            return decryptedString;
        }
    }
}
