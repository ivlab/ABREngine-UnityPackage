/* SocketDataListener.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson
 * <sethalanjohnson@gmail.com>, Greg Abram <gda@tacc.utexas.edu>
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

using System.Collections.Generic;
using System.Collections.Concurrent;
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

    /// <summary>
    /// Listener for incoming upstream data, for example, from a Send2ABR plugin
    /// for ParaView. Operation of this class is controlled by the ABRConfig
    /// option `dataListenerPort`.
    /// </summary>
    public class SocketDataListener
    {
        public int port;

        [SerializeField]
        public TcpListener listener = null;

        private ConcurrentQueue<string> updatedDataPaths;

        public SocketDataListener(int port)
        {
            this.port = port;
            this.updatedDataPaths = new ConcurrentQueue<string>();
        }

        public void StartServer()
        {
            if (listener != null) return;
            listener = new TcpListener(port: port, localaddr: IPAddress.Any);
            listener.Start();
            listener.BeginAcceptTcpClient(
                new System.AsyncCallback(DoAcceptSocketCallback), listener);
            Debug.Log("Listening for data on port " + port);
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
            catch (System.ObjectDisposedException)
            {
                // When closing the socket we get a "Cannot access a disposed
                // object" error
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
                    {
                        RawDataset.JsonHeader json = JsonUtility.FromJson<RawDataset.JsonHeader>(textData.json);
                        RawDataset.BinaryData b = new RawDataset.BinaryData(json, textData.bindata);
                        RawDataset dataset = new RawDataset(json, b);

                        try
                        {
                            // Unload the outdated version of the dataset
                            RawDataset _dataset;
                            if (ABREngine.Instance.Data.TryGetRawDataset(textData.label, out _dataset))
                            {
                                ABREngine.Instance.Data.UnloadRawDataset(textData.label);
                            }

                            // Load in the new data and save it to disk
                            ABREngine.Instance.Data.ImportRawDataset(textData.label, dataset);
                            ABREngine.Instance.Data.CacheRawDataset(textData.label, textData.json, textData.bindata);
                            updatedDataPaths.Enqueue(textData.label);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e);
                        }
                    }
                }
                else
                {
                    Debug.Log("Waiting for all objects to update...");
                    // "Ack" and "OK" all data
                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "ack", cancelToken);
                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "ok", cancelToken);

                    // After all data received, go ahead and update state...
                    // that essentially means flagging that data has changed for
                    // every data impression that depends on the updated data,
                    // then re-rendering the scene.
                    try
                    {
                        // while (!updatedDataPaths.IsEmpty)
                        // {
                        //     string dataPathUpdated;
                        //     // if (updatedDataPaths.TryDequeue(out dataPathUpdated))
                        //     // {
                        //     //     ABREngine.Instance.GetDataImpression(di => (di.InputIndexer.GetInputField("keyData").GetValue(di) as KeyData).Path == dataPathUpdated).RenderHints.DataChanged = true;
                        //     // }
                        // }
                        ABREngine.Instance.GetAllDataImpressions().ForEach(di => di.RenderHints.DataChanged = true);
                        await UnityThreadScheduler.Instance.RunMainThreadWork(() => ABREngine.Instance.Render());
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }

                    Debug.Log("All objects have been unpacked, Sent label \"" + textData.label + "\" " + " ok");
                }
            });

            listener.BeginAcceptSocket(
               new System.AsyncCallback(DoAcceptSocketCallback), listener);

        }
    }
}