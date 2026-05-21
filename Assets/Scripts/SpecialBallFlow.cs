using System;
using System.Collections;
using UnityEngine;

public class SpecialBallFlow : MonoBehaviour
{
    public static SpecialBallFlow Instance { get; private set; }

    [Serializable]
    public struct SpecialBallEntry
    {
        [Tooltip("Tag applied to the spawned special ball.")]
        public string tag;
        [Tooltip("Exact cluster size that triggers spawning this special ball.")]
        public int clusterCount;
        [Tooltip("Sprite applied to the spawned special ball.")]
        public Sprite sprite;
        [Tooltip("Class name of the MonoBehaviour to AddComponent onto the spawned ball. Example: SPBomb. Leave empty for no behavior.")]
        public string abilityScriptName;
        [Tooltip("Score awarded when this special ball pops. Replaces the regular combo-based score.")]
        public int points;
        [Tooltip("Sprite swapped onto balls that this special ball DIRECTLY affects (e.g., bomb's touching neighbors). Cluster cascade members are NOT overlaid.")]
        public Sprite effectSprite;
    }

    [Header("Base Ball Prefab")]
    [Tooltip("Base prefab used as the template for every special ball. Tag, sprite, and ability are overridden per entry.")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Special Ball Entries")]
    [Tooltip("Round-down matching: the entry with the LARGEST clusterCount that is still <= the popped cluster size is spawned. If the cluster is smaller than every entry's clusterCount, NO special ball spawns.")]
    [SerializeField] private SpecialBallEntry[] entries;

    [Header("Spawn Timing")]
    [Tooltip("Extra delay (seconds) added after the last ball finishes popping before the special ball appears.")]
    [SerializeField] private float postPopDelay = 0f;

    [Header("Duplicate Guard")]
    [Tooltip("If a new spawn request arrives within this time AND distance of the previous, it is ignored. Prevents two requests for the same cluster from producing two special balls.")]
    [SerializeField] private float duplicateTimeWindow = 0.2f;
    [SerializeField] private float duplicateDistance = 0.5f;

    private float lastSpawnRequestTime = -999f;
    private Vector3 lastSpawnRequestPos;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RequestSpecialSpawn(int clusterSize, Vector3 position, Vector3 scale, float popDelay)
    {
        if (Time.time - lastSpawnRequestTime < duplicateTimeWindow &&
            (position - lastSpawnRequestPos).sqrMagnitude < duplicateDistance * duplicateDistance)
        {
            return;
        }

        if (!TryFindEntry(clusterSize, out SpecialBallEntry entry)) return;

        lastSpawnRequestTime = Time.time;
        lastSpawnRequestPos = position;
        StartCoroutine(SpawnAfterDelay(entry, position, scale, popDelay + postPopDelay));
    }

    private bool TryFindEntry(int clusterSize, out SpecialBallEntry entry)
    {
        if (entries == null || entries.Length == 0)
        {
            entry = default;
            return false;
        }

        int bestIndex = -1;
        int bestCount = int.MinValue;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].clusterCount > clusterSize) continue;
            if (entries[i].clusterCount > bestCount)
            {
                bestCount = entries[i].clusterCount;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            entry = default;
            return false;
        }
        entry = entries[bestIndex];
        return true;
    }

    private IEnumerator SpawnAfterDelay(SpecialBallEntry entry, Vector3 position, Vector3 scale, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SpawnSpecialBall(entry, position, scale);
    }

    private void SpawnSpecialBall(SpecialBallEntry entry, Vector3 position, Vector3 scale)
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[SpecialBallFlow] ballPrefab is null. Cannot spawn special ball.");
            return;
        }

        GameObject ball = Instantiate(ballPrefab, position, Quaternion.identity);
        ball.transform.localScale = scale;

        SpecialBallScore scoreMarker = ball.AddComponent<SpecialBallScore>();
        scoreMarker.points = entry.points;
        scoreMarker.effectSprite = entry.effectSprite;

        if (!string.IsNullOrEmpty(entry.tag)) ball.tag = entry.tag;

        if (entry.sprite != null)
        {
            SpriteRenderer sr = ball.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = entry.sprite;
        }

        if (!string.IsNullOrEmpty(entry.abilityScriptName))
        {
            System.Type type = FindAbilityType(entry.abilityScriptName);
            if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                ball.AddComponent(type);
                Debug.Log($"[SpecialBallFlow] Spawned special ball '{ball.tag}' with ability '{type.Name}' at {position}.");
            }
            else
            {
                Debug.LogError($"[SpecialBallFlow] Could not find MonoBehaviour type named '{entry.abilityScriptName}'. Check spelling.");
            }
        }
        else
        {
            Debug.LogWarning($"[SpecialBallFlow] Entry (clusterCount={entry.clusterCount}) has no abilityScriptName. Special ball will have no behavior.");
        }
    }

    private static System.Type FindAbilityType(string name)
    {
        System.Type t = System.Type.GetType(name);
        if (t != null) return t;
        foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
