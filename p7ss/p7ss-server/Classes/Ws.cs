using p7ss_server.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;
using System.Collections.Generic;

namespace p7ss_server.Classes
{
    internal class Ws : Core
    {
        internal static readonly List<SocketsList> AllSockets = new List<SocketsList>();

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

            options.Standards.RegisterRfc6455(delegate (WebSocketFactoryRfc6455 factory)
            {
                factory.MessageExtensions.RegisterDeflateCompression();
            });

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

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + " WebSocket Server listening: " + string.Join(", ", Array.ConvertAll(listenEndPoints, e => e.ToString())));

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
                        string clientIp = webSocket.HttpRequest.Headers["Hoobes-Client-Ip"];

                        if (!string.IsNullOrEmpty(clientIp))
                        {
                            if (BannedSockets.Where(x => x.Ip == clientIp).ToList().Count > 0)
                            {
                                await webSocket.CloseAsync();
                            }
                            else
                            {
                                await EchoAllIncomingMessagesAsync(webSocket, clientIp, cancellation);
                            }
                        }
                        else
                        {
                            await webSocket.CloseAsync();
                        }
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
                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    WebSocketMessageReadStream messageRead = await webSocket.ReadMessageAsync(cancellation);

                    if (messageRead != null && messageRead.MessageType == WebSocketMessageType.Text)
                    {
                        StreamReader reader = new StreamReader(messageRead, Utf8NoBom);
                        string request = await reader.ReadToEndAsync();

                        if (!string.IsNullOrEmpty(request))
                        {
                            await webSocket.WriteStringAsync(request, cancellation).ConfigureAwait(false);

                            string decrypt = Cryptography.DeCrypt(Convert.FromBase64String(request));

                            if (!string.IsNullOrEmpty(decrypt))
                            {
                                JObject json = JObject.Parse(decrypt);
                                string response = null;

                                switch ((string)json["method"])
                                {
                                    default:
                                        BanSocket(clientIp, "method");

                                        break;
                                }

                                if (response != null)
                                {
                                    using (WebSocketMessageWriteStream messageWriter = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                                    {
                                        using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
                                        {
                                            await sw.WriteAsync(
                                                Convert.ToBase64String(
                                                    Cryptography.EnCrypt(
                                                        response
                                                    )
                                                )
                                            );
                                        }
                                    }
                                }
                            }
                            else
                            {
                                BanSocket(clientIp, "decrypt");
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                BanSocket(clientIp, "FormatException");
            }
            catch (JsonReaderException)
            {
                BanSocket(clientIp, "JsonReaderException");
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
            }
            finally
            {
                await webSocket.CloseAsync();
            }
        }

        internal static async void SendAllMessage(string message)
        {
            //List<SocketsList> pairSockets = pair == "@all"
            //    ? language == "@all"
            //        ? AllSockets.Where(x => x.Language != "#api").ToList()
            //        : AllSockets.Where(x => x.Language == language).ToList()
            //    : AllSockets.Where(x => x.Pair == pair).ToList();

            //if (pairSockets.Count > 0)
            //{
            //    foreach (var current in pairSockets.ToList())
            //    {
            //        try
            //        {
            //            using (WebSocketMessageWriteStream messageWriter = current.Ws.CreateMessageWriter(WebSocketMessageType.Text))
            //            {
            //                using (StreamWriter sw = new StreamWriter(messageWriter, Utf8NoBom))
            //                {
            //                    await sw.WriteAsync(message);
            //                    await sw.FlushAsync();
            //                }
            //            }
            //        }
            //        catch (Exception)
            //        {
            //            AllSockets.Remove(current);
            //        }
            //    }
            //}
        }
    }

    class SocketsList
    {
        public string Ip;
        public WebSocket Ws;
    }
}
