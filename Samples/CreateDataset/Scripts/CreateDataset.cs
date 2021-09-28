/* CreateDataset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
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

using UnityEngine;
using System.Threading.Tasks;
using IVLab.Utilities;

namespace IVLab.ABREngine.Examples
{
    /// <summary>
    /// This example creates a simple cube dataset with one variable, loads it
    /// into ABR, and displays a single data impression with the cube. Attach
    /// this component to a GameObject to load the example dataset. This example
    /// assumes a blank ABR configuration.
    /// </summary>
    public class CreateDataset : MonoBehaviour
    {
        private const string datasetPath = "ABR/Examples";
        private const string keyDataName = "Cube";
        private const string scalarVar = "XAxis";
        private string KeyDataPath
        {
            get
            {
                return DataPath.Join(DataPath.Join(datasetPath, DataPath.DataPathType.KeyData), keyDataName);
            }
        }
        private string ScalarVarPath
        {
            get
            {
                return DataPath.Join(DataPath.Join(datasetPath, DataPath.DataPathType.ScalarVar), scalarVar);
            }
        }

        void Start()
        {
            ABREngine.GetInstance();
            Task.Run(async () =>
            {
                try
                {
                    // Initialize the ABREngine
                    await ABREngine.Instance.WaitUntilInitialized();

                    await CreateCube();
                    Debug.Log("Loaded Cube");

                    await CreateDataImpression();
                    Debug.Log("Registered Data Impression");

                    await UnityThreadScheduler.Instance.RunMainThreadWork(() => {
                        ABREngine.Instance.Render();
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            });
        }

        /// <summary>
        /// Create a data impression to render the cube. See the CreateState.cs
        /// file for a more complete example of creating a state.
        /// </summary>
        private async Task CreateDataImpression()
        {
            Dataset ds = null;
            if (!ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(KeyDataPath), out ds))
            {
                Debug.LogError("Unable to load dataset " + KeyDataPath);
                return;
            }

            IKeyData kd = null;
            if (!ds.TryGetKeyData(KeyDataPath, out kd))
            {
                Debug.LogError("Key data not found in dataset: " + KeyDataPath);
                return;
            }

            ScalarDataVariable sv = null;
            if (!ds.TryGetScalarVar(ScalarVarPath, out sv))
            {
                Debug.LogError("Dataset does not have variable " + ScalarVarPath);
                return;
            }

            await UnityThreadScheduler.Instance.RunMainThreadWork(() => {
                SimpleSurfaceDataImpression di = new SimpleSurfaceDataImpression();
                di.keyData = kd as SurfaceKeyData;
                di.colorVariable = sv;
                di.colormap = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;

                ABREngine.Instance.RegisterDataImpression(di);
            });
        }

        /// <summary>
        /// Create a 2x2x2 cube
        /// </summary>
        private async Task CreateCube()
        {
            RawDataset ds = new RawDataset();

            ds.vectorArrays = new SerializableVectorArray[0];
            ds.vectorArrayNames = new string[0];
            ds.meshTopology = MeshTopology.Triangles;
            ds.bounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);

            ds.scalarArrayNames = new string[] { scalarVar };
            ds.scalarMins = new float[] { -2.0f };
            ds.scalarMaxes = new float[] { 2.0f };
            ds.scalarArrays = new SerializableFloatArray[1];
            ds.scalarArrays[0] = new SerializableFloatArray();
            ds.scalarArrays[0].array = new float[] {
                // Bottom verts
                -2, 2, -2, 2,

                // Top verts
                -2, 2, -2, 2,
            };

            // Construct the vertices
            Vector3[] vertices = {
                // Bottom verts
                new Vector3(-1, -1, -1), // 0
                new Vector3( 1, -1, -1), // 1
                new Vector3(-1, -1,  1), // 2
                new Vector3( 1, -1,  1), // 3

                // Top verts
                new Vector3(-1, 1, -1), // 4
                new Vector3( 1, 1, -1), // 5
                new Vector3(-1, 1,  1), // 6
                new Vector3( 1, 1,  1), // 7
            };
            ds.vertexArray = vertices;

            // Construct triangle indices/faces - LEFT HAND RULE, outward-facing normals
            int[] indices = {
                // Bottom face
                0, 1, 3,
                0, 3, 2,

                // Top face
                4, 7, 5,
                4, 6, 7,

                // Front face
                0, 5, 1,
                0, 4, 5,

                // Back face
                2, 7, 6,
                2, 3, 7,

                // Left face
                0, 6, 4,
                0, 2, 6,

                // Right face
                3, 5, 7,
                3, 1, 5
            };
            ds.indexArray = indices;

            // How many verts per cell are there? (each triangle is a cell)
            int[] cellIndexCounts = {
                3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
            };
            ds.cellIndexCounts = cellIndexCounts;

            // Where does each cell begin?
            int[] cellIndexOffsets = {
                0, 3, 6, 9, 12, 15, 18, 21, 24, 27, 30
            };
            ds.cellIndexOffsets = cellIndexOffsets;

            Debug.Log("Loading raw dataset " + KeyDataPath);

            try
            {
                await ABREngine.Instance.Data.ImportRawDataset(KeyDataPath, ds);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error loading dataset:");
                Debug.LogError(e);
            }
        }
    }
}