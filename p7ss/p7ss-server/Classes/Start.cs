using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using p7ss_server.Configs;
using System;
using System.Collections.Generic;
using System.IO;

namespace p7ss_server.Classes
{
    internal class Start : Core
    {
        internal static void DatabasesConnect()
        {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = Hostname,
                Database = Basename,
                UserID = Username,
                Password = Password,
                CharacterSet = Charset,
                Port = Port,
                SslMode = MySqlSslMode.None
            };

            MainDbConnect.ConnectionString = builder.ConnectionString;
            MainDbConnect.Open();
        }

        internal static void GetBannedSockets()
        {
            while (true)
            {
                try
                {
                    if (!File.Exists(Params.BannedSocketsFile))
                    {
                        try
                        {
                            using (StreamWriter streamWriter = new StreamWriter(Params.BannedSocketsFile))
                            {
                                streamWriter.Write("[]");
                            }

                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Exception, class 'Start': " + e);

                            Environment.Exit(0);
                        }
                    }

                    using (StreamReader sr = new StreamReader(Params.BannedSocketsFile))
                    {
                        string json = sr.ReadToEnd();

                        if (json != "[]")
                        {
                            BannedSockets = JsonConvert.DeserializeObject<List<BannedSockets>>(json, SerializerSettings);
                        }

                        break;
                    }
                }
                catch (Exception)
                {
                    File.Delete(Params.BannedSocketsFile);
                }
            }

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Loaded banned IP's: " + BannedSockets.Count);
        }
    }
}
