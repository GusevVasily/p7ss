using p7ss_server.Classes;
using System;
using System.Threading;

namespace p7ss_server
{
    internal class Program : Core
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Console.Clear();
                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Running system...");

                Start.DatabasesConnect();
                Start.GetBannedSockets();

                Thread wsThread = new Thread(Ws.Open)
                {
                    IsBackground = true
                };

                wsThread.Start();

                while (true)
                {
                    string[] command = Console.ReadLine()?.Split(" ".ToCharArray());

                    if (command != null)
                    {
                        switch (command[0].ToLower())
                        {
                            default:
                                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Command '" + command[0] + "' not found. Write 'help' for a list of commands");

                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Exception, class 'Program': " + e);

                MainDbConnect.Close();

                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Error... Aborted.");

                Thread.Sleep(5000);

                Restart();
            }
        }
    }
}
