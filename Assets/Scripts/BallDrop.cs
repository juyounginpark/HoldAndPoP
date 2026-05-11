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
        startPosition = transform.position;
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
                    transform.position += new Vector3(moveSpeed * Time.deltaTime, 0f, 0f);
                }
                else
                {
                    ReleaseCarriedBall();
                    settleTimer = 0f;
                    state = State.Waiting;
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
                }
                break;

            case State.Returning:
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    startPosition,
                    returnSpeed * Time.deltaTime);

                if (transform.position == startPosition)
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
            carriedBall.transform.position = transform.position;
        }
    }

    private IEnumerator LoadBallAnimation()
    {
        Vector3 pullPosition = startPosition + Vector3.left * pullBackDistance;

        while (transform.position != pullPosition)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                pullPosition,
                pullSpeed * Time.deltaTime);
            yield return null;
        }

        SpawnCarriedBall();

        while (transform.position != startPosition)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                startPosition,
                pullSpeed * Time.deltaTime);
            yield return null;
        }

        state = State.Idle;
    }

    private void SpawnCarriedBall()
    {
        if (ballPrefab == null) return;
        carriedBall = Instantiate(ballPrefab, transform.position, Quaternion.identity);
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

    private void HandleTrashHit(GameObject other)
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
