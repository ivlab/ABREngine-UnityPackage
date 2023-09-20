using System;
using System.Collections;
using System.Collections.Generic;
using IVLab.ABREngine;
using UnityEngine;

public class GeneratePoints : MonoBehaviour
{
    [SerializeField, Tooltip("Number of glyphs to generate")]
    public int numberOfGlyphs = 1000;

    // Unique identifier for the dataset
    public string dataPath = "GeneratedExample/Points/KeyData/Points";

    private SimpleGlyphDataImpression gdi;
    private DataImpressionGroup group;


    // Start is called before the first frame update
    void Start()
    {

        // Generate the actual data points
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < numberOfGlyphs; i++)
        {
            points.Add(UnityEngine.Random.insideUnitSphere * 0.5f);
        }


        // // Load the data into ABR
        RawDataset pointsRds = RawDatasetAdapter.PointsToPoints(points, new Bounds(Vector3.zero, Vector3.one), null, null);
        KeyData pointsKd = ABREngine.Instance.Data.ImportRawDataset(dataPath, pointsRds);

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh m = g.GetComponent<MeshFilter>().sharedMesh;
        GlyphVisAsset gva = new GlyphVisAsset(new List<Mesh>() {m}, null);
        Destroy(g);

        // Load glyph from Resources
        // string glyphUuid = "1af02820-f1ed-11e9-a623-8c85900fd4af";
        // GlyphVisAsset glyph = ABREngine.Instance.VisAssets.GetVisAsset<GlyphVisAsset>(new System.Guid(glyphUuid));

        // Data impression for applying artist style to data
        gdi = DataImpression.Create<SimpleGlyphDataImpression>(Guid.NewGuid().ToString(), "Generated Points");
        gdi.keyData = pointsKd;
        gdi.glyph = gva;

        // Tell ABR about the new data impression
        ABREngine.Instance.RegisterDataImpression(gdi);
        ABREngine.Instance.Render();

        group = ABREngine.Instance.GetGroupFromImpression(gdi);
    }
}
