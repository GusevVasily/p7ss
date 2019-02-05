using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class GetDialogs : Core
    {
        internal static object Execute(SocketsList thisAuthSocket, int requestId, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
            };

            GetDialogsBody dataObject = new GetDialogsBody
            {
                Offset = !string.IsNullOrEmpty((string)data["offset"])
                    ? (int)data["offset"] <= 50 ? (int)data["offset"] : 0
                    : 0,
                Limit = !string.IsNullOrEmpty((string)data["limit"])
                    ? (int)data["limit"] <= 50 ? (int)data["limit"] : 50
                    : 50
            };

            using (MySqlConnection connect1 = new MySqlConnection())
            {
                MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
                {
                    Server = MainDb.Hostname,
                    Database = MainDb.Basename,
                    UserID = MainDb.Username,
                    Password = MainDb.Password,
                    CharacterSet = MainDb.Charset,
                    Port = MainDb.Port,
                    SslMode = MySqlSslMode.None
                };

                connect1.ConnectionString = builder.ConnectionString;
                connect1.Open();

                MySqlCommand command = new MySqlCommand("SELECT * FROM `im` WHERE `users` LIKE '%|" + thisAuthSocket.UserId + "|%' ORDER BY `time_update` DESC LIMIT " + dataObject.Offset + ", " + dataObject.Limit, connect1);
                MySqlDataReader reader1 = command.ExecuteReader();
                List<ResponseGetDialogs> dialogs = new List<ResponseGetDialogs>();
                while (reader1.Read())
                {
                    switch (reader1.GetString(1))
                    {
                        case "channels":
                            // TODO

                            break;

                        case "chats":
                            // TODO

                            break;

                        case "users":
                            string[] users = reader1.GetString(2).Split("|".ToCharArray());
                            int recipient = users[1] == thisAuthSocket.UserId.ToString() ? Convert.ToInt32(users[2]) : Convert.ToInt32(users[1]);
                            ResponseGetDialogs peer = null;
                            DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader1.GetString(1) + "/" + reader1.GetInt32(0));
                            foreach (var file in dir.GetFiles().OrderByDescending(x => x.FullName))
                            {
                                peer = new ResponseGetDialogs
                                {
                                    Id = reader1.GetInt32(0)
                                };

                                using (MySqlConnection connect2 = new MySqlConnection())
                                {
                                    connect2.ConnectionString = builder.ConnectionString;
                                    connect2.Open();

                                    command = new MySqlCommand("SELECT `name`, `avatar` FROM `users` WHERE `id` = '" + recipient + "'", connect2);
                                    MySqlDataReader reader2 = command.ExecuteReader();
                                    while (reader2.Read())
                                    {
                                        peer.Name = reader2.GetString(0);
                                        peer.Avatar = reader2.GetString(1);
                                    }

                                    reader2.Close();
                                }

                                string messages;
                                using (StreamReader sr = new StreamReader(file.ToString()))
                                {
                                    messages = sr.ReadToEnd();
                                }

                                if (!string.IsNullOrEmpty(messages))
                                {
                                    JArray json = JArray.Parse(messages);
                                    for (var i = json.Count - 1; i >= 0; i--)
                                    {
                                        if (peer.Message == null)
                                        {
                                            bool delete = false;
                                            foreach (var current in json[i]["hide"])
                                            {
                                                if ((int)current == thisAuthSocket.UserId)
                                                {
                                                    delete = true;
                                                }
                                            }

                                            if (!delete)
                                            {
                                                peer.Message = (string)json[i]["text"];
                                                peer.Date = (int)json[i]["date"];
                                            }
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }

                                break;
                            }

                            if (peer != null)
                            {
                                dialogs.Add(peer);
                            }

                            break;
                    }
                }

                reader1.Close();

                responseObject.Result = true;
                responseObject.Response = dialogs;
            }

            return responseObject;
        }
    }

    class GetDialogsBody
    {
        public int Offset;
        public int Limit;
    }

    class ResponseGetDialogs
    {
        public int Id;
        public string Name;
        public string Avatar;
        public string Message;
        public int Date;
    }
}
