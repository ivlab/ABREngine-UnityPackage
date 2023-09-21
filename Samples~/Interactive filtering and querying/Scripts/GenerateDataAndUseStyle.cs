using System;
using System.Collections;
using System.Collections.Generic;
using IVLab.ABREngine;
using UnityEngine;

// Demonstrates an example of generating some data in Unity and using an
// existing Data Impression to style it
public class GenerateDataAndUseStyle : MonoBehaviour
{
    [SerializeField, Tooltip("Number of glyphs to generate")]
    private int numberOfGlyphs = 1000;

    [SerializeField, Tooltip("Number of lines to generate")]
    private int numberOfLines = 10;

    // Unique identifier for the dataset
    [SerializeField, Tooltip("Data Path to use for the generated point data")]
    private string pointsDataPath = "GeneratedExample/Points/KeyData/Points";

    [SerializeField, Tooltip("Data Path to use for the generated line data")]
    private string lineDataPath = "GeneratedExample/Points/KeyData/Lines";

    // you can use a tag to style data impressions...
    [SerializeField, Tooltip("Tag to use when copying the style from an existing data impression")]
    private string pointStyleTag = "PointStyle";

    // or, by name directly
    [SerializeField, Tooltip("Tag to use when copying the style from an existing data impression")]
    private string lineStyleName = "LineStyle";

    private SimpleGlyphDataImpression gdi;
    private SimpleLineDataImpression ldi;

    // Start is called before the first frame update
    void Start()
    {
        // Generate the actual data points
        List<Vector3> points = new List<Vector3>();
        List<float> data = new List<float>();
        for (int i = 0; i < numberOfGlyphs; i++)
        {
            var pt = UnityEngine.Random.insideUnitSphere * 0.5f;
            points.Add(pt);
            data.Add(pt.sqrMagnitude);
        }

        // Load the data into ABR
        const string pointVarName = "sqrMagnitude";
        RawDataset pointsRds = RawDatasetAdapter.PointsToPoints(points, new Bounds(Vector3.zero, Vector3.one), new Dictionary<string, List<float>> {{pointVarName, data}}, null);
        KeyData pointsKd = ABREngine.Instance.Data.ImportRawDataset(pointsDataPath, pointsRds);
        ScalarDataVariable pointsSqrMag = pointsKd.GetScalarVariable(pointVarName);


        // Generate some lines
        List<List<Vector3>> lines = new List<List<Vector3>>();
        List<float> lineData = new List<float>();
        const float xIncrement = 0.01f;
        const float amplitude = 0.1f;
        const float frequency = 20.0f;
        for (int l = 0; l < numberOfLines; l++)
        {
            List<Vector3> line = new List<Vector3>();
            var startPt = UnityEngine.Random.insideUnitSphere * 0.5f;
            var currentPt = startPt;
            while (currentPt.magnitude < 0.5f)
            {
                var x = currentPt.x + xIncrement;
                currentPt = new Vector3(x, startPt.y + amplitude * Mathf.Sin(x * frequency), startPt.z + amplitude * Mathf.Cos(x * frequency));
                line.Add(currentPt);
                lineData.Add(l + l * x);
            }
            lines.Add(line);
        }

        // Load the data into ABR
        const string lineVarName = "lineProgress";
        RawDataset lineRds = RawDatasetAdapter.PointsToLine(lines, new Bounds(Vector3.zero, Vector3.one), new Dictionary<string, List<float>> {{lineVarName, data}});
        KeyData lineKd = ABREngine.Instance.Data.ImportRawDataset(lineDataPath, lineRds);
        ScalarDataVariable lineVar = lineKd.GetScalarVariable(lineVarName);

        // // Load glyph from Resources
        // // string glyphUuid = "1af02820-f1ed-11e9-a623-8c85900fd4af";
        // // GlyphVisAsset glyph = ABREngine.Instance.VisAssets.GetVisAsset<GlyphVisAsset>(new System.Guid(glyphUuid));

        // // Data impression for applying artist style to data
        // gdi = DataImpression.Create<SimpleGlyphDataImpression>(Guid.NewGuid().ToString(), "Generated Points");
        // gdi.glyph = gva;

        // Create a glyph data impression from a defined style
        var gdis = ABREngine.Instance.GetDataImpressionsWithTag(pointStyleTag);
        gdi = DataImpression.Create<SimpleGlyphDataImpression>(Guid.NewGuid().ToString(), "Generated Points");
        if (gdis.Count > 1)
        {
            Debug.LogWarning($"More than one data impression with tag {pointStyleTag}. Using first one.");
        }
        else if (gdis.Count > 0)
        {
            gdi.CopyExisting(gdis[0]);
        }
        else
        {
            Debug.LogError($"Data impression with tag {pointStyleTag} not found. Using Defaults.");
        }

        // Assign key data and variable
        gdi.keyData = pointsKd;
        gdi.colorVariable = pointsSqrMag;

        // Create a line data impression from a defined style
        ldi = DataImpression.Create<SimpleLineDataImpression>(Guid.NewGuid().ToString(), "Generated Lines");
        try
        {
            var ldiStyle = ABREngine.Instance.GetDataImpression(di => di.name == lineStyleName);
            if (ldiStyle == null)
                throw new KeyNotFoundException();
            ldi.CopyExisting(ldiStyle);
        }
        catch
        {
            Debug.LogError($"Data impression with name {lineStyleName} not found. Using defaults.");
        }

        // Assign line key data and variable
        ldi.keyData = lineKd;
        ldi.colorVariable = lineVar;

        // Tell ABR about the new data impressions
        ABREngine.Instance.RegisterDataImpression(gdi);
        ABREngine.Instance.RegisterDataImpression(ldi);
        ABREngine.Instance.Render();
    }
}
