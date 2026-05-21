using DG.Tweening;
using UnityEngine;

public class BallDrop : MonoBehaviour
{
    private enum State { Idle, Moving, Waiting, Returning, TrashStopped, Loading }

    [Header("Prefab")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Size & Type Source")]
    [Tooltip("Source of both ball scale and the BallType list. BallDrop tags carried balls with the same tag prefixed by 'P' (Ball_RED -> PBall_RED).")]
    [SerializeField] private MapSpawn mapSpawn;

    [Header("Arm & Hand")]
    [Tooltip("The moving arm transform. If unassigned, BallDrop moves its own transform.")]
    [SerializeField] private Transform arm;
    [SerializeField] private Hand hand;
    [Tooltip("Optional. When assigned, BallDrop notifies GameFlow each time the arm starts returning, allowing a pending row to spawn in sync with the return.")]
    [SerializeField] private GameFlow gameFlow;
    [Tooltip("Object active only in state 1 (default). Deactivated when arm stops.")]
    [SerializeField] private GameObject state1Object;
    [Tooltip("Optional. When assigned, shake intensity drives a per-second chance the player loses grip and drops the ball.")]
    [SerializeField] private ArmDanger armDanger;
    [Tooltip("Master toggle for the slip-from-shake feature. Wire a UI Toggle's onValueChanged to SetSlipEnabled.")]
    [SerializeField] private bool slipEnabled = true;
    [Tooltip("Average drop attempts per second when shake is at full intensity. Probability scales with intensity^slipChanceRamp.")]
    [SerializeField] private float slipChancePerSecondAtMax = 4f;
    [Tooltip("Curve applied to intensity before computing slip chance. >1 keeps slips rare until shake is severe.")]
    [SerializeField] private float slipChanceRamp = 2f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Duration for arm to return to start position after dropping a ball.")]
    [SerializeField] private float returnDuration = 0.45f;
    [Tooltip("Ease used while arm slides back to start.")]
    [SerializeField] private Ease returnEase = Ease.OutCubic;
    [Tooltip("Time AreInteractionsDone must be continuously true before returning. Prevents premature return before gravity kicks in.")]
    [SerializeField] private float settleDuration = 0.3f;
    [Tooltip("Pause duration after hitting TRASH before returning to start.")]
    [SerializeField] private float trashStopDuration = 1.5f;
    [Tooltip("How far left to pull back before spawning new ball.")]
    [SerializeField] private float pullBackDistance = 1f;
    [Tooltip("Duration of the pull-back motion (left of start) before spawning the next ball.")]
    [SerializeField] private float pullBackDuration = 0.18f;
    [Tooltip("Ease used during the pull-back phase. OutBack gives a slight overshoot.")]
    [SerializeField] private Ease pullBackEase = Ease.OutQuad;
    [Tooltip("Duration of the push-forward motion from pulled position back to start (loaded with a fresh ball).")]
    [SerializeField] private float pushForwardDuration = 0.22f;
    [Tooltip("Ease used during push-forward. OutBack gives a snappy ready pose.")]
    [SerializeField] private Ease pushForwardEase = Ease.OutBack;

    [Header("Release Dip")]
    [Tooltip("How far arm dips downward when the ball is released.")]
    [SerializeField] private float releaseDipDistance = 0.25f;
    [Tooltip("Duration of the downward dip on release.")]
    [SerializeField] private float releaseDipDownDuration = 0.08f;
    [Tooltip("Duration of rising back to release height after the dip.")]
    [SerializeField] private float releaseDipUpDuration = 0.14f;
    [Tooltip("Ease used while dipping down.")]
    [SerializeField] private Ease releaseDipDownEase = Ease.OutQuad;
    [Tooltip("Ease used while rising back up.")]
    [SerializeField] private Ease releaseDipUpEase = Ease.OutQuad;

    private Vector3 startPosition;
    private State state = State.Idle;
    private float settleTimer = 0f;
    private float trashStopTimer = 0f;
    private GameObject carriedBall;
    private bool initialized = false;
    private Tween armTween;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        if (arm == null) arm = transform;
        startPosition = arm.position;
        if (state1Object != null) state1Object.SetActive(true);
        SpawnCarriedBall();
        initialized = true;
    }

    public void SetArmPosition(Vector3 pos)
    {
        if (arm != null) arm.position = pos;
        if (carriedBall != null) carriedBall.transform.position = pos;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle:
                if (Input.GetMouseButton(0))
                {
                    state = State.Moving;
                }
                break;

            case State.Moving:
                if (Input.GetMouseButton(0))
                {
                    arm.position += new Vector3(moveSpeed * Time.deltaTime, 0f, 0f);
                    if (RollSlip()) ForceRelease();
                }
                else
                {
                    ForceRelease();
                }
                break;

            case State.Waiting:
                if (AreInteractionsDone())
                {
                    settleTimer += Time.deltaTime;
                    if (settleTimer >= settleDuration)
                    {
                        DeactivateAllPBalls();
                        BeginReturn();
                    }
                }
                else
                {
                    settleTimer = 0f;
                }
                break;

            case State.TrashStopped:
                trashStopTimer -= Time.deltaTime;
                if (trashStopTimer <= 0f)
                {
                    BeginReturn();
                }
                break;

            case State.Returning:
            case State.Loading:
                break;
        }

        if (carriedBall != null)
        {
            carriedBall.transform.position = arm.position;
        }
    }

    private void ForceRelease()
    {
        ReleaseCarriedBall();
        PlayReleaseDip();
        settleTimer = 0f;
        state = State.Waiting;
        if (hand != null) hand.Stop();
        if (state1Object != null) state1Object.SetActive(false);
    }

    private bool RollSlip()
    {
        if (!slipEnabled) return false;
        if (armDanger == null || carriedBall == null) return false;
        float intensity = armDanger.Intensity;
        if (intensity <= 0f) return false;
        float chancePerSecond = slipChancePerSecondAtMax * Mathf.Pow(intensity, slipChanceRamp);
        return UnityEngine.Random.value < chancePerSecond * Time.deltaTime;
    }

    public void SetSlipEnabled(bool enabled)
    {
        slipEnabled = enabled;
    }

    private void PlayReleaseDip()
    {
        if (arm == null) return;
        KillArmTween();
        float baseY = arm.position.y;
        Sequence seq = DOTween.Sequence();
        seq.Append(arm.DOMoveY(baseY - releaseDipDistance, releaseDipDownDuration).SetEase(releaseDipDownEase));
        seq.Append(arm.DOMoveY(baseY, releaseDipUpDuration).SetEase(releaseDipUpEase));
        armTween = seq;
    }

    private void BeginReturn()
    {
        state = State.Returning;
        if (hand != null) hand.Return();
        if (state1Object != null) state1Object.SetActive(true);
        if (gameFlow != null) gameFlow.OnArmReturning();

        KillArmTween();
        armTween = arm.DOMove(startPosition, returnDuration)
            .SetEase(returnEase)
            .OnComplete(BeginLoad);
    }

    private void BeginLoad()
    {
        state = State.Loading;
        Vector3 pullPosition = startPosition + Vector3.left * pullBackDistance;

        KillArmTween();
        Sequence seq = DOTween.Sequence();
        seq.Append(arm.DOMove(pullPosition, pullBackDuration).SetEase(pullBackEase));
        seq.AppendCallback(SpawnCarriedBall);
        seq.Append(arm.DOMove(startPosition, pushForwardDuration).SetEase(pushForwardEase));
        seq.OnComplete(() => state = State.Idle);
        armTween = seq;
    }

    private void KillArmTween()
    {
        if (armTween != null && armTween.IsActive())
        {
            armTween.Kill();
        }
        armTween = null;
    }

    private void OnDestroy()
    {
        KillArmTween();
        if (arm != null) arm.DOKill();
    }

    private void SpawnCarriedBall()
    {
        if (ballPrefab == null) return;
        carriedBall = Instantiate(ballPrefab, arm.position, Quaternion.identity);
        ApplyBallScale(carriedBall);
        ApplyRandomType(carriedBall);
        SetPhysicsEnabled(carriedBall, false);
    }

    private void ApplyBallScale(GameObject ball)
    {
        if (mapSpawn == null) return;
        float s = mapSpawn.BallScale;
        ball.transform.localScale = new Vector3(s, s, 1f);
    }

    private void ReleaseCarriedBall()
    {
        if (carriedBall == null) return;
        SetPhysicsEnabled(carriedBall, true);
        carriedBall = null;
    }

    private void ApplyRandomType(GameObject ball)
    {
        if (mapSpawn == null) return;
        MapSpawn.BallType[] types = mapSpawn.BallTypes;
        int unlocked = mapSpawn.UnlockedCount;
        if (types == null || unlocked == 0) return;

        MapSpawn.BallType type = types[UnityEngine.Random.Range(0, unlocked)];

        if (!string.IsNullOrEmpty(type.tag))
        {
            ball.tag = "P" + type.tag;
        }
        if (type.sprite != null)
        {
            SpriteRenderer sr = ball.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = type.sprite;
        }
    }

    private void SetPhysicsEnabled(GameObject ball, bool enabled)
    {
        Collider2D col = ball.GetComponent<Collider2D>();
        if (col != null) col.enabled = enabled;

        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (enabled)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.simulated = true;
            }
            else
            {
                rb.simulated = false;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrashHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTrashHit(collision.gameObject);
    }

    public void HandleTrashHit(GameObject other)
    {
        if (other == null || other.tag != "TRASH") return;
        if (state == State.TrashStopped || state == State.Returning) return;

        ReleaseCarriedBall();
        trashStopTimer = trashStopDuration;
        state = State.TrashStopped;
        if (hand != null) hand.Stop();
        if (state1Object != null) state1Object.SetActive(false);
    }

    private bool AreInteractionsDone()
    {
        if (BallBehavior.ActiveExplosions > 0) return false;

        BallBehavior[] allBalls = FindObjectsByType<BallBehavior>(FindObjectsSortMode.None);
        foreach (BallBehavior b in allBalls)
        {
            if (b == null || b == carriedBall) continue;
            Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
            if (rb == null) continue;
            if (rb.bodyType != RigidbodyType2D.Dynamic) continue;
            if (rb.linearVelocity.sqrMagnitude > 0.01f) return false;
        }
        return true;
    }

    private void DeactivateAllPBalls()
    {
        BallBehavior[] allBalls = FindObjectsByType<BallBehavior>(FindObjectsSortMode.None);
        foreach (BallBehavior bb in allBalls)
        {
            if (bb == null) continue;
            GameObject ball = bb.gameObject;
            if (ball == carriedBall) continue;
            if (ball.tag.StartsWith("PBall_"))
            {
                ball.tag = "Ball_" + ball.tag.Substring("PBall_".Length);
            }
        }
    }
}
