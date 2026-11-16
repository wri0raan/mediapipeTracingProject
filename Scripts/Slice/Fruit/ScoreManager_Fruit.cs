using TMPro;
using UnityEngine;

namespace Zerah.Slicer
{
    public class ScoreManager_Fruit : MonoBehaviour
    {
        public static ScoreManager_Fruit I { get; private set; }

        [Header("UI")]
        public TMP_Text scoreText;
        public string prefix = "Score  ";

        public int Score { get; private set; }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            UpdateUI();
        }

        public void ResetScore(int start = 0)
        {
            Score = start;
            UpdateUI();
        }

        public void Add(int delta = 1)
        {
            Score += delta;
            UpdateUI();
        }

        void UpdateUI()
        {
            if (scoreText != null)
                scoreText.text = $"{prefix}{Score}";
        }
    }
}
