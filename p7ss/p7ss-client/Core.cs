using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace p7ss_client
{
    internal class Core : Config
    {
        internal static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, false);
        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        internal static MyData MyData = null;
        internal static Thread RemoteWsDaemon = null;
    }

    internal class MyData
    {
        public int UserId;
        public string Session;
        public string FirstName;
        public string LastName;
        public string Avatar;
        public string Status;
    }
}
