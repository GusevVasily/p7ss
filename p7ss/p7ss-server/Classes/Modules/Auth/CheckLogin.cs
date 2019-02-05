using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using p7ss_server.Configs;
using TwoFactorAuthNet;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class CheckLogin : Core
    {
        internal static object Execute(string clientIp, int requestId, JToken data)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false,
                Id = requestId
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
                    string symbols = "abcdefghijklmnopqrstuvwxyz01234567890_";

                    for (var i = 0; i < login.Length; i++)
                    {
                        if (symbols.IndexOf(login[i]) == -1)
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

                    MySqlCommand command = new MySqlCommand("SELECT `activated` FROM `users` WHERE `login` = '" + dataObject.Login + "'", connect);
                    MySqlDataReader reader = command.ExecuteReader();
                    TwoFactorAuth tfa = new TwoFactorAuth("p7ss://" + dataObject.Login);
                    string secret = tfa.CreateSecret(160);

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader.GetInt32(0) != 0)
                            {
                                responseObject.Response = 304;

                                return responseObject;
                            }
                        }

                        MainDbSend("UPDATE `users` SET `tfa_secret` = '" + secret + "' WHERE `login` = '" + dataObject.Login + "'");
                    }
                    else
                    {
                        MainDbSend("INSERT INTO `users` (`login`, `name`, `avatar`, `status`, `tfa_secret`, `ip`) VALUES ('" + dataObject.Login + "', '', '', '', '" + secret + "', '" + clientIp + "')");
                    }

                    responseObject = new ResponseJson
                    {
                        Result = true,
                        Id = requestId,
                        Response = new ResponseCheckLogin
                        {
                            Tfa_secret = secret
                        }
                    };

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
