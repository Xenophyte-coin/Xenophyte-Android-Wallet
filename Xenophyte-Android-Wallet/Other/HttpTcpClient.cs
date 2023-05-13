using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xenophyte_Connector_All.Setting;

namespace XenophyteAndroidWallet.Other
{
    public class HttpTcpClient : IDisposable
    {
        #region Disposing Part Implementation 

        private bool _disposed;

        ~HttpTcpClient()
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

        private string _result;
        private Socket _client;
        private long _timeoutDate;
        private bool _connnectionStatus;
        private bool _taskCompletlyDone;
        private CancellationTokenSource _cancellationTokenPacket;

        /// <summary>
        /// Constructor, set the timeout.
        /// </summary>
        /// <param name="timeout"></param>
        public HttpTcpClient(int timeout)
        {
            _result = string.Empty;
            _timeoutDate = DateTimeOffset.Now.ToUnixTimeSeconds() + timeout;
        }

        /// <summary>
        /// Start to send the token request in tcp packet converted into http get request, return the result.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="packet"></param>
        public async Task<string> ProceedTokenPacketByTcp(IPAddress host, int port, string packet)
        {
            try
            {
                if (_client != null)
                {
                    _client?.Close();
                    _client?.Dispose();
                }

                _client = new Socket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await _client.ConnectAsync(host, port);
            }
            catch
            {
#if DEBUG
                Debug.WriteLine("HttpTcpClient can't connect to the host target: " + host + ":" + port + ". The request: " + packet + " can't be sent.");
#endif
                return string.Empty;
            }
            _connnectionStatus = true;
            _cancellationTokenPacket = new CancellationTokenSource();

            try
            {
                await Task.Factory.StartNew(ListenConnection, _cancellationTokenPacket.Token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }

            try
            {
                await Task.Factory.StartNew(KeepAliveRequest, _cancellationTokenPacket.Token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }

            if (await SendPacketToTokenNetwork(packet).ConfigureAwait(false))
            {
                try
                {
                    _result = await WaitPacketResult();
                    CloseConnection();
                    return _result;
                }
                catch
                {
                    // Catch the exception once the task is cancelled.
                }
            }
            CloseConnection();
            return string.Empty;
        }

        /// <summary>
        /// Close the connection opened to the network.
        /// </summary>
        private void CloseConnection()
        {
            try
            {
                _taskCompletlyDone = true;
                _client?.Close();
                _client?.Dispose();
            }
            catch
            {
                // Ignored.
            }
        }

        /// <summary>
        /// Cancel the task of the HttpTcpClient.
        /// </summary>
        private void CancelTask()
        {
            try
            {
                if (_cancellationTokenPacket != null)
                {
                    if (!_cancellationTokenPacket.IsCancellationRequested)
                    {
                        _cancellationTokenPacket.Cancel();
                    }
                }
            }
            catch
            {
                // Ignored.
            }
        }

        /// <summary>
        /// This function cancel the task once the maximum of 
        /// </summary>
        /// <returns></returns>
        private async Task KeepAliveRequest()
        {
            while (!_taskCompletlyDone)
            {
                if (_timeoutDate <= DateTimeOffset.Now.ToUnixTimeSeconds())
                {
#if DEBUG
                    Debug.WriteLine("Timeout reach, close HttpTcpClient object, cancel task.");
#endif
                    _connnectionStatus = false;
                    break;
                }
                await Task.Delay(1000);
            }
            await Task.Run(() => CancelTask()).ConfigureAwait(false); // Cancel the task with a parallel task executed, to prevent issues on cancellation.
        }

        /// <summary>
        /// Wait the packet result expected to receive.
        /// </summary>
        /// <returns></returns>
        private async Task<string> WaitPacketResult()
        {
            while (!_taskCompletlyDone)
            {
                await Task.Delay(1000);
                if (!string.IsNullOrEmpty(_result))
                {
#if DEBUG
                    Debug.WriteLine("Packet Received: " + _result);
#endif
                    _result = "{" + ClassUtility.RemoveHTTPHeader(_result) + "}";
#if DEBUG
                    Debug.WriteLine("Packet formatted: " + _result);
#endif
                    return _result;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Listen incoming packets
        /// </summary>
        /// <returns></returns>
        private async Task ListenConnection()
        {
            while (_connnectionStatus)
            {
                try
                {
                    using (var readerNetwork = new NetworkStream(_client))
                    {
                        int received;
                        byte[] buffer = new byte[ClassConnectorSetting.MaxNetworkPacketSize];
                        while ((received = await readerNetwork.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            _result = Encoding.UTF8.GetString(buffer, 0, received);
                            _connnectionStatus = false;
                            break;
                        }
                    }
                }
                catch
                {
                    break;
                }
            }
            _connnectionStatus = false;
        }

        /// <summary>
        /// Send packet to the network of blockchain.
        /// </summary>
        /// <param name="packet"></param>
        private async Task<bool> SendPacketToTokenNetwork(string packet)
        {
            try
            {
                using (var networkStream = new NetworkStream(_client))
                {
                    byte[] bytePacket = Encoding.UTF8.GetBytes(packet);
                    await networkStream.WriteAsync(bytePacket, 0, bytePacket.Length);
                    await networkStream.FlushAsync();
                }
            }
            catch
            {
                _connnectionStatus = false;
                return false;
            }
            return true;
        }
    }
}