using System.Collections.Generic;
using UnityEngine;
using IVLab.ABREngine;

/// <summary>
/// Example for the main ABREngine class.
/// </summary>
public class CustomDataABRExample : MonoBehaviour
{
    void Start()
    {
        // STEP 1: Define data
        // 9 points in 3D space
        List<Vector3> vertices = new List<Vector3>
            {
                new Vector3(0.0f, 0.5f, 0.0f),
                new Vector3(0.0f, 0.6f, 0.1f),
                new Vector3(0.0f, 0.4f, 0.2f),
                new Vector3(0.1f, 0.3f, 0.0f),
                new Vector3(0.1f, 0.2f, 0.1f),
                new Vector3(0.1f, 0.3f, 0.2f),
                new Vector3(0.2f, 0.0f, 0.0f),
                new Vector3(0.2f, 0.3f, 0.1f),
                new Vector3(0.2f, 0.1f, 0.2f),
            };

        // Data values for those points
        List<float> data = new List<float>();
        for (int i = 0; i < vertices.Count; i++) data.Add(i);

        // Named scalar variable
        Dictionary<string, List<float>> scalarVars = new Dictionary<string, List<float>> { { "someData", data } };

        // Define some generous bounds
        Bounds b = new Bounds(Vector3.zero, Vector3.one);

        // STEP 2: Convert the point list into ABR Format
        RawDataset abrPoints = RawDatasetAdapter.PointsToPoints(vertices, b, scalarVars, null);

        // STEP 3: Import the point data into ABR so we can use it
        KeyData pointsKD = ABREngine.Instance.Data.ImportRawDataset(abrPoints);

        // STEP 4: Import a colormap visasset
        ColormapVisAsset cmap = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(new System.Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8"));

        // STEP 5: Create a Data Impression (layer) for the points, and assign some key data and styling
        SimpleGlyphDataImpression di = DataImpression.Create<SimpleGlyphDataImpression>("Simple Points");
        di.keyData = pointsKD;                                 // Assign key data (point geometry)
        di.colorVariable = pointsKD.GetScalarVariables()[0];   // Assign scalar variable "someData"
        di.colormap = cmap;                                    // Apply colormap
        di.glyphSize = 0.002f;                                 // Apply glyph size styling

        // STEP 6: Register impression with the engine
        ABREngine.Instance.RegisterDataImpression(di);

        // STEP 7: Render the visualization
        ABREngine.Instance.Render();
    }
}