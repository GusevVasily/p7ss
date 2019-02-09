using System.Collections.Generic;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class GetHistory : Core
    {
        internal static object Execute(SocketsList thisAuthSocket, int requestId, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
            };

            if (!string.IsNullOrEmpty((string)data["peer"]))
            {
                GetHistoryBody dataObject = new GetHistoryBody
                {
                    Peer = (int)data["peer"],
                    Offset = !string.IsNullOrEmpty((string)data["offset"])
                        ? (int)data["offset"] <= 50 ? (int)data["offset"] : 0
                        : 0,
                    Limit = !string.IsNullOrEmpty((string)data["limit"])
                        ? (int)data["limit"] <= 50 ? (int)data["limit"] : 50
                        : 50
                };

                using (MySqlConnection connect = new MySqlConnection())
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

                    connect.ConnectionString = builder.ConnectionString;
                    connect.Open();

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `type`, `hash` FROM `im` WHERE `users` = '|" + thisAuthSocket.UserId + "|" + dataObject.Peer + "|' OR `users` = '|" + dataObject.Peer + "|" + thisAuthSocket.UserId + "|'", connect);
                    MySqlDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            string hash = reader.GetString(2);
                            List<ResponseMessageObject> messages = new List<ResponseMessageObject>();
                            DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader.GetString(1) + "/" + reader.GetInt32(0));
                            int residue = 0;
                            foreach (var file in dir.GetFiles().OrderByDescending(x => x.FullName))
                            {
                                using (StreamReader sr = new StreamReader(file.ToString()))
                                {
                                    JArray messagesInFile = JArray.Parse(sr.ReadToEnd());
                                    if (dataObject.Offset > 0)
                                    {
                                        if (dataObject.Offset >= messagesInFile.Count)
                                        {
                                            dataObject.Offset -= messagesInFile.Count;

                                            continue;
                                        }

                                        residue = dataObject.Offset;
                                        dataObject.Offset = 0;
                                    }

                                    for (int i = messagesInFile.Count - residue - 1; i >= 0; i--)
                                    {
                                        if (dataObject.Limit > 0)
                                        {
                                            bool delete = false;
                                            foreach (var current in messagesInFile[i]["hide"])
                                            {
                                                if ((int) current == thisAuthSocket.UserId)
                                                {
                                                    delete = true;
                                                }
                                            }

                                            if (!delete)
                                            {
                                                JObject message = JObject.Parse(Cryptography.DeCrypt((string) messagesInFile[i]["data"], hash, true));
                                                messages.Add(new ResponseMessageObject
                                                {
                                                    Sender = (int) messagesInFile[i]["sender"],
                                                    Id = (int) message["id"],
                                                    Message = (string) message["message"],
                                                    Date = (int)message["date"]
                                                });

                                                dataObject.Limit--;
                                            }
                                        }
                                    }
                                }
                            }

                            responseObject = new ResponseJson
                            {
                                Result = true,
                                Id = requestId,
                                Response = messages
                            };
                        }
                    }
                    else
                    {
                        responseObject.Response = 302;
                    }

                    reader.Close();
                }
            }
            else
            {
                responseObject.Response = 301;
            }

            return responseObject;
        }
    }

    class GetHistoryBody
    {
        public int Peer;
        public int Offset;
        public int Limit;
    }

    class ResponseMessageObject
    {
        public int Sender;
        public int Id;
        public string Message;
        public int Date;
    }
}
