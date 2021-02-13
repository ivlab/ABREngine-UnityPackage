/* StateSubscriber.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using UnityEngine;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class StateSubscriber
    {
        // Buffer size for transferring data to/from server (must match BUF_SIZE in
        // unity_connector.py!)
        private const int BUF_SIZE = 4096;

        /// How long to wait before determining that the client is dead (milliseconds)
        private const int TIMEOUT = 2000;

        private const string SERVER = "127.0.0.1:8000";

        /// Connection to the design server
        private TcpClient _client;

        /// Is the connection thread running?
        private bool _running = false;
        private bool _receiving = false;

        /// Threads to listen for data across the socket
        private Thread _receiverThread;

        class SubscriberInfo
        {
            public string address;
            public int port;
            public string uuid;
        }
        private SubscriberInfo _subscriberInfo;

        public StateSubscriber()
        {
            Task.Run(async () =>
            {
                HttpResponseMessage subMsg = await ABREngine.httpClient.PostAsync("http://" + SERVER + "/api/subscribe", new ByteArrayContent(new byte[0]));
                subMsg.EnsureSuccessStatusCode();
                string msg = await subMsg.Content.ReadAsStringAsync();
                this._subscriberInfo = JsonConvert.DeserializeObject<SubscriberInfo>(msg);
                this._client = new TcpClient(_subscriberInfo.address, _subscriberInfo.port);
                this._running = true;
                this._receiverThread = new Thread(new ThreadStart(this.Receiver));
                this._receiverThread.Start();
            });
        }

        // Tell the Server we've disconnected, then clean up connections and threads
        public void Stop()
        {
            this._receiving = false;
            this._running = false;
            Task.Run(async () =>
            {
                await ABREngine.httpClient.PostAsync("http://" + SERVER + "/api/unsubscribe/" + _subscriberInfo.uuid, new ByteArrayContent(new byte[0]));
            });

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
                CancellationToken ct = new CancellationToken();
                string updateMsg = await StreamMethods.ReadStringFromStreamAsync(this._client.GetStream(), ct);
                // when we get here, we've received a message and can update
                // state!
                ABREngine.Instance.LoadState<HttpStateFileLoader>("http://" + SERVER + "/api/state");
            }
        }
    }
}