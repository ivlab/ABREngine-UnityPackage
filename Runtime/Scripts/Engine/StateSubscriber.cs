/* StateSubscriber.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;
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

        /// Connection to the design server
        private TcpClient _client;

        /// Is the connection thread running?
        private bool _running = false;
        private bool _receiving = false;

        /// Threads to listen for data across the socket
        private Thread _receiverThread;

        public class SubscriberInfo
        {
            public string address;
            public int port;
            public string uuid;
            public string localDataPath;
        }
        public SubscriberInfo subscriberInfo;
        public bool serverIsLocal = false;

        private string _serverAddress;

        public StateSubscriber(string serverAddress)
        {
            _serverAddress = serverAddress;
        }

        public async Task Init()
        {
            // TODO: Add authentication
            HttpResponseMessage subMsg = await ABREngine.httpClient.PostAsync(_serverAddress + "/api/subscribe", new ByteArrayContent(new byte[0]));
            subMsg.EnsureSuccessStatusCode();
            string msg = await subMsg.Content.ReadAsStringAsync();
            this.subscriberInfo = JsonConvert.DeserializeObject<SubscriberInfo>(msg);

            // Check to see if we're running on the same machine as the
            // server.
            bool sameMachine = System.IO.Directory.Exists(subscriberInfo.localDataPath);
            if (sameMachine && subscriberInfo.localDataPath != null)
            {
                serverIsLocal = true;
                Debug.Log("Connected to local state server " + subscriberInfo.address);
            }
            else
            {
                Debug.Log("Connected to remote state server " + subscriberInfo.address);
            }

            this._client = new TcpClient(subscriberInfo.address, subscriberInfo.port);
            this._running = true;
            this._receiverThread = new Thread(new ThreadStart(this.Receiver));
            this._receiverThread.Start();
        }

        // Tell the Server we've disconnected, then clean up connections and threads
        public void Stop()
        {
            this._receiving = false;
            this._running = false;
            Task.Run(async () =>
            {
                await ABREngine.httpClient.PostAsync(_serverAddress + "/api/unsubscribe/" + subscriberInfo.uuid, new ByteArrayContent(new byte[0]));
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
                ABREngine.Instance.LoadState<HttpStateFileLoader>(_serverAddress + ABREngine.Instance.Config.Info.statePathOnServer);
            }
        }
    }
}