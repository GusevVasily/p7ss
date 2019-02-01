﻿using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using TwoFactorAuthNet;
using vtortola.WebSockets;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class SignIn : Core
    {
        internal static object Execute(string clientIp, JToken data, WebSocket socket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
            };

            using (MySqlConnection connect = new MySqlConnection())
            {
                SignInBody dataObject = new SignInBody
                {
                    Login = (string)data["login"],
                    TfaCode = (string)data["tfa_code"]
                };

                if (!string.IsNullOrEmpty(dataObject.Login) && !string.IsNullOrEmpty(dataObject.TfaCode))
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

                    MySqlCommand command = new MySqlCommand("SELECT `id`, `first_name`, `last_name`, `avatar`, `status`, `tfa` FROM `users` WHERE `login` = '" + dataObject.Login + "' AND `activated` = '1'", connect);
                    MySqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            TwoFactorAuth tfa = new TwoFactorAuth("p7ss://" + dataObject.Login);

                            if (tfa.VerifyCode(reader.GetString(5), dataObject.TfaCode))
                            {
                                int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
                                string session = GenerateSession(dataObject.Login);

                                using (MySqlConnection connect2 = new MySqlConnection())
                                {
                                    connect2.ConnectionString = builder.ConnectionString;
                                    connect2.Open();

                                    command = new MySqlCommand(
                                        "UPDATE `users` SET " +
                                        "`ip` = '" + clientIp + "'," +
                                        "`session` = '" + session + "'," +
                                        "`time_auth` = '" + time + "' " +
                                        "WHERE `login` = '" + dataObject.Login + "'",
                                        connect2
                                    );
                                    command.ExecuteNonQuery();
                                }

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
                                    Response = new ResponseSignInBody
                                    {
                                        User_id = reader.GetInt32(0),
                                        Session = session,
                                        First_name = reader.GetString(1),
                                        Last_name = reader.GetString(2),
                                        Status = reader.GetString(3),
                                        Avatar = reader.GetString(4)
                                    }
                                };
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

    class SignInBody
    {
        public string Login;
        public string TfaCode;
    }

    class ResponseSignInBody
    {
        public int User_id;
        public string Session;
        public string First_name;
        public string Last_name;
        public string Avatar;
        public string Status;
    }
}