using System;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.Tcp;

namespace p7ss_client.WebSockets
{
    internal class Remote : Core
    {
        private static CancellationTokenSource _cancellationTokenSource;
        private static WebSocket _socket;

        public static void Open()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += delegate
            {
                _cancellationTokenSource.Cancel();
            };

            int bufferSize = 8192;
            int num = 100 * bufferSize;
            WebSocketListenerOptions webSocketListenerOptions = new WebSocketListenerOptions
            {
                SendBufferSize = bufferSize,
                BufferManager = BufferManager.CreateBufferManager(num, bufferSize)
            };

            webSocketListenerOptions.Standards.RegisterRfc6455();
            webSocketListenerOptions.Transports.ConfigureTcp(delegate (TcpTransport tcp)
            {
                tcp.BacklogSize = 100;
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            WebSocketClient webSocketClient = new WebSocketClient(webSocketListenerOptions);

            _socket = webSocketClient.ConnectAsync(new Uri(RemoteWsDaemonUrl), _cancellationTokenSource.Token).Result;

            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] WebSocket Client listening: " + RemoteWsDaemonUrl); // debug
        }

        public static string Send(string data)
        {
            string json = null;

            try
            {
                Console.WriteLine("request: " + data); // debug

                _socket.WriteStringAsync(data, _cancellationTokenSource.Token).Wait(_cancellationTokenSource.Token);

                json = _socket.ReadStringAsync(_cancellationTokenSource.Token).Result;

                Console.WriteLine("response: " + json); // debug

                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
            }
            catch (FormatException)
            {
                _socket.Dispose();

                _socket = null;
            }
            catch (JsonReaderException)
            {
                _socket.Dispose();

                _socket = null;
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
                _socket.Dispose();

                _socket = null;

                Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] Exception, class 'WebSocket': " + e);
            }

            return json;
        }
    }
}
