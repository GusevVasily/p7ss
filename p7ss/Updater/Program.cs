using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;

namespace Updater
{
    internal class Programs
    {
        private const string UpdateUrl = "https://updates.p7ss.ru/versions.json";

        private static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    if (!string.IsNullOrEmpty(args[0]) && !string.IsNullOrEmpty(args[1]))
                    {
                        switch (args[0])
                        {
                            case "check":
                                using (WebClient wc = new WebClient())
                                {
                                    string versions = wc.DownloadString(UpdateUrl);
                                    if (!string.IsNullOrEmpty(versions))
                                    {
                                        JObject json = JObject.Parse(versions);
                                        if ((string)json["desktop"]["version"] != Assembly.GetExecutingAssembly().GetName().Version.ToString())
                                        {
                                            if (!Directory.Exists("data/update"))
                                            {
                                                Directory.CreateDirectory("data/update");
                                            }

                                            wc.DownloadFile((string)json["desktop"]["link_updater"], "data/update/update.zip");

                                            Environment.Exit(200);
                                        }
                                    }
                                    else
                                    {
                                        Environment.Exit(500);
                                    }
                                }

                                break;

                            case "install":
                                if (Directory.Exists("data/update"))
                                {
                                    if (File.Exists("data/update/p7ss.exe"))
                                    {
                                        File.Delete(args[1]);
                                        File.Move("data/update/p7ss.exe", "p7ss.exe");
                                    }
                                }

                                Directory.Delete("data/update", true);

                                break;
                        }
                    }
                }

                Process.Start("p7ss.exe");

                Environment.Exit(0);
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }
    }
}
