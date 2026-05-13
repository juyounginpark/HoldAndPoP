using UnityEngine;

public class ArmCollisionForwarder : MonoBehaviour
{
    [SerializeField] private BallDrop ballDrop;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ballDrop != null) ballDrop.HandleTrashHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (ballDrop != null) ballDrop.HandleTrashHit(collision.gameObject);
    }
}
