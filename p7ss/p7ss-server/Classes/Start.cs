using MySql.Data.MySqlClient;
using p7ss_server.Configs;

namespace p7ss_server.Classes
{
    internal class Start : Core
    {
        internal static void DatabasesConnect()
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

            MainDbConnect.ConnectionString = builder.ConnectionString;
            MainDbConnect.Open();
        }
    }
}
