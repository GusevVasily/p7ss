using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using vtortola.WebSockets;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class ImportAuthorization : Core
    {
        internal static object Execute(string clientIp, int requestId, JToken data, WebSocket socket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
            };

            if (!string.IsNullOrEmpty((string) data["id"]) && !string.IsNullOrEmpty((string) data["session"]))
            {
                ImportAuthorizationBody dataObject = new ImportAuthorizationBody
                {
                    Id = (int) data["id"],
                    Session = (string) data["session"]
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

                    MySqlCommand command = new MySqlCommand("SELECT `login`, `name`, `avatar`, `status` FROM `users` WHERE `id` = '" + dataObject.Id + "' AND `session` = '" + dataObject.Session + "' AND `activated` = '1'", connect);
                    MySqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            int time = (int) (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
                            string session = GenerateSession(reader.GetString(0));

                            MainDbSend("UPDATE `users` SET " +
                               "`ip` = '" + clientIp + "'," +
                               "`session` = '" + session + "'," +
                               "`time_auth` = '" + time + "' " +
                               "WHERE `id` = '" + dataObject.Id + "'");

                            List<SocketsList> oldSocket = Ws.AuthSockets.Where(x => x.UserId == dataObject.Id).ToList();

                            if (oldSocket.Count > 0)
                            {
                                Ws.AuthSockets.Remove(oldSocket.Last());
                            }

                            Ws.AuthSockets.Add(new SocketsList
                            {
                                UserId = dataObject.Id,
                                Ip = clientIp,
                                Session = session,
                                Ws = socket
                            });

                            responseObject = new ResponseJson
                            {
                                Result = true,
                                Id = requestId,
                                Response = new ResponseImportAuthorization
                                {
                                    User_id = dataObject.Id,
                                    Session = session,
                                    Name = reader.GetString(1),
                                    Avatar = reader.GetString(2),
                                    Status = reader.GetString(3)
                                }
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

    class ImportAuthorizationBody
    {
        public int Id;
        public string Session;
    }

    internal class ResponseImportAuthorization
    {
        public int User_id;
        public string Session;
        public string Name;
        public string Avatar;
        public string Status;
    }
}
