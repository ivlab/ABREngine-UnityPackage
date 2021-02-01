using System.Collections;
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

    [System.Serializable]
    public class BinaryDataHeader
    {
        [SerializeField]
        public MeshTopology meshTopology;

        [SerializeField]
        public int num_points;

        [SerializeField]
        public int num_cells;

        [SerializeField]
        public int num_cell_indices;

        [SerializeField]
        public string[] scalarArrayNames;

        [SerializeField]
        public string[] vectorArrayNames;

        [SerializeField]
        public Bounds bounds;

        [SerializeField]
        public float[] scalarMaxes;

        [SerializeField]
        public float[] scalarMins;
    }

    public class BinaryData
    {
        public float[] vertices;
        public int[] index_array;
        public float[][] scalar_arrays;
        public float[][] vector_arrays;
    }

    public class BoolReference
    {
        public BoolReference(bool b)
        {
            this.state = b;
        }
        public static implicit operator BoolReference(bool b) { return new BoolReference(b); }

        public static implicit operator bool(BoolReference b)
        {
            return b is BoolReference && b.state;
        }

        public bool state = false;
    }



    public class SocketDataListener : Singleton<SocketDataListener>
    {
        public int port = 1900;

        [SerializeField]
        public TcpListener listener = null;

        // private DataManager _dataManager;

        // private DataManager dataManager
        // {
        //     get
        //     {
        //         if (_dataManager == null) _dataManager = GetComponent<DataManager>();
        //         return _dataManager;
        //     }
        // }

        Queue<Action> updateActions = new Queue<Action>();

        List<Task> handlingTasks = new List<Task>();

        Queue<string> logLines = new Queue<string>();

        void Log(string message)
        {
            lock (logLock)
            {
                logLines.Enqueue("[" + System.DateTime.UtcNow.ToString("HH:mm:ss:ff") + "]" + message);
            }
        }

        [TextArea(25, 25)]
        public string log;

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

        static void DecodeBinary(ref BinaryDataHeader bdh, ref BinaryData bd, SocketTextData ptd)
        {
            bdh = JsonUtility.FromJson<BinaryDataHeader>(ptd.json);
            bd = new BinaryData { };

            int offset = 0;

            bd.vertices = new float[3 * bdh.num_points];
            int nbytes = 3 * bdh.num_points * sizeof(float);
            Buffer.BlockCopy(ptd.bindata, offset, bd.vertices, 0, nbytes);
            offset = offset + nbytes;

            bd.index_array = new int[bdh.num_cell_indices];
            nbytes = bdh.num_cell_indices * sizeof(int);
            Buffer.BlockCopy(ptd.bindata, offset, bd.index_array, 0, nbytes);
            offset = offset + nbytes;

            bd.scalar_arrays = new float[bdh.scalarArrayNames.Length][];
            nbytes = bdh.num_points * sizeof(float);
            for (int i = 0; i < bdh.scalarArrayNames.Length; i++)
            {
                bd.scalar_arrays[i] = new float[bdh.num_points];
                Buffer.BlockCopy(ptd.bindata, offset, bd.scalar_arrays[i], 0, nbytes);
                offset = offset + nbytes;
            }

            bd.vector_arrays = new float[bdh.vectorArrayNames.Length][];
            nbytes = 3 * bdh.num_points * sizeof(float);
            for (int i = 0; i < bdh.vectorArrayNames.Length; i++)
            {
                bd.vector_arrays[i] = new float[3 * bdh.num_points];
                Buffer.BlockCopy(ptd.bindata, offset, bd.vector_arrays[i], 0, nbytes);
                offset = offset + nbytes;
            }
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

                    BoolReference handlingDone = new BoolReference(false);
                    // handlingTasks.Add(UntilTrue(() => handlingDone == true));

                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "ok", cancelToken);
                    Debug.Log("Sent label \"" + textData.label + "\" " + " ok");

                    if (textData.label != "")
                        updateActions.Enqueue(() =>
                        {
                            // dataManager.CacheData(textData.label, textData.json, textData.bindata);

                            // Dataset.JsonHeader json = JsonUtility.FromJson<Dataset.JsonHeader>(textData.json);
                            // Dataset.BinaryData b = new Dataset.BinaryData(json, textData.bindata);
                            // Dataset dataset = new Dataset(json, b);
                            // dataManager.HandleDataset(textData.label, dataset, true, handlingDone);
                        });
                }
                else
                {
                    Debug.Log("Waiting for all objects to update...");
                    // await Task.WhenAll(handlingTasks);
                    // handlingTasks.Clear();
                    // await SendStringToSocketAsync(client.Client, "up");
                    await StreamMethods.WriteStringToStreamAsync(client.GetStream(), "up", cancelToken);
                    Debug.Log("All objects have been unpacked, Sent label \"" + textData.label + "\" " + " ok");
                    // dataManager.ReleaseHold();
                    // await SendStringToSocketAsync(client.Client, "ok");
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
        }

        private void OnDestroy()
        {
            StopServer();
        }

        object logLock = new object();
        // Update is called once per frame
        void Update()
        {
            while (updateActions.Count > 0)
            {
                updateActions.Dequeue().Invoke();
            }



            while (logLines.Count > 25)
            {
                logLines.Dequeue();
            }

            log = "";
            lock (logLock)
            {
                foreach (var line in logLines)
                {
                    log += line + "\n";
                }
            }

        }
    }
}