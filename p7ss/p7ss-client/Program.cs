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

            localWsThread.Start();

            while (true)
            {
                Thread.Sleep(30000);

                if (Remote.RemoteSocket != null)
                {
                    if (!Remote.RemoteSocket.IsConnected)
                    {
                        RemoteWsDaemon = null;

                        RemoteWsDaemon = new Thread(Remote.Open)
                        {
                            IsBackground = true
                        };

                        RemoteWsDaemon.Start();

                        CheckRemoteSocket();
                    }
                }
            }
        }
    }
}
