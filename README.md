# BanaRenderPipeline

Custom Render Pipeline with Unity SRP, include following features:
- Defered Shading Pipeline
- PBR / IBL
- Cascaded Shadow Mapping / PCSS (with Shadow Mask optimization)
- TAA
- GPU Instance / Frustum Culling / Oclussion Culling
- Cluster Based Defered Lighting

# Gallery

#### PBR / IBL:

![PBR](Image/PBRIBL.png)

#### CSM / PCSS:

![CSM](Image/CSMPCSS.png)

#### TAA:

<img src="Image/noTAA.png" width="50%"/> <img src="Image/TAA.png" width="50%"/>

[//]: # (![noTAA]&#40;Image/noTAA.png&#41;![TAA]&#40;Image/TAA.png&#41;)

#### GPU Instance / frustum culling / oclussion culling using compute shader:

![GPUInstance](Image/GPUCull.png)

#### Cluster based defered Lighting:

![Cluster](Image/clusterLight.png)

# Reference

[1] AKG4e3, ["Unity SRP 实战（一）延迟渲染与 PBR"](https://zhuanlan.zhihu.com/p/458890891)

[2] JoeyDeVries, ["Learn OpenGL"](https://learnopengl.com/)

[3] AKG4e3, ["Unity SRP 实战（二）级联阴影贴图 CSM"](https://zhuanlan.zhihu.com/p/460945398)

[4] 銀葉吉祥, ["一起来写Unity渲染管线吧"](https://zhuanlan.zhihu.com/p/35862626)

[5] 0向往0, ["由浅入深学习PBR的原理和实现"](https://www.cnblogs.com/timlly/p/10631718.html)

[6] 王江荣, ["使用Compute Shader实现Hi-z遮挡剔除"](https://zhuanlan.zhihu.com/p/396979267)

[7] SardineFish, ["在 Unity SRP 实现 Temporal Anti-aliasing"](https://zhuanlan.zhihu.com/p/138866533)

[8] syb7384, ["URP下GPU Instance以及IndirectDraw探究"](https://www.cnblogs.com/shenyibo/p/14295047.html)

[9] Clawko, ["详解Cubemap、IBL与球谐光照"](https://zhuanlan.zhihu.com/p/463309766)

[10] KillerAery, ["实时阴影技术（1）Shadow Mapping"](https://www.cnblogs.com/KillerAery/p/15201310.html)




