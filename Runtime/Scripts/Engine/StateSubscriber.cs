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
        }
        public SubscriberInfo subscriberInfo;
        private string _serverAddress;
        class NotifierTarget
        {
            public string target;
        }

        public StateSubscriber(string serverAddress)
        {
            _serverAddress = serverAddress;
        }

        public async Task Init()
        {
            string serverIP = new Uri(ABREngine.Instance.Config.Info.serverAddress).Host;
            this.subscriberInfo = new SubscriberInfo {
                address = serverIP,
                port = ABREngine.Instance.Config.Info.stateSubscriberPort.Value
            };

            Debug.Log("Connected to remote state server " + subscriberInfo.address);

            try
            {
                Debug.LogFormat("Trying to connect to state subscriber notifier socket on {0}:{1}", subscriberInfo.address, subscriberInfo.port);
                this._client = new TcpClient(subscriberInfo.address, subscriberInfo.port);
                this._running = true;
                Debug.Log("State subscriber notifier socket listening on port " + subscriberInfo.port);
                this._receiverThread = new Thread(new ThreadStart(this.Receiver));
                this._receiverThread.Start();
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
                CancellationToken ct = new CancellationToken();
                string updateMsg = await StreamMethods.ReadStringFromStreamAsync(this._client.GetStream(), ct);
                // when we get here, we've received a message and can update
                // state (if not paused)!
                NotifierTarget target = JsonConvert.DeserializeObject<NotifierTarget>(updateMsg);
                if (target.target == "state")
                {
                    await ABREngine.Instance.LoadStateAsync<HttpStateFileLoader>(_serverAddress + ABREngine.Instance.Config.Info.statePathOnServer);
                }
            }
        }
    }
}