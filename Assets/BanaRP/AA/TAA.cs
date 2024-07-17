using UnityEngine;

public class TAA
{
    private Vector2[] HaltonSequence = new Vector2[]
    {
        new Vector2(0.5f, 1.0f / 3),
        new Vector2(0.25f, 2.0f / 3),
        new Vector2(0.75f, 1.0f / 9),
        new Vector2(0.125f, 4.0f / 9),
        new Vector2(0.625f, 7.0f / 9),
        new Vector2(0.375f, 2.0f / 9),
        new Vector2(0.875f, 5.0f / 9),
        new Vector2(0.0625f, 8.0f / 9),
    };
    
    public Shader taaShader;

    public Material taaMaterial;
    
    private uint frameCount = 0;

    private Matrix4x4 currentJitteredVpMatrix = new Matrix4x4();
    
    private Matrix4x4 prevJitteredVpMatrix = new Matrix4x4();

    public Vector2 _Jitter;
    
    public Vector2 _prevJitter;
    
    public float BlendAlpha = 0.1f;
    
    public RenderTexture[] m_HistoryTextures = new RenderTexture[2];

    public TAA()
    {
        taaShader = Shader.Find("BanaRP/TAA");
        taaMaterial = new Material(taaShader);
    }

    public void GetHistoryBuffer(out RenderTexture historyRead, out RenderTexture historyWrite)
    {
        historyRead = m_HistoryTextures[frameCount % 2];
        if (historyRead == null || historyRead.width != Screen.width || historyRead.height != Screen.height)
        {
            if(historyRead) RenderTexture.ReleaseTemporary(historyRead);
            historyRead = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            m_HistoryTextures[frameCount % 2] = historyRead;	
        }
        historyWrite = m_HistoryTextures[(frameCount + 1) % 2];
        if (historyWrite == null || historyWrite.width != Screen.width || historyWrite.height != Screen.height)
        {
            if(historyWrite) RenderTexture.ReleaseTemporary(historyWrite);
            historyWrite = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            m_HistoryTextures[(frameCount + 1) % 2] = historyWrite;
        }
    }

    public void SetJitterCamera(ref Camera camera)
    {
        frameCount++;
        
        prevJitteredVpMatrix = currentJitteredVpMatrix;
        
        var proj = camera.projectionMatrix;

        var index = frameCount % HaltonSequence.Length;
        _prevJitter = _Jitter;
        _Jitter = new Vector2(
            (HaltonSequence[index].x - 0.5f) / Screen.width,
            (HaltonSequence[index].y - 0.5f) / Screen.height);
        proj.m02 += _Jitter.x * 2;
        proj.m12 += _Jitter.y * 2;

        camera.projectionMatrix = proj;
        
        Matrix4x4 viewMat = camera.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(proj, false);
        Matrix4x4 viewProjMat = projMat * viewMat;
        currentJitteredVpMatrix = viewProjMat;

        if (prevJitteredVpMatrix == Matrix4x4.zero)
        {
            prevJitteredVpMatrix = currentJitteredVpMatrix;
        }
    }
    
    public void GetReprojectionMat(out Matrix4x4 prevJitteredVpMatrix, out Matrix4x4 currentJitteredVpMatrix)
    {
        prevJitteredVpMatrix = this.prevJitteredVpMatrix;
        currentJitteredVpMatrix = this.currentJitteredVpMatrix;
    }

    public void RevertCamera(ref Camera camera)
    {
        camera.ResetProjectionMatrix();
    }
}