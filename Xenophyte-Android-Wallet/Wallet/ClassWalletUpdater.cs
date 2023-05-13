using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using XenophyteAndroidWallet.Other;
using XenophyteAndroidWallet.User;
using Xenophyte_Connector_All.RPC;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;
using ClassAlgo = XenophyteAndroidWallet.Algo.ClassAlgo;
using ClassAlgoEnumeration = XenophyteAndroidWallet.Algo.ClassAlgoEnumeration;
using ClassAlgoErrorEnumeration = XenophyteAndroidWallet.Algo.ClassAlgoErrorEnumeration;

namespace XenophyteAndroidWallet.Wallet
{
    public class ClassWalletUpdater
    {
        /// <summary>
        /// Const status result
        /// </summary>
        private const string RpcTokenNetworkNotExist = "not_exist";
        private const string RpcTokenNetworkWalletAddressNotExist = "wallet_address_not_exist";
        private const string RpcTokenNetworkWalletBusyOnUpdate = "WALLET-BUSY-ON-UPDATE";

        private CancellationTokenSource _cancellationTokenAutoUpdateWallet;
        private const int WalletUpdateInterval = 10 * 1000; // Each 10 seconds.
        public const int WalletUpdateIntervalObject = 10; // Each 10 seconds.
        public const int WalletUpdateIntervalObjectSuccessDelay = 30; // Each 30 seconds.

        public bool OnUpdateWallet;
        private bool _autoUpdateWalletEnabled;
        private bool _autoSyncWalletEnabled;
        private Dictionary<IPAddress, int> _listOfSeedNodesSpeed = new Dictionary<IPAddress, int>();
        public Dictionary<IPAddress, bool> ListOfSeedNodesAlive = new Dictionary<IPAddress, bool>();
        private HttpClient _httpClient;
        private Thread _threadSyncWallet;
        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassWalletUpdater(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }

        /// <summary>
        /// Enable auto check of seed nodes status.
        /// </summary>
        public void EnableAutoCheckSeedNodes()
        {
            try
            {
                if (_cancellationTokenAutoUpdateWallet != null)
                {
                    if (!_cancellationTokenAutoUpdateWallet.IsCancellationRequested)
                        _cancellationTokenAutoUpdateWallet.Cancel();
                }
            }
            catch
            {
                // Ignored.
            }
            _cancellationTokenAutoUpdateWallet = new CancellationTokenSource();
            try
            {
                Task.Factory.StartNew(async delegate ()
                {
                    while (true)
                    {
                        _listOfSeedNodesSpeed = GetSeedNodeSpeedList();
                        foreach (var seedNode in _listOfSeedNodesSpeed)
                        {
                            bool seedNodeAlive = false;
                            Task taskCheckSeedNode = Task.Run(async () => seedNodeAlive = await CheckTcp.CheckTcpClientAsync(seedNode.Key, ClassConnectorSetting.SeedNodeTokenPort));
                            taskCheckSeedNode.Wait(ClassConnectorSetting.MaxTimeoutConnect);

                            if (ListOfSeedNodesAlive.ContainsKey(seedNode.Key))
                                ListOfSeedNodesAlive[seedNode.Key] = seedNodeAlive;
                            else
                                ListOfSeedNodesAlive.Add(seedNode.Key, seedNodeAlive);

                        }
                        if (!_autoUpdateWalletEnabled)
                        {
                            _autoUpdateWalletEnabled = true;
                            EnableAutoUpdateWallet();
                        }
                        await Task.Delay(WalletUpdateInterval);
                    }
                }, _cancellationTokenAutoUpdateWallet.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }

        /// <summary>
        /// Enable the auto update wallet system.
        /// </summary>
        public void EnableAutoUpdateWallet()
        {
            try
            {
                Task.Factory.StartNew(async delegate ()
                {
                    while (true)
                    {
                        OnUpdateWallet = true;

                        try
                        {
                            if (_mainInterface.WalletDatabase.AndroidWalletDatabase != null)
                            {
                                if (_mainInterface.WalletDatabase.AndroidWalletDatabase.Count > 0)
                                {
                                    IPAddress getSeedNodeRandom = null;
                                    bool seedNodeSelected = false;
                                    foreach (var seedNode in ListOfSeedNodesAlive)
                                    {
                                        if (!seedNodeSelected)
                                        {
                                            if (seedNode.Value)
                                            {
                                                getSeedNodeRandom = seedNode.Key;
                                                seedNodeSelected = true;
                                            }
                                        }
                                    }
                                    if (seedNodeSelected)
                                    {
                                        foreach (var walletObject in _mainInterface.WalletDatabase.AndroidWalletDatabase.ToArray())
                                        {
                                            try
                                            {
                                                if (!walletObject.Value.GetWalletUpdateStatus() && walletObject.Value.GetLastWalletUpdate() <= DateTimeOffset.Now.ToUnixTimeSeconds())
                                                {
                                                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds() + WalletUpdateIntervalObject);
                                                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].SetWalletOnUpdateStatus(true);
                                                    UpdateWalletTarget(getSeedNodeRandom, walletObject.Key);
                                                }
                                            }
                                            catch (Exception error)
                                            {
#if DEBUG
                                                Debug.WriteLine("Error on update wallet: " + walletObject.Key + " | Exception: " + error.Message);
#endif
                                            }
                                        }
                                    }
                                }
#if DEBUG
                                else
                                {
                                    Debug.WriteLine("Their is any wallet saved on the database to update.");
                                }
#endif
                            }
#if DEBUG
                            else
                            {
                                Debug.WriteLine("Wallet Database not initialized, try to update their stats.");
                            }
#endif
                        }
                        catch (Exception error)
                        {
#if DEBUG
                            Debug.WriteLine("Error on function EnableAutoUpdateWallet | Exception: " + error.Message);
#endif
                        }
                        OnUpdateWallet = false;
                        await Task.Delay(WalletUpdateInterval);
                    }

                }, _cancellationTokenAutoUpdateWallet.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }


        /// <summary>
        /// Update wallet target
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        private void UpdateWalletTarget(IPAddress getSeedNodeRandom, string walletAddress)
        {
            Task.Factory.StartNew(async delegate
            {

#if DEBUG
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
#endif
                try
                {
                    if (!await GetWalletBalanceTokenAsync(getSeedNodeRandom, walletAddress))
                    {
                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdate(0);
#if DEBUG
                        Debug.WriteLine("Wallet: " + walletAddress + " update failed. Node: " + getSeedNodeRandom);
#endif
                    }
                    else
                    {
                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds() + WalletUpdateIntervalObject);
#if DEBUG
                        Debug.WriteLine("Wallet: " + walletAddress + " updated successfully. Node: " + getSeedNodeRandom);
#endif
                    }
                }
                catch (Exception error)
                {
#if DEBUG
                    Debug.WriteLine("Error on update wallet: " + walletAddress + " exception: " + error.Message);
#endif
                }
#if DEBUG
                stopwatch.Stop();
                Debug.WriteLine("Wallet: " + walletAddress + " updated in: " + stopwatch.ElapsedMilliseconds + " ms. Node: " + getSeedNodeRandom);
#endif
                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnUpdateStatus(false);

            }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Current).ConfigureAwait(false);
        }

        #region Token functions

        /// <summary>
        /// Get wallet token from token system.
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        private async Task<string> GetWalletTokenAsync(IPAddress getSeedNodeRandom, string walletAddress)
        {
            string encryptedRequest = ClassRpcWalletCommand.TokenAsk + "|empty-token|" + (DateTimeOffset.Now.ToUnixTimeSeconds() + 1).ToString("F0");
            encryptedRequest = ClassAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.AesNetwork, encryptedRequest, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword(), ClassWalletNetworkSetting.KeySize);

            //string responseWallet = await ProceedTokenRequestHttpAsync("http://" + hostTarget + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator  + walletAddress + ClassConnectorSetting.PacketContentSeperator  + encryptedRequest);
            string responseWallet = await ProceedTokenRequestTcpAsync(getSeedNodeRandom, ClassConnectorSetting.SeedNodeTokenPort, ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator + walletAddress + ClassConnectorSetting.PacketContentSeperator + encryptedRequest);

            try
            {
                if (!string.IsNullOrEmpty(responseWallet))
                {
#if DEBUG
                    Debug.WriteLine("Response received: " + responseWallet);
#endif
                    var responseWalletJson = JObject.Parse(responseWallet);



                    responseWallet = responseWalletJson["result"].ToString();
#if DEBUG
                    Debug.WriteLine("Json content received: " + responseWallet);
#endif
                    if (responseWallet != RpcTokenNetworkNotExist)
                    {
                        responseWallet = ClassAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, responseWallet, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword(), ClassWalletNetworkSetting.KeySize);
#if DEBUG
                        Debug.WriteLine("Token Packet Received Decrypted result: " + responseWallet);
#endif

                        var splitResponseWallet = responseWallet.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                        if ((long.Parse(splitResponseWallet[splitResponseWallet.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                        {
                            if (long.Parse(splitResponseWallet[splitResponseWallet.Length - 1]) + 60 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                            {
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletCurrentToken(true, splitResponseWallet[1]);
                                return splitResponseWallet[1];
                            }
                        }
                    }
                }

                return RpcTokenNetworkNotExist;
            }
            catch (Exception error)
            {
#if DEBUG
                Debug.WriteLine("Exception GetWalletTokenAsync: " + error.Message);
                Debug.WriteLine("Packet response: " + responseWallet);
#endif
                return RpcTokenNetworkNotExist;
            }
        }

        /// <summary>
        /// Update wallet balance from token system.
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        public async Task<bool> GetWalletBalanceTokenAsync(IPAddress getSeedNodeRandom, string walletAddress)
        {
            string token = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            if (token != RpcTokenNetworkNotExist)
            {
                if (_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletCurrentToken().Item1)
                {
                    token = _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletCurrentToken().Item2;
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletCurrentToken(false, string.Empty);
                    if (_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetLastWalletUpdateSuccess() != 0)
                    {
                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdateSuccess(DateTimeOffset.Now.ToUnixTimeSeconds() + WalletUpdateIntervalObject);
                    }
                    string encryptedRequest = ClassRpcWalletCommand.TokenAskBalance + ClassConnectorSetting.PacketContentSeperator + token + ClassConnectorSetting.PacketContentSeperator + (DateTimeOffset.Now.ToUnixTimeSeconds() + 1);
                    encryptedRequest = ClassAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.AesNetwork, encryptedRequest, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword(), ClassWalletNetworkSetting.KeySize);
                    await Task.Delay(100);

                    //string responseWallet = await ProceedTokenRequestHttpAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator  + walletAddress + ClassConnectorSetting.PacketContentSeperator  + encryptedRequest);
                    string responseWallet = await ProceedTokenRequestTcpAsync(getSeedNodeRandom, ClassConnectorSetting.SeedNodeTokenPort, ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator + walletAddress + ClassConnectorSetting.PacketContentSeperator + encryptedRequest);

                    try

                    {
#if DEBUG
                        Debug.WriteLine("Json content received: " + responseWallet);
#endif
                        var responseWalletJson = JObject.Parse(responseWallet);
                        responseWallet = responseWalletJson["result"].ToString();

                        if (responseWallet != RpcTokenNetworkNotExist)
                        {
                            responseWallet = ClassAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, responseWallet, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword() + token, ClassWalletNetworkSetting.KeySize);
#if DEBUG
                            Debug.WriteLine("Token Packet Received Decrypted result: " + responseWallet);
#endif
                            if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                            {
                                string walletBalance = responseWallet;
                                var splitWalletBalance = walletBalance.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                                if ((long.Parse(splitWalletBalance[splitWalletBalance.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                                {
                                    if (long.Parse(splitWalletBalance[splitWalletBalance.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                    {
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletBalance(double.Parse(splitWalletBalance[1], NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo));
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletPendingBalance(double.Parse(splitWalletBalance[2], NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo));
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletUniqueId(splitWalletBalance[3]);
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletAnonymousUniqueId(splitWalletBalance[4]);
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdateSuccess(DateTimeOffset.Now.ToUnixTimeSeconds() + WalletUpdateIntervalObjectSuccessDelay);
                                        if (!_autoSyncWalletEnabled)
                                        {
                                            _autoSyncWalletEnabled = true;
                                            _threadSyncWallet = new Thread(async () => await _mainInterface.SyncNetwork.ConnectRpcWalletToRemoteNodeSyncAsync());
                                            _threadSyncWallet.Start();
                                        }
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
#if DEBUG
                        Debug.WriteLine("Exception GetWalletBalanceTokenAsync: " + error.Message);
                        Debug.WriteLine("From content: " + responseWallet);
#endif
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }


        /// <summary>
        /// Send a transaction with token system with a selected wallet address, amount and fee.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="amount"></param>
        /// <param name="fee"></param>
        /// <param name="walletAddressTarget"></param>
        /// <param name="anonymous"></param>
        /// <returns></returns>
        public async Task<string> ProceedTransactionTokenRequestAsync(string walletAddress, string amount, string fee, string walletAddressTarget, bool anonymous)
        {
#if DEBUG
            if (anonymous)
            {
                Debug.WriteLine("Attempt to send an anonymous transaction from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " and anonymous fee option of: " + ClassConnectorSetting.MinimumWalletTransactionAnonymousFee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget);
            }
            else
            {
                Debug.WriteLine("Attempt to send transaction from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget);
            }
#endif
            if (_mainInterface.WalletDatabase.AndroidWalletDatabase.ContainsKey(walletAddress))
            {
                if (!_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletUpdateStatus() && !_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletOnSendTransactionStatus())
                {
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(true);
                    double balanceFromDatabase = double.Parse(_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletBalance().Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);
                    double balanceFromRequest = double.Parse(amount.Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);
                    double feeFromRequest = double.Parse(fee.Replace(".", ","), NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo);

                    if (balanceFromRequest + feeFromRequest <= balanceFromDatabase)
                    {

                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds());
                        IPAddress getSeedNodeRandom = null;
                        bool seedNodeSelected = false;
                        foreach (var seedNode in ListOfSeedNodesAlive)
                        {
                            if (!seedNodeSelected)
                            {
                                if (seedNode.Value)
                                {
                                    getSeedNodeRandom = seedNode.Key;
                                    seedNodeSelected = true;
                                }
                            }
                        }
                        if (seedNodeSelected)
                        {
                            return await SendWalletTransactionTokenAsync(getSeedNodeRandom, walletAddress, walletAddressTarget, amount, fee, anonymous);
                        }

#if DEBUG
                        Debug.WriteLine("Error on send transaction from wallet: " + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress] + " exception: can't connect on each seed nodes checked.");
#endif
                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                        return ClassRpcWalletCommand.SendTokenTransactionRefused + "|None";
                    }
#if DEBUG
                    Debug.WriteLine("Error on send transaction from wallet: " + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress] + " amount insufficient.");
#endif
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                    return ClassRpcWalletCommand.SendTokenTransactionRefused + "|None";
                }

                if (_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletUpdateStatus())
                {
                    return RpcTokenNetworkWalletBusyOnUpdate + "|None";
                }

                return ClassRpcWalletCommand.SendTokenTransactionBusy + "|None";
            }

            return RpcTokenNetworkWalletAddressNotExist + "|None";

        }

        /// <summary>
        /// Send a transaction from a selected wallet address stored to a specific wallet address target.
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        /// <param name="walletAddressTarget"></param>
        /// <param name="amount"></param>
        /// <param name="fee"></param>
        /// <param name="anonymous"></param>
        /// <returns></returns>
        private async Task<string> SendWalletTransactionTokenAsync(IPAddress getSeedNodeRandom, string walletAddress, string walletAddressTarget, string amount, string fee, bool anonymous)
        {

            string tokenWallet = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            if (tokenWallet != RpcTokenNetworkNotExist)
            {
                if (_mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletCurrentToken().Item1)
                {
                    tokenWallet = _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletCurrentToken().Item2;
                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletCurrentToken(false, string.Empty);

                    string encryptedRequest;
                    if (anonymous)
                    {
                        encryptedRequest = ClassRpcWalletCommand.TokenAskWalletSendTransaction + ClassConnectorSetting.PacketContentSeperator + tokenWallet + ClassConnectorSetting.PacketContentSeperator + walletAddressTarget + ClassConnectorSetting.PacketContentSeperator + amount + ClassConnectorSetting.PacketContentSeperator + fee + "|1|" + (DateTimeOffset.Now.ToUnixTimeSeconds() + 1).ToString("F0");
                    }
                    else
                    {
                        encryptedRequest = ClassRpcWalletCommand.TokenAskWalletSendTransaction + ClassConnectorSetting.PacketContentSeperator + tokenWallet + ClassConnectorSetting.PacketContentSeperator + walletAddressTarget + ClassConnectorSetting.PacketContentSeperator + amount + ClassConnectorSetting.PacketContentSeperator + fee + "|0|" + (DateTimeOffset.Now.ToUnixTimeSeconds() + 1).ToString("F0");
                    }
                    encryptedRequest = ClassAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.AesNetwork, encryptedRequest, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword(), ClassWalletNetworkSetting.KeySize);

                    //string responseWallet = await ProceedTokenRequestHttpAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator  + walletAddress + ClassConnectorSetting.PacketContentSeperator  + encryptedRequest);
                    string responseWallet = await ProceedTokenRequestTcpAsync(getSeedNodeRandom, ClassConnectorSetting.SeedNodeTokenPort, ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator + walletAddress + ClassConnectorSetting.PacketContentSeperator + encryptedRequest);

                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnUpdateStatus(false);
                    try
                    {
                        if (!string.IsNullOrEmpty(responseWallet))
                        {
#if DEBUG
                            Debug.WriteLine("Response wallet received: " + responseWallet);
#endif
                            var responseWalletJson = JObject.Parse(responseWallet);
                            responseWallet = responseWalletJson["result"].ToString();
                            if (responseWallet != RpcTokenNetworkNotExist)
                            {
                                responseWallet = ClassAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, responseWallet, walletAddress + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPublicKey() + _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].GetWalletPassword() + tokenWallet, ClassWalletNetworkSetting.KeySize);
                                if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                                {
                                    string walletTransaction = responseWallet;
                                    if (responseWallet != RpcTokenNetworkNotExist)
                                    {
                                        var splitWalletTransaction = walletTransaction.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                                        if ((long.Parse(splitWalletTransaction[splitWalletTransaction.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                                        {
                                            if (long.Parse(splitWalletTransaction[splitWalletTransaction.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                            {
                                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletBalance(double.Parse(splitWalletTransaction[1], NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo));
                                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletPendingBalance(double.Parse(splitWalletTransaction[2], NumberStyles.Currency, ClassUserSetting.GlobalCultureInfo));
#if DEBUG
                                                Debug.WriteLine("Send transaction response " + splitWalletTransaction[0] + " from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " transaction hash: " + splitWalletTransaction[3].ToLower() + " to target -> " + walletAddressTarget);
#endif
                                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                                                return splitWalletTransaction[0] + ClassConnectorSetting.PacketContentSeperator + splitWalletTransaction[3];
                                            }
                                            return splitWalletTransaction[0] + "|expired_packet";
                                        }
                                    }
                                    else
                                    {
                                        _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                                        return ClassRpcWalletCommand.SendTokenTransactionBusy + "|None";
                                    }
                                }
                                else
                                {
                                    _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                                    return ClassRpcWalletCommand.SendTokenTransactionBusy + "|None";
                                }
                            }
                            else
                            {
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
                                return ClassRpcWalletCommand.SendTokenTransactionRefused + "|None";
                            }
                        }
                    }
                    catch (Exception error)
                    {
#if DEBUG
                        Debug.WriteLine("Exception SendWalletTransactionTokenAsync: " + error.Message);
#endif
                    }
                }
            }

#if DEBUG
            Debug.WriteLine("Send transaction refused from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget);
#endif
            _mainInterface.WalletDatabase.AndroidWalletDatabase[walletAddress].SetWalletOnSendTransactionStatus(false);
            return ClassRpcWalletCommand.SendTokenTransactionRefused + "|None";
        }

        #endregion

        #region Other functions


        /// <summary>
        /// Get Seed Node list sorted by the faster to the slowest one.
        /// </summary>
        /// <returns></returns>
        public Dictionary<IPAddress, int> GetSeedNodeSpeedList()
        {
            if (_listOfSeedNodesSpeed.Count == 0)
            {
                foreach (var seedNode in ClassConnectorSetting.SeedNodeIp.ToArray())
                {

                    try
                    {
                        int seedNodeResponseTime = -1;
                        Task taskCheckSeedNode = Task.Run(() => seedNodeResponseTime = CheckPing.CheckPingHost(seedNode.Key, true));
                        taskCheckSeedNode.Wait(ClassConnectorSetting.MaxPingDelay);
                        if (seedNodeResponseTime == -1)
                        {
                            seedNodeResponseTime = ClassConnectorSetting.MaxSeedNodeTimeoutConnect;
                        }
#if DEBUG
                        Debug.WriteLine(seedNode.Key + " response time: " + seedNodeResponseTime + " ms.");
#endif
                        _listOfSeedNodesSpeed.Add(seedNode.Key, seedNodeResponseTime);

                    }
                    catch
                    {
                        _listOfSeedNodesSpeed.Add(seedNode.Key, ClassConnectorSetting.MaxSeedNodeTimeoutConnect); // Max delay.
                    }

                }
            }
            else if (_listOfSeedNodesSpeed.Count != ClassConnectorSetting.SeedNodeIp.Count)
            {
#if DEBUG
                Debug.WriteLine("New seed node(s) listed, update the list of seed nodes sorted by their ping time.");
#endif
                var tmpListOfSeedNodesSpeed = new Dictionary<IPAddress, int>();
                foreach (var seedNode in ClassConnectorSetting.SeedNodeIp.ToArray())
                {

                    try
                    {
                        int seedNodeResponseTime = -1;
                        Task taskCheckSeedNode = Task.Run(() => seedNodeResponseTime = CheckPing.CheckPingHost(seedNode.Key, true));
                        taskCheckSeedNode.Wait(ClassConnectorSetting.MaxPingDelay);
                        if (seedNodeResponseTime == -1)
                        {
                            seedNodeResponseTime = ClassConnectorSetting.MaxSeedNodeTimeoutConnect;
                        }
#if DEBUG
                        Debug.WriteLine(seedNode.Key + " response time: " + seedNodeResponseTime + " ms.");
#endif
                        tmpListOfSeedNodesSpeed.Add(seedNode.Key, seedNodeResponseTime);

                    }
                    catch
                    {
                        tmpListOfSeedNodesSpeed.Add(seedNode.Key, ClassConnectorSetting.MaxSeedNodeTimeoutConnect); // Max delay.
                    }

                }
                _listOfSeedNodesSpeed = tmpListOfSeedNodesSpeed;
#if DEBUG
                Debug.WriteLine("List of seed nodes sorted by their ping time done.");
#endif
            }
            return _listOfSeedNodesSpeed.OrderBy(u => u.Value).ToDictionary(z => z.Key, y => y.Value);
        }


        /// <summary>
        /// Proceed token request in full TCP Mode
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task<string> ProceedTokenRequestTcpAsync(IPAddress host, int port, string packet)
        {

            string httpTokenPacket = "GET /" + packet + " HTTP/1.1\r\n";

            using (var tokenPacketObject = new HttpTcpClient(WalletUpdateIntervalObject))
                return await tokenPacketObject.ProceedTokenPacketByTcp(host, port, httpTokenPacket);
        }

        /// <summary>
        /// Proceed token request throught http protocol.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<string> ProceedTokenRequestHttpAsync(string url)
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
            }

            try
            {
                var response = await _httpClient.GetAsync(url);

                string packetReceived = await response.Content.ReadAsStringAsync();
#if DEBUG
                Debug.WriteLine("Packet Token Network received: " + packetReceived);
#endif
                return packetReceived;
            }
            catch
            {
                // Ignored.
            }


            return RpcTokenNetworkNotExist;
        }

        #endregion

    }
}