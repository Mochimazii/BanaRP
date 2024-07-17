using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class AssignTexture : MonoBehaviour
{
    struct Circle
    {
        public Vector2 origin;
        public Vector2 velocity;
        public float radius;
    }
    
    public ComputeShader computeShader;
    
    public int texResolution = 1024;

    public Color clearColor = new Color();
    public Color circleColor = new Color();

    private Circle[] circleData;
    ComputeBuffer circleBuffer;
    
    // 渲染器组件
    private Renderer rend;
    // 计算着色器内核句柄
    private int circlesHandle;
    private int clearHandle;
    // cs渲染纹理
    private RenderTexture csrenderTexture;

    private void Start()
    {
        csrenderTexture = new RenderTexture(texResolution, texResolution, 0);
        // 允许随机写入
        csrenderTexture.enableRandomWrite = true;
        // 创建渲染纹理实例
        csrenderTexture.Create();
        
        // 获取当前对象的渲染器组件
        rend = GetComponent<Renderer>();
        // 启用渲染器
        rend.enabled = true;
        
        InitComputeShader();
    }
    private void InitComputeShader()
    {
        circlesHandle = computeShader.FindKernel("Circles");
        clearHandle = computeShader.FindKernel("Clear");
        
        // 设置计算着色器中使用的常量或变量
        computeShader.SetTexture(circlesHandle, "Result", csrenderTexture);
        computeShader.SetTexture(clearHandle, "Result", csrenderTexture);
        
        computeShader.SetInt("texResolution", texResolution);
        computeShader.SetVector( "clearColor", clearColor);
        computeShader.SetVector( "circleColor", circleColor);
        
        // Buffer
        computeShader.GetKernelThreadGroupSizes(circlesHandle, out uint threadGroupSizeX, out _, out _);
        circleData = new Circle[threadGroupSizeX];
        
        float speed = 100;
        float halfSpeed = speed * 0.5f;
        float minRadius = 10.0f;
        float maxRadius = 30.0f;
        float radiusRange = maxRadius - minRadius;

        for(int i=0; i<circleData.Length; i++)
        {
            Circle circle = circleData[i];
            circle.origin.x = UnityEngine.Random.value * texResolution;
            circle.origin.y = UnityEngine.Random.value * texResolution;
            circle.velocity.x = (UnityEngine.Random.value * speed) - halfSpeed;
            circle.velocity.y = (UnityEngine.Random.value * speed) - halfSpeed;
            circle.radius = UnityEngine.Random.value * radiusRange + minRadius;
            circleData[i] = circle;
        }
        
        circleBuffer = new ComputeBuffer(circleData.Length, Marshal.SizeOf(new Circle()));
        circleBuffer.SetData(circleData);
        computeShader.SetBuffer(circlesHandle, "circlesBuffer", circleBuffer);

        // 将渲染纹理设置为材质的主纹理
        rend.material.SetTexture("_MainTex", csrenderTexture);
    }
    
    private void DispatchShader(int x, int y, int z)
    {
        computeShader.Dispatch(clearHandle, texResolution/ 8, texResolution / 8, 1);
        computeShader.SetFloat("time", Time.time);
        computeShader.Dispatch(circlesHandle, x, y, z);
    }
    
    private void Update()
    {
        DispatchShader(1, 1, 1);
    }
}