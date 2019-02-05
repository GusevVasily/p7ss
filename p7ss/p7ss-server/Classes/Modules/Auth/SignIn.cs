using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using TwoFactorAuthNet;
using vtortola.WebSockets;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class SignIn : Core
    {
        internal static object Execute(string clientIp, int requestId, JToken data, WebSocket socket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
            };

            if (!string.IsNullOrEmpty((string)data["login"])
                && !string.IsNullOrEmpty((string)data["tfa_code"])
                    && data["tfa_code"].ToString().Length == 6
            )
            {
                SignInBody dataObject = new SignInBody
                {
                    Login = (string)data["login"],
                    TfaCode = (string)data["tfa_code"]
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

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `name`, `avatar`, `status`, `tfa_secret` FROM `users` WHERE `login` = '" + dataObject.Login + "' AND `activated` = '1'", connect);
                    MySqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            TwoFactorAuth tfa = new TwoFactorAuth("p7ss://" + dataObject.Login);

                            //if (tfa.VerifyCode(reader.GetString(4), dataObject.TfaCode))
                            if (true) // debug
                            {
                                int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
                                string session = GenerateSession(dataObject.Login);

                                MainDbSend("UPDATE `users` SET " +
                                           "`ip` = '" + clientIp + "'," +
                                           "`session` = '" + session + "'," +
                                           "`time_auth` = '" + time + "' " +
                                           "WHERE `login` = '" + dataObject.Login + "'");

                                Ws.AuthSockets.Add(new SocketsList
                                {
                                    UserId = reader.GetInt32(0),
                                    Ip = clientIp,
                                    Session = session,
                                    Ws = socket
                                });

                                responseObject = new ResponseJson
                                {
                                    Result = true,
                                    Id = requestId,
                                    Response = new ResponseAuth
                                    {
                                        User_id = reader.GetInt32(0),
                                        Session = session,
                                        Name = reader.GetString(1),
                                        Avatar = reader.GetString(2),
                                        Status = reader.GetString(3)
                                    }
                                };
                            }
                            else
                            {
                                responseObject.Response = 303;
                            }
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

    class SignInBody
    {
        public string Login;
        public string TfaCode;
    }
}
