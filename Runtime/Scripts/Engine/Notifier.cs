/* Notifier.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Notifier for ABR state / cache updates, based on WebSockets.
    /// </summary>
    public class Notifier
    {
        /// Buffer size for transferring data to/from WebSocket server
        private const int BUF_SIZE = 4096;

        /// Connection to the design server
        private ClientWebSocket _client;

        /// Is the connection thread running?
        private bool _running = false;
        private bool _receiving = false;

        /// Threads to listen for data across the socket
        private Thread _receiverThread;

        /// Cancellation token for async operations
        CancellationTokenSource cts;

        /// Addresses to connect to (main server and WS)
        private Uri _serverAddress;
        private Uri _subscriberWebSocket;

        class NotifierTarget
        {
            public string target;
        }

        public Notifier(Uri serverAddress)
        {
            _serverAddress = serverAddress;
            // The trailing slash is Very Important!
            _subscriberWebSocket = new Uri("ws://" + this._serverAddress.Authority + "/ws/");
        }

        public async Task Init()
        {
            cts = new CancellationTokenSource();
            try
            {
                Debug.LogFormat("Trying to connect to state subscriber notifier WebSocket on {0}", this._subscriberWebSocket);
                this._client = new ClientWebSocket();

                // Inspiration from:
                // https://csharp.hotexamples.com/examples/-/ClientWebSocket/-/php-clientwebsocket-class-examples.html#0x5cb1281703a205e0b8dd236b8e8798505c77e14c91383f620be403f868cae48b-43,,96,
                await this._client.ConnectAsync(this._subscriberWebSocket, cts.Token);

                this._running = true;
                if (this._client.State == WebSocketState.Open)
                {
                    Debug.Log("State subscriber notifier WebSocket listening");
                    this._receiverThread = new Thread(new ThreadStart(this.Receiver));
                    this._receiverThread.Start();
                }
                else
                {
                    throw new Exception("Failed to connect to state subscriber notifier: " + this._client.State);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        // Tell the Server we've disconnected, then clean up connections and threads
        public void Stop()
        {
            this._receiving = false;
            this._running = false;
            this._receiverThread?.Join();
        }

        public void ForceDisconnect()
        {
            this._running = false;
        }

        async void Receiver()
        {
            this._receiving = true;
            while (this._receiving && this._running)
            {
                var rcvBytes = new byte[128];
                var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                while (true)
                {
                    WebSocketReceiveResult rcvResult = await this._client.ReceiveAsync(rcvBuffer, cts.Token);
                    byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                    string rcvMsg = Encoding.UTF8.GetString(msgBytes);
                    NotifierTarget target = JsonConvert.DeserializeObject<NotifierTarget>(rcvMsg);
                    if (target.target == "state")
                    {
                        await ABREngine.Instance.LoadStateAsync<HttpStateFileLoader>(_serverAddress + ABREngine.Instance.Config.Info.statePathOnServer);
                    }
                }
            }
        }
    }
}