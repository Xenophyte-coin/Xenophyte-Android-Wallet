using System.Globalization;
using Xenophyte_Connector_All.Setting;

namespace XenophyteAndroidWallet.User
{
    public class ClassUserSetting
    {
        public static string UserWalletPassword;
        public static bool WalletSystemBusy;
        public static string CurrentWalletAddressSelected; // By wallet address.

        public static decimal CurrentAmountSendTransaction;
        public static decimal CurrentFeeSendTransaction = ClassConnectorSetting.MinimumWalletTransactionFee;
        public static string CurrentWalletAddressTargetSendTransaction = string.Empty;
        public static CultureInfo GlobalCultureInfo = new CultureInfo("fr-FR"); // Set the global culture info, I don't suggest to change this, this one is used by the blockchain and by the whole network.
    }
}