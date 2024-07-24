using UnityEngine;
using UnityEngine.Rendering;

public class Blur
{
    ComputeShader blurCS;
    
    public RenderTexture tempRT;
    public RenderTexture destRT;

    public Blur()
    {
        blurCS = Resources.Load<ComputeShader>("GaussBlur11x11");
        tempRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
        tempRT.enableRandomWrite = true;
        tempRT.Create();
        destRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
        destRT.enableRandomWrite = true;
        destRT.Create();
    }
    
    public void DoHorizontalBlur(ref RenderTexture source, ref RenderTexture dest)
    {
        int kid = blurCS.FindKernel("GaussianBlur1x11Main");
        
        blurCS.GetKernelThreadGroupSizes(kid, out uint x, out uint y, out uint z);
        blurCS.SetTexture(kid, "_InputTexture", source);
        blurCS.SetTexture(kid, "_OutputTexture", dest);
        
        blurCS.Dispatch(kid, 
            Mathf.CeilToInt((float)Screen.width / x), 
            Mathf.CeilToInt((float)Screen.height / y),
            1);
    }
    
    public void DoVerticalBlur(ref RenderTexture source, ref RenderTexture dest)
    {
        int kid = blurCS.FindKernel("GaussianBlur11x1Main");
        
        blurCS.GetKernelThreadGroupSizes(kid, out uint x, out uint y, out uint z);
        blurCS.SetTexture(kid, "_InputTexture", source);
        blurCS.SetTexture(kid, "_OutputTexture", dest);
        
        blurCS.Dispatch(kid, 
            Mathf.CeilToInt((float)Screen.width / x), 
            Mathf.CeilToInt((float)Screen.height / y),
            1);
    }
    
    // public void DoBlur(RenderTexture source, out RenderTexture outputRT)
    // {
    //     DoHorizontalBlur(source, tempRT);
    //     DoVerticalBlur(tempRT, destRT);
    //     outputRT = destRT;
    // }
}