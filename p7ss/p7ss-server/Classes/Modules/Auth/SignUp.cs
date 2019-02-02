using System;
using System.IO;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using TwoFactorAuthNet;
using vtortola.WebSockets;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class SignUp : Core
    {
        internal static object Execute(string clientIp, JToken data, WebSocket socket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
            };

            if (!string.IsNullOrEmpty((string)data["login"])
                    && data["login"].ToString().Length >= 5
                    && data["login"].ToString().Length <= 25
                && !string.IsNullOrEmpty((string)data["tfa_code"])
                    && data["tfa_code"].ToString().Length == 6
                && !string.IsNullOrEmpty((string)data["first_name"])
                    && data["first_name"].ToString().Length >= 1
                    && data["first_name"].ToString().Length <= 256
                && !string.IsNullOrEmpty((string)data["last_name"])
                    && data["last_name"].ToString().Length >= 1
                    && data["last_name"].ToString().Length <= 256
            )
            {
                SignUpBody dataObject = new SignUpBody
                {
                    Login = (string)data["login"],
                    TfaCode = (string)data["tfa_code"],
                    FirstName = (string)data["first_name"],
                    LastName = (string)data["last_name"]
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

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `activated`, `tfa_secret` FROM `users` WHERE `login` = '" + dataObject.Login + "' AND `ip` = '" + clientIp + "'", connect);
                    MySqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader.GetInt32(1) == 0)
                            {
                                TwoFactorAuth tfa = new TwoFactorAuth("p7ss://" + dataObject.Login);
                                if (tfa.VerifyCode(reader.GetString(2), dataObject.TfaCode))
                                {
                                    int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
                                    string session = GenerateSession(dataObject.Login);

                                    command = new MySqlCommand(
                                        "UPDATE `users` SET " +
                                        "`first_name` = '" + dataObject.FirstName + "'," +
                                        "`last_name` = '" + dataObject.LastName + "'," +
                                        "`session` = '" + session + "'," +
                                        "`activated` = '1'," +
                                        "`time_auth` = '" + time + "'," +
                                        "`time_reg` = '" + time + "'," +
                                        "`ip_reg` = '" + clientIp + "' " +
                                        "WHERE `login` = '" + dataObject.Login + "'",
                                        MainDbConnect
                                    );
                                    command.ExecuteNonQuery();

                                    Directory.CreateDirectory(Params.AvatarsDir + reader.GetInt32(0));

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
                                        Response = new ResponseAuth
                                        {
                                            User_id = reader.GetInt32(0),
                                            Session = session,
                                            First_name = dataObject.FirstName,
                                            Last_name = dataObject.LastName,
                                            Avatar = "",
                                            Status = ""
                                        }
                                    };
                                }
                                else
                                {
                                    responseObject.Response = 304;
                                }
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

    class SignUpBody
    {
        public string Login;
        public string TfaCode;
        public string FirstName;
        public string LastName;
    }
}
