using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using XenophyteAndroidWallet.User;
using XenophyteAndroidWallet.Wallet;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;

namespace XenophyteAndroidWallet.Sync
{
    public class ClassSortingTransactionType
    {
        public const string TransactionSendType = "SEND";
        public const string TransactionRecvType = "RECV";
    }

    public class ClassSortingTransaction
    {
        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassSortingTransaction(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }

        /// <summary>
        /// Decrypt and sorting a transaction received on sync, finaly save it.
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="walletAddress"></param>
        /// <param name="walletPublicKey"></param>
        /// <param name="anonymous"></param>
        /// <returns></returns>
        public void SaveTransactionSorted(string transaction, string walletAddress, string walletPublicKey, bool anonymous)
        {
            var splitTransaction = transaction.Split(new[] { "#" }, StringSplitOptions.None);
            string type = splitTransaction[0];
            string timestamp = splitTransaction[3];
            string hashTransaction = splitTransaction[4];
            string timestampReceived = splitTransaction[5];
            string blockchainHeight = splitTransaction[6];
            string realTransactionInformationSenderSide = splitTransaction[7];
            string realTransactionInformationReceiverSide = splitTransaction[8];

            string realTransactionInformationDecrypted = "NULL";
            if (type == ClassSortingTransactionType.TransactionSendType)
                realTransactionInformationDecrypted = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, realTransactionInformationSenderSide, walletAddress + walletPublicKey, ClassWalletNetworkSetting.KeySize);
            else if (type == ClassSortingTransactionType.TransactionRecvType)
                realTransactionInformationDecrypted = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, realTransactionInformationReceiverSide, walletAddress + walletPublicKey, ClassWalletNetworkSetting.KeySize);

            if (realTransactionInformationDecrypted != "NULL" && realTransactionInformationDecrypted != ClassAlgoErrorEnumeration.AlgoError)
            {
                var splitDecryptedTransactionInformation = realTransactionInformationDecrypted.Split(new[] { "-" }, StringSplitOptions.None);
                string amountTransaction = splitDecryptedTransactionInformation[0];
                string feeTransaction = splitDecryptedTransactionInformation[1];
                string walletAddressDstOrSrc = splitDecryptedTransactionInformation[2];
                string finalTransaction;
                if (anonymous)
                {
                    finalTransaction = "anonymous#" + type + "#" + hashTransaction + "#" + walletAddressDstOrSrc + "#" + amountTransaction + "#" + feeTransaction + "#" + timestamp + "#" + timestampReceived + "#" + blockchainHeight;
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].InsertWalletTransactionSync(finalTransaction, true, true);
                }
                else
                {
                    finalTransaction = "normal#" + type + "#" + hashTransaction + "#" + walletAddressDstOrSrc + "#" + amountTransaction + "#" + feeTransaction + "#" + timestamp + "#" + timestampReceived + "#" + blockchainHeight;
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].InsertWalletTransactionSync(finalTransaction, false, true);
                }

            }
        }

        /// <summary>
        /// Calculate balance from sync.
        /// </summary>
        /// <param name="walletAddress"></param>
        public void CalculateBalanceFromSync(string walletAddress)
        {
            double currentWalletBalance = 0.00000000d;
            double currentWalletPendingBalance = 0.00000000d;
            foreach (var transaction in _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].WalletListOfTransaction.ToArray())
            {
                var splitTransaction = transaction.Value.Split(new[] { "#" }, StringSplitOptions.None);
#if DEBUG
                Debug.WriteLine("Calculate balance of wallet address: " + walletAddress + " from the transaction: " + transaction.Value);
#endif
                string type = splitTransaction[1];
                double amountTransaction = double.Parse(splitTransaction[4].Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);
                double feeTransaction = double.Parse(splitTransaction[5].Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);
                switch (type)
                {
                    case ClassSortingTransactionType.TransactionRecvType:
                        if (long.Parse(splitTransaction[7]) <= DateTimeOffset.Now.ToUnixTimeSeconds())
                            currentWalletBalance += amountTransaction;
                        else
                            currentWalletPendingBalance += amountTransaction;
                        break;
                    case ClassSortingTransactionType.TransactionSendType:
                        currentWalletBalance -= amountTransaction;
                        currentWalletBalance -= feeTransaction;
                        break;
                }
            }

            foreach (var transaction in _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].WalletListOfAnonymousTransaction.ToArray())
            {
                var splitTransaction = transaction.Value.Split(new[] { "#" }, StringSplitOptions.None);
#if DEBUG
                Debug.WriteLine("Calculate balance of wallet address: " + walletAddress + " from the anonymous transaction: " + transaction.Value);
#endif
                string type = splitTransaction[1];
                double amountTransaction = double.Parse(splitTransaction[4].Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);
                double feeTransaction = double.Parse(splitTransaction[5].Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);

                switch (type)
                {
                    case ClassSortingTransactionType.TransactionRecvType:
                        if (long.Parse(splitTransaction[7]) <= DateTimeOffset.Now.ToUnixTimeSeconds())
                            currentWalletBalance += amountTransaction;
                        else
                            currentWalletPendingBalance += amountTransaction;
                        break;
                    case ClassSortingTransactionType.TransactionSendType:
                        currentWalletBalance -= amountTransaction;
                        currentWalletBalance -= feeTransaction;
                        break;
                }
            }

#if DEBUG
            Debug.WriteLine("Balance of the wallet address: "+walletAddress+" calculated from sync: "+currentWalletBalance.ToString(ClassUserSetting.GlobalCultureInfo).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin);
            Debug.WriteLine("Pending Balance of the wallet address: " + walletAddress + " calculated from sync: " + currentWalletPendingBalance.ToString(ClassUserSetting.GlobalCultureInfo).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin);
#endif
            _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletBalance(currentWalletBalance);
            _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletPendingBalance(currentWalletPendingBalance);
            _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds() + ClassWalletUpdater.WalletUpdateIntervalObject);
            _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdateSuccess(DateTimeOffset.Now.ToUnixTimeSeconds() + ClassWalletUpdater.WalletUpdateIntervalObjectSuccessDelay);

        }
    }

}