using System;
using System.Threading;
using p7ss_client.WebSockets;

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
                Console.ReadLine();
            }
        }
    }
}
