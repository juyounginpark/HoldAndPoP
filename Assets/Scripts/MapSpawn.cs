using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapSpawn : MonoBehaviour
{
    [Serializable]
    public struct BallType
    {
        public string tag;
        public Sprite sprite;
    }

    [Header("Prefab")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn Area (green object)")]
    [SerializeField] private Transform spawnArea;

    [Header("Ball Types")]
    [SerializeField] private BallType[] ballTypes;

    [Header("Row Settings")]
    [Tooltip("Max balls in a full row. Short (offset) rows will have one less.")]
    [SerializeField] private int ballsPerRow = 5;
    [Tooltip("Ball diameter when its localScale is 1. Ball prefab uses CircleCollider2D radius 0.35, so 0.7.")]
    [SerializeField] private float unitBallDiameter = 0.7f;
    [Tooltip("Vertical step per row as a multiplier of cell width. ~0.866 (sqrt(3)/2) for hex close packing.")]
    [SerializeField] private float verticalStepMultiplier = 0.8660254f;

    [Header("Push Animation")]
    [Tooltip("Duration to smoothly animate existing balls up before spawning the new row.")]
    [SerializeField] private float pushAnimationDuration = 0.15f;

    private readonly List<Transform> spawnedBalls = new List<Transform>();
    private bool nextRowIsOffset = false;
    private bool isAnimating = false;

    public float BallScale
    {
        get
        {
            float cellWidth = GetRowWidth() / ballsPerRow;
            return cellWidth / unitBallDiameter;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnRow();
        }
    }

    public void SpawnRow()
    {
        if (isAnimating) return;

        float rowWidth = GetRowWidth();
        float cellWidth = rowWidth / ballsPerRow;
        float pushAmount = cellWidth * verticalStepMultiplier;

        StartCoroutine(SpawnRowCoroutine(pushAmount, rowWidth, cellWidth));
    }

    private IEnumerator SpawnRowCoroutine(float pushAmount, float rowWidth, float cellWidth)
    {
        isAnimating = true;

        yield return AnimatePushUp(pushAmount);

        int count = nextRowIsOffset ? ballsPerRow - 1 : ballsPerRow;
        float offsetX = nextRowIsOffset ? cellWidth : 0.5f * cellWidth;

        float spawnY = spawnArea.position.y;
        float leftEdge = spawnArea.position.x - rowWidth * 0.5f;
        float scale = cellWidth / unitBallDiameter;

        for (int i = 0; i < count; i++)
        {
            float x = leftEdge + offsetX + i * cellWidth;
            Vector3 pos = new Vector3(x, spawnY, 0f);
            GameObject ball = Instantiate(ballPrefab, pos, Quaternion.identity);
            ball.transform.localScale = new Vector3(scale, scale, 1f);
            ApplyRandomType(ball);
            spawnedBalls.Add(ball.transform);
        }

        nextRowIsOffset = !nextRowIsOffset;
        isAnimating = false;
    }

    private IEnumerator AnimatePushUp(float amount)
    {
        List<Transform> toMove = new List<Transform>();
        List<Vector3> startPositions = new List<Vector3>();
        List<Rigidbody2D> rbs = new List<Rigidbody2D>();
        List<RigidbodyType2D> originalTypes = new List<RigidbodyType2D>();

        for (int i = spawnedBalls.Count - 1; i >= 0; i--)
        {
            if (spawnedBalls[i] == null)
            {
                spawnedBalls.RemoveAt(i);
                continue;
            }
            toMove.Add(spawnedBalls[i]);
            startPositions.Add(spawnedBalls[i].position);

            Rigidbody2D rb = spawnedBalls[i].GetComponent<Rigidbody2D>();
            rbs.Add(rb);
            if (rb != null)
            {
                originalTypes.Add(rb.bodyType);
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                originalTypes.Add(RigidbodyType2D.Dynamic);
            }
        }

        Vector3 offset = new Vector3(0f, amount, 0f);
        float elapsed = 0f;
        while (elapsed < pushAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pushAnimationDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < toMove.Count; i++)
            {
                if (toMove[i] != null)
                {
                    toMove[i].position = startPositions[i] + offset * eased;
                }
            }
            yield return null;
        }

        for (int i = 0; i < toMove.Count; i++)
        {
            if (toMove[i] != null)
            {
                toMove[i].position = startPositions[i] + offset;
            }
            if (rbs[i] != null)
            {
                rbs[i].bodyType = originalTypes[i];
            }
        }
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

    private float GetRowWidth()
    {
        Renderer r = spawnArea.GetComponent<Renderer>();
        if (r != null) return r.bounds.size.x;
        return ballsPerRow * unitBallDiameter;
    }
}
