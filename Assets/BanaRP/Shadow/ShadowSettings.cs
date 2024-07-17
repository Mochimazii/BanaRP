using UnityEngine;

[System.Serializable]
public class ShadowSettings
{
    [Range(0.0f, 3.0f)]
    [Tooltip("光源宽度, 影响 Blocker 采样范围, 间接正比影响半影范围")]
    public float pcssWorldLightSize = 1.0f;
}