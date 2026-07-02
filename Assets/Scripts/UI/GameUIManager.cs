using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Oyun içi UI'ı yönetir.
/// Timer baloncukları (ampul mantığı), round ve mod bilgisini günceller.
///
/// Baloncuk sistemi:
/// - 20 baloncuk, her biri 3 saniye → toplam 60 sn
/// - Sağdan sola söner (en sağdaki ilk söner)
/// - Boost baloncukları (45/30/15 sn kala) mavi ampul gösterir
/// - Üç görsel durum: sarı (aktif), mavi (aktif boost), sönük (geçmiş)
/// </summary>
public class GameUIManager : MonoBehaviour
{
  public static GameUIManager Instance { get; private set; }

  [Header("Referanslar")]
  [SerializeField] private TextMeshProUGUI roundText;
  [SerializeField] private TextMeshProUGUI modeText;
  [SerializeField] private RectTransform timerContainer;
  [SerializeField] private GameObject timerBubblePrefab;

  [Header("Baloncuk Sprite'ları")]
  [Tooltip("Aktif normal baloncuk (sarı ampul).")]
  [SerializeField] private Sprite bulbOnSprite;
  [Tooltip("Aktif boost baloncuğu (mavi ampul).")]
  [SerializeField] private Sprite bulbBoostOnSprite;
  [Tooltip("Sönük baloncuk (geçmiş).")]
  [SerializeField] private Sprite bulbOffSprite;

  [Header("Timer Ayarları")]
  [Tooltip("Toplam baloncuk sayısı.")]
  [SerializeField] private int bubbleCount = 20;
  [Tooltip("Her baloncuğun temsil ettiği süre (sn).")]
  [SerializeField] private float secondsPerBubble = 3f;

  // Boost anlarını temsil eden baloncuk index'leri (45/30/15 sn kala).
  // secondsPerBubble = 3 için: i = 5, 10, 15.
  // Boost saniyeleri (kalan süre cinsinden). Değiştirmek istersen buradan.
  [Tooltip("Bu saniyeler KALA baloncuk mavi (boost) gösterir.")]
  [SerializeField] private float[] boostSecondsRemaining = { 45f, 30f, 15f };

  // Oluşturulan baloncukların Image referansları
  private readonly List<Image> bubbles = new List<Image>();

  // Hangi baloncuk index'i boost (mavi) — Start'ta bir kez hesaplanır.
  private HashSet<int> boostBubbleIndices = new HashSet<int>();

  private void Awake()
  {
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
  }

  private void Start()
  {
    CalculateBoostIndices();
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
  /// Boost saniyelerini (45/30/15) baloncuk index'lerine çevirir.
  ///
  /// Sağdan sönme mantığı: i. baloncuk, kalan süre (bubbleCount - i) *
  /// secondsPerBubble değerini temsil eder. Boost saniyesine denk gelen
  /// index mavi olur.
  ///
  /// Örn: 15 sn kala → (20 - i) * 3 = 15 → i = 15.
  /// </summary>
  private void CalculateBoostIndices()
  {
    boostBubbleIndices.Clear();

    foreach (float sec in boostSecondsRemaining)
    {
      // (bubbleCount - i) * secondsPerBubble = sec  →  i = bubbleCount - sec / secondsPerBubble
      int index = bubbleCount - Mathf.RoundToInt(sec / secondsPerBubble);

      if (index >= 0 && index < bubbleCount)
        boostBubbleIndices.Add(index);
    }
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
      bubbles.Add(img);
    }

    // Başlangıçta hepsi aktif (round henüz başlamadıysa da dolu görünsün)
    RefreshAllBubbles(bubbleCount);
  }

  /// <summary>
  /// Kalan süreye göre baloncukları günceller. Sağdan sola söner.
  /// </summary>
  private void UpdateTimerBubbles()
  {
    float remaining = GameManager.Instance.RemainingTime;

    // Kaç baloncuk hâlâ aktif olmalı?
    // Kalan süre / baloncuk süresi → yukarı yuvarla (kısmi baloncuk yanık kalsın)
    int activeBubbles = Mathf.CeilToInt(remaining / secondsPerBubble);
    activeBubbles = Mathf.Clamp(activeBubbles, 0, bubbleCount);

    RefreshAllBubbles(activeBubbles);
  }

  /// <summary>
  /// Tüm baloncukların görselini verilen aktif sayıya göre ayarlar.
  ///
  /// Sağdan sönme: soldaki (küçük index) baloncuklar en son söner.
  /// i < activeBubbles → aktif (yanık), değilse sönük.
  /// Aktif olan boost index'iyse mavi, değilse sarı.
  /// </summary>
  private void RefreshAllBubbles(int activeBubbles)
  {
    for (int i = 0; i < bubbles.Count; i++)
    {
      bool isActive = i < activeBubbles;

      if (!isActive)
      {
        bubbles[i].sprite = bulbOffSprite;
      }
      else if (boostBubbleIndices.Contains(i))
      {
        bubbles[i].sprite = bulbBoostOnSprite; // mavi
      }
      else
      {
        bubbles[i].sprite = bulbOnSprite;      // sarı
      }
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