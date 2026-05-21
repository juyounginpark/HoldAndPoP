using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallBehavior : MonoBehaviour
{
    [Header("Pop Animation")]
    [SerializeField] private float popScaleMultiplier = 1.4f;
    [SerializeField] private float popDuration = 0.25f;
    [SerializeField] private float chainDelay = 0.08f;

    [Header("Group Detection")]
    [SerializeField] private int minGroupSize = 3;
    [Tooltip("Extra distance beyond self radius for neighbor detection. Smaller = stricter touching requirement.")]
    [SerializeField] private float neighborPadding = 0.05f;

    private static readonly Dictionary<string, string> BallToPBall = new Dictionary<string, string>
    {
        { "Ball_RED",   "PBall_RED"   },
        { "Ball_GREEN", "PBall_GREEN" },
        { "Ball_BLUE",  "PBall_BLUE"  },
    };

    public static int ActiveExplosions { get; private set; } = 0;

    private bool isExploding = false;

    public bool IsExploding => isExploding;

    private void OnDestroy()
    {
        if (isExploding) ActiveExplosions--;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.tag == "END" && tag.StartsWith("Ball_") && GameOver.Instance != null)
        {
            GameOver.Instance.TriggerGameOver();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        string otherTag = collision.gameObject.tag;

        if (otherTag == "END")
        {
            if (tag.StartsWith("Ball_") && GameOver.Instance != null) GameOver.Instance.TriggerGameOver();
            return;
        }

        BallBehavior otherBall = collision.gameObject.GetComponent<BallBehavior>();
        bool playCollisionSound = false;
        if (otherBall != null)
        {
            if (GetInstanceID() < otherBall.GetInstanceID()) playCollisionSound = true;
        }
        else if (otherTag == "Wall")
        {
            playCollisionSound = true;
        }

        if (playCollisionSound && InGameSound.Instance != null)
        {
            InGameSound.Instance.PlayBallCollision(collision.relativeVelocity.magnitude);
        }

        if (isExploding) return;
        if (!otherTag.StartsWith("PBall_")) return;

        string otherColor = otherTag.Substring("PBall_".Length);
        string expectedBallTag = "Ball_" + otherColor;

        if (tag == expectedBallTag)
        {
            tag = otherTag;
        }
        else if (tag != otherTag)
        {
            return;
        }

        PopSameColorCluster();
    }

    private void PopSameColorCluster()
    {
        string myPBallTag = tag;
        string myBallTag = "Ball_" + myPBallTag.Substring("PBall_".Length);

        List<BallBehavior> cluster = new List<BallBehavior>();
        HashSet<BallBehavior> visited = new HashSet<BallBehavior>();
        FloodFill(this, myBallTag, myPBallTag, cluster, visited);

        foreach (BallBehavior b in cluster)
        {
            if (b.tag == myBallTag)
            {
                b.tag = myPBallTag;
            }
        }

        if (cluster.Count < minGroupSize) return;

        int comboIndex = 0;
        BallBehavior lastPopped = null;
        for (int i = 0; i < cluster.Count; i++)
        {
            BallBehavior b = cluster[i];
            if (b == null || b.isExploding) continue;
            b.isExploding = true;
            ActiveExplosions++;
            b.StartCoroutine(b.PopAndDestroy(comboIndex * chainDelay, comboIndex));
            lastPopped = b;
            comboIndex++;
        }

        NotifyAdjacentTriggers(cluster);

        if (lastPopped != null && SpecialBallFlow.Instance != null)
        {
            Vector3 lastPos = lastPopped.transform.position;
            Vector3 lastScale = lastPopped.transform.localScale;
            float popDelay = (comboIndex - 1) * chainDelay + popDuration;
            SpecialBallFlow.Instance.RequestSpecialSpawn(cluster.Count, lastPos, lastScale, popDelay);
        }
    }

    public void ForcePopOwnCluster()
    {
        if (isExploding) return;

        string myTag = tag;
        string ballTag, pballTag;
        if (myTag.StartsWith("PBall_"))
        {
            pballTag = myTag;
            ballTag = "Ball_" + myTag.Substring("PBall_".Length);
        }
        else if (myTag.StartsWith("Ball_"))
        {
            ballTag = myTag;
            pballTag = "PBall_" + myTag.Substring("Ball_".Length);
        }
        else
        {
            ForcePopSelf();
            return;
        }

        List<BallBehavior> cluster = new List<BallBehavior>();
        HashSet<BallBehavior> visited = new HashSet<BallBehavior>();
        FloodFill(this, ballTag, pballTag, cluster, visited);

        int comboIndex = 0;
        for (int i = 0; i < cluster.Count; i++)
        {
            BallBehavior b = cluster[i];
            if (b == null || b.isExploding) continue;
            b.isExploding = true;
            ActiveExplosions++;
            b.StartCoroutine(b.PopAndDestroy(comboIndex * chainDelay, comboIndex));
            comboIndex++;
        }

        NotifyAdjacentTriggers(cluster);
    }

    public void ForcePopSelf()
    {
        if (isExploding) return;
        isExploding = true;
        ActiveExplosions++;
        StartCoroutine(PopAndDestroy(0f, 0));
    }

    private void NotifyAdjacentTriggers(List<BallBehavior> popping)
    {
        if (popping == null || popping.Count == 0) return;
        HashSet<ISpecialBallTrigger> notified = new HashSet<ISpecialBallTrigger>();

        foreach (BallBehavior b in popping)
        {
            if (b == null) continue;
            float radius = GetNeighborRadius(b);
            Collider2D[] hits = Physics2D.OverlapCircleAll(b.transform.position, radius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                ISpecialBallTrigger trigger = hit.GetComponent<ISpecialBallTrigger>();
                if (trigger == null) continue;
                if (notified.Add(trigger))
                {
                    trigger.OnAdjacentBallPopping();
                }
            }
        }
    }

    private void FloodFill(BallBehavior start, string ballTag, string pballTag,
        List<BallBehavior> cluster, HashSet<BallBehavior> visited)
    {
        Queue<BallBehavior> queue = new Queue<BallBehavior>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            BallBehavior current = queue.Dequeue();
            cluster.Add(current);

            float radius = GetNeighborRadius(current);
            Collider2D[] hits = Physics2D.OverlapCircleAll(current.transform.position, radius);
            foreach (Collider2D hit in hits)
            {
                BallBehavior other = hit.GetComponent<BallBehavior>();
                if (other == null || other == current) continue;
                if (visited.Contains(other)) continue;
                if (other.tag != ballTag && other.tag != pballTag) continue;
                visited.Add(other);
                queue.Enqueue(other);
            }
        }
    }

    private float GetNeighborRadius(BallBehavior b)
    {
        CircleCollider2D col = b.GetComponent<CircleCollider2D>();
        float scale = b.transform.lossyScale.x;
        float selfRadius = col != null ? col.radius * scale : 0.5f * scale;
        return selfRadius + neighborPadding;
    }

    private IEnumerator PopAndDestroy(float delay, int comboIndex)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (InGameSound.Instance != null) InGameSound.Instance.PlayBallPop();

        yield return PopAnimation();

        if (ScoreUI.Instance != null)
        {
            SpecialBallScore special = GetComponent<SpecialBallScore>();
            if (special != null)
            {
                ScoreUI.Instance.AddSpecialBallScore(transform.position, special.points);
            }
            else
            {
                ScoreUI.Instance.AddBallScore(transform.position, comboIndex);
            }
        }

        Destroy(gameObject);
    }

    private IEnumerator PopAnimation()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 peakScale = originalScale * popScaleMultiplier;
        float half = popDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, peakScale, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(peakScale, Vector3.zero, t / half);
            yield return null;
        }
    }
}
