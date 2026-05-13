using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Head : MonoBehaviour
{
    [Header("Alpha Gradient")]
    [Range(0f, 1f)] [SerializeField] private float topAlpha = 1f;
    [Range(0f, 1f)] [SerializeField] private float bottomAlpha = 0f;

    private static readonly int TopAlphaID = Shader.PropertyToID("_TopAlpha");
    private static readonly int BottomAlphaID = Shader.PropertyToID("_BottomAlpha");

    private SpriteRenderer sr;
    private Material runtimeMaterial;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        Shader gradShader = Shader.Find("Custom/SpriteAlphaGradient");
        if (gradShader == null)
        {
            Debug.LogError("[Head] Shader 'Custom/SpriteAlphaGradient' not found.", this);
            return;
        }
        runtimeMaterial = new Material(gradShader);
        sr.material = runtimeMaterial;
        Apply();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && runtimeMaterial != null) Apply();
    }

    private void Apply()
    {
        runtimeMaterial.SetFloat(TopAlphaID, topAlpha);
        runtimeMaterial.SetFloat(BottomAlphaID, bottomAlpha);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }
}
