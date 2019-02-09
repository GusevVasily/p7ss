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
using System.Linq;
using p7ss_server.Classes.Modules.Auth;
using p7ss_server.Classes.Modules.Messages;

namespace p7ss_server.Classes
{
    internal class Ws : Core
    {
        internal static readonly List<SocketsList> AuthSockets = new List<SocketsList>();

        public static async void Open()
        {
            X509Certificate2 certificate = new X509Certificate2(File.ReadAllBytes(Params.CertificateFile), PfxCertificatePassword);
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

            options.ConnectionExtensions.RegisterSecureConnection(certificate);
            options.Standards.RegisterRfc6455();
            options.Transports.ConfigureTcp(delegate (TcpTransport tcp)
            {
                tcp.BacklogSize = 100;
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            WebSocketListener server = new WebSocketListener(new []
            {
                new Uri(WsDaemon)
            }, options);

            server.StartAsync().Wait(cancellation.Token);

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Server listening: " + WsDaemon);

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
                        NewSocket newSocket = new NewSocket
                        {
                            Hash = GenerateSession(clientIp, true),
                            Date = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds
                        };

                        using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                        {
                            using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                            {
                                await sw.WriteAsync(JsonConvert.SerializeObject(newSocket, SerializerSettings));
                                await sw.FlushAsync();
                            }
                        }

#pragma warning disable 4014
                        EchoAllIncomingMessagesAsync(webSocket, clientIp, newSocket, cancellation);
#pragma warning restore 4014
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    // nothing
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
                    Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] An error occurred while accepting client: " + e);
                }
            }
        }

        private static async Task EchoAllIncomingMessagesAsync(WebSocket webSocket, string clientIp, NewSocket newSocket, CancellationToken cancellation)
        {
            string hash = newSocket.Hash;
            SocketsList thisSocket = new SocketsList
            {
                Hash = newSocket.Hash
            };

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
                            JObject json = JObject.Parse(Cryptography.DeCrypt(request, thisSocket.Hash, false));
                            if (json != null && !string.IsNullOrEmpty((string) json["method"]))
                            {
                                string[] method = json["method"].ToString().Split(".".ToCharArray());
                                int requestId = json["id"] != null ? (int) json["id"] : 0;
                                ResponseJson responseData;
                                object response = new ResponseJson
                                {
                                    Result = false,
                                    Id = json["id"] != null ? (int) json["id"] : 0,
                                    Response = 400
                                };

                                switch (method[0])
                                {
                                    case "auth":
                                        switch (method[1])
                                        {
                                            case "checkLogin":
                                                if (thisSocket.Session == null)
                                                {
                                                    response = CheckLogin.Execute(clientIp, requestId, json["params"]);
                                                }

                                                break;

                                            case "signUp":
                                                if (thisSocket.Session == null)
                                                {
                                                    responseData = (ResponseJson) SignUp.Execute(clientIp, requestId, json["params"], webSocket);
                                                    if (responseData.Result)
                                                    {
                                                        ResponseAuth responseDataBody = (ResponseAuth) responseData.Response;
                                                        string newHash = responseDataBody.Hash;
                                                        thisSocket = new SocketsList
                                                        {
                                                            UserId = responseDataBody.User_id,
                                                            Ip = clientIp,
                                                            Session = responseDataBody.Session,
                                                            Hash = newHash,
                                                            Ws = webSocket
                                                        };

                                                        responseDataBody.Hash = null;
                                                    }

                                                    response = responseData;
                                                }

                                                break;

                                            case "signIn":
                                                if (thisSocket.Session == null)
                                                {
                                                    responseData = (ResponseJson) SignIn.Execute(clientIp, requestId, json["params"], webSocket);
                                                    if (responseData.Result)
                                                    {
                                                        ResponseAuth responseDataBody = (ResponseAuth) responseData.Response;
                                                        string newHash = responseDataBody.Hash;
                                                        thisSocket = new SocketsList
                                                        {
                                                            UserId = responseDataBody.User_id,
                                                            Ip = clientIp,
                                                            Session = responseDataBody.Session,
                                                            Hash = newHash,
                                                            Ws = webSocket
                                                        };

                                                        responseDataBody.Hash = null;
                                                    }

                                                    response = responseData;
                                                }

                                                break;

                                            case "importAuthorization":
                                                responseData = (ResponseJson) ImportAuthorization.Execute(clientIp, requestId, json["params"], webSocket);
                                                if (responseData.Result)
                                                {
                                                    ResponseImportAuthorization responseDataBody = (ResponseImportAuthorization) responseData.Response;
                                                    string newHash = responseDataBody.Hash;
                                                    thisSocket = new SocketsList
                                                    {
                                                        UserId = responseDataBody.User_id,
                                                        Ip = clientIp,
                                                        Session = responseDataBody.Session,
                                                        Hash = newHash,
                                                        Ws = webSocket
                                                    };

                                                    responseDataBody.Hash = null;
                                                }

                                                response = responseData;

                                                break;

                                            case "logOut":
                                                if (thisSocket.Session != null)
                                                {
                                                    response = LogOut.Execute(thisSocket, requestId);
                                                    thisSocket = null;
                                                }

                                                break;

                                            default:
                                                response = new ResponseJson
                                                {
                                                    Result = false,
                                                    Response = 404
                                                };

                                                break;
                                        }

                                        break;

                                    case "messages":
                                        if (thisSocket.Session != null)
                                        {
                                            switch (method[1])
                                            {
                                                case "getDialogs":
                                                    response = GetDialogs.Execute(thisSocket, requestId, json["params"]);

                                                    break;

                                                case "getHistory":
                                                    response = GetHistory.Execute(thisSocket, requestId, json["params"]);

                                                    break;

                                                case "sentMessage":
                                                    responseData = (ResponseJson) SentMessage.Execute(thisSocket, requestId, json["params"]);
                                                    if (responseData.Result)
                                                    {
                                                        ResponseSentMessageWs responseDataBody = (ResponseSentMessageWs) responseData.Response;
                                                        List<SocketsList> twoSocket = AuthSockets.Where(x => x.UserId == responseDataBody.Recipient).ToList();
                                                        if (twoSocket.Count > 0)
                                                        {
                                                            using (WebSocketMessageWriteStream messageWriter = twoSocket.Last().Ws.CreateMessageWriter(WebSocketMessageType.Text))
                                                            {
                                                                using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                                                {
                                                                    AuthSocketSend userMessage = new AuthSocketSend
                                                                    {
                                                                        Module = "message",
                                                                        Type = "new",
                                                                        Data = responseDataBody.Body
                                                                    };

                                                                    await sw.WriteAsync(
                                                                        Cryptography.EnCrypt(
                                                                            JsonConvert.SerializeObject(
                                                                                userMessage,
                                                                                SerializerSettings
                                                                            ),
                                                                            twoSocket.Last().Hash,
                                                                            false
                                                                        )
                                                                    );
                                                                    await sw.FlushAsync();
                                                                }
                                                            }
                                                        }

                                                        responseData.Response = new ResponseSentMessage
                                                        {
                                                            Id = responseDataBody.Body.Id,
                                                            Date = responseDataBody.Body.Date
                                                        };

                                                        response = responseData;
                                                    }

                                                    break;

                                                default:
                                                    response = new ResponseJson
                                                    {
                                                        Result = false,
                                                        Id = requestId,
                                                        Response = 404
                                                    };

                                                    break;
                                            }
                                        }

                                        break;

                                    default:
                                        response = new ResponseJson
                                        {
                                            Result = false,
                                            Id = requestId,
                                            Response = 404
                                        };

                                        break;
                                }

                                using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                {
                                    using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                    {
                                        await sw.WriteAsync(
                                            Cryptography.EnCrypt(
                                                JsonConvert.SerializeObject(
                                                    response,
                                                    SerializerSettings
                                                ),
                                                hash,
                                                false
                                            )
                                        );
                                        await sw.FlushAsync();

                                        ResponseJson res = (ResponseJson) response;
                                        switch ((string)json["method"])
                                        {
                                            case "auth.signUp":
                                            case "auth.signIn":
                                                if (res.Result)
                                                {
                                                    hash = thisSocket.Hash;
                                                }

                                                break;

                                            case "auth.importAuthorization":
                                                if (res.Result)
                                                {
                                                    hash = thisSocket.Hash;
                                                }

                                                break;

                                            case "auth.logOut":
                                                await webSocket.CloseAsync();

                                                break;
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
                if (thisSocket.Session != null)
                {
                    LogOut.Execute(thisSocket);
                }

                await webSocket.CloseAsync();
            }
            catch (JsonReaderException)
            {
                if (thisSocket.Session != null)
                {
                    LogOut.Execute(thisSocket);
                }

                await webSocket.CloseAsync();
            }
            catch (WebSocketException)
            {
                if (thisSocket.Session != null)
                {
                    LogOut.Execute(thisSocket);
                }

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

                if (thisSocket.Session != null)
                {
                    LogOut.Execute(thisSocket);
                }

                await webSocket.CloseAsync();
            }
        }
    }

    class NewSocket
    {
        public string Hash;
        public int Date;
    }

    class AuthSocketSend
    {
        public string Module;
        public string Type;
        public object Data;
    }

    internal class SocketsList
    {
        public int UserId;
        public string Ip;
        public string Session;
        public string Hash;
        public WebSocket Ws;
    }

    internal class ResponseJson
    {
        public bool Result;
        public int Id;
        public object Response;
    }

    internal class ResponseAuth
    {
        public int User_id;
        public string Session;
        public string Hash;
        public string Name;
        public string Avatar;
        public string Status;
    }
}
