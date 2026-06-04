using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button restartButton;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (messageText != null)
            messageText.text = "Elarion has fallen.\nThe chord is lost.";
    }

    private void Restart()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
