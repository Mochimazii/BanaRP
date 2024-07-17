using UnityEngine;
using UnityEngine.Rendering;

public class InstanceDrawer
{
    public static void CheckAndInit(InstanceBehavior idata)
    {
        if(idata.matrixBuffer!=null && idata.validMatrixBuffer!=null && idata.argsBuffer!=null) return;

        int sizeofMatrix4x4 = 4 * 4 * 4;
        idata.matrixBuffer = new ComputeBuffer(idata.instanceCount, sizeofMatrix4x4);
        idata.validMatrixBuffer = new ComputeBuffer(idata.instanceCount, sizeofMatrix4x4, ComputeBufferType.Append);
        idata.argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        // 传变换矩阵到 GPU
        idata.matrixBuffer.SetData(idata.matrices);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 }; // 绘制参数
        if (idata.instanceMesh != null) 
        {
            args[0] = (uint)idata.instanceMesh.GetIndexCount(idata.subMeshIndex);// 实例的索引数量
            args[1] = (uint)0;// 要绘制的实例数量
            args[2] = (uint)idata.instanceMesh.GetIndexStart(idata.subMeshIndex);// 实例的索引起始位置
            args[3] = (uint)idata.instanceMesh.GetBaseVertex(idata.subMeshIndex);// 实例的第一个顶点索引
        }
        idata.argsBuffer.SetData(args);
    }

    public static void DrawPrevUnCulled(InstanceBehavior idata, Camera camera, ComputeShader computeShader, ref CommandBuffer cmd)
    {
        if (idata == null || camera == null || computeShader == null) return;
        CheckAndInit(idata);
        
        // 清空绘制计数
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        idata.argsBuffer.GetData(args);
        args[1] = 0;
        idata.argsBuffer.SetData(args);
        Matrix4x4[] prevUnculledMatrices = new Matrix4x4[idata.validMatrixBuffer.count];
        idata.validMatrixBuffer.GetData(prevUnculledMatrices);
        idata.matrixBuffer.SetData(prevUnculledMatrices);
        idata.validMatrixBuffer.SetCounterValue(0);
        
        // 计算视椎体平面
        Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);//法线朝视椎体内
        Vector4[] planes = new Vector4[6];
        for (int i = 0; i < 6; i++)
        {
            // Ax+ By + Cz + D = 0
            planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);
        }
        
        // 物体 boundbox 顶点
        Vector4[] boundingBox = new Vector4[8];
        Bounds bounds = idata.instanceMesh.bounds;
        boundingBox[0] = new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 1);
        boundingBox[1] = new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, 1);
        boundingBox[2] = new Vector4(boundingBox[0].x, boundingBox[0].y, boundingBox[1].z, 1);
        boundingBox[3] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[0].z, 1);
        boundingBox[4] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[0].z, 1);
        boundingBox[5] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[1].z, 1);
        boundingBox[6] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[1].z, 1);
        boundingBox[7] = new Vector4(boundingBox[1].x, boundingBox[1].y, boundingBox[0].z, 1);
        
        // 传参到 Compute Shader
        int kernelId = computeShader.FindKernel("CSMain");
        computeShader.SetBool("_occlusionCull", false);
        computeShader.SetInt("_instanceCount", prevUnculledMatrices.Length);
        computeShader.SetVectorArray("_planes", planes);
        computeShader.SetVectorArray("_bounds", boundingBox);
        computeShader.SetBuffer(kernelId, "_matrixBuffer", idata.matrixBuffer);
        computeShader.SetBuffer(kernelId, "_validMatrixBuffer", idata.validMatrixBuffer);
        computeShader.SetBuffer(kernelId, "_argsBuffer", idata.argsBuffer);
        idata.instanceMaterial.SetBuffer("_validMatrixBuffer", idata.validMatrixBuffer);
        
        // 剔除
        int threadGroups = Mathf.CeilToInt(prevUnculledMatrices.Length / 128.0f);
        computeShader.Dispatch(kernelId, threadGroups, 1, 1);
        idata.argsBuffer.GetData(args);
        
        cmd.DrawMeshInstancedIndirect(
            idata.instanceMesh,
            idata.subMeshIndex,
            idata.instanceMaterial,
            1,
            idata.argsBuffer);
    }

    public static void Draw(InstanceBehavior idata, Camera camera, ComputeShader computeShader, Matrix4x4 vpMatrix, RenderTexture hizBuffer, ref CommandBuffer cmd)
    {
        if (idata == null || camera == null || computeShader == null) return;
        CheckAndInit(idata);
        
        // 清空绘制计数
        idata.matrixBuffer.SetData(idata.matrices);
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        idata.argsBuffer.GetData(args);
        args[1] = 0;
        idata.argsBuffer.SetData(args);
        idata.validMatrixBuffer.SetCounterValue(0);
        
        // 计算视椎体平面
        Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);//法线朝视椎体内
        Vector4[] planes = new Vector4[6];
        for (int i = 0; i < 6; i++)
        {
            // Ax+ By + Cz + D = 0
            planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);
        }
        
        // 物体 boundbox 顶点
        Vector4[] boundingBox = new Vector4[8];
        Bounds bounds = idata.instanceMesh.bounds;
        boundingBox[0] = new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 1);
        boundingBox[1] = new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, 1);
        boundingBox[2] = new Vector4(boundingBox[0].x, boundingBox[0].y, boundingBox[1].z, 1);
        boundingBox[3] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[0].z, 1);
        boundingBox[4] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[0].z, 1);
        boundingBox[5] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[1].z, 1);
        boundingBox[6] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[1].z, 1);
        boundingBox[7] = new Vector4(boundingBox[1].x, boundingBox[1].y, boundingBox[0].z, 1);
        
        
        // 传参到 Compute Shader
        int kernelId = computeShader.FindKernel("CSMain");
        computeShader.SetBool("_occlusionCull", true);
        computeShader.SetInt("_instanceCount", idata.instanceCount);
        computeShader.SetInt("_depthTextureSize", hizBuffer.width);
        computeShader.SetVectorArray("_planes", planes);
        computeShader.SetVectorArray("_bounds", boundingBox);
        computeShader.SetMatrix("_vpMatrix", vpMatrix);
        computeShader.SetTexture(kernelId, "_hizBuffer", hizBuffer);
        computeShader.SetBuffer(kernelId, "_matrixBuffer", idata.matrixBuffer);
        computeShader.SetBuffer(kernelId, "_validMatrixBuffer", idata.validMatrixBuffer);
        computeShader.SetBuffer(kernelId, "_argsBuffer", idata.argsBuffer);
        idata.instanceMaterial.SetBuffer("_validMatrixBuffer", idata.validMatrixBuffer);
        
        // 剔除
        int threadGroups = Mathf.CeilToInt(idata.instanceCount / 128.0f);
        computeShader.Dispatch(kernelId, threadGroups, 1, 1);
        idata.argsBuffer.GetData(args);
        
        cmd.DrawMeshInstancedIndirect(
            idata.instanceMesh,
            idata.subMeshIndex,
            idata.instanceMaterial,
            1,
            idata.argsBuffer);
    }
}