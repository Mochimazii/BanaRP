using UnityEngine;

struct MainCameraSettings
{
    public Vector3 position;
    public Quaternion rotation;
    public float nearClipPlane;
    public float farClipPlane;
    public float aspect;
}
public class CSM
{
    // 视锥分割
    public float[] splits = {0.07f, 0.13f, 0.25f, 0.55f};
    
    // 主相机设置
    MainCameraSettings mainCameraSettings;
    
    // 主相机视锥
    Vector3[] nearPlaneCorners = new Vector3[4];
    Vector3[] farPlaneCorners = new Vector3[4];
    
    // 主相机视锥划分出的子视锥
    Vector3[] f0_near = new Vector3[4], f0_far = new Vector3[4];
    Vector3[] f1_near = new Vector3[4], f1_far = new Vector3[4];
    Vector3[] f2_near = new Vector3[4], f2_far = new Vector3[4];
    Vector3[] f3_near = new Vector3[4], f3_far = new Vector3[4];
    Vector3[] s_near = new Vector3[4], s_far = new Vector3[4];// random set

    // 主相机视锥体光源空间AABB包围盒, 世界空间坐标
    Vector3[] box0, box1, box2, box3, sceneBox;
    
    // 0-3 for 0-3 level frustum, 4 for scene
    float[] nearDist = new float[5];
    
    // 子视椎体宽度
    public float[] frustumWidths = new float[4];

    public CSM()
    {
        // 获取场景中所有的 MeshRenderer 组件
        MeshRenderer[] allMeshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();

        // 初始化最小和最大点
        Vector3 minPoint = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maxPoint = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        
        // 遍历所有的 MeshRenderer 组件
        foreach (MeshRenderer meshRenderer in allMeshRenderers)
        {
            // 获取模型的包围盒
            Bounds bounds = meshRenderer.bounds;

            // 更新最小和最大点
            minPoint = Vector3.Min(minPoint, bounds.min);
            maxPoint = Vector3.Max(maxPoint, bounds.max);
        }
        
        s_near = new Vector3[]
        {
            new Vector3(minPoint.x, minPoint.y, minPoint.z),
            new Vector3(minPoint.x, minPoint.y, maxPoint.z),
            new Vector3(minPoint.x, maxPoint.y, minPoint.z),
            new Vector3(minPoint.x, maxPoint.y, maxPoint.z)
        };
        
        s_far = new Vector3[]
        {
            new Vector3(maxPoint.x, minPoint.y, minPoint.z),
            new Vector3(maxPoint.x, minPoint.y, maxPoint.z),
            new Vector3(maxPoint.x, maxPoint.y, minPoint.z),
            new Vector3(maxPoint.x, maxPoint.y, maxPoint.z)
        };
    }

    // 光源空间下视锥的AABB的世界坐标
    Vector3[] LightSpaceAABB(Vector3[] nearCorners, Vector3[] farCorners, Vector3 lightDir, int level)
    {
        // 修复光照反向问题 还有黑线问题，增加摄像机
        Matrix4x4 toShadowViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
        Matrix4x4 toShadowView = toShadowViewInv.inverse;

        for (int i = 0; i < 4; i++)
        {
            farCorners[i] = toShadowView.MultiplyPoint(farCorners[i]);
            nearCorners[i] = toShadowView.MultiplyPoint(nearCorners[i]);
        }
        
        // 计算AABB包围盒
        float[] x = new float[8];
        float[] y = new float[8];
        float[] z = new float[8];
        for(int i=0; i<4; i++)
        {
            x[i] = nearCorners[i].x; x[i+4] = farCorners[i].x;
            y[i] = nearCorners[i].y; y[i+4] = farCorners[i].y;
            z[i] = nearCorners[i].z; z[i+4] = farCorners[i].z;
        }
        float xmin=Mathf.Min(x), xmax=Mathf.Max(x);
        float ymin=Mathf.Min(y), ymax=Mathf.Max(y);
        float zmin=Mathf.Min(z), zmax=Mathf.Max(z);
        
        // 设置光源空间下的AABB包围盒近平面值
        nearDist[level] = zmin;

        Vector3[] lightSpaceAABBCorners =
        {
            new Vector3(xmin, ymin, zmin), new Vector3(xmin, ymin, zmax), new Vector3(xmin, ymax, zmin),
            new Vector3(xmin, ymax, zmax),
            new Vector3(xmax, ymin, zmin), new Vector3(xmax, ymin, zmax), new Vector3(xmax, ymax, zmin),
            new Vector3(xmax, ymax, zmax)
        };
        
        // AABB包围盒转世界坐标
        for (int i = 0; i < 8; i++)
        {
            lightSpaceAABBCorners[i] = toShadowViewInv.MultiplyPoint(lightSpaceAABBCorners[i]);
        }

        
        // 视锥体还原
        for (int i = 0; i < 4; i++)
        {
            farCorners[i] = toShadowViewInv.MultiplyPoint(farCorners[i]);
            nearCorners[i] = toShadowViewInv.MultiplyPoint(nearCorners[i]);
        }

        return lightSpaceAABBCorners;
    }

    public void ConfigCameraToShadowSpace(ref Camera camera, Vector3 lightDir, int level, float resolution)
    {
        // 选择对应视锥包围盒
        var box = new Vector3[8];
        var f_near = new Vector3[4]; var f_far = new Vector3[4];
        if(level==0) {box=box0; f_near=f0_near; f_far=f0_far;}
        if(level==1) {box=box1; f_near=f1_near; f_far=f1_far;}
        if(level==2) {box=box2; f_near=f2_near; f_far=f2_far;}
        if(level==3) {box=box3; f_near=f3_near; f_far=f3_far;}
        
        // 计算 box 近平面中点
        Vector3 center = (box[2] + box[4]) / 2;  //(box[3] + box[4]) / 2
        float w = Vector3.Magnitude(box[0] - box[4]);
        float h = Vector3.Magnitude(box[0] - box[2]);
        float d = Vector3.Magnitude(box[0] - box[1]);
        float diagonalLen = Vector3.Magnitude(f_near[0] - f_far[2]);
        float disPerTexel = diagonalLen / resolution;
        
        Matrix4x4 toShadowViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
        Matrix4x4 toShadowView = toShadowViewInv.inverse;

        // 相机坐标旋转到光源坐标系下取整
        center = toShadowView.MultiplyPoint(center);
        for(int i=0; i<2; i++)
            center[i] = Mathf.Floor(center[i] / disPerTexel) * disPerTexel;
        center = toShadowViewInv.MultiplyPoint(center);
        
        // 配置相机
        camera.transform.rotation = Quaternion.LookRotation(lightDir);
        camera.transform.position = center;
        camera.nearClipPlane = nearDist[4] - nearDist[level];//-distance;
        camera.farClipPlane = d;//distance;
        camera.aspect = 1.0f;
        camera.orthographicSize = diagonalLen * 0.5f;
    }
    
    // 用相机和光源方向更新 CSM 信息
    public void Update(Camera camera, Vector3 lightDir)
    {
        // 获取主相机视椎体从相机出发到 Corner 的 view space 向量
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPlaneCorners);
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPlaneCorners);
        
        // 视锥体顶点转世界坐标
        for (int i = 0; i < 4; i++)
        {   
            nearPlaneCorners[i] = camera.transform.TransformVector(nearPlaneCorners[i]) + camera.transform.position;
            farPlaneCorners[i] = camera.transform.TransformVector(farPlaneCorners[i]) + camera.transform.position;
        }
        
        // 按照比例划分相机视椎体
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = farPlaneCorners[i] - nearPlaneCorners[i];

            f0_near[i] = nearPlaneCorners[i];
            f0_far[i] = f0_near[i] + dir * splits[0];

            f1_near[i] = f0_far[i];
            f1_far[i] = f1_near[i] + dir * splits[1];

            f2_near[i] = f1_far[i];
            f2_far[i] = f2_near[i] + dir * splits[2];

            f3_near[i] = f2_far[i];
            f3_far[i] = f3_near[i] + dir * splits[3];
        }
        
        // 计算包围盒
        sceneBox = LightSpaceAABB(s_near, s_far, lightDir, 4);
        box0 = LightSpaceAABB(f0_near, f0_far, lightDir, 0);
        box1 = LightSpaceAABB(f1_near, f1_far, lightDir, 1);
        box2 = LightSpaceAABB(f2_near, f2_far, lightDir, 2);
        box3 = LightSpaceAABB(f3_near, f3_far, lightDir, 3);
        
        // 更新 Frustum width
        frustumWidths[0] = Vector3.Magnitude(f0_far[2]-f0_near[0]);
        frustumWidths[1] = Vector3.Magnitude(f1_far[2]-f1_near[0]);
        frustumWidths[2] = Vector3.Magnitude(f2_far[2]-f2_near[0]);
        frustumWidths[3] = Vector3.Magnitude(f3_far[2]-f3_near[0]);
    }

    public void SaveMainCameraSettings(ref Camera camera)
    {
        mainCameraSettings.position = camera.transform.position;
        mainCameraSettings.rotation = camera.transform.rotation;
        mainCameraSettings.farClipPlane = camera.farClipPlane;
        mainCameraSettings.nearClipPlane = camera.nearClipPlane;
        mainCameraSettings.aspect = camera.aspect;
        camera.orthographic = true;
    }
    
    public void RevertMainCameraSettings(ref Camera camera)
    {
        camera.transform.position = mainCameraSettings.position;
        camera.transform.rotation = mainCameraSettings.rotation;
        camera.farClipPlane = mainCameraSettings.farClipPlane;
        camera.nearClipPlane = mainCameraSettings.nearClipPlane;
        camera.aspect = mainCameraSettings.aspect;
        camera.orthographic = false;
    }
    
    // -------------------- DEBUG -------------------- //
    // 相机视椎体
    void DrawFrustum(Vector3[] nearPlaneCorners, Vector3[] farPlaneCorners, Color color)
    {
        for (int i = 0; i < 4; i++)
            Debug.DrawLine(nearPlaneCorners[i], farPlaneCorners[i], color);

        Debug.DrawLine(farPlaneCorners[0], farPlaneCorners[1], color);
        Debug.DrawLine(farPlaneCorners[0], farPlaneCorners[3], color);
        Debug.DrawLine(farPlaneCorners[2], farPlaneCorners[1], color);
        Debug.DrawLine(farPlaneCorners[2], farPlaneCorners[3], color);
        Debug.DrawLine(nearPlaneCorners[0], nearPlaneCorners[1], color);
        Debug.DrawLine(nearPlaneCorners[0], nearPlaneCorners[3], color);
        Debug.DrawLine(nearPlaneCorners[2], nearPlaneCorners[1], color);
        Debug.DrawLine(nearPlaneCorners[2], nearPlaneCorners[3], color);
    }
    
    // 面朝光源的包围盒
    void DrawAABB(Vector3[] corners, Color color)
    {
        Debug.DrawLine(corners[0], corners[1], color);
        Debug.DrawLine(corners[0], corners[2], color);
        Debug.DrawLine(corners[0], corners[4], color);

        Debug.DrawLine(corners[6], corners[2], color);
        Debug.DrawLine(corners[6], corners[7], color);
        Debug.DrawLine(corners[6], corners[4], color);

        Debug.DrawLine(corners[5], corners[1], color);
        Debug.DrawLine(corners[5], corners[7], color);
        Debug.DrawLine(corners[5], corners[4], color);

        Debug.DrawLine(corners[3], corners[1], color);
        Debug.DrawLine(corners[3], corners[2], color);
        Debug.DrawLine(corners[3], corners[7], color);
    }
    
    public void DebugDraw()
    {
        DrawFrustum(nearPlaneCorners, farPlaneCorners, Color.white);
        DrawAABB(box0, Color.red);  
        DrawAABB(box1, Color.green);
        DrawAABB(box2, Color.cyan);
        DrawAABB(box3, Color.yellow);
        DrawAABB(sceneBox, Color.magenta);
    }
}