/* Notifier.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using IVLab.Utilities;

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
        private bool _sending = false;

        /// Threads to listen for data across the WebSocket
        private Thread _receiverThread;
        private Thread _senderThread;

        /// Queue of messages (bytes) to send to websocket
        private ConcurrentQueue<byte[]> _outgoingQueue = new ConcurrentQueue<byte[]>();

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

        public void Init()
        {
            cts = new CancellationTokenSource();
            try
            {
                Debug.LogFormat("Trying to connect to state subscriber notifier WebSocket on {0}", this._subscriberWebSocket);
                this._client = new ClientWebSocket();

                // Inspiration from:
                // https://csharp.hotexamples.com/examples/-/ClientWebSocket/-/php-clientwebsocket-class-examples.html#0x5cb1281703a205e0b8dd236b8e8798505c77e14c91383f620be403f868cae48b-43,,96,
                this._client.ConnectAsync(this._subscriberWebSocket, cts.Token);

                // Wait for a max of ~5s to see if the client can connect
                int tries = 0;
                while (this._client.State != WebSocketState.Open && tries < 50)
                {
                    tries++;
                    Thread.Sleep(100);
                }

                if (this._client.State == WebSocketState.Open)
                {
                    this._running = true;
                    Debug.Log("State subscriber notifier WebSocket listening");
                    this._receiverThread = new Thread(new ThreadStart(this.Receiver));
                    this._receiverThread.Start();
                    this._senderThread = new Thread(new ThreadStart(this.Sender));
                    this._senderThread.Start();
                }
                else
                {
                    throw new Exception($"Failed to connect to state subscriber notifier after {tries} tries: " + this._client.State);
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
            this._sending = false;
            this._running = false;
            this._receiverThread?.Join();
            this._senderThread?.Join();
            this._client.Dispose();
            Debug.Log("Disconnected state subscriber notifier");
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
                var rcvBytes = new byte[256];
                var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                WebSocketReceiveResult rcvResult = await this._client.ReceiveAsync(rcvBuffer, cts.Token);
                byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                string rcvMsg = Encoding.UTF8.GetString(msgBytes);
                NotifierTarget target = JsonConvert.DeserializeObject<NotifierTarget>(rcvMsg);
                if (target.target == "state")
                {
                    // Load the state (make sure this happens in the main thread),
                    await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
                    {
                        ABREngine.Instance.LoadState<HttpStateFileLoader>(_serverAddress + ABREngine.Instance.Config.Info.statePathOnServer);
                    });

                    // ... then immediately send back a thumbnail of what we just rendered
                    // 0. Try and get the Screenshot component
                    byte[] thumbnail = null;
                    Screenshot scr = null;
                    await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
                    {
                        if (Camera.main.TryGetComponent<Screenshot>(out scr))
                        {
                            // 1. Capture the screen bytes in LateUpdate
                            thumbnail = scr.CaptureView(128, 128, false, -1);
                        }
                    });

                    if (thumbnail != null)
                    {
                        // 2. Serialize it
                        string b64ThumbStr = System.Convert.ToBase64String(thumbnail);

                        JObject output = new JObject();
                        output.Add("target", "thumbnail");
                        output.Add("content", b64ThumbStr);

                        byte[] finalOut = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(output));

                        // 3. Send to server
                        this._outgoingQueue.Enqueue(finalOut);
                    }
                }
            }
        }

        async void Sender()
        {
            this._sending = true;
            while (this._sending && this._running)
            {
                while (!this._outgoingQueue.IsEmpty) {
                    byte[] outgoingMessage = null;
                    this._outgoingQueue.TryDequeue(out outgoingMessage);
                    if (outgoingMessage != null)
                    {
                        await this._client.SendAsync(new ArraySegment<byte>(outgoingMessage), WebSocketMessageType.Text, true, cts.Token);
                    }
                }
            }
        }
    }
}