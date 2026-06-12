using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gelişmiş yapay zeka fırça kontrolcüsü.
///
/// Easy       — Çoğunlukla rastgele hareket, nadiren strateji
/// Medium     — Boyasız alana gitmeye çalışır, orta tepki süresi
/// Hard       — Agresif boyama, rakip boya alanına girer, hızlı tepki
/// Superhuman — Gerçek zamanlı skor takibi, birinci sıradaki rakibi
///              hedefler ve o bölgeyi kendi rengiyle boyar
/// </summary>
[RequireComponent(typeof(BrushController))]
public class BrushAI : MonoBehaviour
{
  public enum Difficulty { Easy, Medium, Hard, Superhuman }

  [Header("Yapay Zeka Ayarları")]
  [SerializeField] private Difficulty difficulty = Difficulty.Medium;

  private BrushController brushController;
  private float decisionTimer;
  private float currentTurnDirection = 0f;

  // Sıkışma tespiti
  private Vector3 lastPosition;
  private float stuckTimer;

  // Hedef pozisyon (Superhuman için)
  private Vector2 targetPosition;
  private bool hasTarget = false;

  // Her zorluk için karar aralığı (saniye)
  private readonly float[] decisionIntervals = { 1.0f, 0.5f, 0.2f, 0.08f };

  // Strateji kullanma olasılığı (0-1)
  private readonly float[] strategyChance = { 0.15f, 0.5f, 0.85f, 1.0f };

  private void Awake()
  {
    brushController = GetComponent<BrushController>();
  }

  private void Start()
  {
    lastPosition = transform.position;
    // Aynı anda karar vermesinler
    decisionTimer = Random.Range(0f, GetInterval());
  }

  private void Update()
  {
    if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

    DetectStuck();
    decisionTimer += Time.deltaTime;

    if (decisionTimer >= GetInterval())
    {
      decisionTimer = 0f;
      MakeDecision();
    }

    ApplyTurn();
  }

  private void MakeDecision()
  {
    switch (difficulty)
    {
      case Difficulty.Easy: DecideEasy(); break;
      case Difficulty.Medium: DecideMedium(); break;
      case Difficulty.Hard: DecideHard(); break;
      case Difficulty.Superhuman: DecideSuperhuman(); break;
    }
  }

  // ─── KOLAY ────────────────────────────────────────────────
  /// <summary>
  /// Çoğunlukla rastgele hareket eder.
  /// Nadiren önündeki alana bakar.
  /// </summary>
  private void DecideEasy()
  {
    if (Random.value < strategyChance[(int)difficulty])
      TurnTowardUnpainted(0.8f); // Kısa menzil
    else
      RandomTurn(0.6f); // Sık düz gitme
  }

  // ─── ORTA ─────────────────────────────────────────────────
  /// <summary>
  /// Boyasız alana gitmeye çalışır.
  /// Önünde boyalı alan varsa alternatif yön arar.
  /// </summary>
  private void DecideMedium()
  {
    if (Random.value < strategyChance[(int)difficulty])
      TurnTowardUnpainted(1.5f);
    else
      RandomTurn(0.4f);
  }

  // ─── ZOR ──────────────────────────────────────────────────
  /// <summary>
  /// Agresif strateji. Boyasız alana gider,
  /// sıkışırsa rakip boya alanına girerek üstüne yazar.
  /// </summary>
  private void DecideHard()
  {
    if (Random.value < strategyChance[(int)difficulty])
    {
      // Önce boyasız alan ara
      bool found = TurnTowardUnpainted(2.0f);

      // Bulamazsa rakip boyasına gir
      if (!found) TurnTowardRivalPaint();
    }
    else
    {
      RandomTurn(0.2f);
    }
  }

  // ─── İNSAN ÜSTÜ ───────────────────────────────────────────
  /// <summary>
  /// Gerçek zamanlı skor takibi yapar.
  /// En yüksek yüzdeye sahip rakibin boya bölgesini hedefler
  /// ve o bölgeyi kendi rengiyle boyar. Yüzde farkı az ise
  /// en büyük boyasız alana gider.
  /// </summary>
  private void DecideSuperhuman()
  {
    if (GameManager.Instance == null) return;

    var brushes = GameManager.Instance.GetBrushes();

    // Cache'den oku — 2 saniyede bir güncellenir, her frame hesaplanmaz
    float[] scores = PaintManager.Instance?.GetCachedScores();
    if (scores == null) return;

    int myIndex = brushes.IndexOf(brushController);
    if (myIndex < 0 || myIndex >= scores.Length) return;

    float myScore = scores[myIndex];

    int leaderIndex = -1;
    float leaderScore = -1f;

    for (int i = 0; i < scores.Length; i++)
    {
      if (i == myIndex) continue;
      if (scores[i] > leaderScore) { leaderScore = scores[i]; leaderIndex = i; }
    }

    if (leaderIndex < 0) return;

    if (leaderScore - myScore > 0.05f)
      TurnTowardPosition(brushes[leaderIndex].transform.position);
    else
      TurnTowardUnpainted(3.0f);
  }

  // ─── YARDIMCI METODLAR ────────────────────────────────────

  /// <summary>
  /// Önde, solda, sağda boyasız alan tarar ve o yöne döner.
  /// </summary>
  /// <returns>Boyasız alan bulundu mu?</returns>
  private bool TurnTowardUnpainted(float lookDistance)
  {
    Vector2 fwd = transform.up;
    Vector2 left = Quaternion.Euler(0, 0, 60f) * fwd;
    Vector2 right = Quaternion.Euler(0, 0, -60f) * fwd;

    bool fwdFree = CheckUnpainted((Vector2)transform.position + fwd * lookDistance);
    bool leftFree = CheckUnpainted((Vector2)transform.position + left * lookDistance);
    bool rightFree = CheckUnpainted((Vector2)transform.position + right * lookDistance);

    if (fwdFree) { currentTurnDirection = 0f; return true; }
    if (leftFree && !rightFree) { currentTurnDirection = 1f; return true; }
    if (rightFree && !leftFree) { currentTurnDirection = -1f; return true; }
    if (leftFree && rightFree) { currentTurnDirection = Random.value > 0.5f ? 1f : -1f; return true; }

    return false;
  }

  /// <summary>
  /// En yakın rakip boya alanına doğru döner.
  /// Hard modu için — rakip boyasının üstüne yaz.
  /// </summary>
  private void TurnTowardRivalPaint()
  {
    List<BrushController> brushes = GameManager.Instance.GetBrushes();
    Vector2 myPos = transform.position;

    BrushController nearest = null;
    float minDist = float.MaxValue;

    foreach (BrushController b in brushes)
    {
      if (b == brushController) continue;
      float dist = Vector2.Distance(myPos, b.transform.position);
      if (dist < minDist) { minDist = dist; nearest = b; }
    }

    if (nearest != null)
      TurnTowardPosition(nearest.transform.position);
  }

  /// <summary>
  /// Belirli bir dünya pozisyonuna doğru döner.
  /// </summary>
  private void TurnTowardPosition(Vector2 targetPos)
  {
    Vector2 dirToTarget = (targetPos - (Vector2)transform.position).normalized;
    Vector2 myForward = transform.up;

    float cross = myForward.x * dirToTarget.y - myForward.y * dirToTarget.x;

    if (cross > 0.1f) currentTurnDirection = 1f;
    else if (cross < -0.1f) currentTurnDirection = -1f;
    else currentTurnDirection = 0f;
  }

  /// <summary>
  /// Belirli bir pozisyonun boyasız olup olmadığını kontrol eder.
  /// </summary>
  private bool CheckUnpainted(Vector2 worldPos)
  {
    return PaintManager.Instance != null &&
           PaintManager.Instance.IsPositionUnpainted(worldPos);
  }

  /// <summary>
  /// Rastgele dönüş yapar. straightChance ile düz gitme olasılığı ayarlanır.
  /// </summary>
  private void RandomTurn(float straightChance)
  {
    float roll = Random.value;
    if (roll < straightChance) currentTurnDirection = 0f;
    else if (roll < straightChance + 0.35f) currentTurnDirection = 1f;
    else currentTurnDirection = -1f;
  }

  /// <summary>
  /// Belirli bir süre hareketsiz kalırsa zorla yön değiştirir.
  /// </summary>
  private void DetectStuck()
  {
    if (Vector3.Distance(transform.position, lastPosition) < 0.01f)
    {
      stuckTimer += Time.deltaTime;
      if (stuckTimer > 0.4f)
      {
        currentTurnDirection = Random.value > 0.5f ? 1f : -1f;
        stuckTimer = 0f;
      }
    }
    else
    {
      stuckTimer = 0f;
    }

    lastPosition = transform.position;
  }

  private void ApplyTurn()
  {
    if (Mathf.Abs(currentTurnDirection) > 0.01f)
      brushController.TurnAI(currentTurnDirection);
  }

  private float GetInterval() => decisionIntervals[(int)difficulty];

  public Difficulty GetDifficulty() => difficulty;
  public void SetDifficulty(Difficulty d)
  {
    difficulty = d;
  }
}