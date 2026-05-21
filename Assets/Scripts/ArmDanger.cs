using UnityEngine;

public class ArmDanger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Arm transform that gets shaken. Usually the armRoot.")]
    [SerializeField] private Transform arm;
    [Tooltip("END line transform. If unassigned, looked up by tag \"END\" on Awake.")]
    [SerializeField] private Transform endLine;
    [Tooltip("Sprite renderers tinted toward dangerColor as danger rises.")]
    [SerializeField] private SpriteRenderer[] tintTargets;

    [Header("Danger Range")]
    [Tooltip("Distance from END at which shake/tint START (zero danger above this gap).")]
    [SerializeField] private float dangerStartDistance = 4f;
    [Tooltip("Distance from END at which danger reaches full intensity.")]
    [SerializeField] private float dangerMaxDistance = 0.5f;

    [Header("Shake")]
    [Tooltip("Max horizontal shake amplitude at full danger (world units).")]
    [SerializeField] private float maxShakeAmplitudeX = 0.35f;
    [Tooltip("Max vertical shake amplitude at full danger (world units).")]
    [SerializeField] private float maxShakeAmplitudeY = 0.25f;
    [Tooltip("How fast the shake oscillates. Higher = jitterier.")]
    [SerializeField] private float shakeFrequency = 30f;
    [Tooltip("Curve applied to danger ratio before driving shake amplitude. 1 = linear; >1 = stays calmer until close to END.")]
    [SerializeField] private float shakeRamp = 1.5f;

    [Header("Color")]
    [SerializeField] private Color dangerColor = new Color(1f, 0.25f, 0.25f, 1f);

    private Color[] originalColors;
    private Vector3 currentShakeOffset = Vector3.zero;
    private float noiseSeedX;
    private float noiseSeedY;
    private float currentIntensity;

    public float Intensity => currentIntensity;

    private void Awake()
    {
        TryFindEndLine();

        if (tintTargets != null && tintTargets.Length > 0)
        {
            originalColors = new Color[tintTargets.Length];
            for (int i = 0; i < tintTargets.Length; i++)
            {
                if (tintTargets[i] != null) originalColors[i] = tintTargets[i].color;
            }
        }

        noiseSeedX = Random.value * 1000f;
        noiseSeedY = Random.value * 1000f + 500f;
    }

    private void LateUpdate()
    {
        if (arm == null) return;

        float ratio = ComputeDangerRatio();

        arm.position -= currentShakeOffset;

        float intensity = ratio > 0f ? Mathf.Pow(ratio, shakeRamp) : 0f;
        currentIntensity = intensity;

        if (intensity > 0f)
        {
            float t = Time.time * shakeFrequency;
            float nx = (Mathf.PerlinNoise(noiseSeedX, t) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(noiseSeedY, t) - 0.5f) * 2f;
            currentShakeOffset = new Vector3(
                nx * maxShakeAmplitudeX * intensity,
                ny * maxShakeAmplitudeY * intensity,
                0f);
            arm.position += currentShakeOffset;
        }
        else
        {
            currentShakeOffset = Vector3.zero;
        }

        if (tintTargets != null && originalColors != null)
        {
            for (int i = 0; i < tintTargets.Length; i++)
            {
                if (tintTargets[i] == null) continue;
                tintTargets[i].color = Color.Lerp(originalColors[i], dangerColor, intensity);
            }
        }
    }

    private void TryFindEndLine()
    {
        if (endLine != null) return;
        GameObject endObj = GameObject.FindGameObjectWithTag("END");
        if (endObj != null) endLine = endObj.transform;
    }

    private float ComputeDangerRatio()
    {
        if (endLine == null)
        {
            TryFindEndLine();
            if (endLine == null) return 0f;
        }

        BallBehavior[] balls = FindObjectsByType<BallBehavior>(FindObjectsSortMode.None);
        float highestY = float.NegativeInfinity;
        foreach (BallBehavior b in balls)
        {
            if (b == null) continue;
            if (!b.tag.StartsWith("Ball_")) continue;
            float y = b.transform.position.y;
            if (y > highestY) highestY = y;
        }

        if (float.IsNegativeInfinity(highestY)) return 0f;

        float distance = endLine.position.y - highestY;
        return Mathf.Clamp01(Mathf.InverseLerp(dangerStartDistance, dangerMaxDistance, distance));
    }
}
