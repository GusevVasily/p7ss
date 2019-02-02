using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class LogOut : Core
    {
        internal static object Execute(SocketsList thisAuthSosket)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = true
            };

            int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            MySqlCommand command = new MySqlCommand("UPDATE `users` SET `session` = NULL, `time_logout` = '" + time + "' WHERE `id` = '" + thisAuthSosket.UserId + "'", MainDbConnect);
            command.ExecuteNonQuery();

            List<SocketsList> sockets = Ws.AuthSockets.Where(x => x.UserId == thisAuthSosket.UserId).ToList();

            if (sockets.Count > 0)
            {
                Ws.AuthSockets.Remove(sockets.Last());
            }

            return responseObject;
        }
    }
}
