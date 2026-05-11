using UnityEngine;

public class GameFlow : MonoBehaviour
{
    [Header("Map Spawn")]
    [SerializeField] private MapSpawn mapSpawn;

    [Header("Spawn Timing")]
    [Tooltip("Rows pre-spawned at game start.")]
    [SerializeField] private int initialRows = 5;
    [Tooltip("Initial spawn interval in seconds (start slow).")]
    [SerializeField] private float initialInterval = 5f;
    [Tooltip("Minimum spawn interval — won't go faster than this.")]
    [SerializeField] private float minInterval = 0.8f;
    [Tooltip("Log curve weight. interval = initial - logFactor * ln(1 + elapsedTime). Larger = decreases faster.")]
    [SerializeField] private float logFactor = 0.8f;

    private float spawnTimer;
    private float elapsedTime = 0f;

    public float CurrentInterval => Mathf.Max(minInterval, initialInterval - logFactor * Mathf.Log(1f + elapsedTime));
    public float ElapsedTime => elapsedTime;

    private void Start()
    {
        if (mapSpawn != null)
        {
            for (int i = 0; i < initialRows; i++)
            {
                mapSpawn.SpawnRow();
            }
        }
        spawnTimer = initialInterval;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            if (mapSpawn != null) mapSpawn.SpawnRow();
            spawnTimer = CurrentInterval;
        }
    }
}
