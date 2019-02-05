using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class SendMessage : Core
    {
        internal static object Execute(SocketsList thisAuthSocket, int requestId, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
            };

            if (!string.IsNullOrEmpty((string)data["peer"])
                && !string.IsNullOrEmpty((string)data["message"])
                && data["message"].ToString().Length > 1
            )
            {
                SendMessageBody dataObject = new SendMessageBody
                {
                    Peer = (int)data["peer"],
                    Message = (string)data["message"]
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

                    MySqlCommand command = new MySqlCommand("SELECT * FROM `im` WHERE `users` = '|" + thisAuthSocket.UserId + "|" + dataObject.Peer + "|' OR `users` = '|" + dataObject.Peer + "|" + thisAuthSocket.UserId + "|'", connect1);
                    MySqlDataReader reader1 = command.ExecuteReader();
                    string fileName = DateTime.Now.ToString("yyyyMMdd") + ".dat",
                        historyJson = null,
                        type = "users";
                    int time = (int) (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds,
                        recipient = 0,
                        messageId = 1,
                        imDir = 0;
                    if (reader1.HasRows)
                    {
                        while (reader1.Read())
                        {
                            imDir = reader1.GetInt32(0);
                            type = reader1.GetString(1);

                            switch (type)
                            {
                                case "channels":
                                    // TODO

                                    break;

                                case "chats":
                                    // TODO

                                    break;

                                case "users":
                                    string[] users = reader1.GetString(2).Split("|".ToCharArray());
                                    recipient = users[1] == thisAuthSocket.UserId.ToString() ? Convert.ToInt32(users[2]) : Convert.ToInt32(users[1]);
                                    DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader1.GetString(1) + "/" + imDir);
                                    foreach (var file in dir.GetFiles().OrderByDescending(x => x.FullName))
                                    {
                                        using (StreamReader sr = new StreamReader(file.ToString()))
                                        {
                                            historyJson = sr.ReadToEnd();
                                        }

                                        JArray history = JArray.Parse(historyJson);
                                        messageId = (int)history[history.Count - 1]["id"] + 1;

                                        break;
                                    }

                                    break;
                            }

                            MainDbSend("UPDATE `im` SET `time_update` = '" + time + "' WHERE `id` = '" + reader1.GetInt32(0) + "'");
                        }

                        reader1.Close();
                    }
                    else
                    {
                        using (MySqlConnection connect2 = new MySqlConnection())
                        {
                            connect2.ConnectionString = builder.ConnectionString;
                            connect2.Open();

                            command = new MySqlCommand("SELECT `id` FROM `users` WHERE `id` = '" + dataObject.Peer + "'", connect2);
                            MySqlDataReader reader2 = command.ExecuteReader();
                            if (!reader2.HasRows)
                            {
                                responseObject.Response = 303;

                                return responseObject;
                            }

                            string users = "|" + thisAuthSocket.UserId + "|" + dataObject.Peer + "|";
                            recipient = dataObject.Peer;

                            MainDbSend("INSERT INTO `im` (`type`, `users`, `time_add`, `time_update`) VALUES ('" + type + "', '" + users + "', '" + time + "', '" + time + "')");
                            reader2.Close();

                            command = new MySqlCommand("SELECT `id` FROM `im` WHERE `users` = '" + users + "'", connect2);
                            reader2 = command.ExecuteReader();
                            while (reader2.Read())
                            {
                                imDir = reader2.GetInt32(0);

                                Directory.CreateDirectory(Params.MessagesDir + type + "/" + imDir);
                            }

                            reader2.Close();
                        }
                    }

                    MessageParamsAdd newMessage = new MessageParamsAdd
                    {
                        Id = messageId,
                        Sender = thisAuthSocket.UserId,
                        Text = dataObject.Message,
                        Date = time,
                        Hide = new JArray()
                    };

                    using (StreamWriter sw = new StreamWriter(Params.MessagesDir + type + "/" + imDir + "/" + fileName))
                    {
                        while (true)
                        {
                            try
                            {
                                if (historyJson == null)
                                {
                                    sw.Write(JsonConvert.SerializeObject(new List<object>
                                    {
                                        newMessage
                                    }, SerializerSettings));
                                }
                                else
                                {
                                    sw.Write(historyJson.Remove(historyJson.Length - 1, 1) + "," + JsonConvert.SerializeObject(newMessage, SerializerSettings) + "]");
                                }

                                break;
                            }
                            catch (IOException)
                            {
                                Thread.Sleep(100);
                            }
                        }
                    }

                    newMessage.Hide = null;
                    responseObject = new ResponseJson
                    {
                        Result = true,
                        Id = requestId,
                        Response = new ResponseSendMessageWs
                        {
                            Recipient = recipient,
                            Body = newMessage
                        }
                    };
                }
            }
            else
            {
                responseObject.Response = 301;
            }

            return responseObject;
        }
    }

    class SendMessageBody
    {
        public int Peer;
        public string Message;
    }

    internal class MessageParamsAdd
    {
        public int Id;
        public int Sender;
        public string Text;
        public int Date;
        public JArray Hide;
    }

    internal class ResponseSendMessage
    {
        public int Id;
        public int Date;
    }

    internal class ResponseSendMessageWs
    {
        public int Recipient;
        public MessageParamsAdd Body;
    }
}
