using System;
using System.Collections;
using UnityEngine;

public class BallDrop : MonoBehaviour
{
    private enum State { Idle, Moving, Waiting, Returning, TrashStopped, Loading }

    [Serializable]
    public struct BallType
    {
        public string tag;
        public Sprite sprite;
    }

    [Header("Prefab")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Size Reference")]
    [SerializeField] private MapSpawn mapSpawn;

    [Header("Arm & Hand")]
    [Tooltip("The moving arm transform. If unassigned, BallDrop moves its own transform.")]
    [SerializeField] private Transform arm;
    [SerializeField] private Hand hand;
    [Tooltip("Object active only in state 1 (default). Deactivated when arm stops.")]
    [SerializeField] private GameObject state1Object;

    [Header("Ball Types")]
    [SerializeField] private BallType[] ballTypes;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float returnSpeed = 8f;
    [Tooltip("Time AreInteractionsDone must be continuously true before returning. Prevents premature return before gravity kicks in.")]
    [SerializeField] private float settleDuration = 0.3f;
    [Tooltip("Pause duration after hitting TRASH before returning to start.")]
    [SerializeField] private float trashStopDuration = 1.5f;
    [Tooltip("How far left to pull back before spawning new ball.")]
    [SerializeField] private float pullBackDistance = 1f;
    [Tooltip("Speed of the pull-back / return-to-ready motion.")]
    [SerializeField] private float pullSpeed = 6f;

    private Vector3 startPosition;
    private State state = State.Idle;
    private float settleTimer = 0f;
    private float trashStopTimer = 0f;
    private GameObject carriedBall;

    private void Start()
    {
        if (arm == null) arm = transform;
        startPosition = arm.position;
        if (state1Object != null) state1Object.SetActive(true);
        SpawnCarriedBall();
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
                }
                else
                {
                    ReleaseCarriedBall();
                    settleTimer = 0f;
                    state = State.Waiting;
                    if (hand != null) hand.Stop();
                    if (state1Object != null) state1Object.SetActive(false);
                }
                break;

            case State.Waiting:
                if (AreInteractionsDone())
                {
                    settleTimer += Time.deltaTime;
                    if (settleTimer >= settleDuration)
                    {
                        DeactivateAllPBalls();
                        state = State.Returning;
                        if (hand != null) hand.Return();
                        if (state1Object != null) state1Object.SetActive(true);
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
                    state = State.Returning;
                    if (hand != null) hand.Return();
                    if (state1Object != null) state1Object.SetActive(true);
                }
                break;

            case State.Returning:
                arm.position = Vector3.MoveTowards(
                    arm.position,
                    startPosition,
                    returnSpeed * Time.deltaTime);

                if (arm.position == startPosition)
                {
                    state = State.Loading;
                    StartCoroutine(LoadBallAnimation());
                }
                break;

            case State.Loading:
                break;
        }

        if (carriedBall != null)
        {
            carriedBall.transform.position = arm.position;
        }
    }

    private IEnumerator LoadBallAnimation()
    {
        Vector3 pullPosition = startPosition + Vector3.left * pullBackDistance;

        while (arm.position != pullPosition)
        {
            arm.position = Vector3.MoveTowards(
                arm.position,
                pullPosition,
                pullSpeed * Time.deltaTime);
            yield return null;
        }

        SpawnCarriedBall();

        while (arm.position != startPosition)
        {
            arm.position = Vector3.MoveTowards(
                arm.position,
                startPosition,
                pullSpeed * Time.deltaTime);
            yield return null;
        }

        state = State.Idle;
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
        if (ballTypes == null || ballTypes.Length == 0) return;

        BallType type = ballTypes[UnityEngine.Random.Range(0, ballTypes.Length)];

        if (!string.IsNullOrEmpty(type.tag))
        {
            ball.tag = type.tag;
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

        if (carriedBall != null)
        {
            Destroy(carriedBall);
            carriedBall = null;
        }
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

    private static readonly (string from, string to)[] PBallToBall = new (string, string)[]
    {
        ("PBall_RED",   "Ball_RED"),
        ("PBall_GREEN", "Ball_GREEN"),
        ("PBall_BLUE",  "Ball_BLUE"),
    };

    private void DeactivateAllPBalls()
    {
        foreach (var pair in PBallToBall)
        {
            GameObject[] balls = GameObject.FindGameObjectsWithTag(pair.from);
            foreach (GameObject ball in balls)
            {
                if (ball == carriedBall) continue;
                ball.tag = pair.to;
            }
        }
    }
}
