using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Ionic.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using p7ss_client.Classes.WebSockets;

namespace p7ss_client
{
    internal class Core : Config
    {
        internal static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, false);
        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        internal static UserData UserData;
        internal static Thread RemoteWsDaemon;

        internal static void UpdateSettings(object data)
        {
            while (true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("data/settings.dat"))
                    {
                        sw.Write(JsonConvert.SerializeObject(data, SerializerSettings));
                    }

                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }
        }

        internal static ResponseLocal CheckRemoteSocket()
        {
            ResponseLocal localUserData;
            if (RemoteWsDaemon == null)
            {
                RemoteWsDaemon = new Thread(Remote.Open)
                {
                    IsBackground = true
                };

                RemoteWsDaemon.Start();

                Thread.Sleep(1000);

                if (Directory.Exists("data"))
                {
                    if (File.Exists("data/settings.dat"))
                    {
                        try
                        {
                            JObject settingsJson;
                            using (StreamReader sr = new StreamReader("data/settings.dat"))
                            {
                                settingsJson = JObject.Parse(sr.ReadToEnd());
                            }

                            if (settingsJson["user_id"] != null && settingsJson["session"] != null)
                            {
                                settingsJson = Remote.Send(
                                    new Random((int) DateTime.Now.Ticks).Next(),
                                    new RemoteSend
                                    {
                                        Method = "auth.importAuthorization",
                                        Id = new Random((int) DateTime.Now.Ticks).Next(),
                                        Params = new ImportAuthorization
                                        {
                                            Id = (int) settingsJson["user_id"],
                                            Session = (string) settingsJson["session"]
                                        }
                                    }
                                );

                                if (settingsJson != null)
                                {
                                    if ((bool)settingsJson["result"])
                                    {
                                        UserData = new UserData
                                        {
                                            User_id = (int) settingsJson["response"]["user_id"],
                                            Session = (string) settingsJson["response"]["session"],
                                            Name = (string) settingsJson["response"]["name"],
                                            Avatar = (string) settingsJson["response"]["avatar"],
                                            Status = (string) settingsJson["response"]["status"]
                                        };

                                        localUserData = new ResponseLocal
                                        {
                                            Module = "main",
                                            Data = new UserData
                                            {
                                                User_id = (int) settingsJson["response"]["user_id"],
                                                Name = (string) settingsJson["response"]["name"],
                                                Avatar = (string) settingsJson["response"]["avatar"],
                                                Status = (string) settingsJson["response"]["status"]
                                            }
                                        };

                                        UpdateSettings(UserData);

                                        return localUserData;
                                    }
                                }
                            }
                        }
                        catch (IOException) { }
                        catch (JsonReaderException) { }
                    }
                }
                else
                {
                    Directory.CreateDirectory("data");
                }

                UpdateSettings(new JObject());
            }
            else
            {
                if (UserData != null)
                {
                    JObject settingsJson = Remote.Send(
                        new Random((int) DateTime.Now.Ticks).Next(),
                        new RemoteSend
                        {
                            Method = "auth.importAuthorization",
                            Id = new Random((int) DateTime.Now.Ticks).Next(),
                            Params = new ImportAuthorization
                            {
                                Id = UserData.User_id,
                                Session = UserData.Session
                            }
                        }
                    );

                    if (settingsJson != null)
                    {
                        if ((bool)settingsJson["result"])
                        {
                            UserData = new UserData
                            {
                                User_id = (int) settingsJson["response"]["user_id"],
                                Session = (string) settingsJson["response"]["session"],
                                Name = (string) settingsJson["response"]["name"],
                                Avatar = (string) settingsJson["response"]["avatar"],
                                Status = (string) settingsJson["response"]["status"]
                            };

                            localUserData = new ResponseLocal
                            {
                                Module = "main",
                                Data = new UserData
                                {
                                    User_id = (int) settingsJson["response"]["user_id"],
                                    Name = (string) settingsJson["response"]["name"],
                                    Avatar = (string) settingsJson["response"]["avatar"],
                                    Status = (string) settingsJson["response"]["status"]
                                }
                            };

                            UpdateSettings(UserData);

                            return localUserData;
                        }
                    }
                }
            }

            UpdateSettings(new JObject());

            return null;
        }

        internal static void CheckUpdate()
        {
            Thread.Sleep(60 * 1000);

            while (true)
            {
                try
                {
                    if (File.Exists("Updater.exe"))
                    {
                        Process updater = Process.Start("Updater.exe", "check " + Assembly.GetEntryAssembly().Location);
                        updater.WaitForExit();
                        if (updater.ExitCode == 200)
                        {
                            using (ZipFile zip = ZipFile.Read("data/update/update.zip"))
                            {
                                zip.ExtractAll("data/update");
                            }

                            // TODO: Уведомить юзера о новом обновлении (Local WS)

                            File.Move("data/update/Updater.exe", "Updater.exe");
                            Process.Start("Updater.exe", "install " + Assembly.GetEntryAssembly().Location); 
                            Environment.Exit(0);
                        }
                    }
                }
                catch (Exception)
                {
                    if (Directory.Exists("data/update"))
                    {
                        Directory.Delete("data/update");
                    }
                }

                Thread.Sleep(30 * 60 * 1000);
            }
        }
    }

    internal class ResponseLocal
    {
        public string Module;
        public object Data;
    }

    class ResponseLocalData
    {
        public object Error_code;
        public string Tfa_secret;
    }

    internal class UserData
    {
        public int User_id;
        public string Session;
        public string Name;
        public string Avatar;
        public string Status;
    }
}
