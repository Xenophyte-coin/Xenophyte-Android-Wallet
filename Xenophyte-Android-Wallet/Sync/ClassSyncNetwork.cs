using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XenophyteAndroidWallet.Wallet;
using XenophyteAndroidWallet.WalletDatabase;
using Xenophyte_Connector_All.Remote;
using Xenophyte_Connector_All.Setting;

namespace XenophyteAndroidWallet.Sync
{
    public class ClassSyncNetwork
    {
        private  Socket _tcpRemoteNodeClient;
        private  CancellationTokenSource _cancellationTokenListenRemoteNode;
        private  CancellationTokenSource _cancellationTokenCheckConnection;
        private  CancellationTokenSource _cancellationTokenAutoSync;
        public  bool ConnectionStatus;
        private  bool _enableCheckConnectionStatus;
        private const int MaxTimeout = 30;

        /// <summary>
        /// Current wallet to sync.
        /// </summary>
        private  string _currentWalletAddressOnSync;

        /// <summary>
        /// Current wallet uniques id to sync.
        /// </summary>
        private  string _currentWalletIdOnSync;
        private  string _currentAnonymousWalletIdOnSync;

        /// <summary>
        /// Current total transaction to sync on the wallet.
        /// </summary>
        private  int _currentWalletTransactionToSync;
        private  int _currentWalletAnonymousTransactionToSync;

        /// <summary>
        /// Check if the current wait a transaction.
        /// </summary>
        private  bool _currentWalletOnSyncTransaction;

        /// <summary>
        /// Save last packet received date.
        /// </summary>
        private  long _lastPacketReceived;

        private  IPAddress _currentSeedNode;

        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassSyncNetwork(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }


        /// <summary>
        /// Connect RPC Wallet to a remote node selected.
        /// </summary>
        public  async Task ConnectRpcWalletToRemoteNodeSyncAsync()
        {
            while (!ConnectionStatus)
            {
                try
                {
                    _tcpRemoteNodeClient?.Close();
                    _tcpRemoteNodeClient?.Dispose();
                    

                    bool seedNodeSelected = false;
                    IPAddress randomSeedNode = null;
                    foreach (var seedNode in _mainInterface.WalletUpdater.ListOfSeedNodesAlive.ToArray())
                    {
                        if (!seedNodeSelected)
                        {
                            if (seedNode.Value)
                            {
                                seedNodeSelected = true;
                                randomSeedNode = seedNode.Key;
                            }
                        }
                    }
                    _currentSeedNode = randomSeedNode;

                    if (seedNodeSelected)
                    {
                        _tcpRemoteNodeClient = new Socket(randomSeedNode.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        await _tcpRemoteNodeClient.ConnectAsync(randomSeedNode, ClassConnectorSetting.RemoteNodePort);
                    }

                    ConnectionStatus = true;
                    break;
                }
                catch
                {
#if DEBUG
                    Debug.WriteLine("Unable to connect to Remote Node host " + _currentSeedNode + ":" + ClassConnectorSetting.RemoteNodePort + " retry in 5 seconds.");
#endif
                }

                await Task.Delay(5000);
            }
            if (ConnectionStatus)
            {
#if DEBUG
                Debug.WriteLine("Connect to Remote Node host " + _currentSeedNode + ":" + ClassConnectorSetting.RemoteNodePort + " successfully done, start to sync.");
#endif
                _lastPacketReceived = DateTimeOffset.Now.ToUnixTimeSeconds();

                if (!_enableCheckConnectionStatus)
                {
                    _enableCheckConnectionStatus = true;
                    CheckRpcWalletConnectionToSync();
                }
                ListenRemoteNodeSync();
                AutoSyncWallet();
            }
        }

        /// <summary>
        /// Listen remote node sync packet received.
        /// </summary>
        private  void ListenRemoteNodeSync()
        {
            _cancellationTokenListenRemoteNode = new CancellationTokenSource();

            try
            {
                Task.Factory.StartNew(async delegate
                {
                    while (ConnectionStatus)
                    {
                        try
                        {
                            using (var networkReader = new NetworkStream(_tcpRemoteNodeClient))
                            {
                                using (BufferedStream bufferedStreamNetwork = new BufferedStream(networkReader, ClassConnectorSetting.MaxNetworkPacketSize))
                                {
                                    byte[] buffer = new byte[ClassConnectorSetting.MaxNetworkPacketSize];
                                    int received = await bufferedStreamNetwork.ReadAsync(buffer, 0, buffer.Length);
                                    if (received > 0)
                                    {
                                        _lastPacketReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        string packetReceived = Encoding.UTF8.GetString(buffer, 0, received);
                                        if (packetReceived.Contains(ClassConnectorSetting.PacketSplitSeperator))
                                        {
                                            var splitPacketReceived = packetReceived.Split(new[] { ClassConnectorSetting.PacketSplitSeperator }, StringSplitOptions.None);
                                            if (splitPacketReceived.Length > 1)
                                            {
                                                foreach (var packetEach in splitPacketReceived)
                                                {
                                                    if (!string.IsNullOrEmpty(packetEach))
                                                    {
                                                        if (packetEach.Length > 1)
                                                        {
                                                            HandlePacketReceivedFromSync(packetEach);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                HandlePacketReceivedFromSync(packetReceived.Replace(ClassConnectorSetting.PacketSplitSeperator, ""));
                                            }
                                        }
                                        else
                                        {
                                            HandlePacketReceivedFromSync(packetReceived);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception error)
                        {
#if DEBUG
                            Debug.WriteLine("Exception: " + error.Message + " to listen packet received from Remote Node host " + _currentSeedNode + ":" + ClassConnectorSetting.RemoteNodePort + " retry to connect in a few seconds..");
#endif
                            break;
                        }
                    }
                    ConnectionStatus = false;
                }, _cancellationTokenListenRemoteNode.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }

        /// <summary>
        /// Handle packet received from remote node sync.
        /// </summary>
        /// <param name="packet"></param>
        private  void HandlePacketReceivedFromSync(string packet)
        {
            var splitPacket = packet.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);

            switch (splitPacket[0])
            {
                case ClassRemoteNodeCommandForWallet.RemoteNodeRecvPacketEnumeration.WalletYourNumberTransaction:
#if DEBUG
                    Debug.WriteLine("Their is " + splitPacket[1] + " transaction to sync for wallet address: " + _currentWalletAddressOnSync);
#endif
                    _currentWalletTransactionToSync = int.Parse(splitPacket[1]);
                    _currentWalletOnSyncTransaction = false;
                    break;
                case ClassRemoteNodeCommandForWallet.RemoteNodeRecvPacketEnumeration.WalletYourAnonymityNumberTransaction:
#if DEBUG
                    Debug.WriteLine("Their is " + splitPacket[1] + " anonymous transaction to sync for wallet address: " + _currentWalletAddressOnSync);
#endif
                    _currentWalletAnonymousTransactionToSync = int.Parse(splitPacket[1]);
                    _currentWalletOnSyncTransaction = false;
                    break;
                case ClassRemoteNodeCommandForWallet.RemoteNodeRecvPacketEnumeration.WalletTransactionPerId:

                    _mainInterface.SortingTransaction.SaveTransactionSorted(splitPacket[1], _currentWalletAddressOnSync, _mainInterface.WalletDatabase.AndroidWalletDatabase[_currentWalletAddressOnSync].GetWalletPublicKey(), false);
#if DEBUG
                    Debug.WriteLine(_currentWalletAddressOnSync + " total transaction sync " + _mainInterface.WalletDatabase.AndroidWalletDatabase[_currentWalletAddressOnSync].GetWalletTotalTransactionSync() + "/" + _currentWalletTransactionToSync);
#endif
                    _currentWalletOnSyncTransaction = false;
                    break;
                case ClassRemoteNodeCommandForWallet.RemoteNodeRecvPacketEnumeration.WalletAnonymityTransactionPerId:
                    _mainInterface.SortingTransaction.SaveTransactionSorted(splitPacket[1], _currentWalletAddressOnSync, _mainInterface.WalletDatabase.AndroidWalletDatabase[_currentWalletAddressOnSync].GetWalletPublicKey(), true);
#if DEBUG
                    Debug.WriteLine(_currentWalletAddressOnSync + " total anonymous transaction sync " + _mainInterface.WalletDatabase.AndroidWalletDatabase[_currentWalletAddressOnSync].GetWalletTotalAnonymousTransactionSync() + "/" + _currentWalletAnonymousTransactionToSync);
#endif
                    _currentWalletOnSyncTransaction = false;
                    break;
#if DEBUG
                default:
                    Debug.WriteLine("Unknown packet received: " + packet);
                    break;
#endif
            }
        }

        /// <summary>
        /// Check rpc wallet connection to remote node sync.
        /// </summary>
        private  void CheckRpcWalletConnectionToSync()
        {
            _cancellationTokenCheckConnection = new CancellationTokenSource();

            try
            {
                Task.Factory.StartNew(async delegate
                {
                    while (true)
                    {
                        try
                        {
                            if (!ConnectionStatus || _lastPacketReceived + MaxTimeout < DateTimeOffset.Now.ToUnixTimeSeconds())
                            {
                                ConnectionStatus = false;
                                _lastPacketReceived = 0;
                                await Task.Delay(100);
                                CancelTaskListenRemoteNode();
                                CancelAutoSync();
                                await Task.Delay(1000);
#if DEBUG
                                Debug.WriteLine("Connection to remote node host is closed, retry to connect");
#endif
                                await ConnectRpcWalletToRemoteNodeSyncAsync();
                            }
                        }
                        catch
                        {
                            ConnectionStatus = false;
                            _lastPacketReceived = 0;
                        }
                        await Task.Delay(1000);
                    }
                }, _cancellationTokenCheckConnection.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception 
            }
        }

        /// <summary>
        /// Cancel the task of listen packets receive from a Remote Node.
        /// </summary>
        private  void CancelTaskListenRemoteNode()
        {
            try
            {
                if (_cancellationTokenListenRemoteNode != null)
                {
                    if (!_cancellationTokenListenRemoteNode.IsCancellationRequested)
                    {
                        _cancellationTokenListenRemoteNode.Cancel();
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        /// <summary>
        /// Cancel the task who check the current connection status.
        /// </summary>
        private  void CancelTaskCheckConnection()
        {
            try
            {
                if (_cancellationTokenCheckConnection != null)
                {
                    if (!_cancellationTokenCheckConnection.IsCancellationRequested)
                    {
                        _cancellationTokenCheckConnection.Cancel();
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        /// <summary>
        /// Cancel the task who auto sync transactions of wallets stored inside the Rpc Wallet Database.
        /// </summary>
        private  void CancelAutoSync()
        {
            try
            {
                if (_cancellationTokenAutoSync != null)
                {
                    if (!_cancellationTokenAutoSync.IsCancellationRequested)
                    {
                        _cancellationTokenAutoSync.Cancel();
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }


        /// <summary>
        /// Send a packet to remote node.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private  async Task<bool> SendPacketToRemoteNode(string packet)
        {
            try
            {
                using (var networkWriter = new NetworkStream(_tcpRemoteNodeClient))
                {
                    using (BufferedStream bufferedStream = new BufferedStream(networkWriter, ClassConnectorSetting.MaxNetworkPacketSize))
                    {
                        var bytePacket = Encoding.UTF8.GetBytes(packet + ClassConnectorSetting.PacketSplitSeperator);
                        await bufferedStream.WriteAsync(bytePacket, 0, bytePacket.Length);
                        await bufferedStream.FlushAsync();
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Auto sync wallets.
        /// </summary>
        private  void AutoSyncWallet()
        {
            _cancellationTokenAutoSync = new CancellationTokenSource();
            try
            {
                Task.Factory.StartNew(async delegate
                {
                    while (ConnectionStatus)
                    {
                        try
                        {
                            foreach (var walletObject in _mainInterface.WalletDatabase.AndroidWalletDatabase.ToArray()) // Copy temporaly the database of wallets in the case of changes on the enumeration done by a parallal process, update sync of all of them.
                            {
                                if (_mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletUniqueId() != "-1" && _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletAnonymousUniqueId() != "-1")
                                {
                                    #region Attempt to sync the current wallet on the database.


                                    _currentWalletIdOnSync = walletObject.Value.GetWalletUniqueId();
                                    _currentAnonymousWalletIdOnSync = walletObject.Value.GetWalletAnonymousUniqueId();
                                    _currentWalletAddressOnSync = walletObject.Key;
                                    _currentWalletOnSyncTransaction = true;
                                    if (await SendPacketToRemoteNode(ClassRemoteNodeCommandForWallet.RemoteNodeSendPacketEnumeration.WalletAskHisNumberTransaction + ClassConnectorSetting.PacketContentSeperator + walletObject.Value.GetWalletUniqueId()))
                                    {
                                        while (_currentWalletOnSyncTransaction)
                                        {
                                            if (!ConnectionStatus)
                                            {
                                                break;
                                            }
                                            await Task.Delay(50);
                                        }

                                        if (_currentWalletTransactionToSync > 0)
                                        {
                                            if (_currentWalletTransactionToSync > _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletTotalTransactionSync()) // Start to sync transaction.
                                            {
                                                for (int i = _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletTotalTransactionSync(); i < _currentWalletTransactionToSync; i++)
                                                {
                                                    _currentWalletOnSyncTransaction = true;
                                                    if (!await SendPacketToRemoteNode(ClassRemoteNodeCommandForWallet.RemoteNodeSendPacketEnumeration.WalletAskTransactionPerId + ClassConnectorSetting.PacketContentSeperator + walletObject.Value.GetWalletUniqueId() + ClassConnectorSetting.PacketContentSeperator + i))
                                                    {
                                                        ConnectionStatus = false;
                                                        break;
                                                    }
                                                    while (_currentWalletOnSyncTransaction)
                                                    {
                                                        if (!ConnectionStatus)
                                                        {
                                                            break;
                                                        }
                                                        await Task.Delay(50);
                                                    }

                                                }
                                            }
                                        }
                                        _currentWalletOnSyncTransaction = true;
                                        if (await SendPacketToRemoteNode(ClassRemoteNodeCommandForWallet.RemoteNodeSendPacketEnumeration.WalletAskHisAnonymityNumberTransaction + ClassConnectorSetting.PacketContentSeperator + walletObject.Value.GetWalletAnonymousUniqueId()))
                                        {
                                            while (_currentWalletOnSyncTransaction)
                                            {
                                                if (!ConnectionStatus)
                                                {
                                                    break;
                                                }
                                                await Task.Delay(50);
                                            }

                                            if (_currentWalletAnonymousTransactionToSync > 0)
                                            {
                                                if (_currentWalletAnonymousTransactionToSync > _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletTotalAnonymousTransactionSync()) // Start to sync transaction.
                                                {
                                                    for (int i = _mainInterface.WalletDatabase.AndroidWalletDatabase[walletObject.Key].GetWalletTotalAnonymousTransactionSync(); i < _currentWalletAnonymousTransactionToSync; i++)
                                                    {
                                                        _currentWalletOnSyncTransaction = true;
                                                        if (!await SendPacketToRemoteNode(ClassRemoteNodeCommandForWallet.RemoteNodeSendPacketEnumeration.WalletAskAnonymityTransactionPerId + ClassConnectorSetting.PacketContentSeperator + walletObject.Value.GetWalletAnonymousUniqueId() + ClassConnectorSetting.PacketContentSeperator + i))
                                                        {
                                                            ConnectionStatus = false;
                                                            break;
                                                        }
                                                        while (_currentWalletOnSyncTransaction)
                                                        {
                                                            if (!ConnectionStatus)
                                                            {
                                                                break;
                                                            }
                                                            await Task.Delay(50);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            ConnectionStatus = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        ConnectionStatus = false;
                                        break;
                                    }

                                    #endregion
                                }
                            }
                        }
                        catch (Exception error)
                        {
#if DEBUG
                            Debug.WriteLine("Exception: " + error.Message + " to send packet on Remote Node host " + _currentSeedNode + ":" + ClassConnectorSetting.RemoteNodePort + " retry to connect in a few seconds..");
#endif
                            break;
                        }
                        await Task.Delay(1000);
                    }
                    ConnectionStatus = false;
                }, _cancellationTokenAutoSync.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }
    }
}