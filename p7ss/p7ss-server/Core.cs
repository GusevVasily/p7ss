using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using p7ss_server.Configs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace p7ss_server
{
    internal class Core : Main
    {
        internal static List<BannedSockets> BannedSockets = new List<BannedSockets>();
        internal static readonly MySqlConnection MainDbConnect = new MySqlConnection();
        internal static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, false);
        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        internal static void BanSocket(string ip, string type)
        {
            List<BannedSockets> list = BannedSockets.Where(x => x.Ip == ip).ToList();

            if (list.Count == 0)
            {
                while (true)
                {
                    try
                    {
                        using (StreamWriter sw = new StreamWriter(Params.BannedSocketsFile))
                        {
                            BannedSockets.Add(new BannedSockets
                            {
                                Ip = ip,
                                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                Type = type
                            });

                            sw.Write(JsonConvert.SerializeObject(BannedSockets, SerializerSettings));

                            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] IP '" + ip + "' was Banned, reason: " + type);
                        }

                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        internal static void Restart()
        {
            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "/bin/bash",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            process.Start();

            using (process.StandardOutput)
            {
                process.StandardInput.WriteLine("screen -dmS p7ss mono p7ss.exe; exit");
            }

            Environment.Exit(0);
        }

        internal static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine("Unobserved Exception: " + e.Exception);
        }

        internal static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception: " + e.ExceptionObject);
        }
    }

    internal class BannedSockets
    {
        public string Ip;
        public string Date;
        public string Type;
    }
}
