using System;
using System.Linq;

namespace p7ss_server.Classes.Modules.Auth
{
    internal class LogOut : Core
    {
        internal static object Execute(SocketsList thisAuthSocket, int requestId = 0)
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = true,
                Id = requestId
            };

            int time = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            
            MainDbSend("UPDATE `users` SET `session` = NULL, `time_logout` = '" + time + "' WHERE `id` = '" + thisAuthSocket.UserId + "'");

            Ws.AuthSockets.Remove(Ws.AuthSockets.Where(x => x.UserId == thisAuthSocket.UserId).ToList().Last());

            return responseObject;
        }
    }
}
