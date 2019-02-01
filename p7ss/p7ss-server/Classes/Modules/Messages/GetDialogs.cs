using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7ss_server.Classes.Modules.Messages
{
    internal class GetDialogs
    {
        internal static object Execute()
        {
            ResponseJson responseObject = new ResponseJson
            {
                Result = false
            };

            return responseObject;
        }
    }
}
