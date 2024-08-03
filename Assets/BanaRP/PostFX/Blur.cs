using UnityEngine;
using UnityEngine.Rendering;

public class Blur
{
    ComputeShader blurCS;
    
    public RenderTexture tempRT;
    public RenderTexture destRT;
    
    private ComputeBuffer gaussianWeightsBuffer;

    public Blur()
    {
        blurCS = Resources.Load<ComputeShader>("GaussBlur11x11");
        // tempRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
        // tempRT.enableRandomWrite = true;
        // tempRT.Create();
        // destRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
        // destRT.enableRandomWrite = true;
        // destRT.Create();
    }
    
    public float[] CalculateGaussianWeights(int radius, float sigma)
    {
        float[] weights = new float[radius * 2 + 1];
        float twoSigmaSquare = 2.0f * sigma * sigma;
        float sigmaRoot = Mathf.Sqrt(twoSigmaSquare * Mathf.PI);
        float total = 0.0f;
        for (int i = -radius; i <= radius; i++)
        {
            float distance = i * i;
            int index = i + radius;
            weights[index] = Mathf.Exp(-distance / twoSigmaSquare) / sigmaRoot;
            total += weights[index];
        }
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }
        
        // set weights to struct buffer
        gaussianWeightsBuffer = new ComputeBuffer(weights.Length, sizeof(float));
        
        return weights;
    }

    public void CreateTextures()
    {
        if (tempRT == null || tempRT.width != Screen.width || tempRT.height != Screen.height)
        {
            if(tempRT) RenderTexture.ReleaseTemporary(tempRT);
            tempRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            tempRT.enableRandomWrite = true;
            tempRT.Create();
        }

        if (destRT == null || destRT.width != Screen.width || destRT.height != Screen.height)
        {
            if (destRT) RenderTexture.ReleaseTemporary(destRT);
            destRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            destRT.enableRandomWrite = true;
            destRT.Create();
        }
    }
    
    public void DoHorizontalBlur(CommandBuffer cmd, ref RenderTexture source, ref RenderTexture dest)
    {
        int kid = blurCS.FindKernel("GaussianBlur1x11Main");
        
        blurCS.GetKernelThreadGroupSizes(kid, out uint x, out uint y, out uint z);
        cmd.SetComputeTextureParam(blurCS, kid, "_InputTexture", source);
        cmd.SetComputeTextureParam(blurCS, kid, "_OutputTexture", dest);
        cmd.DispatchCompute(blurCS, kid,
            Mathf.CeilToInt((float)Screen.width / x),
            Mathf.CeilToInt((float)Screen.height / y),
            1);
        // blurCS.SetTexture(kid, "_InputTexture", source);
        // blurCS.SetTexture(kid, "_OutputTexture", dest);
        //
        // blurCS.Dispatch(kid, 
        //     Mathf.CeilToInt((float)Screen.width / x), 
        //     Mathf.CeilToInt((float)Screen.height / y),
        //     1);
    }
    
    public void DoVerticalBlur(CommandBuffer cmd, ref RenderTexture source, ref RenderTexture dest)
    {
        int kid = blurCS.FindKernel("GaussianBlur11x1Main");
        
        // blurCS.GetKernelThreadGroupSizes(kid, out uint x, out uint y, out uint z);
        // blurCS.SetTexture(kid, "_InputTexture", source);
        // blurCS.SetTexture(kid, "_OutputTexture", dest);
        //
        // blurCS.Dispatch(kid, 
        //     Mathf.CeilToInt((float)Screen.width / x), 
        //     Mathf.CeilToInt((float)Screen.height / y),
        //     1);
        blurCS.GetKernelThreadGroupSizes(kid, out uint x, out uint y, out uint z);
        cmd.SetComputeTextureParam(blurCS, kid, "_InputTexture", source);
        cmd.SetComputeTextureParam(blurCS, kid, "_OutputTexture", dest);
        cmd.DispatchCompute(blurCS, kid,
            Mathf.CeilToInt((float)Screen.width / x),
            Mathf.CeilToInt((float)Screen.height / y),
            1);
    }
    
    public void Execute(CommandBuffer cmd, ref RenderTexture source, ref RenderTexture dest)
    {
        DoHorizontalBlur(cmd, ref source, ref tempRT);
        DoVerticalBlur(cmd, ref tempRT, ref dest);
    }
    
}