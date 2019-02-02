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
        internal static object Execute(SocketsList thisAuthSocket, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
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

                    MySqlCommand command = new MySqlCommand("SELECT * FROM `im` WHERE `id` = '" + dataObject.Peer + "'", connect1);
                    MySqlDataReader reader1 = command.ExecuteReader();
                    string fileName = DateTime.Now.ToString("yyyyMMdd") + ".dat", historyJson = null, type = "users";
                    int time = (int) (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds,
                        recipient = 0,
                        id = 1;

                    if (reader1.HasRows)
                    {
                        while (reader1.Read())
                        {
                            if (reader1.GetString(2).Contains("|" + thisAuthSocket.UserId + "|"))
                            {
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
                                        recipient = users[1] == thisAuthSocket.UserId.ToString()
                                            ? Convert.ToInt32(users[2])
                                            : Convert.ToInt32(users[1]);
                                        DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader1.GetString(1) + "/" + reader1.GetInt32(0));

                                        foreach (var file in dir.GetFiles().OrderByDescending(x => x.FullName))
                                        {
                                            using (StreamReader sr = new StreamReader(file.ToString()))
                                            {
                                                historyJson = sr.ReadToEnd();

                                                JArray history = JArray.Parse(historyJson);

                                                id = (int)history[history.Count - 1]["id"] + 1;
                                            }

                                            break;
                                        }

                                        break;
                                }
                            }
                            else
                            {
                                responseObject.Response = 302;

                                return responseObject;
                            }
                        }

                        command = new MySqlCommand("UPDATE `im` SET `time_update` = '" + time + "' WHERE `id` = '" + reader1.GetInt32(0) + "'", MainDbConnect);
                    }
                    else
                    {
                        using (MySqlConnection connect2 = new MySqlConnection())
                        {
                            connect1.ConnectionString = builder.ConnectionString;
                            connect1.Open();

                            command = new MySqlCommand("SELECT `id` FROM `users` WHERE `id` = '" + dataObject.Peer + "'", connect2);
                            MySqlDataReader reader2 = command.ExecuteReader();

                            if (reader2.HasRows)
                            {
                                string users = "|" + thisAuthSocket.UserId + "|" + dataObject.Peer + "|";
                                command = new MySqlCommand("INSERT INTO `im` (`type`, `users`, `time_add`, `time_update`) VALUES ('" + type + "', '" + users + "', '" + time + "', '" + time + "')", MainDbConnect);
                                recipient = dataObject.Peer;

                                reader2.Close();
                            }
                            else
                            {
                                reader2.Close();

                                responseObject.Response = 303;

                                return responseObject;
                            }
                        }
                    }

                    MessageParamsAdd newMessage = new MessageParamsAdd
                    {
                        Id = id,
                        Sender = thisAuthSocket.UserId,
                        Text = dataObject.Message,
                        Date = time,
                        Hide = new JArray()
                    };

                    using (StreamWriter sw = new StreamWriter(Params.MessagesDir + reader1.GetString(1) + "/" + reader1.GetInt32(0) + "/" + fileName))
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

                    reader1.Close();

                    command.ExecuteNonQuery();

                    newMessage.Hide = null;

                    responseObject.Result = true;
                    responseObject.Response = new ResponseSendMessageWs
                    {
                        Recipient = recipient,
                        Body = newMessage
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
