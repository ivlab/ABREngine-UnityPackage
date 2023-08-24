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

    // Update is called once per frame
    void Update()
    {
        DataImpression impression = ABREngine.Instance.GetDataImpression(keyDataPathToMeasure);
        DataImpressionGroup group = ABREngine.Instance.GetGroupFromImpression(impression);

        if (!measureFromClosestDataPoint)
        {
            Vector3 point1DataSpace = group.WorldSpacePointToDataSpace(measurePoint1.position);
            Vector3 point2DataSpace = group.WorldSpacePointToDataSpace(measurePoint2.position);

            Vector3 diffWorldSpace = measurePoint2.position - measurePoint1.position;
            Vector3 diffDataSpace = point2DataSpace - point1DataSpace;
            Vector3 half = measurePoint1.position + diffWorldSpace * 0.5f;

            DebugDraw.Line(measurePoint2.position, measurePoint1.position, Color.cyan, thickness: 0.01f);
            DebugDraw.Text(half + Vector3.up * 0.1f, "World Space Distance: " + diffWorldSpace.magnitude.ToString("F2"), Color.cyan);
            DebugDraw.Text(half + Vector3.up * 0.2f, "Data Space Distance: " + diffDataSpace.magnitude.ToString("F2"), Color.cyan);
        }
    }
}
