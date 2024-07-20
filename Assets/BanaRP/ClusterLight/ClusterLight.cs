using System.ComponentModel;
using UniGLTF;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LightType = UnityEngine.LightType;

public class ClusterLight
{
    public static int numClusterX = 16;
    public static int numClusterY = 16;
    public static int numClusterZ = 16;
    
    public static int MAX_NUM_POINT_LIGHT = 128;
    public static int MAX_NUM_SPOT_LIGHT = 128;
    public static int MAX_NUM_LIGHT = MAX_NUM_POINT_LIGHT + MAX_NUM_SPOT_LIGHT;
    
    public static int MAX_POINT_LIGHTS_PER_CLUSTER = 8;
    public static int MAX_SPOT_LIGHTS_PER_CLUSTER = 8;
    public static int MAX_NUM_LIGHTS_PER_CLUSTER = MAX_POINT_LIGHTS_PER_CLUSTER + MAX_SPOT_LIGHTS_PER_CLUSTER;

    public static int SIZE_POINT_LIGHT = 32;
    
    struct PointLight
    {
        public Vector3 color;
        public float intensity;
        public Vector3 position;
        public float range;
    }
    
    public static int SIZE_SPOT_LIGHT = 48;

    struct SpotLight
    {
        public Vector3 color;
        public float intensity;
        public Vector3 position;
        public float height;
        public Vector3 direction;
        public float bottomRadius;
    }
    
    public static int SIZE_CLUSTERBOX = 8 * 3 * 4;
    
    struct ClusterBox
    {
        public Vector3 p0, p1, p2, p3, p4, p5, p6, p7;
    }
    
    public static int SIZE_LIGHTINDEX = sizeof(int) * 3;
    
    struct LightIndex
    {
        public int start;
        public int point_count;
        public int spot_count;
    }
    
    ComputeShader clusterGenCS;
    ComputeShader lightAssignCS;
    
    public ComputeBuffer clusterBuffer;     // 簇列表
    public ComputeBuffer lightBuffer;       // 光源列表
    public ComputeBuffer lightAssignBuffer; // 光源分配结果
    public ComputeBuffer assignTable;       // 光源分配索引表 (start index, count index) to lightAssignBuffer
    
    public ComputeBuffer pointLightBuffer;  // 点光源列表
    public ComputeBuffer spotLightBuffer;   // 聚光灯列表
    public RenderTexture clusterPointerBuffer; // 簇指针表
    
    // debug
    struct Normal
    {
        public Vector3 position;
        public Vector3 normal;
    }
    public ComputeBuffer normalBuffer;      // 法线列表
    
    public ClusterLight()
    {
        int numClusters = numClusterX * numClusterY * numClusterZ;
        
        lightBuffer = new ComputeBuffer(MAX_NUM_LIGHT, SIZE_POINT_LIGHT);
        clusterBuffer = new ComputeBuffer(numClusters, SIZE_CLUSTERBOX);
        lightAssignBuffer = new ComputeBuffer(numClusters * MAX_NUM_LIGHTS_PER_CLUSTER, sizeof(uint));
        assignTable = new ComputeBuffer(numClusters, SIZE_LIGHTINDEX);
        
        pointLightBuffer = new ComputeBuffer(MAX_NUM_POINT_LIGHT, SIZE_POINT_LIGHT);
        spotLightBuffer = new ComputeBuffer(MAX_NUM_SPOT_LIGHT, SIZE_SPOT_LIGHT);
        //clusterPointerBuffer = new Texture3D(numClusterX, numClusterY, numClusterZ, TextureFormat.RGB48, false);
        clusterPointerBuffer = new RenderTexture(numClusterX, numClusterY, 0, RenderTextureFormat.ARGB64);
        clusterPointerBuffer.dimension = TextureDimension.Tex3D;
        clusterPointerBuffer.filterMode = FilterMode.Point;
        clusterPointerBuffer.volumeDepth = numClusterZ;
        clusterPointerBuffer.enableRandomWrite = true;
        clusterPointerBuffer.useMipMap = false;
        clusterPointerBuffer.Create();
        
        // debug
        normalBuffer = new ComputeBuffer(numClusters, sizeof(float) * 6);

        clusterGenCS = Resources.Load<ComputeShader>("ClusterGen");
        lightAssignCS = Resources.Load<ComputeShader>("LightAssign");
    }

    ~ClusterLight()
    {
        clusterBuffer.Release();
        lightBuffer.Release();
        lightAssignBuffer.Release();
        assignTable.Release();
        // debug
        normalBuffer.Release();
    }

    public void ClusterGen(Camera camera)
    {
        Matrix4x4 viewMat = camera.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 viewProjMat = projMat * viewMat;
        Matrix4x4 viewProjMatInv = viewProjMat.inverse;
        
        clusterGenCS.SetMatrix("_viewProjMat", viewProjMat);
        clusterGenCS.SetMatrix("_viewProjMatInv", viewProjMatInv);
        clusterGenCS.SetFloat("_numClusterX", numClusterX);
        clusterGenCS.SetFloat("_numClusterY", numClusterY);
        clusterGenCS.SetFloat("_numClusterZ", numClusterZ);
        
        int kernelId = clusterGenCS.FindKernel("ClusterGen");
        clusterGenCS.SetBuffer(kernelId, "_clusterBuffer", clusterBuffer);
        clusterGenCS.Dispatch(kernelId, numClusterZ, 1, 1);
    }

    public void UpdateLightBuffer(Light[] lights)
    {
        PointLight[] pointLights = new PointLight[MAX_NUM_POINT_LIGHT];
        int pointLightCount = 0;
        SpotLight[] spotLights = new SpotLight[MAX_NUM_SPOT_LIGHT];
        int spotLightCount = 0;
        
        foreach (var light in lights)
        {
            if (light.type == LightType.Point)
            {
                PointLight pl = new PointLight();
                pl.color = new Vector3(light.color.r, light.color.g, light.color.b);
                pl.intensity = light.intensity;
                pl.position = light.transform.position;
                pl.range = light.range;
                pointLights[pointLightCount++] = pl;
            }
            else if (light.type == LightType.Spot)
            {
                SpotLight sl = new SpotLight();
                sl.color = new Vector3(light.color.r, light.color.g, light.color.b);
                sl.intensity = light.intensity;
                sl.position = light.transform.position;
                sl.height = light.range;
                sl.direction = light.transform.forward;
                             
                float spotAngle = light.spotAngle;
                // 将角度从度转换为弧度
                float angleInRadians = spotAngle * Mathf.Deg2Rad / 2;
                // 计算底圆的半径
                float bottomRadius = Mathf.Tan(angleInRadians) * sl.height;
                sl.bottomRadius = bottomRadius;
                spotLights[spotLightCount++] = sl;
            }
            
        }
        spotLightBuffer.SetData(spotLights);
        pointLightBuffer.SetData(pointLights);
        
        lightAssignCS.SetInt("_pointNumLights", pointLightCount);
        lightAssignCS.SetInt("_spotNumLights", spotLightCount);
        // lightBuffer.SetData(pointLights);
        //
        // lightAssignCS.SetInt("_numLights", pointLightCount + spotLightCount);
    }
    
    public void UpdateLightBuffer(VisibleLight[] visibleLights)
    {
        PointLight[] pointLights = new PointLight[MAX_NUM_POINT_LIGHT];
        int pointLightCount = 0;
        SpotLight[] spotLights = new SpotLight[MAX_NUM_SPOT_LIGHT];
        int spotLightCount = 0;
        
        foreach (var visibleLight in visibleLights)
        {
            var light = visibleLight.light;
            if (light.type == LightType.Point)
            {
                PointLight pl = new PointLight();
                pl.color = new Vector3(light.color.r, light.color.g, light.color.b);
                pl.intensity = light.intensity;
                pl.position = light.transform.position;
                pl.range = light.range;
                pointLights[pointLightCount++] = pl;
            }
            else if (light.type == LightType.Spot)
            {
                SpotLight sl = new SpotLight();
                sl.color = new Vector3(light.color.r, light.color.g, light.color.b);
                sl.intensity = light.intensity;
                sl.position = light.transform.position;
                sl.height = light.range;
                sl.direction = light.transform.forward;
                             
                float spotAngle = light.spotAngle;
                // 将角度从度转换为弧度
                float angleInRadians = spotAngle * Mathf.Deg2Rad / 2;
                // 计算底圆的半径
                float bottomRadius = Mathf.Tan(angleInRadians) * sl.height;
                sl.bottomRadius = bottomRadius;
                spotLights[spotLightCount++] = sl;
            }
            
        }
        spotLightBuffer.SetData(spotLights);
        pointLightBuffer.SetData(pointLights);
        
        lightAssignCS.SetInt("_pointNumLights", pointLightCount);
        lightAssignCS.SetInt("_spotNumLights", spotLightCount);
    }
    
    public void LightAssign()
    {
        lightAssignCS.SetInt("_maxNumLightsPerCluster", MAX_NUM_LIGHTS_PER_CLUSTER);
        lightAssignCS.SetFloat("_numClusterX", numClusterX);
        lightAssignCS.SetFloat("_numClusterY", numClusterY);
        lightAssignCS.SetFloat("_numClusterZ", numClusterZ);
        
        int kernelId = lightAssignCS.FindKernel("LightAssign");
        lightAssignCS.SetBuffer(kernelId, "_clusterBuffer", clusterBuffer);
        lightAssignCS.SetBuffer(kernelId, "_lightBuffer", lightBuffer);
        lightAssignCS.SetBuffer(kernelId, "_lightAssignBuffer", lightAssignBuffer);
        lightAssignCS.SetBuffer(kernelId, "_assignTable", assignTable);
        // test
        lightAssignCS.SetBuffer(kernelId, "_pointLightBuffer", pointLightBuffer);
        lightAssignCS.SetBuffer(kernelId, "_spotLightBuffer", spotLightBuffer);
        lightAssignCS.SetTexture(kernelId, "_clusterPointerBuffer", clusterPointerBuffer);
        // debug
        lightAssignCS.SetBuffer(kernelId, "_normalBuffer", normalBuffer);
        
        lightAssignCS.Dispatch(kernelId, numClusterZ, 1, 1);
    }

    public void SetShaderParameters()
    {
        Shader.SetGlobalFloat("_numClusterX", numClusterX);
        Shader.SetGlobalFloat("_numClusterY", numClusterY);
        Shader.SetGlobalFloat("_numClusterZ", numClusterZ);

        Shader.SetGlobalBuffer("_pointLightBuffer", pointLightBuffer);
        Shader.SetGlobalBuffer("_spotLightBuffer", spotLightBuffer);
        //Shader.SetGlobalBuffer("_lightBuffer", lightBuffer);
        Shader.SetGlobalBuffer("_lightAssignBuffer", lightAssignBuffer);
        Shader.SetGlobalBuffer("_assignTable", assignTable);
    }
    
    void DrawBox(ClusterBox box, Color color)
    {
        Debug.DrawLine(box.p0, box.p1, color);
        Debug.DrawLine(box.p0, box.p2, color);
        Debug.DrawLine(box.p0, box.p4, color);
        
        Debug.DrawLine(box.p6, box.p2, color);
        Debug.DrawLine(box.p6, box.p7, color);
        Debug.DrawLine(box.p6, box.p4, color);

        Debug.DrawLine(box.p5, box.p1, color);
        Debug.DrawLine(box.p5, box.p7, color);
        Debug.DrawLine(box.p5, box.p4, color);

        Debug.DrawLine(box.p3, box.p1, color);
        Debug.DrawLine(box.p3, box.p2, color);
        Debug.DrawLine(box.p3, box.p7, color);
    }
    
    public void DebugCluster()
    {
        ClusterBox[] boxes = new ClusterBox[numClusterX * numClusterY * numClusterZ];
        clusterBuffer.GetData(boxes, 0, 0, numClusterX * numClusterY * numClusterZ);
        
        foreach (var box in boxes)
        {
            DrawBox(box, Color.gray);
        }
    }

    public void DebugNormal()
    {
        int numClusters = numClusterX * numClusterY * numClusterZ;
        Normal[] normals = new Normal[numClusters];
        normalBuffer.GetData(normals, 0, 0, numClusters);
        
        foreach (var normal in normals)
        {
            Debug.DrawLine(normal.position, normal.position + normal.normal, Color.magenta);
        }
    }
    
    public void DebugLightAssign()
    {
        int numClusters = numClusterX * numClusterY * numClusterZ;

        ClusterBox[] boxes = new ClusterBox[numClusters];
        clusterBuffer.GetData(boxes, 0, 0, numClusters);

        LightIndex[] indices = new LightIndex[numClusters];
        assignTable.GetData(indices, 0, 0, numClusters);
        
        for(int i=0; i<indices.Length; i++)
        {
            if(indices[i].point_count>0)
            {
                DrawBox(boxes[i], Color.red);
            }else if(indices[i].spot_count>0)
            {
                DrawBox(boxes[i], Color.green);
            }
                
        }
        
    }
    
    
}