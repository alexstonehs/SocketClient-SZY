using System;
using System.Text;

namespace ZySocketClient
{
    internal class TestRun
    {
        public void Start()
        {
            SocketClient socketClient = new SocketClient("127.0.0.1", 6002, "1234567890123", Encoding.UTF8);
            Console.WriteLine("listening (127.0.0.1) port 6002");
            socketClient.ConnectionEstablished += SocketClientOnConnectionEstablished;
            socketClient.ConnectionLost += SocketClientOnConnectionLost;
            socketClient.DataReceived += SocketClientOnDataReceived;
            socketClient.ErrorCatch += SocketClientOnErrorCatch;
            socketClient.SendingResponse += SocketClientOnSendingResponse;
            socketClient.Connect();
            TestDataSend s = new TestDataSend(socketClient); //Emulate for data send
        }

        private static void SocketClientOnSendingResponse(string id, byte[] sendBytes, bool success)
        {
            Console.WriteLine($"Socket[{id}] Data {Encoding.ASCII.GetString(sendBytes)} send result: {(success ? "successed" : "failed")}");
        }

        private static void SocketClientOnDataReceived(string strData, string id)
        {
            Console.WriteLine($"ID:{id} Data received：{strData}");
        }

        private static void SocketClientOnConnectionLost(string id)
        {
            Console.WriteLine($"ID:{id} disconnected");
        }

        private static void SocketClientOnErrorCatch(Type methodType, Exception ex)
        {
            Console.WriteLine($"Error：{ex.Message}, methodType：{methodType}");
        }

        private static void SocketClientOnConnectionEstablished(string id)
        {
            Console.WriteLine($"ID:{id} Connected");
        }
    }
}
