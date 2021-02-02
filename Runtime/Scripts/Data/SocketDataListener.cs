/* SocketDataListener.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson
 * <sethalanjohnson@gmail.com>, Greg Abram <gda@tacc.utexas.edu>
 *
 */

using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public struct SocketTextData
    {
        public string label;
        public string json;
        public string data;
        public byte[] bindata;
    }

    [RequireComponent(typeof(DataManager))]
    public class SocketDataListener : Singleton<SocketDataListener>
    {
        public int port = 1900;

        [SerializeField]
        public TcpListener listener = null;

        public void StartServer()
        {
            if (listener != null) return;
            listener = new TcpListener(port: port, localaddr: IPAddress.Any);
            listener.Start();
            listener.BeginAcceptTcpClient(
                new System.AsyncCallback(DoAcceptSocketCallback), listener);
        }

        public void StopServer()
        {
            listener.Stop();
            listener = null;
        }

        static async Task<string> GetSocketTextAsync(TcpClient client, CancellationToken cancelToken)
        {
            string text = await StreamMethods.ReadStringFromStreamAsync(client.GetStream(), cancelToken);
            return text;
        }

        static async Task<byte[]> GetSocketDataAsync(TcpClient client, CancellationToken cancelToken)
        {
            int bytesInData = await StreamMethods.ReadIntFromStreamAsync(client.GetStream(), cancelToken);

            byte[] buffer = new byte[bytesInData];

            int offset = 0;
            while (offset < bytesInData)
            {
                int bytesRead = await client.GetStream().ReadAsync(buffer, offset, bytesInData - offset);
                offset = offset + bytesRead;
            }

            return buffer;
        }

        static async Task<SocketTextData> GetSocketLabelAsync(TcpClient client, CancellationToken cancelToken)
        {
            SocketTextData ptd = new SocketTextData
            {
                label = "",
                data = "",
            };

            ptd.label = await StreamMethods.ReadStringFromStreamAsync(client.GetStream(), cancelToken);

            return ptd;
        }

        static async Task<SocketTextData> GetSocketTextDataAsync(TcpClient client, CancellationToken cancelToken)
        {
            SocketTextData ptd = await GetSocketLabelAsync(client, cancelToken);

            if (ptd.label != "update")
            {
                ptd.json = await GetSocketTextAsync(client, cancelToken);
                ptd.bindata = await GetSocketDataAsync(client, cancelToken);
            }

            return ptd;
        }

        public void DoAcceptSocketCallback(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            // End the operation and display the received data on the
            //console.
            // Socket clientSocket = null;
            TcpClient client = null;
            try
            {
                client = listener.EndAcceptTcpClient(ar);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return;
            }
            Debug.Log("Handling a connection");
            Task.Run(async () =>
            {
                CancellationToken cancelToken = new CancellationToken();
                var textDataTask = GetSocketTextDataAsync(client, cancelToken);

                SocketTextData textData = await textDataTask;
                Debug.Log("Accepting data stream for label \"" + textData.label + "\"");
                if (textData.label != "update")
                {
                    // We don't need to wait to send "ok" because this isn't an update.
                    // We do, however, need to first log that there's a dataset to handle
                    // to make the update wait for it.

                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "ok", cancelToken);
                    Debug.Log("Sent label \"" + textData.label + "\" " + " ok");

                    if (textData.label != "")
                        await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
                        {
                            Dataset.JsonHeader json = JsonUtility.FromJson<Dataset.JsonHeader>(textData.json);
                            Dataset.BinaryData b = new Dataset.BinaryData(json, textData.bindata);
                            Dataset dataset = new Dataset(json, b);

                            DataManager.Instance.ImportDataset(textData.label, ref dataset);
                            DataManager.Instance.CacheData(textData.label, textData.json, textData.bindata);
                        });
                }
                else
                {
                    Debug.Log("Waiting for all objects to update...");
                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "up", cancelToken);
                    Debug.Log("All objects have been unpacked, Sent label \"" + textData.label + "\" " + " ok");
                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "ok", cancelToken);
                    Debug.Log("Sent update ok");
                }
            });

            listener.BeginAcceptSocket(
               new System.AsyncCallback(DoAcceptSocketCallback), listener);

        }


        // Start is called before the first frame update
        void Start()
        {
            StartServer();
            UnityThreadScheduler.GetInstance();
        }

        private void OnDestroy()
        {
            StopServer();
        }
    }
}