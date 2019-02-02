using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class GetDialogs : Core
    {
        internal static object Execute(SocketsList thisAuthSosket, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
            };

            using (MySqlConnection connect1 = new MySqlConnection())
            {
                GetDialogsBody dataObject = new GetDialogsBody
                {
                    Offset = (int)data["offset"] <= 50 ? (int)data["offset"] : 50,
                    Limit = (int)data["limit"] <= 50 ? (int)data["limit"] : 50,
                };

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

                MySqlCommand command = new MySqlCommand("SELECT * FROM `im` WHERE `users` LIKE '%|" + thisAuthSosket.UserId + "|%' ORDER BY `time_update` DESC LIMIT " + dataObject.Offset + ", " + dataObject.Limit, connect1);
                MySqlDataReader reader1 = command.ExecuteReader();

                List<ResponseGetDialogsList> dialogs = new List<ResponseGetDialogsList>();

                while (reader1.Read())
                {
                    switch (reader1.GetString(1))
                    {
                        case "users":
                            string[] users = reader1.GetString(2).Split(new[] { "|" }, StringSplitOptions.None);
                            int twoId = users[1] == thisAuthSosket.UserId.ToString() ? Convert.ToInt32(users[2]) : Convert.ToInt32(users[1]);
                            ResponseGetDialogsList dialog = null;
                            DirectoryInfo dir = new DirectoryInfo(Params.MessagesDir + reader1.GetString(1) + "/" + reader1.GetInt32(0));
                            
                            foreach (var file in dir.GetFiles().OrderByDescending(x => x.FullName))
                            {
                                dialog = new ResponseGetDialogsList
                                {
                                    Id = reader1.GetInt32(0)
                                };

                                using (MySqlConnection connect2 = new MySqlConnection())
                                {
                                    connect2.ConnectionString = builder.ConnectionString;
                                    connect2.Open();

                                    command = new MySqlCommand("SELECT `first_name`, `last_name`, `avatar` FROM `users` WHERE `id` = '" + twoId + "'", connect2);
                                    MySqlDataReader reader2 = command.ExecuteReader();

                                    while (reader2.Read())
                                    {
                                        dialog.Name = reader2.GetString(1) != null
                                            ? reader2.GetString(0) + " " + reader2.GetString(1)
                                            : reader2.GetString(0);
                                        dialog.Avatar = reader2.GetString(2);
                                    }

                                    reader2.Close();
                                }

                                using (StreamReader sr = new StreamReader(file.ToString()))
                                {
                                    string messages = sr.ReadToEnd();

                                    if (!string.IsNullOrEmpty(messages))
                                    {
                                        JObject json = JObject.Parse(messages);

                                        for (var i = json["data"].Count() - 1; i >= 0; i--)
                                        {
                                            if (dialog.Message == null)
                                            {
                                                bool delete = false;

                                                foreach (var current in json["data"][i]["hide"])
                                                {
                                                    if ((int)current == thisAuthSosket.UserId)
                                                    {
                                                        delete = true;
                                                    }
                                                }

                                                if (!delete)
                                                {
                                                    dialog.Message = (string)json["data"][i]["text"];
                                                    dialog.Date = (int)json["data"][i]["date"];
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                        break;
                                    }
                                }

                                break;
                            }

                            if (dialog != null)
                            {
                                dialogs.Add(dialog);
                            }

                            break;

                        default:
                            // TODO

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

    class ResponseGetDialogsList
    {
        public int Id;
        public string Name;
        public string Avatar;
        public string Message;
        public int Date;
    }
}
