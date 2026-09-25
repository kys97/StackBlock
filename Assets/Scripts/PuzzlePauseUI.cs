using System;
using UnityEngine;

// Scene-local presentation/input only. GameManager owns the shared pause state.
public sealed class PuzzlePauseUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    private GameManager owner;
    private Func<KeyCode, bool> getKeyDown = Input.GetKeyDown;

    private void Start()
    {
        owner = GameManager.Instance;
        owner.PauseChanged += ShowPause;
        ShowPause(owner.IsPaused);
    }

    private void OnEnable()
    {
        if (owner == null) return;
        owner.PauseChanged += ShowPause;
        ShowPause(owner.IsPaused);
    }

    private void Update()
    {
        if (getKeyDown(KeyCode.Escape)) TogglePause();
    }

    public void PauseGame() => owner?.PauseGame();
    public void ResumeGame() => owner?.ResumeGame();
    public void TogglePause() => owner?.TogglePause();

    private void ShowPause(bool paused) => pausePanel.SetActive(paused);

    private void OnDisable()
    {
        if (owner == null) return;
        owner.PauseChanged -= ShowPause;
        // Also covers direct scene unloads that bypass the navigation buttons.
        owner.ResumeGame();
        if (pausePanel != null) pausePanel.SetActive(false);
    }
}
