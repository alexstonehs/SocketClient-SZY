using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZySocketClient
{


    /// <summary>
    /// An IPv4 TCP connected socket.
    /// </summary>
    public sealed class SocketClient : IDisposable
    {
        private readonly Encoding _encoding;
        private readonly string _socketId;

        public delegate void DataReceive(string strData, string id);
        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event DataReceive DataReceived;

        public delegate void DataBytesReceive(byte[] bData, string id);
        /// <summary>
        /// 数据字节接收事件
        /// </summary>
        public event DataBytesReceive DataBytesReceived;

        public delegate void Disconnect(string id);
        /// <summary>
        /// 连接断开事件
        /// </summary>
        public event Disconnect ConnectionLost;

        public delegate void CatchException(Type methodType, Exception ex);
        /// <summary>
        /// 错误捕获事件
        /// </summary>
        public event CatchException ErrorCatch;

        public delegate void IsConnect(string id);

        public delegate void SendingResult(string id, byte[] sendBytes, bool success);

        public event SendingResult SendingResponse;


        /// <summary>
        /// 连接建立事件
        /// </summary>
        public event IsConnect ConnectionEstablished;
        public bool Connected;

        private readonly int _port;

        private readonly string _host;

        public string ErrorMsg;
        /// <summary>
        /// 数据发送队列
        /// </summary>
        private readonly BlockingCollection<object> _sendDataColl;

        private readonly Thread _autoReconnectThread;

        private AutoResetEvent _mEvent = new AutoResetEvent(false);

        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Constructs and connects the socket.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        public SocketClient(EndPoint endpoint) : this(endpoint, Encoding.UTF8) { }

        /// <summary>
        /// Constructs and connects the socket.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        /// <param name="encoding">Encoding of the content sended and received by the socket</param>
        public SocketClient(EndPoint endpoint, Encoding encoding)
        {
            _encoding = encoding;
            InitSocketConnection(endpoint);
            _cancellationTokenSource = new CancellationTokenSource();
            _autoReconnectThread = new Thread(AutoReconnect)
            {
                IsBackground = true,
                Name = "NetSocket_AutoReconnect"
            };
            _autoReconnectThread.Start();
            Task.Factory.StartNew(SendingDataColl, TaskCreationOptions.LongRunning);
            //Task t = new Task(TimerManage);
            //t.Start();
        }

        private void TimerManage()
        {
            //_receiveTimer = new Timer(100);
            //_receiveTimer.Elapsed += socket_rev;

            //_receiveTimer.Start();
            //_holdEvent.WaitOne();
            //_receiveTimer.Stop();
        }
        /// <summary>  
        /// 接收消息  
        /// </summary>  
        /// <param name="socket"></param>  
        public void AsyncReceive(Socket socket)
        {
            byte[] data = new byte[4096];
            //开始接收数据  
            socket.BeginReceive(data, 0, data.Length, SocketFlags.None,
            asyncResult =>
            {
                try
                {
                    int length = socket.EndReceive(asyncResult);
                    if (length < 1)
                    {
                        ConnectionLost?.Invoke(_socketId);
                        Connected = false;
                        return;
                    }
                    data = data.Take(length).ToArray();
                    DataBytesReceived?.Invoke(data, _socketId);
                    //Console.WriteLine(string.Format("收到服务器消息:{0}", Encoding.UTF8.GetString(data)));
                    if (DataReceived != null)
                    {
                        string reStr = _encoding.GetString(data).TrimEnd('\0');
                        DataReceived(reStr, _socketId);
                    }
                    AsyncReceive(socket);
                }
                catch (Exception e)
                {
                    ErrorCatch?.Invoke(MethodBase.GetCurrentMethod().DeclaringType, e);
                }
            }, null);
          
        }


        /// <summary>
        /// Constructs and connects the socket.
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="port">Port to connect to</param>
        /// <param name="id"></param>
        public SocketClient(string host, int port, string id) : this(host, port, id, Encoding.UTF8) { }

        /// <summary>
        /// Constructs and connects the socket.
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="port">Port to connect to</param>
        /// <param name="id"></param>
        /// <param name="encoding">Encoding of the content sended and received by the socket</param>
        public SocketClient(string host, int port, string id, Encoding encoding)
        {
            _encoding = encoding;
            _host = host;
            _port = port;
            _socketId = id;
            _cancellationTokenSource = new CancellationTokenSource();
            _sendDataColl = new BlockingCollection<object>(50);
            _autoReconnectThread = new Thread(AutoReconnect)
            {
                IsBackground = true,
                Name = "NetSocket_AutoReconnect"
            };
            _autoReconnectThread.Start();
            Task.Factory.StartNew(SendingDataColl, TaskCreationOptions.LongRunning);
        }

        private void SendingDataColl()
        {
            while (!_sendDataColl.IsCompleted)
            {
                foreach (object dataObj in _sendDataColl.GetConsumingEnumerable())
                {

                    try
                    {
                        byte[] transBytes;
                        if (dataObj is string s)
                        {
                            transBytes = _encoding.GetBytes(s);
                        }
                        else
                        {
                            transBytes = (byte[])dataObj;
                        }

                        if (!Connected)
                        {
                            string msg = "[Connection Lost]";
                            transBytes = transBytes.Concat(Encoding.ASCII.GetBytes(msg)).ToArray();
                            SendingResponse?.Invoke(_socketId, transBytes, false);
                            continue;
                        }

                        bool b = Send(transBytes);
                        SendingResponse?.Invoke(_socketId, transBytes, b);
                    }
                    catch (Exception e)
                    {
                        ErrorCatch?.Invoke(MethodBase.GetCurrentMethod().DeclaringType, e);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化Socket连接
        /// </summary>
        private void InitSocketConnection()
        {
            try
            {
                UnderlyingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                UnderlyingSocket.Connect(_host, _port);
                AsyncReceive(UnderlyingSocket);
                Connected = true;
                ConnectionEstablished?.Invoke(_socketId);
            }
            catch (Exception e)
            {
                ErrorCatch?.Invoke(MethodBase.GetCurrentMethod().DeclaringType, e);
            }
        }
        /// <summary>
        /// 初始化Socket连接
        /// </summary>
        private void InitSocketConnection(EndPoint endPoint)
        {
            UnderlyingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            UnderlyingSocket.Connect(endPoint);
            AsyncReceive(UnderlyingSocket);
            Connected = true;
        }

        internal SocketClient(Socket socket)
        {
            _encoding = Encoding.UTF8;
            UnderlyingSocket = socket;
        }

        public void Connect()
        {
            Task.Factory.StartNew(InitSocketConnection);
        }
        /// <summary>
        /// 执行异步连接初始化
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            await Task.Factory.StartNew(InitSocketConnection);
            return Connected;
        }
        /// <summary>
        /// 重置（断开）现有连接
        /// </summary>
        public bool ResetConnection()
        {
            if (Connected)
            {
                if (UnderlyingSocket != null)
                {
                    UnderlyingSocket.Shutdown(SocketShutdown.Both);
                    UnderlyingSocket.Close();
                    Connected = false;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// True if there's any data to receive on the socket.
        /// </summary>
        public bool AnythingToReceive
        {
            get
            {
                return UnderlyingSocket.Available > 0;
            }
        }

        /// <summary>
        /// The underlying socket.
        /// </summary>
        public Socket UnderlyingSocket { get; private set; }

        /// <summary>
        /// Disposes the socket.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _sendDataColl?.CompleteAdding();
                UnderlyingSocket.Shutdown(SocketShutdown.Both);
                UnderlyingSocket.Close();
                UnderlyingSocket.Dispose();
                _cancellationTokenSource.Cancel();
                _mEvent.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Receives any pending data.
        /// This blocks execution until there's data available.
        /// </summary>
        /// <param name="bufferSize">Amount of data to read</param>
        /// <returns>Received data</returns>
        public string Receive(int bufferSize = 4096)
        {
            var buffer = new byte[bufferSize];
            UnderlyingSocket.Receive(buffer);
            return _encoding.GetString(buffer).TrimEnd('\0');
        }

        public bool CheckSocketIsConnected()
        {
            bool blockingState = false;
            bool needResetBlockingState = false;
            try
            {
                blockingState = UnderlyingSocket.Blocking;
                needResetBlockingState = true;
                byte[] tmp = new byte[1];

                UnderlyingSocket.Blocking = false;
                UnderlyingSocket.Send(tmp, 0, 0);
                return true;
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (!e.NativeErrorCode.Equals(10035))
                {
                    Console.WriteLine("Disconnected: error code {0}!", e.NativeErrorCode);
                    return false;
                }
                else
                {
                    Console.WriteLine("Still Connected, but the Send would block");
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (needResetBlockingState)
                {
                    UnderlyingSocket.Blocking = blockingState;
                }
            }
        }
        /// <summary>
        /// 将数据加入发送队列
        /// </summary>
        /// <param name="data"></param>
        public void SendingData(object data)
        {
            _sendDataColl?.Add(data);
        }
        /// <summary>
        /// Sends the given data.
        /// </summary>
        /// <param name="data">Data to send</param>
        public bool Send(string data)
        {
            var bytes = _encoding.GetBytes(data);
            try
            {
                UnderlyingSocket.Send(bytes);
                return true;
            }
            catch (Exception e)
            {
                ErrorMsg = e.Message;
                return false;
            }
        }

        private void AutoReconnect()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                bool cancel = _mEvent.WaitOne(10000);
                if (cancel)
                {
                    continue;
                }

                if (Connected)
                {
                    if (!CheckSocketIsConnected())
                    {
                        try
                        {
                            InitSocketConnection();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                else
                {
                    try
                    {
                        InitSocketConnection();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

            }

        }
        public bool Send(byte[] data)
        {
            //var bytes = _encoding.GetBytes(data);
            try
            {
                int resCount = UnderlyingSocket.Send(data);
                if (resCount > 0)
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                ErrorMsg = e.Message;
                return false;
            }
        }

        public bool SendAsync(byte[] data)
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.SetBuffer(data, 0, data.Length);
            e.Completed += EOnCompleted;
            bool completedAsync = false;
            try
            {
                completedAsync = UnderlyingSocket.SendAsync(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return completedAsync;
            //if (!completedAsync)
            //{
            //    //EOnCompleted(this, e);
            //}


        }

        private void EOnCompleted(object sender, SocketAsyncEventArgs e)
        {

            if (e.SocketError == SocketError.Success)
            {
                Console.WriteLine("发送成功");
            }
        }
    }
}
