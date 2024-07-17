using System;
using UnityEngine;

[ExecuteAlways]
public class ClusterLightDebug : MonoBehaviour
{
    ClusterLight clusterLight;

    private void Update()
    {
        if (clusterLight == null)
        {
            clusterLight = new ClusterLight();
        }
        
        // 更新光源
        var lights = FindObjectsOfType(typeof(Light)) as Light[];
        clusterLight.UpdateLightBuffer(lights);
        
        // 划分 cluster
        Camera camera = Camera.main;
        clusterLight.ClusterGen(camera);
        
        clusterLight.LightAssign();
        
        clusterLight.DebugCluster();
        //clusterLight.DebugNormal();
        clusterLight.DebugLightAssign();
    }
}
