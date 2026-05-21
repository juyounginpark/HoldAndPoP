using UnityEngine;

[RequireComponent(typeof(BallBehavior))]
public class SPBomb : MonoBehaviour, ISpecialBallTrigger
{
    [Tooltip("Maximum gap (world units) between colliders that still counts as touching when scanning neighbors to chain-pop.")]
    [SerializeField] private float touchTolerance = 0.1f;
    [Tooltip("Extra radius added to the OverlapCircleAll scan when chain-popping neighbors.")]
    [SerializeField] private float scanPadding = 0.5f;

    private BallBehavior myBall;
    private Collider2D myCollider;
    private bool activated = false;

    private void Awake()
    {
        myBall = GetComponent<BallBehavior>();
        myCollider = GetComponent<Collider2D>();
    }

    public void OnAdjacentBallPopping()
    {
        if (activated) return;
        Activate();
    }

    private void Activate()
    {
        activated = true;

        SpecialBallScore data = GetComponent<SpecialBallScore>();
        Sprite overlay = data != null ? data.effectSprite : null;

        float scanRadius = GetMyRadius() + touchTolerance + scanPadding;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            BallBehavior other = hit.GetComponent<BallBehavior>();
            if (other == null || other == myBall) continue;

            if (myCollider != null)
            {
                ColliderDistance2D dist = myCollider.Distance(hit);
                if (!dist.isValid || dist.distance > touchTolerance) continue;
            }

            if (overlay != null)
            {
                SpriteRenderer sr = other.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = overlay;

                other.transform.rotation = Quaternion.identity;
                Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.angularVelocity = 0f;
                    rb.freezeRotation = true;
                }
            }

            other.ForcePopOwnCluster();
        }

        if (myBall != null) myBall.ForcePopSelf();
        else Destroy(gameObject);
    }

    private float GetMyRadius()
    {
        CircleCollider2D col = myCollider as CircleCollider2D;
        float scale = transform.lossyScale.x;
        return col != null ? col.radius * scale : 0.5f * scale;
    }
}
