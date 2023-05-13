using System;
#if DEBUG
using System.Diagnostics;
#endif
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xenophyte_Connector_All.Seed;
using Xenophyte_Connector_All.Utils;
using AesCryptoProviderCustom = XenophyteAndroidWallet.Other.AesCryptoProviderCustom;

namespace XenophyteAndroidWallet
{
    public class ClassAlgoErrorEnumeration
    {
        public const string AlgoError = "WRONG";
    }

    public class ClassAlgoEnumeration
    {
        public const string AesNetwork = "AES-NETWORK"; // 0
        public const string RijndaelWallet = "RIJNDAEL-WALLET"; // 1
        public const string Xor = "XOR"; // 2
    }

    /// <summary>
    /// Copied from Xenophyte-Connector-All, replace AesManagedProvider by AesCryptoProviderCustom on Decryption part for fix a bug on System.Core.dll (I think the update about this library is not published yet.)
    /// </summary>
    public class ClassCustomAlgo
    {


        /// <summary>
        ///     Decrypt the result received and retrieve it.
        /// </summary>
        /// <param name="idAlgo"></param>
        /// <param name="result"></param>
        /// <param name="key"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string GetDecryptedResultManual(string idAlgo, string result, string key, int size)
        {
            if (result == ClassSeedNodeStatus.SeedNone || result == ClassSeedNodeStatus.SeedError)
            {
                return result;
            }

            try
            {
                switch (idAlgo)
                {
                    case ClassAlgoEnumeration.RijndaelWallet:
                        if (ClassUtils.IsBase64String(result))
                        {
                            return ClassAes.DecryptStringManualWallet(result, key, size);
                        }

                        break;
                    case ClassAlgoEnumeration.AesNetwork:
                        if (ClassUtils.IsBase64String(result))
                        {
                            return ClassAes.DecryptStringManualNetwork(result, key, size);
                        }
                        break;
                    case ClassAlgoEnumeration.Xor:
                        break;
                }
            }
            catch (Exception erreur)
            {
#if DEBUG
                Debug.WriteLine("Error Decrypt of " + result + " with key: " + key + " : " + erreur.Message);
#endif
                return ClassAlgoErrorEnumeration.AlgoError;
            }

            return ClassAlgoErrorEnumeration.AlgoError;
        }


        /// <summary>
        ///     Encrypt the result received and retrieve it.
        /// </summary>
        /// <param name="idAlgo"></param>
        /// <param name="result"></param>
        /// <param name="key"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string GetEncryptedResultManual(string idAlgo, string result, string key, int size)
        {
            try
            {
                switch (idAlgo)
                {
                    case ClassAlgoEnumeration.AesNetwork:
                        return ClassAes.EncryptStringManualNetwork(result, key, size);
                    case ClassAlgoEnumeration.RijndaelWallet:
                        return ClassAes.EncryptStringManualWallet(result, key, size);
                    case ClassAlgoEnumeration.Xor:
                        break;
                }
            }
            catch (Exception erreur)
            {
#if DEBUG
                Debug.WriteLine("Error Encrypt of " + result + " : " + erreur.Message);
#endif
                return ClassAlgoErrorEnumeration.AlgoError;
            }

            return ClassAlgoErrorEnumeration.AlgoError;
        }

    }


    public class ClassAes
    {



        /// <summary>
        /// Encrypt string from AesCryptoServiceProvider.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        public static string EncryptStringManualNetwork(string plainText, string passPhrase, int keySize)
        {
            using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, Encoding.UTF8.GetBytes(ClassUtils.FromHex(passPhrase.Substring(0, 8)))))
            {
                byte[] keyBytes = password.GetBytes(keySize / 8);
                using (var symmetricKey = new AesCryptoProviderCustom() { Mode = CipherMode.CFB })
                {
                    byte[] initVectorBytes = password.GetBytes(16);
                    symmetricKey.BlockSize = 128;
                    symmetricKey.KeySize = keySize;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    symmetricKey.Key = keyBytes;
                    using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                Array.Clear(plainTextBytes, 0, plainTextBytes.Length);
                                Array.Clear(keyBytes, 0, keyBytes.Length);
                                Array.Clear(initVectorBytes, 0, initVectorBytes.Length);
                                return Convert.ToBase64String(memoryStream.ToArray());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decrypt string with AesCryptoServiceProvider.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        public static string DecryptStringManualNetwork(string cipherText, string passPhrase, int keySize)
        {
            using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, Encoding.UTF8.GetBytes(ClassUtils.FromHex(passPhrase.Substring(0, 8)))))
            {

                byte[] keyBytes = password.GetBytes(keySize / 8);
                using (var symmetricKey = new AesCryptoProviderCustom() { Mode = CipherMode.CFB })
                {
                    byte[] initVectorBytes = password.GetBytes(16);
                    symmetricKey.BlockSize = 128;
                    symmetricKey.KeySize = keySize;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    symmetricKey.Key = keyBytes;
                    using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                    {
                        byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
                        using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                                int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                Array.Clear(keyBytes, 0, keyBytes.Length);
                                Array.Clear(cipherTextBytes, 0, cipherTextBytes.Length);
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Encrypt string from AesNetwork.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        public static string EncryptStringManualWallet(string plainText, string passPhrase, int keySize)
        {
            using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, Encoding.UTF8.GetBytes(ClassUtils.FromHex(passPhrase.Substring(0, 8)))))
            {
                byte[] keyBytes = password.GetBytes(keySize / 8);
                using (var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CFB })
                {
                    byte[] initVectorBytes = password.GetBytes(16);
                    symmetricKey.BlockSize = 128;
                    symmetricKey.KeySize = keySize;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    symmetricKey.Key = keyBytes;
                    using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                Array.Clear(plainTextBytes, 0, plainTextBytes.Length);
                                Array.Clear(keyBytes, 0, keyBytes.Length);
                                Array.Clear(initVectorBytes, 0, initVectorBytes.Length);
                                return Convert.ToBase64String(memoryStream.ToArray());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decrypt string with AesNetwork.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        public static string DecryptStringManualWallet(string cipherText, string passPhrase, int keySize)
        {
            using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, Encoding.UTF8.GetBytes(ClassUtils.FromHex(passPhrase.Substring(0, 8)))))
            {

                byte[] keyBytes = password.GetBytes(keySize / 8);
                using (var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CFB })
                {
                    byte[] initVectorBytes = password.GetBytes(16);
                    symmetricKey.BlockSize = 128;
                    symmetricKey.KeySize = keySize;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    symmetricKey.Key = keyBytes;
                    using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                    {
                        byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
                        using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                                int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                Array.Clear(keyBytes, 0, keyBytes.Length);
                                Array.Clear(cipherTextBytes, 0, cipherTextBytes.Length);
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }
    }
}