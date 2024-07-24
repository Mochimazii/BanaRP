using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class BanaRenderPipeline : RenderPipeline
{
    private BanaRenderPipelineAsset renderPipelineAsset;

    private RenderTexture gDepth;                                               // depth attachment
    private RenderTexture[] gBuffers = new RenderTexture[4];                    // color attachment
    private RenderTargetIdentifier[] gBufferIDs = new RenderTargetIdentifier[4]; // tex ID
    
    // 阴影
    private CSM _csm;
    RenderTexture[] csmShadowmaps = new RenderTexture[4];
    RenderTexture shadowMask;
    RenderTexture visibilityMap;
    
    // TAA
    private TAA _taa;
    
    // Hierarchical-Z
    RenderTexture hizBuffer;
    
    // Light
    ClusterLight clusterLight;
    
    // Blur
    Blur _blur;
    
    public BanaRenderPipeline(BanaRenderPipelineAsset asset)
    {
        renderPipelineAsset = asset;
        
        // 创建纹理
        gDepth  = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        gBuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        gBuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        gBuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        gBuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
        
        // 给纹理 ID 赋值
        for (int i = 0; i < 4; i++)
        {
            gBuffers[i].filterMode = FilterMode.Point;
            gBufferIDs[i] = gBuffers[i];
        }
        
        // Hierarchical-Z
        int hizSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
        hizBuffer = new RenderTexture(hizSize, hizSize, 0, RenderTextureFormat.RHalf);
        hizBuffer.autoGenerateMips = false;
        hizBuffer.useMipMap = true;
        hizBuffer.filterMode = FilterMode.Point;
        
        // 阴影
        shadowMask = new RenderTexture(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        visibilityMap = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        _csm = new CSM();
        for (int i = 0; i < 4; i++)
        {
            csmShadowmaps[i] = new RenderTexture(1024, 1024, 24, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        }
        
        
        // TAA history buffer
        _taa = new TAA();
        //CurrentFrameRenderBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //historyBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        
        // Light
        clusterLight = new ClusterLight();
        
        // Blur
        _blur = new Blur();
    }
    
    protected override void Render (ScriptableRenderContext context, Camera[] cameras)
    {
        // 遍历所有摄像机
        foreach (Camera camera in cameras)
        {
            // gbuffer
            Shader.SetGlobalTexture("_gDepth", gDepth);
            for (int i = 0; i < 4; i++)
            {
                Shader.SetGlobalTexture("_GT" + i, gBuffers[i]);
            }
            
            // 设置变换矩阵
            // Matrix4x4 viewMat = camera.worldToCameraMatrix;
            // Matrix4x4 projMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            // Matrix4x4 viewProjMat = projMat * viewMat;
            // Matrix4x4 invViewProjMat = viewProjMat.inverse;
            // Shader.SetGlobalMatrix("_vpMat", viewProjMat);
            // Shader.SetGlobalMatrix("_vpMatInv", invViewProjMat);
            Shader.SetGlobalTexture("_hizBuffer", hizBuffer);
            
            // 设置 IBL 贴图
            Shader.SetGlobalTexture("_irradianceIBL", renderPipelineAsset.irradianceIBL);
            Shader.SetGlobalTexture("_prefilteredSpecIBL", renderPipelineAsset.prefilteredSpecIBL);
            Shader.SetGlobalTexture("_brdfLUT", renderPipelineAsset.brdfLUT);
            
            // 设置 shadow map
            Shader.SetGlobalTexture("_shadowMask", shadowMask);
            Shader.SetGlobalTexture("_visibilityMap", visibilityMap);
            _csm.SetSplit(camera.nearClipPlane, camera.farClipPlane);
            for(int i=0; i<4; i++)
            {
                Shader.SetGlobalTexture("_shadowtex"+i, csmShadowmaps[i]);
                Shader.SetGlobalFloat("_split"+i, _csm.splits[i]);
            }
            
            // ------------------------ Pass ------------------------ //
            
            ClusterLightingPass(context, camera);
            
            ShadowCastPass(context, camera);
            
            GbufferPass(context, camera);
            
            if (!Handles.ShouldRenderGizmos())
            {
                HierarchicalZPass(context, camera);
            }
            
            InstanceDrawPass(context, Camera.main);
            
            VisibilityMapPass(context, camera);
            
            LightPass(context, camera);
            
            // ------------------------ Pass End ------------------------ //
            
            // 在需要时调度命令绘制天空盒
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                CommandBuffer cmd = new CommandBuffer { name = "SkyBox" };
                context.DrawSkybox(camera);
                context.Submit();
            }
            // 绘制 Gizmos
            if (Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
            
            PostFXPass(context, camera);
            
            // 指示图形 API 执行所有调度的命令
            context.Submit();
        }
    }

    void ClusterLightingPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "ClusterLightingPass" };
        
        camera.TryGetCullingParameters(out var cullingParameters);
        var cullingResults = context.Cull(ref cullingParameters);
        
        // 设置光照
        clusterLight.UpdateLightBuffer(cullingResults.visibleLights.ToArray());
        
        // 划分 cluster
        clusterLight.ClusterGen(camera);
        
        // 配置点光源 
        clusterLight.LightAssign();
        
        clusterLight.SetShaderParameters();
    }
    
    void PostFXPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "PostFX" };
        
        if (renderPipelineAsset.TAA && camera.cameraType == CameraType.Game)
        {
            _taa.GetHistoryBuffer(out RenderTexture historyRead, out RenderTexture historyWrite);
            
            Shader.SetGlobalTexture("_HistoryTex", historyRead);

            Shader.SetGlobalFloat("_BlendAlpha", _taa.BlendAlpha);
        
            cmd.Blit(BuiltinRenderTextureType.CameraTarget, historyWrite, _taa.taaMaterial);
            cmd.Blit(historyWrite, BuiltinRenderTextureType.CameraTarget);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        if (renderPipelineAsset.GaussianBlur)
        {
            // RenderTexture tempRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            // cmd.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            // var destRT = _blur.DoBlur(tempRT);
            // cmd.Blit(destRT, BuiltinRenderTextureType.CameraTarget);
            // tempRT.Release();
            
            RenderTexture tempRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            cmd.Blit(BuiltinRenderTextureType.CameraTarget, tempRT);
            _blur.DoHorizontalBlur(ref tempRT, ref _blur.tempRT);
            _blur.DoVerticalBlur(ref _blur.tempRT,ref _blur.destRT);
            cmd.Blit(_blur.destRT, BuiltinRenderTextureType.CameraTarget);
            tempRT.Release();
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
        
        context.Submit();
    }
    
    void VisibilityMapPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "VisibilityMapPass" };

        cmd.BeginSample("VisibilityMapPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        if (renderPipelineAsset.csmSettings.useShadowMask)
        {
            // temp texture for filter
            RenderTexture tempTex1 = RenderTexture.GetTemporary(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            RenderTexture tempTex2 = RenderTexture.GetTemporary(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            
            cmd.Blit(gBufferIDs[0], tempTex1, new Material(Shader.Find("BanaRP/ShadowMask")));
            cmd.Blit(tempTex1, tempTex2, new Material(Shader.Find("BanaRP/blur1xN")));
            cmd.Blit(tempTex2, shadowMask, new Material(Shader.Find("BanaRP/blurNx1")));
            
            RenderTexture.ReleaseTemporary(tempTex1);
            RenderTexture.ReleaseTemporary(tempTex2);
        }                
        
        // generate visibility map
        cmd.Blit(gBufferIDs[0], visibilityMap, new Material(Shader.Find("BanaRP/VisibilityMapPass")));
        
        cmd.EndSample("VisibilityMapPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        context.Submit();
    }
    
    void GbufferPass(ScriptableRenderContext context, Camera camera)
    {
        
        context.SetupCameraProperties(camera);
        var cmd = new CommandBuffer {
            name = "GBufferCMD"
        };
        
        cmd.SetRenderTarget(gBufferIDs, gDepth);                            // 设置 RT
        cmd.ClearRenderTarget(true, true, Color.clear);// 清屏
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        cmd.BeginSample("GbufferPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        // TAA jitter
        if (renderPipelineAsset.TAA && camera.cameraType == CameraType.Game)
        {
            _taa.SetJitterCamera(ref camera);
            _taa.GetReprojectionMat(out Matrix4x4 prevJitteredVpMatrix, out Matrix4x4 currentJitteredVpMatrix);
            Shader.SetGlobalVector("_Jitter", _taa._Jitter);
            Shader.SetGlobalVector("_prevJitter", _taa._prevJitter);
            Shader.SetGlobalMatrix("_prevJitteredVpMatrix", prevJitteredVpMatrix);
            Shader.SetGlobalMatrix("_currentJitteredVpMatrixInv", currentJitteredVpMatrix.inverse);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
        
        Matrix4x4 viewMat = camera.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 viewProjMat = projMat * viewMat;
        Matrix4x4 invViewProjMat = viewProjMat.inverse;
        Shader.SetGlobalMatrix("_vpMat", viewProjMat);
        Shader.SetGlobalMatrix("_vpMatInv", invViewProjMat);
        
        camera.TryGetCullingParameters(out var cullingParameters);  // 从当前摄像机获取剔除参数
        var cullingResults = context.Cull(ref cullingParameters);   // 使用剔除参数执行剔除操作，并存储结果
            
        ShaderTagId shaderTagId = new ShaderTagId("GBuffer");   // 基于 LightMode 通道标签值，向 Unity 告知要绘制的几何体
        var sortingSettings = new SortingSettings(camera);           // 基于当前摄像机，向 Unity 告知如何对几何体进行排序
        DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);// 创建描述要绘制的几何体以及绘制方式的 DrawingSettings 结构
        // 额外的 ShaderTagId
        ShaderTagId strandardShaderTagId = new ShaderTagId("UniversalForward");        
        drawingSettings.SetShaderPassName(1, strandardShaderTagId);
            
        // 告知 Unity 如何过滤剔除结果，以进一步指定要绘制的几何体
        // 使用 FilteringSettings.defaultValue 可指定不进行过滤
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
            
        // 基于定义的设置，调度命令绘制几何体
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        // draw prev unculled instances
        // InstanceBehavior[] instanceBehaviors = GameObject.FindObjectsOfType<InstanceBehavior>();
        // ComputeShader cullingCS = Resources.Load<ComputeShader>("Culling");
        // foreach (var instance in instanceBehaviors)
        // {
        //     InstanceDrawer.DrawPrevUnCulled(instance, Camera.main, cullingCS, ref cmd);
        // }
        
        cmd.EndSample("GbufferPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        context.Submit();
        
        //Profiler.EndSample();
    }

    void HierarchicalZPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "HiZPass" };

        // 创建纹理
        int size = hizBuffer.width;
        int mipNum = Mathf.FloorToInt(Mathf.Log(size, 2));
        RenderTexture[] mips = new RenderTexture[mipNum];// 0级是原始深度图
        for (int i = 0; i < mips.Length; i++)
        {
            int mipSize = size / (int)Mathf.Pow(2, i);
            mips[i] = RenderTexture.GetTemporary(mipSize, mipSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            mips[i].filterMode = FilterMode.Point;
        }
        
        // down sample mipmap
        Material HizMat = new Material(Shader.Find("BanaRP/HizBlit"));
        cmd.Blit(gDepth, mips[0]);
        for (int i = 1; i < mips.Length; i++)
        {
            cmd.Blit(mips[i - 1], mips[i], HizMat);
        }

        for (int i = 0; i < mips.Length; i++)
        {
            cmd.CopyTexture(mips[i], 0, 0, hizBuffer, 0, i);
            RenderTexture.ReleaseTemporary(mips[i]);
        }
        
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        context.Submit();
    }
    
    void InstanceDrawPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer {  name = "InstanceDrawPass" };
        cmd.SetRenderTarget(gBufferIDs, gDepth);
        InstanceBehavior[] instanceBehaviors = GameObject.FindObjectsOfType<InstanceBehavior>();
        
        Matrix4x4 viewMat = camera.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 viewProjMat = projMat * viewMat;

        ComputeShader cullingCS = Resources.Load<ComputeShader>("Culling");
        foreach (var instance in instanceBehaviors)
        {
            InstanceDrawer.Draw(instance, camera, cullingCS, viewProjMat, hizBuffer, ref cmd);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        context.Submit();
    }
    
    void LightPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "LightPass" };
        
        // Light
        int MainLightDirectionID = Shader.PropertyToID("_mainLightDirection");
        int MainLightColorID = Shader.PropertyToID("_mainLightColor");
                
        Light light = RenderSettings.sun;
        Shader.SetGlobalVector(MainLightDirectionID, -light.transform.forward);
        Shader.SetGlobalVector(MainLightColorID, light.color.linear * light.intensity);
        
        Material mat = new Material(Shader.Find("BanaRP/LightPass"));
        
        cmd.Blit(gBufferIDs[0], BuiltinRenderTextureType.CameraTarget, mat);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        context.Submit();
    }
    
    void ShadowCastPass(ScriptableRenderContext context, Camera camera)
    {
        Profiler.BeginSample("ShadowPass");
        // light info
        Light light = RenderSettings.sun;
        Vector3 lightDir = light.transform.forward;
        
        // update shadowmap
        _csm.Update(camera, lightDir);
        renderPipelineAsset.csmSettings.Set();
        
        _csm.SaveMainCameraSettings(ref camera);
        for(int level = 0; level < 4; level++)
        {
            // 设置相机
            _csm.ConfigCameraToShadowSpace(ref camera, lightDir, level, 1024);
            
            CommandBuffer cmd = new CommandBuffer
            {
                name = "shadowmap " + level
            };
            
            cmd.BeginSample("shadowmap " + level);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 设置阴影矩阵，视锥分割参数
            Matrix4x4 viewMat = camera.worldToCameraMatrix;
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 viewProjMat = projMat * viewMat;
            Shader.SetGlobalMatrix("_shadowVpMat" + level, viewProjMat);
            Shader.SetGlobalFloat("_frustumWidth" + level, _csm.frustumWidths[level]);
            
            // 绘制准备
            context.SetupCameraProperties(camera);
            cmd.SetRenderTarget(csmShadowmaps[level]);
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 剔除
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);
            // 绘制设置
            ShaderTagId shaderTagId = new ShaderTagId("ShadowOnly");
            SortingSettings sortingSettings = new SortingSettings(camera);
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;
            
            // 绘制
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            
            cmd.EndSample("shadowmap " + level);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            context.Submit();
        }
        _csm.RevertMainCameraSettings(ref camera);
        
        Profiler.EndSample();
    }
    
    static ComputeShader FindComputeShader(string shaderName)
    {
        ComputeShader[] css = Resources.FindObjectsOfTypeAll(typeof(ComputeShader)) as ComputeShader[];
        for (int i = 0; i < css.Length; i++) 
        { 
            if (css[i].name == shaderName) 
                return css[i];
        }
        return null;
    }
}