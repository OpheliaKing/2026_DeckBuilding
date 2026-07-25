using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 타이틀 UI.
    /// 새 게임은 항상 표시, 이어하기(_continueButton)는 세이브가 있을 때만 활성화한다.
    /// </summary>
    public class StartUI : UIBase
    {
        [SerializeField]
        private GameObject _continueButton;

        public void Setup(bool hasSaveData)
        {
            if (_continueButton != null)
                _continueButton.SetActive(hasSaveData);
        }

        public void OnClickStartButton()
        {
            CloseSelf();

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StartUI] GameManager.Instance가 없습니다.");
                return;
            }

            gameManager.OnTitleNewGameClicked();
        }

        public void OnClickContinueButton()
        {
            CloseSelf();

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StartUI] GameManager.Instance가 없습니다.");
                return;
            }

            gameManager.OnTitleContinueClicked();
        }

        public void OnClickOptionButton()
        {
            // TODO: 옵션 UI
        }

        public void OnClickQuitButton()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CloseSelf()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
