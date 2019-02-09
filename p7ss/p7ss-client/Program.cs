using System.Threading;
using p7ss_client.Classes.WebSockets;

namespace p7ss_client
{
    internal class Program : Core
    {
        private static void Main()
        {
            Thread localWsThread = new Thread(Local.Open)
            {
                IsBackground = true
            };

            Thread checkUpdateThread = new Thread(CheckUpdate)
            {
                IsBackground = true
            };

            localWsThread.Start();
            checkUpdateThread.Start();

            while (true)
            {
                Thread.Sleep(30000);

                if (Remote.RemoteSocket != null)
                {
                    if (!Remote.RemoteSocket.IsConnected)
                    {
                        RemoteWsDaemonThread = null;

                        CheckRemoteSocket();
                    }
                }
            }
        }
    }
}
