using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;

namespace XenophyteAndroidWallet.Sync
{
    public class ClassSyncDatabaseEnumeration
    {
        public const string DatabaseSyncStartLine = "[TRANSACTION]";
        public const string DatabaseAnonymousTransactionMode = "anonymous";
        public const string DatabaseAnonymousTransactionType = "ANONYMOUS";
    }

    public class ClassSyncDatabase
    {
        private const string XenophyteDatabaseDir = "Xenophyte";
        private const string SyncDatabaseFile = "walletsync.xenopdb";
        private StreamWriter _syncDatabaseStreamWriter;
        public bool InSave;
        private long _totalTransactionRead;

        public Dictionary<string, long> DatabaseTransactionSync = new Dictionary<string, long>();

        private static string walletDirectory = Android.App.Application.Context.GetExternalFilesDir(Android.OS.Environment.DirectoryDocuments).AbsolutePath + "/" + XenophyteDatabaseDir + "/";
        private readonly string walletSyncDatabasePath = walletDirectory + SyncDatabaseFile;

        /// <summary>
        /// Object interface access.
        /// </summary>
        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="interface"></param>
        public ClassSyncDatabase(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }

        /// <summary>
        /// Initialize sync database.
        /// </summary>
        /// <returns></returns>
        public bool InitializeSyncDatabase()
        {

            try
            {
                if (!Directory.Exists(walletDirectory))
                    Directory.CreateDirectory(walletDirectory);

                if (!File.Exists(walletSyncDatabasePath))
                    File.Create(walletSyncDatabasePath).Close();
                else
                {
                    using (FileStream fs = File.Open(walletSyncDatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (StreamReader sr = new StreamReader(bs))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (line.Contains(ClassSyncDatabaseEnumeration.DatabaseSyncStartLine))
                                    {
                                        string transactionLine = line.Replace(ClassSyncDatabaseEnumeration.DatabaseSyncStartLine, "");
                                        var splitTransactionLine = transactionLine.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                                        string walletAddress = splitTransactionLine[0];
                                        if (_mainInterface.WalletDatabase.AndroidWalletDatabase.ContainsKey(walletAddress))
                                        {
                                            string transaction = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, splitTransactionLine[1], walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
                                            transaction += "#" + walletAddress;

                                            var splitTransaction = transaction.Split(new[] { "#" }, StringSplitOptions.None);
                                            if (splitTransaction[0] == ClassSyncDatabaseEnumeration.DatabaseAnonymousTransactionMode)
                                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].InsertWalletTransactionSync(transaction, true, false);
                                            else
                                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].InsertWalletTransactionSync(transaction, false, false);

                                            if (!DatabaseTransactionSync.ContainsKey(transaction))
                                                DatabaseTransactionSync.Add(transaction, long.Parse(splitTransaction[7]));
                                            
                                            _totalTransactionRead++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (DatabaseTransactionSync.Count > 0)
                    {
                        foreach (var wallet in _mainInterface.WalletDatabase.AndroidWalletDatabase.ToArray())
                            _mainInterface.SortingTransaction.CalculateBalanceFromSync(wallet.Key);
                    }
                }
            }
            catch
            {
                return false;
            }
            _syncDatabaseStreamWriter = new StreamWriter(walletSyncDatabasePath, true, Encoding.UTF8, 8192) { AutoFlush = true };
#if DEBUG
            Debug.WriteLine("Total transaction read from sync database: " + _totalTransactionRead);
#endif
            return true;
        }

        /// <summary>
        /// Insert a new transaction to database.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="walletPublicKey"></param>
        /// <param name="transaction"></param>
        public async void InsertTransactionToSyncDatabaseAsync(string walletAddress, string walletPublicKey, string transaction)
        {
            await Task.Factory.StartNew(delegate
            {
                InSave = true;
                bool success = false;
                while (!success)
                {
                    try
                    {
                        string transactionTmp = transaction + "#" + walletAddress;
                        var splitTransaction = transactionTmp.Split(new[] { "#" }, StringSplitOptions.None);
                        if (!DatabaseTransactionSync.ContainsKey(transactionTmp))
                            DatabaseTransactionSync.Add(transactionTmp, long.Parse(splitTransaction[7]));

                        transaction = ClassCustomAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, transaction, walletAddress + walletPublicKey, ClassWalletNetworkSetting.KeySize);
                        string transactionLine = ClassSyncDatabaseEnumeration.DatabaseSyncStartLine + walletAddress + ClassConnectorSetting.PacketContentSeperator + transaction;
                        _syncDatabaseStreamWriter.WriteLine(transactionLine);
                        success = true;
                    }
                    catch
                    {
                        _syncDatabaseStreamWriter = new StreamWriter(walletSyncDatabasePath, true, Encoding.UTF8, 8192) { AutoFlush = true };
                    }
                }
                _totalTransactionRead++;
#if DEBUG
                Debug.WriteLine("Total transaction saved: " + DatabaseTransactionSync.Count);
#endif
                InSave = false;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
        }
    }
}