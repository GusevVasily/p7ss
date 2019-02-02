using MySql.Data.MySqlClient;
using System;
using System.Linq;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class LogOut : Core
    {
        internal static object Execute(SocketsList thisAuthSocket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = true
            };

            int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            MySqlCommand command = new MySqlCommand("UPDATE `users` SET `session` = NULL, `time_logout` = '" + time + "' WHERE `id` = '" + thisAuthSocket.UserId + "'", MainDbConnect);
            command.ExecuteNonQuery();

            Ws.AuthSockets.Remove(Ws.AuthSockets.Where(x => x.UserId == thisAuthSocket.UserId).ToList().Last());

            return responseObject;
        }
    }
}
