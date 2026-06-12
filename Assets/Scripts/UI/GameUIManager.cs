using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Oyun içi UI'ı yönetir.
/// Timer baloncukları, round ve mod bilgisini günceller.
/// </summary>
public class GameUIManager : MonoBehaviour
{
  public static GameUIManager Instance { get; private set; }

  [Header("Referanslar")]
  [SerializeField] private TextMeshProUGUI roundText;
  [SerializeField] private TextMeshProUGUI modeText;
  [SerializeField] private RectTransform timerContainer;
  [SerializeField] private GameObject timerBubblePrefab;

  [Header("Timer Ayarları")]
  [SerializeField] private int bubbleCount = 20;         // Toplam balon sayısı
  [SerializeField] private Color bubbleActiveColor = Color.white;
  [SerializeField] private Color bubbleInactiveColor = new Color(1f, 1f, 1f, 0.2f);

  // Oluşturulan balonların listesi
  private List<Image> bubbles = new List<Image>();

  private void Awake()
  {
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
  }

  private void Start()
  {
    CreateBubbles();
    UpdateRoundText(1);
  }

  private void Update()
  {
    if (GameManager.Instance == null) return;
    if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

    UpdateTimerBubbles();
  }

  /// <summary>
  /// Baloncukları başlangıçta oluşturur.
  /// </summary>
  private void CreateBubbles()
  {
    // Önce varsa temizle
    foreach (Transform child in timerContainer)
      Destroy(child.gameObject);

    bubbles.Clear();

    for (int i = 0; i < bubbleCount; i++)
    {
      GameObject bubble = Instantiate(timerBubblePrefab, timerContainer);
      Image img = bubble.GetComponent<Image>();
      img.color = bubbleActiveColor;
      bubbles.Add(img);
    }
  }

  /// <summary>
  /// Kalan süreye göre balonları söndürür.
  /// Soldan sağa sırayla söner.
  /// </summary>
  private void UpdateTimerBubbles()
  {
    float remaining = GameManager.Instance.RemainingTime;
    float total = 60f;
    float ratio = remaining / total;           // 1.0 → 0.0
    int activeBubbles = Mathf.CeilToInt(ratio * bubbleCount);

    for (int i = 0; i < bubbles.Count; i++)
    {
      // Soldan sağa söner — son aktif balon önce söner
      bubbles[i].color = i < activeBubbles
          ? bubbleActiveColor
          : bubbleInactiveColor;
    }
  }

  /// <summary>
  /// Round numarasını günceller. GameManager tarafından çağrılır.
  /// </summary>
  public void UpdateRoundText(int round)
  {
    if (roundText != null)
      roundText.text = $"ROUND {round:D2}";
  }

  /// <summary>
  /// Mod metnini günceller.
  /// </summary>
  public void SetModeText(string mode)
  {
    if (modeText != null)
      modeText.text = mode;
  }
}