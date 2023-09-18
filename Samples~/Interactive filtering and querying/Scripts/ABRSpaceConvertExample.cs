using System.Collections;
using System.Collections.Generic;
using IVLab.ABREngine;
using UnityEngine;

/// <summary>
/// Measure the distance between two points
/// </summary>
public class ABRSpaceConvertExample : MonoBehaviour
{
    [Header("Assign these objects and move them around to measure points in space.")]
    public Transform measurePoint1;
    public Transform measurePoint2;

    [Header("Which key data object to measure data points in")]
    public string keyDataPathToMeasure;

    [Header("Constrain the endpoint measurements to the data")]
    bool measureFromClosestDataPoint = false;

    void Start()
    {
        Debug.Log(System.IO.Path.GetFullPath("Packages/edu.umn.cs.ivlab/DocumentationSrc~/docfx.json"));
    }

    // Update is called once per frame
    void Update()
    {
        DataImpression impression = ABREngine.Instance.GetDataImpression(keyDataPathToMeasure);
        DataImpressionGroup group = ABREngine.Instance.GetGroupFromImpression(impression);

        if (!measureFromClosestDataPoint)
        {
            Vector3 point1DataSpace = group.WorldSpacePointToDataSpace(measurePoint1.position);
            Vector3 point2DataSpace = group.WorldSpacePointToDataSpace(measurePoint2.position);
            Vector3 point1WorldSpace = group.DataSpacePointToWorldSpace(point1DataSpace);
            Vector3 point2WorldSpace = group.DataSpacePointToWorldSpace(point2DataSpace);


            Vector3 diffWorldSpace = measurePoint2.position - measurePoint1.position;
            Vector3 diffDataSpace = point2DataSpace - point1DataSpace;
            Vector3 half = measurePoint1.position + diffWorldSpace * 0.5f;

            DebugDraw.Line(measurePoint2.position, measurePoint1.position, Color.cyan, thickness: 0.01f);
            DebugDraw.Text(half + Vector3.up * 0.1f, "World Space Distance: " + diffWorldSpace.magnitude.ToString("F3"), Color.cyan);
            DebugDraw.Text(half + Vector3.up * 0.2f, "Data Space Distance: " + diffDataSpace.magnitude.ToString("F3"), Color.cyan);

            DebugDraw.Text(measurePoint1.position + Vector3.up * 0.3f, "Point 1 World Space from data space: " + point1WorldSpace.ToString("F3"), Color.cyan);
            DebugDraw.Text(measurePoint1.position + Vector3.up * 0.2f, "Point 1 Data Space: " + point1DataSpace.ToString("F3"), Color.cyan);
            DebugDraw.Text(measurePoint1.position + Vector3.up * 0.1f, "Point 1 World Space: " + measurePoint1.position.ToString("F3"), Color.cyan);

            DebugDraw.Text(measurePoint2.position + Vector3.up * 0.3f, "Point 2 World Space from data space: " + point2WorldSpace.ToString("F3"), Color.cyan);
            DebugDraw.Text(measurePoint2.position + Vector3.up * 0.2f, "Point 2 Data Space: " + point2DataSpace.ToString("F3"), Color.cyan);
            DebugDraw.Text(measurePoint2.position + Vector3.up * 0.1f, "Point 2 World Space: " + measurePoint2.position.ToString("F3"), Color.cyan);

            DebugDraw.Text(group.BoundsInWorldSpace.center + group.BoundsInWorldSpace.extents + Vector3.up * 0.1f, "World Space Bounds", Color.green);
            DebugDraw.Bounds(group.BoundsInWorldSpace, Color.green, thickness: 0.01f);

            Bounds groupContainer;
            if (group.TryGetContainerBoundsInWorldSpace(out groupContainer))
            {
                DebugDraw.Text(groupContainer.center + groupContainer.extents + Vector3.up * 0.1f, "Data Container " + group.name, Color.blue);
                DebugDraw.Bounds(groupContainer, Color.blue, thickness: 0.01f);
            }
        }

        float moveAmt = 0.5f;
        Vector3 moveVector1 = Vector3.zero;
        Vector3 moveVector2 = Vector3.zero;
        if (Input.GetKeyDown(KeyCode.W))
            moveVector1 += moveAmt * Vector3.forward;
        if (Input.GetKeyDown(KeyCode.A))
            moveVector1 += moveAmt * Vector3.left;
        if (Input.GetKeyDown(KeyCode.S))
            moveVector1 += moveAmt * Vector3.back;
        if (Input.GetKeyDown(KeyCode.D))
            moveVector1 += moveAmt * Vector3.right;
        if (Input.GetKeyDown(KeyCode.Q))
            moveVector1 += moveAmt * Vector3.down;
        if (Input.GetKeyDown(KeyCode.E))
            moveVector1 += moveAmt * Vector3.up;
        if (Input.GetKeyDown(KeyCode.I))
            moveVector2 += moveAmt * Vector3.forward;
        if (Input.GetKeyDown(KeyCode.J))
            moveVector2 += moveAmt * Vector3.left;
        if (Input.GetKeyDown(KeyCode.K))
            moveVector2 += moveAmt * Vector3.back;
        if (Input.GetKeyDown(KeyCode.L))
            moveVector2 += moveAmt * Vector3.right;
        if (Input.GetKeyDown(KeyCode.U))
            moveVector2 += moveAmt * Vector3.down;
        if (Input.GetKeyDown(KeyCode.O))
            moveVector2 += moveAmt * Vector3.up;

        measurePoint1.transform.position += moveVector1;
        measurePoint2.transform.position += moveVector2;
    }
}
