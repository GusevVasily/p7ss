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

            using (MySqlConnection connect = new MySqlConnection())
            {
                SignUpBody dataObject = new SignUpBody
                {
                    Login = (string)data["login"],
                    TfaCode = (string)data["tfa_code"],
                    FirstName = (string)data["first_name"],
                    LastName = (string)data["last_name"]
                };

                if (!string.IsNullOrEmpty(dataObject.Login)
                        && dataObject.Login.Length >= 5
                        && dataObject.Login.Length <= 25
                    && !string.IsNullOrEmpty(dataObject.TfaCode)
                        && dataObject.TfaCode.Length == 6
                    && !string.IsNullOrEmpty(dataObject.FirstName)
                        && dataObject.FirstName.Length >= 1
                        && dataObject.FirstName.Length <= 256
                    //&& !string.IsNullOrEmpty(dataObject.LastName)
                        && dataObject.LastName.Length >= 0
                        && dataObject.LastName.Length <= 256
                )
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

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `activated`, `tfa` FROM `users` WHERE `login` = '" + dataObject.Login + "' AND `ip` = '" + clientIp + "'", connect);
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
                                        Response = new ResponseSignUpBody
                                        {
                                            User_id = reader.GetInt32(0),
                                            Session = session,
                                            First_name = dataObject.FirstName,
                                            Last_name = dataObject.LastName
                                        }
                                    };
                                }
                                else
                                {
                                    responseObject.Error_code = 304;
                                }
                            }
                            else
                            {
                                responseObject.Error_code = 303;
                            }
                        }
                    }
                    else
                    {
                        responseObject.Error_code = 302;
                    }

                    reader.Close();
                }
                else
                {
                    responseObject.Error_code = 301;
                }
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

    class ResponseSignUpBody
    {
        public int User_id;
        public string Session;
        public string First_name;
        public string Last_name;
    }
}
