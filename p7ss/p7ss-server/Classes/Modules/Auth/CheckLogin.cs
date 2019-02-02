using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using TwoFactorAuthNet;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class CheckLogin : Core
    {
        internal static object Execute(string clientIp, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
            };

            CheckLoginBody dataObject = new CheckLoginBody
            {
                Login = (string)data["login"]
            };

            if (!string.IsNullOrEmpty(dataObject.Login)
                    && dataObject.Login.Length >= 5
                    && dataObject.Login.Length <= 32
            )
            {
                using (MySqlConnection connect = new MySqlConnection())
                {
                    string login = dataObject.Login.ToLower();
                    string[] symbols = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "_" };

                    for (var i = 0; i < dataObject.Login.Length; i++)
                    {
                        if (Array.IndexOf(symbols, login[i]) == 0)
                        {
                            responseObject.Response = 302;

                            return responseObject;
                        }
                    }

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

                    MySqlCommand command = new MySqlCommand("SELECT `activated` FROM `users` WHERE `login` = '" + dataObject.Login + "' AND `activated` = '1'", connect);
                    MySqlDataReader reader = command.ExecuteReader();

                    if (!reader.HasRows)
                    {
                        TwoFactorAuth tfa = new TwoFactorAuth("p7ss://" + dataObject.Login);
                        string secret = tfa.CreateSecret(160);

                        command = new MySqlCommand("INSERT INTO `users` (`login`, `first_name`, `last_name`, `avatar`, `status`, `tfa`, `ip`) VALUES ('" + dataObject.Login + "', '', '', '', '', '" + secret + "', '" + clientIp + "')", MainDbConnect);
                        command.ExecuteNonQuery();

                        responseObject = new ResponseJson
                        {
                            Result = true,
                            Response = new ResponseCheckLogin
                            {
                                Tfa_secret = secret
                            }
                        };
                    }
                    else
                    {
                        responseObject.Response = 303;
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

    class CheckLoginBody
    {
        public string Login;
    }

    class ResponseCheckLogin
    {
        public string Tfa_secret;
    }
}
