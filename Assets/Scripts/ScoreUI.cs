using DG.Tweening;
using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [Header("Total Score Display")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Floating Score")]
    [Tooltip("Prefab placed at the ball's position. Must have a child named 'Score' with a TMP_Text component.")]
    [SerializeField] private GameObject floatingScorePrefab;
    [Tooltip("How long the floating score stays visible before being destroyed.")]
    [SerializeField] private float floatingScoreDuration = 1f;
    [Tooltip("How far the floating score drifts upward in world units over its lifetime.")]
    [SerializeField] private float floatingScoreRise = 1.2f;
    [Tooltip("Peak scale multiplier reached during the pop-in.")]
    [SerializeField] private float floatingScorePopScale = 1.6f;
    [Tooltip("Duration of the initial pop-in (seconds). Must be < floatingScoreDuration.")]
    [SerializeField] private float floatingScorePopDuration = 0.22f;
    [Tooltip("How long the popped score is held at full scale before it starts drifting up (seconds).")]
    [SerializeField] private float floatingScoreHoldDuration = 0.12f;
    [Tooltip("Horizontal sway amplitude (world units) applied around the spawn X during the hold. 0 disables sway.")]
    [SerializeField] private float floatingScoreSwayAmplitude = 0.25f;
    [Tooltip("How many right-left-center cycles to play during the hold.")]
    [SerializeField] private int floatingScoreSwayCycles = 1;
    [Tooltip("Maximum rotation (degrees) for the spawn wobble. 0 disables the wobble.")]
    [SerializeField] private float floatingScoreWobbleAngle = 18f;
    [Tooltip("Brightness multiplier applied to the text on spawn before easing back to its normal color.")]
    [SerializeField] private float floatingScoreFlashBoost = 1.6f;
    [Tooltip("Duration of the fade-out at the end of the lifetime (seconds).")]
    [SerializeField] private float floatingScoreFadeDuration = 0.35f;

    [Header("Total Score Pulse")]
    [Tooltip("Which edge of the score text the pulse should grow from. Right keeps right-aligned text anchored to its right edge.")]
    [SerializeField] private PulseAnchor totalScorePulseAnchor = PulseAnchor.Right;
    [Tooltip("Temporary color flashed on the total score text when it increases.")]
    [SerializeField] private Color totalScorePulseColor = new Color(1f, 0.4f, 0.7f, 1f);
    [Tooltip("Peak scale multiplier reached during the bounce.")]
    [SerializeField] private float totalScorePunchScale = 0.4f;
    [Tooltip("Length of the bounce/jump animation (seconds).")]
    [SerializeField] private float totalScorePunchDuration = 0.35f;
    [Tooltip("Pixels the total score jumps upward before easing back down.")]
    [SerializeField] private float totalScoreJumpHeight = 18f;
    [Tooltip("How long it takes for the pink flash to ease back to the original color.")]
    [SerializeField] private float totalScoreColorReturnDuration = 0.45f;

    public enum PulseAnchor { Left, Center, Right }

    [Header("Scoring")]
    [Tooltip("Base points per ball. Nth ball in a chain (1-indexed) gets N * basePoints.")]
    [SerializeField] private int basePointsPerBall = 100;

    public static ScoreUI Instance { get; private set; }

    private int totalScore = 0;
    private Vector3 scoreTextBaseScale;
    private Vector3 scoreTextBaseLocalPos;
    private Color scoreTextBaseColor;
    private bool scoreTextBaseCached;

    public int TotalScore => totalScore;

    private void Awake()
    {
        Instance = this;
        CacheScoreTextBaseState();
        UpdateScoreText();
    }

    public void AddBallScore(Vector3 worldPosition, int comboIndex)
    {
        int points = basePointsPerBall * (comboIndex + 1);
        totalScore += points;
        UpdateScoreText();
        PulseTotalScore();
        ShowFloatingScore(worldPosition, points);
    }

    public void AddSpecialBallScore(Vector3 worldPosition, int points)
    {
        totalScore += points;
        UpdateScoreText();
        PulseTotalScore();
        ShowFloatingScore(worldPosition, points);
    }

    private void UpdateScoreText()
    {
        if (scoreText != null) scoreText.text = totalScore.ToString();
    }

    private void CacheScoreTextBaseState()
    {
        if (scoreText == null || scoreTextBaseCached) return;

        // For UI text, make sure the RectTransform pivot matches the chosen pulse anchor
        // so DOPunchScale grows from the correct edge without shifting the visual position.
        RectTransform rt = scoreText.transform as RectTransform;
        if (rt != null)
        {
            float desiredPivotX = totalScorePulseAnchor switch
            {
                PulseAnchor.Left => 0f,
                PulseAnchor.Right => 1f,
                _ => 0.5f,
            };
            if (!Mathf.Approximately(rt.pivot.x, desiredPivotX))
            {
                float pivotDeltaX = desiredPivotX - rt.pivot.x;
                Vector3 compensated = rt.localPosition + new Vector3(pivotDeltaX * rt.rect.width * rt.localScale.x, 0f, 0f);
                rt.pivot = new Vector2(desiredPivotX, rt.pivot.y);
                rt.localPosition = compensated;
            }
        }

        Transform t = scoreText.transform;
        scoreTextBaseScale = t.localScale;
        scoreTextBaseLocalPos = t.localPosition;
        scoreTextBaseColor = scoreText.color;
        scoreTextBaseCached = true;
    }

    private void PulseTotalScore()
    {
        if (scoreText == null) return;
        CacheScoreTextBaseState();

        Transform t = scoreText.transform;

        // Reset to baseline so rapid combos don't compound transforms or trap a tweened color.
        DOTween.Kill(t, false);
        DOTween.Kill(scoreText, false);
        t.localScale = scoreTextBaseScale;
        t.localPosition = scoreTextBaseLocalPos;
        scoreText.color = scoreTextBaseColor;

        // Bounce: punch scale for the "grew & shrank" feel.
        t.DOPunchScale(scoreTextBaseScale * totalScorePunchScale, totalScorePunchDuration, 6, 0.8f)
            .SetLink(scoreText.gameObject);

        // Jump: up then ease back to its anchored position.
        Vector3 jumpTarget = scoreTextBaseLocalPos + Vector3.up * totalScoreJumpHeight;
        float halfJump = totalScorePunchDuration * 0.5f;
        Sequence jump = DOTween.Sequence().SetTarget(t).SetLink(scoreText.gameObject);
        jump.Append(t.DOLocalMove(jumpTarget, halfJump).SetEase(Ease.OutQuad));
        jump.Append(t.DOLocalMove(scoreTextBaseLocalPos, halfJump).SetEase(Ease.InQuad));

        // Pink flash → ease back to the original color.
        scoreText.color = totalScorePulseColor;
        DOTween.To(() => scoreText.color, c => scoreText.color = c, scoreTextBaseColor, totalScoreColorReturnDuration)
            .SetEase(Ease.OutQuad)
            .SetTarget(scoreText)
            .SetLink(scoreText.gameObject);
    }

    private void ShowFloatingScore(Vector3 pos, int points)
    {
        if (floatingScorePrefab == null) return;

        GameObject inst = Instantiate(floatingScorePrefab, pos, Quaternion.identity);
        TMP_Text tmp = inst.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = points.ToString();
        }
        else
        {
            Debug.LogWarning("ScoreUI: floating score prefab has no TMP_Text component.", inst);
        }

        PlayFloatingScoreTween(inst.transform, tmp, pos);
    }

    private void PlayFloatingScoreTween(Transform t, TMP_Text tmp, Vector3 startPos)
    {
        Vector3 baseScale = t.localScale;
        t.localScale = Vector3.zero;

        float popUp = floatingScorePopDuration;
        float popSettle = floatingScorePopDuration * 0.45f;
        float hold = Mathf.Max(0f, floatingScoreHoldDuration);
        float spawnTime = popUp + popSettle + hold;
        float riseTime = Mathf.Max(0.05f, floatingScoreDuration - spawnTime);

        Sequence seq = DOTween.Sequence().SetTarget(t).SetLink(t.gameObject);

        // Pop in: zero -> overshoot -> settle.
        seq.Append(t.DOScale(baseScale * floatingScorePopScale, popUp).SetEase(Ease.OutBack, 3f));
        seq.Append(t.DOScale(baseScale, popSettle).SetEase(Ease.OutQuad));

        // Rotation wobble runs alongside the pop for extra juice.
        if (floatingScoreWobbleAngle > 0f)
        {
            t.DOPunchRotation(new Vector3(0f, 0f, floatingScoreWobbleAngle), popUp + popSettle, 6, 0.6f)
                .SetLink(t.gameObject);
        }

        // Hold at full size — sway left/right around the spawn X so the player sees the number clearly before it drifts.
        if (hold > 0f)
        {
            if (floatingScoreSwayAmplitude > 0f)
            {
                int cycles = Mathf.Max(1, floatingScoreSwayCycles);
                float cycleDur = hold / cycles;
                float quarter = cycleDur * 0.25f;
                for (int i = 0; i < cycles; i++)
                {
                    // center -> +amp -> -amp -> center, ending exactly on the spawn X so the rise begins cleanly.
                    seq.Append(t.DOMoveX(startPos.x + floatingScoreSwayAmplitude, quarter).SetEase(Ease.InOutSine));
                    seq.Append(t.DOMoveX(startPos.x - floatingScoreSwayAmplitude, quarter * 2f).SetEase(Ease.InOutSine));
                    seq.Append(t.DOMoveX(startPos.x, quarter).SetEase(Ease.InOutSine));
                }
            }
            else
            {
                seq.AppendInterval(hold);
            }
        }

        // Then rise — only after the spawn pop has played out.
        seq.Append(t.DOMove(startPos + Vector3.up * floatingScoreRise, riseTime).SetEase(Ease.OutCubic));

        if (tmp != null)
        {
            Color baseColor = tmp.color;
            float boost = Mathf.Max(1f, floatingScoreFlashBoost);
            Color flashColor = new Color(
                Mathf.Clamp01(baseColor.r * boost),
                Mathf.Clamp01(baseColor.g * boost),
                Mathf.Clamp01(baseColor.b * boost),
                baseColor.a);
            tmp.color = flashColor;

            // Ease flash back to base color during the pop.
            DOTween.To(() => tmp.color, c => tmp.color = c, baseColor, popUp + popSettle)
                .SetEase(Ease.OutQuad)
                .SetTarget(tmp)
                .SetLink(t.gameObject);

            float fade = Mathf.Min(floatingScoreFadeDuration, floatingScoreDuration);
            float fadeDelay = Mathf.Max(0f, floatingScoreDuration - fade);
            DOTween.To(() => tmp.color.a, a =>
            {
                Color c = tmp.color;
                c.a = a;
                tmp.color = c;
            }, 0f, fade)
                .SetDelay(fadeDelay)
                .SetEase(Ease.InQuad)
                .SetTarget(tmp)
                .SetLink(t.gameObject);
        }

        Destroy(t.gameObject, floatingScoreDuration);
    }
}
