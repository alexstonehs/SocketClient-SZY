using System.Text;
using System.Threading;


namespace ZySocketClient
{
    internal class TestDataSend
    {
        public TestDataSend(SocketClient socketClient)
        {
            Thread t = new Thread(() =>
            {
                while (true)
                {
                    bool b = socketClient.Send(Encoding.ASCII.GetBytes("aaaaaabbbbb"));
                    Thread.Sleep(2000);
                }
            })
            { IsBackground = true, Name = "SendThread" };
            t.Start();
        }
    }
}
