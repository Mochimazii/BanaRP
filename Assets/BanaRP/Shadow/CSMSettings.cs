using UnityEngine;

[System.Serializable]
public class CSMSettings
{
    public bool useShadowMask = false;
    public ShadowSettings level0;
    public ShadowSettings level1;
    public ShadowSettings level2;
    public ShadowSettings level3;
    
    public void Set()
    {
        ShadowSettings[] levels = {level0, level1, level2, level3};
        float[] pcssWorldLightSize = new float[4];
        for(int i=0; i<4; i++)
        {
            pcssWorldLightSize[i] = levels[i].pcssWorldLightSize;
        }
        Shader.SetGlobalFloatArray("_pcssWorldLightSize", pcssWorldLightSize);
        Shader.SetGlobalFloat("_useShadowMask", useShadowMask ? 1.0f : 0.0f);
        // for(int i=0; i<4; i++)
        // {
        //     Shader.SetGlobalFloat("_pcssWorldLightSize"+i, levels[i].pcssWorldLightSize);
        // }
    }
}