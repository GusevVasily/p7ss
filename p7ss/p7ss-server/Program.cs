using p7ss_server.Classes;
using System;
using System.Threading;

namespace p7ss_server
{
    internal class Program : Core
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Console.Clear();
                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Running system...");

                Start.DatabasesConnect();

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
                            case "clear":
                                Console.Clear();
                                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Console was cleaned.");

                                break;

                            case "exit":
                                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Aborted.");

                                Thread.Sleep(5000);

                                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Application is closed.");

                                Environment.Exit(0);

                                break;

                            case "restart":
                                Restart();

                                break;

                            case "?":
                            case "h":
                            case "help":
                                Console.WriteLine("'clear' for a Clear console");
                                Console.WriteLine("'exit' for a close app");
                                Console.WriteLine("'restart' for a Restart app");

                                break;

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
