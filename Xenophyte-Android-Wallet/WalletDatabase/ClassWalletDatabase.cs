using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using XenophyteAndroidWallet.User;
using XenophyteAndroidWallet.Wallet;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Wallet;
using Encoding = System.Text.Encoding;
using Xenophyte_Connector_All.Utils;
#if DEBUG
using System.Diagnostics;
#endif

namespace XenophyteAndroidWallet.WalletDatabase
{
    public class ClassWalletDatabaseEnumeration
    {
        public const string DatabaseWalletStartLine = "[WALLET]";
    }

    public class ClassWalletDatabase
    {
        private const string XenophyteDatabaseDir = "Xenophyte";
        private const string WalletDatabaseFileName = "wallet.xenopdb";
        public  Dictionary<string, ClassWalletObject> AndroidWalletDatabase = new Dictionary<string, ClassWalletObject>();
        private  StreamWriter _walletDatabaseWriter;
        private string walletDirectoryPath = Android.App.Application.Context.GetExternalFilesDir(Android.OS.Environment.DirectoryDocuments).AbsolutePath + "\\" + XenophyteDatabaseDir + "\\";
        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassWalletDatabase(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }


        /// <summary>
        /// Check if a wallet database exist.
        /// </summary>
        /// <returns></returns>
        public  bool CheckWalletDatabase()
        {

            if (!Directory.Exists(walletDirectoryPath))
            {
#if DEBUG
                Debug.WriteLine("Wallet Database Directory: " + walletDirectoryPath + " not exist. Create it.");
#endif
                Directory.CreateDirectory(walletDirectoryPath);
            }

            if (!File.Exists(walletDirectoryPath + WalletDatabaseFileName))
            {
#if DEBUG
                Debug.WriteLine("Wallet Database File: " + walletDirectoryPath + WalletDatabaseFileName + " not exist. Create it.");
#endif
                File.Create(walletDirectoryPath + WalletDatabaseFileName).Close();
                return false;
            }

            int totalWallet = 0;
            using (FileStream fs = File.Open(walletDirectoryPath + WalletDatabaseFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BufferedStream bs = new BufferedStream(fs))
                {
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.StartsWith(ClassWalletDatabaseEnumeration.DatabaseWalletStartLine))
                            {
                                totalWallet++;
                            }
                        }
                    }
                }
            }

            if (totalWallet == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Load wallet database.
        /// </summary>
        /// <returns></returns>
        public  bool LoadWalletDatabase()
        {

            using (FileStream fs = File.Open(walletDirectoryPath + WalletDatabaseFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BufferedStream bs = new BufferedStream(fs))
                {
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string line;
                        int lineRead = 0;
                        while ((line = sr.ReadLine()) != null)
                        {
                            lineRead++;
                            if (line.StartsWith(ClassWalletDatabaseEnumeration.DatabaseWalletStartLine))
                            {
                                string walletData = line.Replace(ClassWalletDatabaseEnumeration.DatabaseWalletStartLine, "");
                                walletData = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, walletData, GenerateWalletEncryptionKey(ClassUserSetting.UserWalletPassword), ClassWalletNetworkSetting.KeySize);
                                if (walletData != ClassAlgoErrorEnumeration.AlgoError)
                                {
                                    var splitWalletData = walletData.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                                    if (!AndroidWalletDatabase.ContainsKey(splitWalletData[0]))
                                        AndroidWalletDatabase.Add(splitWalletData[0], new ClassWalletObject(splitWalletData[0], splitWalletData[1], splitWalletData[4], splitWalletData[2], splitWalletData[3], line.Replace(ClassWalletDatabaseEnumeration.DatabaseWalletStartLine, ""), _mainInterface));
                                }
                                else
                                {
#if DEBUG
                                    Debug.WriteLine("Error on wallet database file | cannot to decrypt wallet line: " + lineRead);
#endif
                                    return false;
                                }
                            }
                        }
                        if (lineRead == 0 || AndroidWalletDatabase.Count == 0)
                        {
#if DEBUG
                            Debug.WriteLine("Android Wallet don't contain any wallet on his database, switch to first initialization menu");
#endif
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Input a new wallet into database file.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="publicKey"></param>
        /// <param name="privateKey"></param>
        /// <param name="password"></param>
        /// <param name="pinCode"></param>
        public  bool InputWalletToDatabase(string walletAddress, string publicKey, string privateKey, string password, string pinCode)
        {

            if (AndroidWalletDatabase.ContainsKey(walletAddress))
            {
#if DEBUG
                Debug.WriteLine("Wallet Database File, already contain Wallet Address data of: "+walletAddress);
#endif
                return true;
            }

            if (_walletDatabaseWriter == null)
                _walletDatabaseWriter = new StreamWriter(walletDirectoryPath + WalletDatabaseFileName, true){AutoFlush = true};
            

            try
            {

                string walletData = walletAddress + ClassConnectorSetting.PacketContentSeperator +
                                    publicKey + ClassConnectorSetting.PacketContentSeperator +
                                    privateKey + ClassConnectorSetting.PacketContentSeperator + 
                                    pinCode + ClassConnectorSetting.PacketContentSeperator +
                                    password;
                AndroidWalletDatabase.Add(walletAddress, new ClassWalletObject(walletAddress, publicKey, password, privateKey, pinCode, walletData, _mainInterface));

                walletData = ClassCustomAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, walletData, GenerateWalletEncryptionKey(ClassUserSetting.UserWalletPassword), ClassWalletNetworkSetting.KeySize);

                walletData = ClassWalletDatabaseEnumeration.DatabaseWalletStartLine + walletData;

                _walletDatabaseWriter.WriteLine(walletData);
                return true;
            }
            catch(Exception error)
            {
#if DEBUG
                Debug.WriteLine("Can't input wallet data of Wallet Address: "+walletAddress+" to wallet database file encrypted. Exception: "+error.Message);
#endif
                try
                {
                    _walletDatabaseWriter?.Close();
                    _walletDatabaseWriter?.Dispose();
                    _walletDatabaseWriter = null;
                }
                catch(Exception errorStream)
                {
#if DEBUG
                    Debug.WriteLine("Can't close stream writer who target wallet database file encrypted. Exception: " + errorStream.Message);
#endif
                }
                return false;
            }
        }

        /// <summary>
        /// Generate an encryption key from the password before to use the final round of AES.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public  string GenerateWalletEncryptionKey(string password)
        {
            List<string> listSha256 = new List<string>();

            for(int i = 0; i < password.Length; i++)
            {
                if (i < password.Length)
                {
                    using (var sha = SHA512.Create())
                    {
                        byte[] passKey = Encoding.UTF8.GetBytes(password + password[i]);
                        string passKeyHex = ClassUtility.BytesToHex(sha.ComputeHash(passKey));
                        string encryptedKeyHex = ClassCustomAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, passKeyHex, password, ClassWalletNetworkSetting.KeySize);
                        listSha256.Add(encryptedKeyHex);
                    }
                }
            }

#if DEBUG
            Debug.WriteLine("New encryption key: "+ string.Join(string.Empty, listSha256)+" from password selected: " + password);
#endif
            return string.Join(string.Empty, listSha256);
        }
    }
}