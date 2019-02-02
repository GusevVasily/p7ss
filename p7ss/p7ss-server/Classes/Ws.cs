using p7ss_server.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;
using System.Collections.Generic;
using p7ss_server.Classes.Modules.Auth;
using p7ss_server.Classes.Modules.Messages;

namespace p7ss_server.Classes
{
    internal class Ws : Core
    {
        internal static readonly List<SocketsList> AuthSockets = new List<SocketsList>();

        public static async void Open()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
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

            options.Standards.RegisterRfc6455(delegate (WebSocketFactoryRfc6455 factory) { });
            options.Transports.ConfigureTcp(delegate (TcpTransport tcp)
            {
                tcp.BacklogSize = 100;
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            X509Certificate2 certificate = new X509Certificate2(File.ReadAllBytes(Params.CertificateFile), PfxCertificatePassword);

            options.ConnectionExtensions.RegisterSecureConnection(certificate);

            Uri[] listenEndPoints = {
                new Uri(WsDaemon)
            };

            WebSocketListener server = new WebSocketListener(listenEndPoints, options);

            server.StartAsync().Wait(cancellation.Token);

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Server listening: " + string.Join(", ", Array.ConvertAll(listenEndPoints, e => e.ToString())));

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
                            Console.WriteLine("Service WS server has stopped accepting new clients.");

                            break;
                        }
                    }
                    else
                    {
                        string clientIp = webSocket.HttpRequest.Headers["CF-Connecting-IP"];
                        //string clientIp = "127.0.0.1"; // debug

                        await EchoAllIncomingMessagesAsync(webSocket, clientIp, cancellation);
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

        private static async Task EchoAllIncomingMessagesAsync(WebSocket webSocket, string clientIp, CancellationToken cancellation)
        {
            try
            {
                SocketsList thisAuthSocket = null;

                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    WebSocketMessageReadStream messageRead = await webSocket.ReadMessageAsync(cancellation);

                    if (messageRead != null && messageRead.MessageType == WebSocketMessageType.Text)
                    {
                        string request = await new StreamReader(messageRead, Utf8NoBom).ReadToEndAsync();

                        await webSocket.WriteStringAsync(request, cancellation).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(request))
                        {
                            JObject json = JObject.Parse(request);

                            if (json != null && !string.IsNullOrEmpty((string)json["method"]))
                            {
                                string[] method = json["method"].ToString().Split(new[] { "." }, StringSplitOptions.None);
                                ResponseJson responseData;
                                object response = new ResponseJson
                                {
                                    Result = false,
                                    Error_code = 400
                                };

                                switch (method[0])
                                {
                                    case "auth":
                                        switch (method[1])
                                        {
                                            case "checkLogin":
                                                if (thisAuthSocket == null)
                                                {
                                                    response = CheckLogin.Execute(clientIp, json["params"]);
                                                }

                                                break;

                                            case "signUp":
                                                if (thisAuthSocket == null)
                                                {
                                                    responseData = (ResponseJson)SignUp.Execute(clientIp, json["params"], webSocket);

                                                    if (responseData.Result)
                                                    {
                                                        ResponseSignUpBody responseDataBody = (ResponseSignUpBody)responseData.Response;

                                                        thisAuthSocket = new SocketsList
                                                        {
                                                            UserId = responseDataBody.User_id,
                                                            Ip = clientIp,
                                                            Session = responseDataBody.Session,
                                                            Ws = webSocket
                                                        };
                                                    }

                                                    response = responseData;
                                                }

                                                break;

                                            case "signIn":
                                                if (thisAuthSocket == null)
                                                {
                                                    responseData = (ResponseJson)SignIn.Execute(clientIp, json["params"], webSocket);

                                                    if (responseData.Result)
                                                    {
                                                        ResponseSignInBody responseDataBody = (ResponseSignInBody)responseData.Response;

                                                        thisAuthSocket = new SocketsList
                                                        {
                                                            UserId = responseDataBody.User_id,
                                                            Ip = clientIp,
                                                            Session = responseDataBody.Session,
                                                            Ws = webSocket
                                                        };
                                                    }

                                                    response = responseData;
                                                }

                                                break;

                                            case "logOut":
                                                if (thisAuthSocket != null)
                                                {
                                                    thisAuthSocket = null;
                                                    response = LogOut.Execute(thisAuthSocket);
                                                }

                                                break;

                                            default:
                                                response = new ResponseJson
                                                {
                                                    Result = false,
                                                    Error_code = 404
                                                };

                                                break;
                                        }

                                        break;

                                    case "messages":
                                        if (thisAuthSocket != null)
                                        {
                                            switch (method[1])
                                            {
                                                case "getDialogs":
                                                    response = GetDialogs.Execute(thisAuthSocket, json["params"]);

                                                    break;

                                                default:
                                                    response = new ResponseJson
                                                    {
                                                        Result = false,
                                                        Error_code = 404
                                                    };

                                                    break;
                                            }

                                            break;
                                        }

                                        break;

                                    default:
                                        response = new ResponseJson
                                        {
                                            Result = false,
                                            Error_code = 404
                                        };

                                        break;
                                }

                                using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                {
                                    using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                    {
                                        await sw.WriteAsync(
                                            JsonConvert.SerializeObject(
                                                response,
                                                SerializerSettings
                                            )
                                        );
                                        await sw.FlushAsync();

                                        if ((string)json["method"] == "auth.logOut")
                                        {
                                            await webSocket.CloseAsync();
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

        //internal static async void SendAllMessage(string message)
        //{
        //    List<SocketsList> pairSockets = pair == "@all"
        //        ? language == "@all"
        //            ? AllSockets.Where(x => x.Language != "#api").ToList()
        //            : AllSockets.Where(x => x.Language == language).ToList()
        //        : AllSockets.Where(x => x.Pair == pair).ToList();

        //    if (pairSockets.Count > 0)
        //    {
        //        foreach (var current in pairSockets.ToList())
        //        {
        //            try
        //            {
        //                using (WebSocketMessageWriteStream messageWriter = current.Ws.CreateMessageWriter(WebSocketMessageType.Text))
        //                {
        //                    using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
        //                    {
        //                        await sw.WriteAsync(message);
        //                        await sw.FlushAsync();
        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                AllSockets.Remove(current);
        //            }
        //        }
        //    }
        //}
    }

    class SocketsList
    {
        public int UserId;
        public string Ip;
        public string Session;
        public WebSocket Ws;
    }

    internal class ResponseJson
    {
        public bool Result;
        public object Response;
        public int Error_code;
    }
}
