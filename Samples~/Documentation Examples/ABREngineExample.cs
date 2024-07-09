using UnityEngine;
using IVLab.ABREngine;

// Look in the Resources folder of this example to see the data and VisAssets.
// Before running this example, make sure the ABR Configuration is set to ABRExamplesConfig!
public class ABREngineExample : MonoBehaviour
{
    // Editable in Unity Editor
    [SerializeField, Tooltip("Animated spin rate for the data"), Range(0.0f, 30.0f)]
    private float spinRate = 1.0f;

    // Store the data impression for use in Update()
    private SimpleGlyphDataImpression dataImpression;

    void Start()
    {
        // Load example VisAssets from resources
        // A white to orange colormap
        ColormapVisAsset orange = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(new System.Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8"));

        // A triangular glyph
        GlyphVisAsset triangular = ABREngine.Instance.VisAssets.GetVisAsset<GlyphVisAsset>(new System.Guid("1af02820-f1ed-11e9-a623-8c85900fd4af"));



        // Load an example dataset from resources
        // This Key Data has multiple scalar variables defined at each point in
        // the dataset. We'll use the "XAxis" variable, this increases
        // monotonically from -10 to +10 along the X axis.
        KeyData pointsKd = ABREngine.Instance.Data.LoadData("Demo/Wavelet/KeyData/Points");
        ScalarDataVariable xAxisScalarVar = pointsKd.GetScalarVariable("XAxis");


        // Create a Glyph DataImpression and store it for later use
        dataImpression = DataImpression.Create<SimpleGlyphDataImpression>("Example Points");

        // Assign the key data to the Data Impression. This is the "Geometry"
        // with which the data impression will be drawn.
        dataImpression.keyData = pointsKd;

        // Tell ABR to map the Color visual channel of the Data Impression to
        // the "XAxis" variable.
        dataImpression.colorVariable = xAxisScalarVar;

        // Lastly, apply the colormap and glyph (visual style for this Data
        // Impresssion)
        dataImpression.colormap = orange;
        dataImpression.glyph = triangular;


        // Finally, add the Data Impression to the ABREngine, and re-render.
        // Note, this will automatically look for the DataImpression Group
        // "Demo/Wavelet" in the scene, and put this data impression as a child
        // of that group.
        ABREngine.Instance.RegisterDataImpression(dataImpression);
        ABREngine.Instance.Render();
    }

    void Update()
    {
        // Access the individual data impression that was created earlier and
        // spin it around.
        //
        // Usually we want to do any manipulations like this at the "Group"
        // level rather than individual data impressions so that multiple data
        // impressions in the same dataset remain spatially registered with each
        // other.
        DataImpressionGroup pointsGroup = ABREngine.Instance.GetGroupFromImpression(dataImpression);
        pointsGroup.transform.rotation *= Quaternion.Euler(new Vector3(0, Time.deltaTime * spinRate, 0));
    }
}
