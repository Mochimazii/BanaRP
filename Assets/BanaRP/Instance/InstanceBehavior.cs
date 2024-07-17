using System;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteInEditMode]
public class InstanceBehavior : MonoBehaviour
{
    [HideInInspector] public Matrix4x4[] matrices; //变换矩阵 （持久保存）
    
    [HideInInspector] public ComputeBuffer matrixBuffer; //全部实体的变换矩阵（运行时生成的 GPU buffer）
    [HideInInspector] public ComputeBuffer validMatrixBuffer; //剔除后剩余实体的变换矩阵（运行时生成的 GPU buffer）
    [HideInInspector] public ComputeBuffer argsBuffer; //绘制参数（运行时生成的 GPU buffer）
    
    [HideInInspector] public int subMeshIndex = 0;  // 子网格下标   （持久保存）
    [HideInInspector] public int instanceCount = 0; // instance 数目（持久保存）
    
    public Mesh instanceMesh;
    public Material instanceMaterial;
    
    public Vector3 center = new Vector3(0,0,0);
    public int randomInstanceNum = 5000;
    public float distanceMin = 5.0f;
    public float distanceMax = 50.0f;
    public float heightMin = 0f;
    public float heightMax = 0.5f;
    
    private void GenerateRandomInstanceData()
    {
        instanceCount = randomInstanceNum;
        
        matrices = new Matrix4x4[instanceCount];
        for (int i = 0; i < instanceCount; i++) 
        {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            float distance = Mathf.Sqrt(Random.Range(0.0f, 1.0f)) * (distanceMax - distanceMin) + distanceMin;
            float height = Random.Range(heightMin, heightMax);

            Vector3 pos = new Vector3(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance);

            Quaternion q = Quaternion.AngleAxis(90, Vector3.right);

            Matrix4x4 m = Matrix4x4.Rotate(q);
            m.SetColumn(3, new Vector4(pos.x, pos.y, pos.z, 1));

            matrices[i] = m;
        }

        Debug.Log("Instance Data Generate Success");
    }

    private void Awake()
    {
        GenerateRandomInstanceData();
    }
}