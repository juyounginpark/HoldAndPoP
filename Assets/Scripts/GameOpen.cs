using System.Collections;
using UnityEngine;

public class GameOpen : MonoBehaviour
{
    [Header("Jar Head")]
    [Tooltip("The lid/cap object that vibrates and pops off at intro.")]
    [SerializeField] private Transform jarHead;

    [Header("Disabled During Intro")]
    [Tooltip("Arm visual root — hidden via SetActive(false) during intro.")]
    [SerializeField] private GameObject armRoot;
    [SerializeField] private BallDrop ballDrop;
    [SerializeField] private GameFlow gameFlow;
    [SerializeField] private MapSpawn mapSpawn;

    [Header("Intro Timing")]
    [Tooltip("Delay before the open sequence begins.")]
    [SerializeField] private float initialDelay = 1f;
    [Tooltip("How many times to play the jar-open sound.")]
    [SerializeField] private int lidSoundRepeats = 2;
    [Tooltip("Delay between consecutive jar-open sound plays.")]
    [SerializeField] private float lidSoundInterval = 0.6f;

    [Header("Vibrate Phase (lid loosening — stepped)")]
    [Tooltip("How many vibrate→step-up cycles before the pop.")]
    [SerializeField] private int loosenCycles = 4;
    [Tooltip("Duration of each vibrate burst.")]
    [SerializeField] private float vibrateBurstDuration = 0.6f;
    [Tooltip("Duration of each y step-up animation between bursts.")]
    [SerializeField] private float stepUpDuration = 0.25f;
    [Tooltip("Y distance gained per step-up.")]
    [SerializeField] private float stepUpDistance = 0.18f;
    [Tooltip("Horizontal shake amplitude during a burst.")]
    [SerializeField] private float shakeMagnitudeX = 0.05f;
    [Tooltip("Extra vertical jitter during a burst.")]
    [SerializeField] private float shakeMagnitudeY = 0.03f;
    [Tooltip("Shake frequency in Hz.")]
    [SerializeField] private float shakeFrequency = 25f;

    [Header("Pop Phase (lid flies off)")]
    [SerializeField] private float popDistance = 8f;
    [SerializeField] private float popSpeed = 20f;

    [Header("Initial Ball Drop")]
    [Tooltip("Position the sequential drop starts from.")]
    [SerializeField] private Transform startSpawn;
    [Tooltip("Position the sequential drop ends at — balls spawn along Start→End.")]
    [SerializeField] private Transform endSpawn;
    [Tooltip("How many balls to drop in the initial sequence.")]
    [SerializeField] private int initialBallCount = 5;
    [Tooltip("Delay between each ball drop.")]
    [SerializeField] private float dropInterval = 0.2f;
    [Tooltip("Initial speed (units/sec) given to each ball toward EndSpawn — mass-independent.")]
    [SerializeField] private float dropSpeed = 5f;

    [Header("Settle Wait")]
    [Tooltip("Continuous time balls must be still before ARM entry begins.")]
    [SerializeField] private float settleHoldDuration = 0.5f;
    [Tooltip("Velocity squared below this is considered still.")]
    [SerializeField] private float settleVelocityThreshold = 0.01f;

    [Header("Arm Entry")]
    [Tooltip("Offset from start position where ARM begins its entrance (should be offscreen).")]
    [SerializeField] private Vector3 armEntryOffset = new Vector3(-8f, 0f, 0f);
    [Tooltip("Speed of slide-in from entry offset to start position.")]
    [SerializeField] private float armEntrySpeed = 10f;

    [Header("End Line")]
    [Tooltip("Game-over line — hidden until ARM entry completes.")]
    [SerializeField] private GameObject endLine;

    private Vector3 armStartPosition;

    private void Awake()
    {
        if (armRoot != null)
        {
            armStartPosition = armRoot.transform.position;
            armRoot.SetActive(false);
        }
        if (ballDrop != null) ballDrop.enabled = false;
        if (gameFlow != null)
        {
            gameFlow.SkipInitialSpawn();
            gameFlow.enabled = false;
        }
        if (mapSpawn != null) mapSpawn.enabled = false;
        if (endLine != null) endLine.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        yield return new WaitForSeconds(initialDelay);

        if (jarHead != null)
        {
            Vector3 baseLocal = jarHead.localPosition;
            Vector3 anchor = baseLocal;

            StartCoroutine(PlayLidSoundRepeated());

            for (int i = 0; i < loosenCycles; i++)
            {
                float elapsed = 0f;
                while (elapsed < vibrateBurstDuration)
                {
                    elapsed += Time.deltaTime;
                    float phase = elapsed * shakeFrequency * 2f * Mathf.PI;
                    float xShake = Mathf.Sin(phase) * shakeMagnitudeX;
                    float yJitter = Mathf.Sin(phase * 0.5f) * shakeMagnitudeY;
                    jarHead.localPosition = anchor + new Vector3(xShake, yJitter, 0f);
                    yield return null;
                }

                Vector3 stepStart = anchor;
                Vector3 stepEnd = anchor + new Vector3(0f, stepUpDistance, 0f);
                float st = 0f;
                while (st < stepUpDuration)
                {
                    st += Time.deltaTime;
                    float k = Mathf.Clamp01(st / stepUpDuration);
                    float eased = 1f - Mathf.Pow(1f - k, 3f);
                    jarHead.localPosition = Vector3.Lerp(stepStart, stepEnd, eased);
                    yield return null;
                }
                anchor = stepEnd;
                jarHead.localPosition = anchor;
            }

            if (InGameSound.Instance != null) InGameSound.Instance.PlayLidPop();

            Vector3 popTarget = anchor + new Vector3(0f, popDistance, 0f);
            while ((jarHead.localPosition - popTarget).sqrMagnitude > 0.0001f)
            {
                jarHead.localPosition = Vector3.MoveTowards(
                    jarHead.localPosition, popTarget, popSpeed * Time.deltaTime);
                yield return null;
            }

            jarHead.gameObject.SetActive(false);
        }

        if (mapSpawn != null && startSpawn != null && initialBallCount > 0)
        {
            Vector3 spawnPos = startSpawn.position;
            Vector2 forceDir = Vector2.zero;
            if (endSpawn != null)
            {
                Vector3 delta = endSpawn.position - startSpawn.position;
                if (delta.sqrMagnitude > 0.0001f) forceDir = ((Vector2)delta).normalized;
            }

            for (int i = 0; i < initialBallCount; i++)
            {
                GameObject ball = mapSpawn.SpawnSingleBall(spawnPos);
                if (ball != null && forceDir != Vector2.zero)
                {
                    Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = forceDir * dropSpeed;
                }
                yield return new WaitForSeconds(dropInterval);
            }
        }

        float settleTimer = 0f;
        while (settleTimer < settleHoldDuration)
        {
            if (AreBallsSettled()) settleTimer += Time.deltaTime;
            else settleTimer = 0f;
            yield return null;
        }

        if (mapSpawn != null) mapSpawn.enabled = true;
        if (gameFlow != null) gameFlow.enabled = true;

        if (armRoot != null)
        {
            armRoot.transform.position = armStartPosition;
            armRoot.SetActive(true);

            if (ballDrop != null) ballDrop.Initialize();

            Vector3 entryStart = armStartPosition + armEntryOffset;
            if (ballDrop != null) ballDrop.SetArmPosition(entryStart);
            else armRoot.transform.position = entryStart;

            Vector3 current = entryStart;
            while ((current - armStartPosition).sqrMagnitude > 0.0001f)
            {
                current = Vector3.MoveTowards(
                    current, armStartPosition, armEntrySpeed * Time.deltaTime);
                if (ballDrop != null) ballDrop.SetArmPosition(current);
                else armRoot.transform.position = current;
                yield return null;
            }
            if (ballDrop != null) ballDrop.SetArmPosition(armStartPosition);
            else armRoot.transform.position = armStartPosition;
        }

        if (ballDrop != null) ballDrop.enabled = true;
        if (endLine != null) endLine.SetActive(true);
        if (InGameSound.Instance != null) InGameSound.Instance.PlayMainBgmFadeIn();
    }

    private IEnumerator PlayLidSoundRepeated()
    {
        for (int i = 0; i < lidSoundRepeats; i++)
        {
            if (InGameSound.Instance != null) InGameSound.Instance.PlayLidVibrate();
            if (i < lidSoundRepeats - 1) yield return new WaitForSeconds(lidSoundInterval);
        }
    }

    private bool AreBallsSettled()
    {
        if (BallBehavior.ActiveExplosions > 0) return false;

        BallBehavior[] balls = FindObjectsByType<BallBehavior>(FindObjectsSortMode.None);
        foreach (BallBehavior b in balls)
        {
            if (b == null) continue;
            Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
            if (rb == null) continue;
            if (rb.bodyType != RigidbodyType2D.Dynamic) continue;
            if (rb.linearVelocity.sqrMagnitude > settleVelocityThreshold) return false;
        }
        return true;
    }
}
