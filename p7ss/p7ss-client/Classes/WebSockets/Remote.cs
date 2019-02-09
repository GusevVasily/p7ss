using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;

namespace p7ss_client.Classes.WebSockets
{
    internal class Remote : Core
    {
        private static CancellationTokenSource _cancellation;
        private static readonly Dictionary<int, JObject> Requests = new Dictionary<int, JObject>();
        internal static WebSocket RemoteSocket;

        public static async void Open()
        {
            while (true)
            {
                try
                {
                    _cancellation = new CancellationTokenSource();
                    Console.CancelKeyPress += delegate
                    {
                        _cancellation.Cancel();
                    };
                    int bufferSize = 8192;
                    int num = 100 * bufferSize;
                    WebSocketListenerOptions webSocketListenerOptions = new WebSocketListenerOptions
                    {
                        SendBufferSize = bufferSize,
                        BufferManager = BufferManager.CreateBufferManager(num, bufferSize)
                    };

                    webSocketListenerOptions.Standards.RegisterRfc6455();
                    webSocketListenerOptions.Transports.ConfigureTcp(delegate(TcpTransport tcp)
                    {
                        tcp.BacklogSize = 100;
                        tcp.ReceiveBufferSize = bufferSize;
                        tcp.SendBufferSize = bufferSize;
                    });

                    WebSocketClient webSocketClient = new WebSocketClient(webSocketListenerOptions);
                    RemoteSocket = webSocketClient.ConnectAsync(new Uri(RemoteWsDaemonUrl), _cancellation.Token).Result;
                    Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Client listening: " + RemoteWsDaemonUrl); // debug

                    break;
                }
                catch (AggregateException)
                {
                    Thread.Sleep(3000);
                }
                catch (ArgumentException)
                {
                    Thread.Sleep(3000);
                }
            }

            await AcceptWebSocketsAsync();
        }

        private static async Task AcceptWebSocketsAsync()
        {
            await Task.Yield();

            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    WebSocketMessageReadStream messageRead = await RemoteSocket.ReadMessageAsync(_cancellation.Token);
                    if (messageRead != null && messageRead.MessageType == WebSocketMessageType.Text)
                    {
                        string message = await new StreamReader(messageRead, Utf8NoBom).ReadToEndAsync();
                        if (!string.IsNullOrEmpty(message))
                        {
                            bool encrypted = true;
                            while (true)
                            {
                                try
                                {
                                    JObject json = JObject.Parse(encrypted ? Cryptography.DeCrypt(message, UserData.Hash) : message);
                                    if (!string.IsNullOrEmpty((string)json["result"]))
                                    {
                                        Requests.Add((int)json["id"], json);
                                    }
                                    else if (!string.IsNullOrEmpty((string)json["module"]))
                                    {
                                        Local.SendMessage(json.ToString());
                                    }
                                    else if (!string.IsNullOrEmpty((string)json["hash"]))
                                    {
                                        UserData.Hash = (string)json["hash"];
                                    }

                                    break;
                                }
                                catch (ArgumentNullException)
                                {
                                    encrypted = false;
                                }
                            }
                        }
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }

        internal static JObject Send(int num, object data)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    RemoteSocket.WriteStringAsync(
                        Cryptography.EnCrypt(
                            JsonConvert.SerializeObject(
                                data,
                                SerializerSettings
                            ),
                            UserData.Hash
                        )
                    ).Wait(_cancellation.Token);

                    for (int i = 0; i < 50; i++)
                    {
                        Thread.Sleep(200);

                        List<KeyValuePair<int, JObject>> search = Requests.Where(x => x.Key == num).ToList();
                        if (search.Count > 0)
                        {
                            Requests.Remove(search.Last().Key);

                            return search.Last().Value;
                        }
                    }
                }
                catch (AggregateException) { }

                RemoteWsDaemonThread = null;

                CheckRemoteSocket();
                Thread.Sleep(3000);

                attempts++;
                if (attempts == 5)
                {
                    return null;
                }
            }
        }
    }

    class RemoteSend
    {
        public string Method;
        public int Id;
        public object Params;
    }

    class ImportAuthorization
    {
        public int Id;
        public string Session;
    }
}
