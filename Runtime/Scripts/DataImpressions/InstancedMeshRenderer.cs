/* KeyData.cs
 *
 * Copyright (c) 2020 University of Minnesota
 * Authors: Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class InstancedMeshRenderer : MonoBehaviour
{
    public Matrix4x4[] instanceLocalTransforms;
    public Vector4[] colors;
    public int instanceCount = 100000;
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int subMeshIndex = 0;
    public Bounds bounds;

    public int cachedInstanceCount = -1;
    private int cachedSubMeshIndex = -1;
    private ComputeBuffer colorBuffer;
    private ComputeBuffer transformBuffer;
    private ComputeBuffer transformBufferInverse;


    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    bool invalid = true;

    public MaterialPropertyBlock block;

    public bool useInstanced = true;

    void OnEnable()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        UpdateBuffers();
        
        //block = new MaterialPropertyBlock();

    }

    // TODO Need to fix this sometime. Late Update causes glyphs to not appear on Screenshot camera, while
    // Update can result in one-frame delays in getting object transform. 
    // I actually fixed this by adjusting the Script Execution Order in the Project Settings. 
    //void LateUpdate()
    void Update()
    {
        if (argsBuffer == null) cachedInstanceCount = -1;
        // Update starting position buffer
        if (cachedInstanceCount != instanceCount || cachedSubMeshIndex != subMeshIndex)
            UpdateBuffers();
        if (invalid) return;
        //// Pad input
        //if (Input.GetAxisRaw("Horizontal") != 0.0f)
        //    instanceCount = (int)Mathf.Clamp(instanceCount + Input.GetAxis("Horizontal") * 40000, 1.0f, 5000000.0f);
        block?.SetMatrix("_ObjectTransform", GetComponent<MeshRenderer>().localToWorldMatrix);
        block?.SetMatrix("_ObjectTransformInverse", GetComponent<MeshRenderer>().worldToLocalMatrix);
        Bounds transformedBounds = new Bounds();

        transformedBounds.center = GetComponent<MeshRenderer>().worldToLocalMatrix * bounds.center;
        transformedBounds.center = Vector3.zero;
        transformedBounds.size = GetComponent<MeshRenderer>().worldToLocalMatrix * (bounds.size.magnitude*Vector3.one*1.4f);
        transformedBounds.size = Vector3.one * 100;
        //if (strategy != null)
        //    strategy.SetMaterialBlock(block);
        // Render
        if (useInstanced)
        {
            Graphics.DrawMeshInstancedIndirect(instanceMesh, subMeshIndex, instanceMaterial, transformedBounds, argsBuffer, 0, block, ShadowCastingMode.On, true,gameObject.layer);
        }
        else
        {
            for(int i = 0; i < instanceLocalTransforms.Length; i++)
            {
                block.SetColor("_Color", colors[i]);
                Graphics.DrawMesh(instanceMesh, transform.localToWorldMatrix* instanceLocalTransforms[i], instanceMaterial, 0, null, 0, block);
            }
        }
    }

    //void OnGUI()
    //{
    //    GUI.Label(new Rect(265, 25, 200, 30), "Instance Count: " + instanceCount.ToString());
    //    instanceCount = (int)GUI.HorizontalSlider(new Rect(25, 20, 200, 30), (float)instanceCount, 1.0f, 5000000.0f);
    //}

    void UpdateBuffers()
    {
        invalid = true;
        if (instanceLocalTransforms == null || instanceLocalTransforms.Length == 0 || block == null) return;
        invalid = false;
        instanceCount = instanceLocalTransforms.Length;
        //Debug.Log("UpdatingBuffers");
        // Ensure submesh index is in range
        if (instanceMesh != null)
            subMeshIndex = Mathf.Clamp(subMeshIndex, 0, instanceMesh.subMeshCount - 1);

        // Positions
        if (colorBuffer != null)
            colorBuffer.Release();
        colorBuffer = new ComputeBuffer(instanceCount, 4 * 4);

        if (transformBuffer != null)
            transformBuffer.Release();
        transformBuffer = new ComputeBuffer(instanceCount, 4 * 16);

        if (transformBufferInverse != null)
            transformBufferInverse.Release();
        transformBufferInverse = new ComputeBuffer(instanceCount, 4 * 16);



        Matrix4x4[] instanceLocalTransformsInverse = new Matrix4x4[instanceLocalTransforms.Length];
        for (int i = 0; i < instanceLocalTransforms.Length; i++)
        {
            instanceLocalTransformsInverse[i] = instanceLocalTransforms[i].inverse;
        }
        if (colors != null)
            colorBuffer.SetData(colors);
        transformBuffer.SetData(instanceLocalTransforms);
        transformBufferInverse.SetData(instanceLocalTransformsInverse);
        colorBuffer.SetData(colors);



        block.SetBuffer("transformBuffer", transformBuffer);
        block.SetBuffer("transformBufferInverse", transformBufferInverse);
        block.SetBuffer("colorBuffer", colorBuffer);

        // Indirect args
        if (instanceMesh != null)
        {
            args[0] = (uint)instanceMesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)instanceCount;
            args[2] = (uint)instanceMesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)instanceMesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }
        argsBuffer.SetData(args);

        cachedInstanceCount = instanceCount;
        cachedSubMeshIndex = subMeshIndex;
    }

    void OnDisable()
    {
        if (colorBuffer != null)
            colorBuffer.Release();
        colorBuffer = null;

        if (transformBuffer != null)
            transformBuffer.Release();
        transformBuffer = null;

        if (transformBufferInverse != null)
            transformBufferInverse.Release();
        transformBufferInverse = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;
    }
}