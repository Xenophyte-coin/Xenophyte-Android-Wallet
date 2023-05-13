using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
#if DEBUG
using System.Diagnostics;
#endif

namespace XenophyteAndroidWallet.Graphic
{
    public class GraphicSetting
    {
        public const int MaxTransactionPerPage = 7;
        public static int CurrentTransactionPage = 1;
        public static ConcurrentDictionary<string, TransactionGraphicObject> ListTransactionHistory = new ConcurrentDictionary<string, TransactionGraphicObject>();
        public static ConcurrentDictionary<string, Rectangle> ListTransactionPositionHistory = new ConcurrentDictionary<string, Rectangle>();
    
        /// <summary>
        /// Check if this one have an available transaction next page.
        /// </summary>
        /// <returns></returns>
        public static bool CanIncrementPage()
        {
            if (ListTransactionHistory != null)
            {
                double totalPage = Math.Ceiling((double)ListTransactionHistory.Count / MaxTransactionPerPage);

                if (CurrentTransactionPage + 1 <= totalPage)
                    return true;
            }

            return false;
        }
    }
}