using System.Collections;
using UnityEngine;

public class InGameSound : MonoBehaviour
{
    public static InGameSound Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Separate AudioSource for looping BGM.")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Clips")]
    [Tooltip("Short rattle played at the start of each jarHead vibrate burst.")]
    [SerializeField] private AudioClip lidVibrateClip;
    [Tooltip("Played when the jar lid flies off.")]
    [SerializeField] private AudioClip lidPopClip;
    [Tooltip("Played when a ball pops.")]
    [SerializeField] private AudioClip ballPopClip;
    [Tooltip("Played on ball-to-ball collisions.")]
    [SerializeField] private AudioClip ballCollisionClip;
    [Tooltip("Main BGM, looped from ARM entry until game over.")]
    [SerializeField] private AudioClip mainBgmClip;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float lidVibrateVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float lidPopVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float ballPopVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float ballCollisionVolume = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float bgmTargetVolume = 0.6f;

    [Header("BGM Fade")]
    [SerializeField] private float bgmFadeInDuration = 1.5f;

    [Header("Collision Tuning")]
    [Tooltip("Below this relative speed (units/sec) the collision is silent.")]
    [SerializeField] private float collisionMinVelocity = 1f;
    [Tooltip("Relative speed at which collision reaches full volume.")]
    [SerializeField] private float collisionMaxVelocity = 6f;
    [Tooltip("Global cooldown to prevent sound spam from many simultaneous contacts.")]
    [SerializeField] private float collisionCooldown = 0.04f;

    private float lastCollisionTime = -10f;
    private Coroutine bgmFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayLidVibrate()
    {
        if (audioSource == null || lidVibrateClip == null) return;
        audioSource.PlayOneShot(lidVibrateClip, lidVibrateVolume);
    }

    public void PlayLidPop()
    {
        if (audioSource == null || lidPopClip == null) return;
        audioSource.PlayOneShot(lidPopClip, lidPopVolume);
    }

    public void PlayBallPop()
    {
        if (audioSource == null || ballPopClip == null) return;
        audioSource.PlayOneShot(ballPopClip, ballPopVolume);
    }

    public void PlayBallCollision(float impactSpeed)
    {
        if (audioSource == null || ballCollisionClip == null) return;
        if (impactSpeed < collisionMinVelocity) return;
        if (Time.time - lastCollisionTime < collisionCooldown) return;
        lastCollisionTime = Time.time;

        float t = Mathf.InverseLerp(collisionMinVelocity, collisionMaxVelocity, impactSpeed);
        float volume = ballCollisionVolume * Mathf.Lerp(0.3f, 1f, t);
        audioSource.PlayOneShot(ballCollisionClip, volume);
    }

    public void PlayMainBgmFadeIn()
    {
        if (bgmSource == null || mainBgmClip == null) return;

        bgmSource.clip = mainBgmClip;
        bgmSource.loop = true;
        bgmSource.volume = 0f;
        bgmSource.Play();

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(FadeBgm(bgmTargetVolume, bgmFadeInDuration));
    }

    public void StopMainBgm()
    {
        if (bgmSource == null) return;
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }
        bgmSource.Stop();
    }

    private IEnumerator FadeBgm(float targetVolume, float duration)
    {
        float startVolume = bgmSource.volume;
        if (duration <= 0f)
        {
            bgmSource.volume = targetVolume;
            bgmFadeRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, t / duration);
            yield return null;
        }
        bgmSource.volume = targetVolume;
        bgmFadeRoutine = null;
    }
}
