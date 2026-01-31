using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineFeature : ScriptableRendererFeature
{
    class OutlinePass : ScriptableRenderPass
    {
        Material outlineMaterial;
        RenderTargetIdentifier source;
        RenderTargetHandle tempTexture;

        public OutlinePass(Material material)
        {
            this.outlineMaterial = material;
            tempTexture.Init("_TemporaryColorTexture");
        }

        public void Setup(RenderTargetIdentifier source)
        {
            this.source = source;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (outlineMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Outline Pass");

            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            cmd.GetTemporaryRT(tempTexture.id, opaqueDesc, FilterMode.Bilinear);
            Blit(cmd, source, tempTexture.Identifier());
            Blit(cmd, tempTexture.Identifier(), source, outlineMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public Material outlineMaterial;
    OutlinePass outlinePass;

    public override void Create()
    {
        outlinePass = new OutlinePass(outlineMaterial);
        outlinePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        outlinePass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(outlinePass);
    }
}
