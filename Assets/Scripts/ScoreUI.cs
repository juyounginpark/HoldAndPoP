using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [Header("Total Score Display")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Floating Score")]
    [Tooltip("Prefab placed at the ball's position. Must have a child named 'Score' with a TMP_Text component.")]
    [SerializeField] private GameObject floatingScorePrefab;
    [Tooltip("How long the floating score stays visible before being destroyed.")]
    [SerializeField] private float floatingScoreDuration = 1f;

    [Header("Scoring")]
    [Tooltip("Base points per ball. Nth ball in a chain (1-indexed) gets N * basePoints.")]
    [SerializeField] private int basePointsPerBall = 100;

    public static ScoreUI Instance { get; private set; }

    private int totalScore = 0;

    private void Awake()
    {
        Instance = this;
        UpdateScoreText();
    }

    public void AddBallScore(Vector3 worldPosition, int comboIndex)
    {
        int points = basePointsPerBall * (comboIndex + 1);
        totalScore += points;
        UpdateScoreText();
        ShowFloatingScore(worldPosition, points);
    }

    private void UpdateScoreText()
    {
        if (scoreText != null) scoreText.text = totalScore.ToString();
    }

    private void ShowFloatingScore(Vector3 pos, int points)
    {
        if (floatingScorePrefab == null) return;

        GameObject inst = Instantiate(floatingScorePrefab, pos, Quaternion.identity);
        TMP_Text tmp = inst.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = points.ToString();
        }
        else
        {
            Debug.LogWarning("ScoreUI: floating score prefab has no TMP_Text component.", inst);
        }
        Destroy(inst, floatingScoreDuration);
    }
}
