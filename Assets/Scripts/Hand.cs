using System.Collections;
using UnityEngine;

public class Hand : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite stoppedSprite;

    [Header("Shake Effect")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.05f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Vector3 baseLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        baseLocalPosition = transform.localPosition;
        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
        }
    }

    public void Stop()
    {
        if (spriteRenderer != null && stoppedSprite != null)
        {
            spriteRenderer.sprite = stoppedSprite;
        }

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(Shake());
    }

    public void Return()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
            transform.localPosition = baseLocalPosition;
        }

        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
        }
    }

    private IEnumerator Shake()
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            transform.localPosition = baseLocalPosition + new Vector3(x, y, 0f);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = baseLocalPosition;
        shakeRoutine = null;
    }
}
