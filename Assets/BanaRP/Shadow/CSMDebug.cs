using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ShadowCameraDebug : MonoBehaviour
{
    CSM csm;

    public bool DrawAABB = false;
    
    double RadicalInverse(int Base, int i)
    {
        double Digit, Radical, Inverse;
        Digit = Radical = 1.0 / (double) Base;
        Inverse = 0.0;
        while(i > 0)
        {
            // i余Base求出i在"Base"进制下的最低位的数
            // 乘以Digit将这个数镜像到小数点右边
            Inverse += Digit * (double) (i % Base);
            Digit *= Radical;

            // i除以Base即可求右一位的数
            i /= Base;
        }
        return Inverse;
    }
    
    double Halton(int Dimension, int Index)
    {
        // 直接用第Dimension个质数作为底数调用RadicalInverse即可
        return RadicalInverse(Dimension, Index);
    }
    
    void Update()
    {
        Camera camera = GetComponent<Camera>();

        Light light = RenderSettings.sun;
        Vector3 lightDir = light.transform.forward;
        
        // 更新 shadowmap
        if (csm == null)csm = new CSM();
        
        csm.Update(camera, lightDir);
        if (DrawAABB)
        {
            csm.DebugDraw();
        }
        
        // Halton test
        // for (int i = 1; i <= 8; i++)
        // {
        //     double a = Halton(3, i);
        //     Debug.Log("Halton 1:" + a);
        // }
    }
}
