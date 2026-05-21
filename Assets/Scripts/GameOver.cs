using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text finalScoreText;

    public static GameOver Instance { get; private set; }

    private bool isOver = false;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void TriggerGameOver()
    {
        if (isOver) return;
        isOver = true;

        if (InGameSound.Instance != null) InGameSound.Instance.StopMainBgm();

        Time.timeScale = 0f;

        if (panel != null) panel.SetActive(true);
        if (finalScoreText != null && ScoreUI.Instance != null)
        {
            finalScoreText.text = ScoreUI.Instance.TotalScore.ToString();
        }
    }

    public void TryAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
