using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;

namespace p7ss_client.Classes.WebSockets
{
    internal class Local : Core
    {
        private static WebSocket _localSocket;

        public static async void Open()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (a, b) => cancellation.Cancel();
            int bufferSize = 8192;
            int bufferPoolSize = 100 * bufferSize;
            WebSocketListenerOptions options = new WebSocketListenerOptions
            {
                PingTimeout = TimeSpan.FromSeconds(5.0),
                NegotiationTimeout = TimeSpan.FromSeconds(5.0),
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize)
            };

            options.Standards.RegisterRfc6455();
            options.Transports.ConfigureTcp(delegate (TcpTransport tcp)
            {
                tcp.BacklogSize = 100;
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            int port = 0;
            foreach (var current in LocalWsDaemonPorts)
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
                bool isAvailable = true;
                foreach (TcpConnectionInformation tcp in tcpConnInfoArray)
                {
                    if (tcp.LocalEndPoint.Port == current)
                    {
                        isAvailable = false;

                        break;
                    }
                }

                if (isAvailable)
                {
                    port = current;

                    break;
                }
            }

            if (port != 0)
            {
                WebSocketListener localServer = new WebSocketListener(new []
                {
                    new Uri(LocalWsDaemonUrl + port)
                }, options);

                localServer.StartAsync().Wait(cancellation.Token);

                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Server listening: " + LocalWsDaemonUrl + port); // debug

                await AcceptWebSocketsAsync(localServer, cancellation.Token);
            }
            //else
            //{
                // Если нет открытых портов (из тех 10 штук)
                // TODO
            //}
        }

        private static async Task AcceptWebSocketsAsync(WebSocketListener server, CancellationToken cancellation)
        {
            await Task.Yield();

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    WebSocket webSocket = await server.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                    if (webSocket == null)
                    {
                        if (cancellation.IsCancellationRequested || !server.IsStarted)
                        {
                            break;
                        }
                    }
                    else
                    {
                        ResponseLocal localUserData;
                        if (_localSocket != null)
                        {
                            try
                            {
                                localUserData = new ResponseLocal
                                {
                                    Module = "close",
                                    Data = new ResponseLocalData()
                                };

                                using (WebSocketMessageWriteStream messageWriter = _localSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                {
                                    using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                    {
                                        await sw.WriteAsync(JsonConvert.SerializeObject(localUserData, SerializerSettings));
                                        await sw.FlushAsync();
                                    }
                                }

                                await _localSocket.CloseAsync();
                            }
                            catch (WebSocketException)
                            {
                                // nothing
                            }
                        }

                        _localSocket = webSocket;
                        localUserData = CheckRemoteSocket() ?? new ResponseLocal
                        {
                            Module = "auth",
                            Data = new ResponseLocalData()
                        };

                        using (WebSocketMessageWriteStream messageWriter = _localSocket.CreateMessageWriter(WebSocketMessageType.Text))
                        {
                            using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                            {
                                await sw.WriteAsync(JsonConvert.SerializeObject(localUserData, SerializerSettings));
                                await sw.FlushAsync();
                            }
                        }

#pragma warning disable 4014
                        EchoAllIncomingMessagesAsync(cancellation);
#pragma warning restore 4014
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    _localSocket = null;
                }
                catch (AggregateException)
                {
                    _localSocket = null;
                }
                catch (NullReferenceException)
                {
                    _localSocket = null;
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred while accepting client: " + e);
                }
            }
        }

        private static async Task EchoAllIncomingMessagesAsync(CancellationToken cancellation)
        {
            try
            {
                while (_localSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    WebSocketMessageReadStream messageRead = await _localSocket.ReadMessageAsync(cancellation);
                    if (messageRead != null && messageRead.MessageType == WebSocketMessageType.Text)
                    {
                        string request = await new StreamReader(messageRead, Utf8NoBom).ReadToEndAsync();
                        if (!string.IsNullOrEmpty(request))
                        {
                            JObject json = JObject.Parse(request);
                            if (!string.IsNullOrEmpty((string) json["method"]) && json["params"] != null)
                            {
                                int requestId = new Random((int) DateTime.Now.Ticks).Next();
                                json["id"] = requestId;
                                string[] method = json["method"].ToString().Split(".".ToCharArray());
                                if (!string.IsNullOrEmpty(method[0]) && !string.IsNullOrEmpty(method[1]))
                                {
                                    JObject response = Remote.Send(requestId, json);
                                    if (response != null)
                                    {
                                        ResponseLocal localResponse = new ResponseLocal
                                        {
                                            Module = (string) json["method"]
                                        };

                                        if ((bool) response["result"])
                                        {
                                            switch (method[0])
                                            {
                                                case "auth":
                                                    switch (method[1])
                                                    {
                                                        case "logOut":
                                                            UserData = new UserData();
                                                            RemoteWsDaemonThread = null;

                                                            UpdateSettings();

                                                            break;

                                                        case "checkLogin":
                                                            localResponse.Data = new ResponseLocalData
                                                            {
                                                                Tfa_secret = (string) response["response"]["tfa_secret"]
                                                            };

                                                            break;

                                                        case "signUp":
                                                        case "signIn":
                                                            UserData = new UserData
                                                            {
                                                                User_id = (int) response["response"]["user_id"],
                                                                Session = (string) response["response"]["session"],
                                                                Hash = GenerateSession((string)response["response"]["session"]),
                                                                Name = (string) response["response"]["name"],
                                                                Avatar = (string) response["response"]["avatar"],
                                                                Status = (string) response["response"]["status"]
                                                            };

                                                            UserData userData = new UserData
                                                            {
                                                                User_id = (int)response["response"]["user_id"],
                                                                Session = (string)response["response"]["session"],
                                                                Name = (string)response["response"]["name"],
                                                                Avatar = (string)response["response"]["avatar"],
                                                                Status = (string)response["response"]["status"]
                                                            };

                                                            if (!Directory.Exists("data"))
                                                            {
                                                                Directory.CreateDirectory("data");
                                                            }

                                                            UpdateSettings(userData);

                                                            localResponse.Data = new ResponseLocal
                                                            {
                                                                Module = "main",
                                                                Data = new UserData
                                                                {
                                                                    User_id = (int) response["response"]["user_id"],
                                                                    Name = (string) response["response"]["name"],
                                                                    Avatar = (string) response["response"]["avatar"],
                                                                    Status = (string) response["response"]["status"]
                                                                }
                                                            };

                                                            break;
                                                    }

                                                    break;

                                                default:
                                                    localResponse.Data = response["response"];

                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            // 400 ошибка (авторизован/не авторизован)
                                            // TODO
                                            localResponse.Data = new ResponseLocalData
                                            {
                                                Error_code = (int) response["response"]
                                            };
                                        }

                                        using (WebSocketMessageWriteStream messageWriter = _localSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                        {
                                            using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                            {
                                                await sw.WriteAsync(
                                                    JsonConvert.SerializeObject(
                                                        localResponse,
                                                        SerializerSettings
                                                    )
                                                );
                                                await sw.FlushAsync();
                                            }
                                        }

                                        if ((string) json["method"] == "auth.logOut")
                                        {
                                            await _localSocket.CloseAsync();

                                            _localSocket = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                await _localSocket.CloseAsync();
            }
            catch (JsonReaderException)
            {
                await _localSocket.CloseAsync();
            }
            catch (WebSocketException)
            {
                await _localSocket.CloseAsync();
            }
            catch (AggregateException)
            {
                // nothing
            }
            catch (NullReferenceException)
            {
                // nothing
            }
            catch (Exception e)
            {
                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Exception, class 'WebSocket': " + e);

                await _localSocket.CloseAsync();
            }
        }

        internal static async void SendMessage(string message)
        {
            try
            {
                using (WebSocketMessageWriteStream messageWriter = _localSocket.CreateMessageWriter(WebSocketMessageType.Text))
                using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                {
                    await sw.WriteAsync(message);
                    await sw.FlushAsync();
                }
            }
            catch (Exception)
            {
                _localSocket = null;
            }
        }
    }
}
