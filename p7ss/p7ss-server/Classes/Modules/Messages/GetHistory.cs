using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class GetHistory : Core
    {
        internal static object Execute(SocketsList thisAuthSocket, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
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

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `type` FROM `im` WHERE `id` = '" + dataObject.Peer + "' AND `users` LIKE '%|" + thisAuthSocket.UserId + "|%'", connect1);
                    MySqlDataReader reader1 = command.ExecuteReader();

                    if (reader1.HasRows)
                    {
                        while (reader1.Read())
                        {
                            List<MessageParamsAdd> messages = new List<MessageParamsAdd>();
                            DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader1.GetString(1) + "/" + reader1.GetInt32(0));
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
                                            messages.Add(new MessageParamsAdd
                                            {
                                                Id = (int)messagesInFile[i]["id"],
                                                Text = (string)messagesInFile[i]["text"],
                                                Date = (int)messagesInFile[i]["date"],
                                            });

                                            dataObject.Limit--;
                                        }
                                    }
                                }
                            }

                            responseObject.Result = true;
                            responseObject.Response = messages;
                        }
                    }
                    else
                    {
                        responseObject.Response = 302;
                    }

                    reader1.Close();
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

    class ResponseGetHistory
    {
        public int Id;
        public string Name;
        public string Avatar;
        public string Message;
        public int Date;
    }
}
