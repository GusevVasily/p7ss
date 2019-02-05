namespace p7ss_client
{
    internal class Config
    {
        public static int[] LocalWsDaemonPorts = { 4639, 2278, 815, 4693, 8103, 2956, 1682, 173, 273, 2900 };
        public const string LocalWsDaemonUrl = "ws://127.0.0.1:";
        public const string RemoteWsDaemonUrl = "wss://api.p7ss.ru";
    }
}
