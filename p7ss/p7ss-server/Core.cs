﻿using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using p7ss_server.Configs;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace p7ss_server
{
    internal class Core : Main
    {
        internal static readonly MySqlConnection MainDbConnect = new MySqlConnection();
        internal static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, false);
        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        internal static string GenerateSession(string str, bool auth)
        {
            string hash;

            if (auth)
            {
                using (SHA512Managed sha512 = new SHA512Managed())
                {
                    hash = BitConverter.ToString(
                        sha512.ComputeHash(
                            Encoding.UTF8.GetBytes(
                                "p7ss://" + str + "/" + new Random((int)DateTime.Now.Ticks).Next()
                            )
                        )
                    ).Replace("-", "").ToLower();
                }
            }
            else
            {
                using (SHA256Managed sha256 = new SHA256Managed())
                {
                    hash = BitConverter.ToString(
                        sha256.ComputeHash(
                            Encoding.UTF8.GetBytes(
                                str
                            )
                        )
                    ).Replace("-", "").ToLower();
                }
            }

            return hash;
        }

        internal static void MainDbSend(string sql)
        {
            while (true)
            {
                try
                {
                    MySqlCommand mySqlCommand = new MySqlCommand(sql, MainDbConnect);

                    mySqlCommand.ExecuteNonQuery();

                    break;
                }
                catch (MySqlException)
                {
                    MainDbConnect.Open();
                }
            }
        }

        internal static void Restart()
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
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
}
