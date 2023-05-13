using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XenophyteAndroidWallet.WalletDatabase;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;
using System.Net;

namespace XenophyteAndroidWallet.Wallet
{
    public class ClassWalletCreatorEnumeration
    {
        public const string WalletCreatorPending = "pending";
        public const string WalletCreatorError = "error";
        public const string WalletCreatorSuccess = "success";
    }

    public class ClassWalletCreator : IDisposable
    {

        #region Disposing Part Implementation 

        private bool _disposed;

        ~ClassWalletCreator()
        {
            Dispose(false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
            }

            _disposed = true;
        }

        #endregion

        /// <summary>
        /// Objects
        /// </summary>
        public string WalletPhase;
        private string _certificateConnection;
        private string _walletPassword;
        private string _walletPrivateKey;
        private string _walletAddress;
        public bool WalletInPendingCreate;
        public string WalletCreateResult;
        public string WalletAddressResult;

        /// <summary>
        /// Class objects
        /// </summary>
        public Socket _socketClient; // Used for connect the wallet to seed nodes.

        /// <summary>
        /// Threading
        /// </summary>
        private CancellationTokenSource _cancellationTokenListenNetwork;

        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClassWalletCreator(Interface mainInterface)
        {
            _mainInterface = mainInterface;
            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorPending;
        }

        /// <summary>
        /// Start to connect on the blockchain wallet network.
        /// </summary>
        /// <param name="walletPhase"></param>
        /// <param name="walletPassword"></param>
        /// <param name="privatekey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<bool> StartWalletConnectionAsync(string walletPhase, string walletPassword, string privatekey = null, string walletAddress = null)
        {

            WalletInPendingCreate = true;
            _walletPassword = walletPassword;
            WalletPhase = walletPhase;
            _walletPrivateKey = privatekey;
            _walletAddress = walletAddress;
            if (!await InitlizationWalletConnectionAsync())
            {
                FullDisconnection();
                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                return false;
            }

            _certificateConnection = ClassUtils.GenerateCertificate();
            if (!await SendPacketBlockchainNetworkWalletAsync(_certificateConnection, string.Empty, false))
            {
                FullDisconnection();
                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                return false;
            }

            ListenBlockchainNetworkWallet();


            switch (WalletPhase)
            {
                case ClassWalletPhase.Create:
                    await Task.Delay(1000);
                    if (!await SendPacketBlockchainNetworkWalletAsync(ClassWalletCommand.ClassWalletSendEnumeration.CreatePhase + ClassConnectorSetting.PacketContentSeperator + _walletPassword, _certificateConnection, true))
                    {
                        FullDisconnection();
                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                        return false;
                    }
                    break;
                case ClassWalletPhase.Restore:
                    using (ClassWalletRestoreFunctions walletRestoreFunctionsObject = new ClassWalletRestoreFunctions())
                    {
                        string encryptedQrCodeRestoreRequest = walletRestoreFunctionsObject.GenerateQrCodeKeyEncryptedRepresentation(privatekey, walletPassword);

                        if (encryptedQrCodeRestoreRequest != null)
                        {
                            await Task.Delay(1000);
                            if (!await SendPacketBlockchainNetworkWalletAsync(ClassWalletCommand.ClassWalletSendEnumeration.AskPhase + ClassConnectorSetting.PacketContentSeperator + encryptedQrCodeRestoreRequest, _certificateConnection, true))
                            {
                                FullDisconnection();
                                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                return false;
                            }
                        }
                        else
                        {
                            FullDisconnection();
                            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// Initialization of the wallet connection.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> InitlizationWalletConnectionAsync()
        {
           

            foreach (IPAddress ipAddress in ClassConnectorSetting.SeedNodeIp.Keys)
            {
                try
                {
                    _socketClient = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    await _socketClient.ConnectAsync(ipAddress, ClassConnectorSetting.SeedNodePort);
                    return true;
                }
                catch
                {
                    // Ignoed, catch the exception once the task is cancelled.
                }
            }

            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
            return false;
        }

        /// <summary>
        /// Full disconnection of the wallet.
        /// </summary>
        public void FullDisconnection()
        {

            try
            {
                if (_cancellationTokenListenNetwork != null)
                {
                    if (!_cancellationTokenListenNetwork.IsCancellationRequested)
                    {
                        _cancellationTokenListenNetwork.Cancel();
                        _cancellationTokenListenNetwork.Dispose();
                    }
                }
            }
            catch
            {
                // Ignored
            }

            try
            {
                _socketClient?.Close();
                _socketClient?.Dispose();
            }
            catch
            {
                // Ignored
            }

            WalletInPendingCreate = false;
            _walletAddress = string.Empty;
            _walletPassword = string.Empty;
            _walletPrivateKey = string.Empty;
            _certificateConnection = string.Empty;
        }

        /// <summary>
        /// Send packet to the blockchain network wallet.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="certificate"></param>
        /// <param name="encrypted"></param>
        public async Task<bool> SendPacketBlockchainNetworkWalletAsync(string packet, string certificate, bool encrypted)
        {

            try
            {
                byte[] packetToSend;

                if (encrypted)
                {
                    packetToSend = Encoding.UTF8.GetBytes(ClassCustomAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.AesNetwork, packet, certificate, ClassWalletNetworkSetting.KeySize));
                }
                else
                {
                    packetToSend = Encoding.UTF8.GetBytes(packet);
                }

                using(NetworkStream networkStream = new NetworkStream(_socketClient))
                {
                    await networkStream.WriteAsync(packetToSend, 0, packetToSend.Length);
                    await networkStream.FlushAsync();
                }
               
                Array.Clear(packetToSend, 0, packetToSend.Length);
            }
            catch
            {
                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Listen the blockchain network.
        /// </summary>
        public void ListenBlockchainNetworkWallet()
        {
            _cancellationTokenListenNetwork = new CancellationTokenSource();

            try
            {
                Task.Factory.StartNew(async delegate
                {
                    while (WalletCreateResult == ClassWalletCreatorEnumeration.WalletCreatorPending)
                    {

                        try
                        {
                            int received;

                            byte[] packetReceived = new byte[ClassConnectorSetting.MaxNetworkPacketSize];

                            using (var reader = new NetworkStream(_socketClient))
                            {
                                while ((received = await reader.ReadAsync(packetReceived, 0, packetReceived.Length)) > 0)
                                {

                                    if (WalletCreateResult != ClassWalletCreatorEnumeration.WalletCreatorPending)
                                    {
                                        break;
                                    }

                                    if (received > 0)
                                    {
                                        string packetWallet = Encoding.UTF8.GetString(packetReceived, 0, received);

#if DEBUG
                                        Debug.WriteLine("Packet Wallet Received: " + packetWallet);
#endif
                                        if (packetWallet.Contains(ClassConnectorSetting.PacketSplitSeperator)) // Character separator.
                                        {
                                            var splitPacket = packetWallet.Split(new[] { ClassConnectorSetting.PacketSplitSeperator }, StringSplitOptions.None);
                                            foreach (var packetEach in splitPacket)
                                            {
                                                if (!string.IsNullOrEmpty(packetEach))
                                                {
                                                    if (packetEach.Length > 5)
                                                    {
                                                        if (packetEach == ClassAlgoErrorEnumeration.AlgoError)
                                                        {
                                                            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                                            break;
                                                        }


                                                        string packetDecrypt = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, packetEach.Replace(ClassConnectorSetting.PacketSplitSeperator, ""), _certificateConnection, ClassWalletNetworkSetting.KeySize).Replace(ClassConnectorSetting.PacketSplitSeperator, "");
#if DEBUG
                                                        if (packetDecrypt != ClassAlgoErrorEnumeration.AlgoError)
                                                        {
                                                            Debug.WriteLine("1# - Packet successfully decrypted: " + packetDecrypt);
                                                        }
#endif
                                                        if (packetDecrypt == ClassAlgoErrorEnumeration.AlgoError)
                                                        {
#if DEBUG
                                                            Debug.WriteLine("1# - Packet decrypt error, can't decrypt packet received: " + packetEach);
#endif
                                                            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                                            break;
                                                        }

                                                        await Task.Run(() => HandlePacketBlockchainNetworkWalletAsync(packetDecrypt)).ConfigureAwait(false);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(packetWallet))
                                            {
                                                if (packetWallet.Length > 1)
                                                {
                                                    packetWallet = packetWallet.Replace(ClassConnectorSetting.PacketSplitSeperator, "");
                                                    if (packetWallet == ClassAlgoErrorEnumeration.AlgoError)
                                                    {
                                                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                                        break;
                                                    }

                                                    string packetDecrypt = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, packetWallet, _certificateConnection, ClassWalletNetworkSetting.KeySize).Replace(ClassConnectorSetting.PacketSplitSeperator, "");
#if DEBUG
                                                    if (packetDecrypt != ClassAlgoErrorEnumeration.AlgoError)
                                                    {
                                                        Debug.WriteLine("2# - Packet successfully decrypted: " + packetDecrypt);
                                                    }
#endif
                                                    if (packetDecrypt == ClassAlgoErrorEnumeration.AlgoError)
                                                    {
#if DEBUG
                                                        Debug.WriteLine("2# - Packet decrypt error, can't decrypt packet received: " + packetWallet);
#endif
                                                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                                        break;
                                                    }

                                                    await Task.Run(() => HandlePacketBlockchainNetworkWalletAsync(packetDecrypt)).ConfigureAwait(false);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            Array.Clear(packetReceived, 0, packetReceived.Length);
                        }
                        catch (Exception error)
                        {
#if DEBUG
                            Debug.WriteLine("Exception ListenNetwork function: " + error.Message);
#endif
                            break;
                        }
                    }
                    if (WalletCreateResult == ClassWalletCreatorEnumeration.WalletCreatorPending)
                    {
                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                    }
                }, _cancellationTokenListenNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }

        /// <summary>
        /// Handle packet wallet received from the blockchain network.
        /// </summary>
        /// <param name="packet"></param>
        private void HandlePacketBlockchainNetworkWalletAsync(string packet)
        {
            var splitPacket = packet.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);

            switch (splitPacket[0])
            {
                case ClassWalletCommand.ClassWalletReceiveEnumeration.WaitingCreatePhase:
#if DEBUG
                    Debug.WriteLine("Request to create a wallet successfully received.");
#endif
                    break;
                case ClassWalletCommand.ClassWalletReceiveEnumeration.WalletCreatePasswordNeedLetters:
                case ClassWalletCommand.ClassWalletReceiveEnumeration.WalletCreatePasswordNeedMoreCharacters:
                    WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                    FullDisconnection();
                    break;
                case ClassWalletCommand.ClassWalletReceiveEnumeration.CreatePhase:
                    if (splitPacket[1] == ClassAlgoErrorEnumeration.AlgoError)
                    {
                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                        FullDisconnection();
                    }
                    else
                    {
                        var decryptWalletDataCreate = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, splitPacket[1], _walletPassword, ClassWalletNetworkSetting.KeySize);
                        if (decryptWalletDataCreate == ClassAlgoErrorEnumeration.AlgoError)
                        {
                            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                            FullDisconnection();
                        }
                        else
                        {
#if DEBUG
                            Debug.WriteLine("Wallet data received, decrypted successfully. Decompress now.");
#endif
                            try
                            {
                                string walletDataCreate = ClassUtility.DecompressData(decryptWalletDataCreate);
#if DEBUG
                                Debug.WriteLine("Decompress wallet data decrypted successfully done: " + walletDataCreate);
#endif
                                var splitDecryptWalletDataCreate = walletDataCreate.Split(new[] { "\n" }, StringSplitOptions.None);
                                var walletAddress = splitDecryptWalletDataCreate[0];
                                var publicKey = splitDecryptWalletDataCreate[2];
                                var privateKey = splitDecryptWalletDataCreate[3];
                                var pinWallet = privateKey.Split(new []{"$"}, StringSplitOptions.None)[1];
                                WalletAddressResult = walletAddress;
                                if (_mainInterface.WalletDatabase.AndroidWalletDatabase.Count < int.MaxValue - 1)
                                {
#if DEBUG
                                    Debug.WriteLine("Wallet created successfully: " + walletAddress);
#endif
                                    if (_mainInterface.WalletDatabase.InputWalletToDatabase(walletAddress, publicKey, privateKey, _walletPassword, pinWallet))
                                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorSuccess;
                                    else
                                    {
                                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
#if DEBUG
                                        Debug.WriteLine("Unexpected error on input new wallet data to wallet database file.");
#endif
                                    }
                                }
                                else
                                {
                                    WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
#if DEBUG
                                    Debug.WriteLine("Create wallet error, the maximum wallet of: " + (int.MaxValue - 1).ToString("F0") + " has been reach.");
#endif

                                }
                            }

#if DEBUG
                            catch (Exception error)
                            {
                                Debug.WriteLine("Create wallet error on decompress part: " + error.Message);
#else
                            catch
                            {
#endif
                            }
                        }
                    }
                    break;
                case ClassWalletCommand.ClassWalletReceiveEnumeration.WalletAskSuccess:
                    string walletDataCreation = splitPacket[1];

                    if (walletDataCreation == ClassAlgoErrorEnumeration.AlgoError)
                    {
#if DEBUG
                        Debug.WriteLine("Restoring wallet failed, please try again later.");
#endif
                        WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                        FullDisconnection();
                    }
                    else
                    {
                        var decryptWalletDataCreation = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.AesNetwork, walletDataCreation, _walletPrivateKey, ClassWalletNetworkSetting.KeySize);
                        if (decryptWalletDataCreation == ClassAlgoErrorEnumeration.AlgoError)
                        {
#if DEBUG
                            Debug.WriteLine("Restoring wallet failed, please try again later.");
#endif
                            WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                            FullDisconnection();
                        }
                        else
                        {
                            var splitWalletData = decryptWalletDataCreation.Split(new[] { "\n" }, StringSplitOptions.None);
                            var publicKey = splitWalletData[2];
                            var privateKey = splitWalletData[3];
                            var pinCode = splitWalletData[4];
                            if (_mainInterface.WalletDatabase.AndroidWalletDatabase.ContainsKey(_walletAddress))
                            {
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[_walletAddress].SetWalletAddress(_walletAddress);
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[_walletAddress].SetWalletPublicKey(publicKey);
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[_walletAddress].SetWalletPrivateKey(privateKey);
                                _mainInterface.WalletDatabase.AndroidWalletDatabase[_walletAddress].SetWalletPinCode(pinCode);
                                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorSuccess;
                                FullDisconnection();
                            }
                            else
                            {
#if DEBUG
                                Debug.WriteLine("Restoring wallet failed, wallet address: " + _walletAddress + " not exist inside database, please try again later.");
#endif
                                WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                                FullDisconnection();
                            }
                        }
                    }
                    break;
            }
        }
    }
}