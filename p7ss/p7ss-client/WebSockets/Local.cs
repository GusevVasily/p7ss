using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;

namespace p7ss_client.WebSockets
{
    internal class Local : Core
    {
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

            options.Standards.RegisterRfc6455(delegate { });
            options.Transports.ConfigureTcp(delegate (TcpTransport tcp)
            {
                tcp.BacklogSize = 100;
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            Uri[] listenEndPoints = {
                new Uri(LocalWsDaemonUrl)
            };

            WebSocketListener server = new WebSocketListener(listenEndPoints, options);

            server.StartAsync().Wait(cancellation.Token);

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Server listening: " + LocalWsDaemonUrl); // debug

            await AcceptWebSocketsAsync(server, cancellation.Token);
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
                        ResponseLocal data = new ResponseLocal
                        {
                            Module = "auth.Start",
                            Data = new ResponseLocalData()
                        };

                        using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                        using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                        {
                            await sw.WriteAsync(JsonConvert.SerializeObject(data, SerializerSettings));
                            await sw.FlushAsync();
                        }

#pragma warning disable 4014
                        EchoAllIncomingMessagesAsync(webSocket, cancellation);
#pragma warning restore 4014
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
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
                    Console.WriteLine("An error occurred while accepting client: " + e);
                }
            }
        }

        private static async Task EchoAllIncomingMessagesAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            try
            {
                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    WebSocketMessageReadStream messageRead = await webSocket.ReadMessageAsync(cancellation);

                    if (messageRead != null && messageRead.MessageType == WebSocketMessageType.Text)
                    {
                        string request = await new StreamReader(messageRead, Utf8NoBom).ReadToEndAsync();

                        if (!string.IsNullOrEmpty(request))
                        {
                            JObject json = JObject.Parse(request);

                            if (json != null && !string.IsNullOrEmpty((string)json["method"]))
                            {
                                if (RemoteWsDaemon == null)
                                {
                                    RemoteWsDaemon = new Thread(Remote.Open)
                                    {
                                        IsBackground = true
                                    };

                                    RemoteWsDaemon.Start();

                                    Thread.Sleep(1000);
                                }

                                string[] method = json["method"].ToString().Split(".".ToCharArray());

                                if (!string.IsNullOrEmpty(method[0]) && !string.IsNullOrEmpty(method[1]))
                                {
                                    string result = Remote.Send(request);

                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        JObject response = JObject.Parse(result);

                                        if (!string.IsNullOrEmpty(response.ToString()))
                                        {
                                            ResponseLocal localResponse = new ResponseLocal
                                            {
                                                Module = (string)json["method"]
                                            };

                                            if ((bool)response["result"])
                                            {
                                                switch (method[0])
                                                {
                                                    case "auth":
                                                        switch (method[1])
                                                        {
                                                            case "checkLogin":
                                                                localResponse.Data = new ResponseLocalData
                                                                {
                                                                    Tfa_secret = (string)response["response"]["tfa_secret"]
                                                                };

                                                                break;

                                                            case "signUp":
                                                            case "signIn":
                                                                MyData myData = new MyData
                                                                {
                                                                    UserId = (int)response["response"]["user_id"],
                                                                    Session = (string)response["response"]["session"],
                                                                    FirstName = (string)response["response"]["first_name"],
                                                                    LastName = (string)response["response"]["last_name"],
                                                                    Avatar = (string)response["response"]["avatar"],
                                                                    Status = (string)response["response"]["status"]
                                                                };

                                                                MyData = myData;
                                                                myData.Session = null;

                                                                localResponse.Data = myData;

                                                                break;
                                                        }

                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                localResponse.Data = new ResponseLocalData
                                                {
                                                    Error_code = (int)response["response"]
                                                };
                                            }

                                            using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                            using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                            {
                                                await sw.WriteAsync(JsonConvert.SerializeObject(localResponse, SerializerSettings));
                                                await sw.FlushAsync();
                                            }
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
                await webSocket.CloseAsync();
            }
            catch (JsonReaderException)
            {
                await webSocket.CloseAsync();
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

                await webSocket.CloseAsync();
            }
        }
    }

    class ResponseLocal
    {
        public string Module;
        public object Data;
    }

    class ResponseLocalData
    {
        public object Error_code;
        public string Tfa_secret;
    }
}
