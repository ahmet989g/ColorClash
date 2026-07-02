using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Oyunun ana yöneticisi.
/// Round sistemi, oyun durumu ve fırça yönetimini üstlenir.
/// İlk frame'de kamera hazır olmayabileceği için
/// RepositionBrushes bir frame beklenerek çağrılır.
/// </summary>
public class GameManager : MonoBehaviour
{
  public static GameManager Instance { get; private set; }

  [Header("Oyun Ayarları")]
  [SerializeField] private int totalRounds = 3;
  [SerializeField] private float roundDuration = 60f;

  [Header("Fırça Referansları")]
  [SerializeField] private List<BrushController> brushes = new List<BrushController>();

  public enum GameState { WaitingToStart, Playing, RoundEnd, GameEnd }
  public GameState CurrentState { get; private set; } = GameState.WaitingToStart;

  public int CurrentRound { get; private set; } = 1;
  public float RemainingTime { get; private set; }

  private int[] roundWins;

  // Köşe oranları — kamera boyutuna göre ölçeklenir
  private readonly Vector2[] cornerOffsets = new Vector2[]
  {
        new Vector2(-0.75f,  0.75f),  // Sol Üst  (P1)
        new Vector2( 0.75f,  0.75f),  // Sağ Üst  (P2)
        new Vector2(-0.75f, -0.75f),  // Sol Alt   (P3)
        new Vector2( 0.75f, -0.75f),  // Sağ Alt   (P4)
  };

  private void Awake()
  {
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
  }

  private void Start()
  {
    roundWins = new int[brushes.Count];

    // Kamera aspect ratio'su ilk frame'de hazır olmayabilir.
    // Bir frame bekleyip konumlandırıyoruz.
    StartCoroutine(StartFirstRound());
  }

  /// <summary>
  /// İlk round'u bir frame bekleyerek başlatır.
  /// Kameranın aspect ratio'su bu sayede kesin doğru olur.
  /// </summary>
  private IEnumerator StartFirstRound()
  {
    yield return null; // Bir frame bekle
    StartRound();
  }

  private void Update()
  {
    if (CurrentState != GameState.Playing) return;

    RemainingTime -= Time.deltaTime;

    if (RemainingTime <= 0f)
    {
      RemainingTime = 0f;
      EndRound();
    }
  }

  public void StartRound()
  {
    CurrentState = GameState.Playing;
    RemainingTime = roundDuration;
    BoostManager.Instance.StartNewRound();

    if (PaintManager.Instance != null)
      PaintManager.Instance.ClearCanvas();

    // UI güncelle
    if (GameUIManager.Instance != null)
    {
      GameUIManager.Instance.UpdateRoundText(CurrentRound);
      GameUIManager.Instance.SetModeText("BATTLE MOD");
    }

    RepositionBrushes();
    Debug.Log($"Round {CurrentRound} başladı!");
  }

  private void EndRound()
  {
    CurrentState = GameState.RoundEnd;

    float[] scores = new float[brushes.Count];
    float highestScore = 0f;
    int winnerIndex = 0;

    for (int i = 0; i < brushes.Count; i++)
    {
      // Round sonunda kesin hesap yap
      scores[i] = PaintManager.Instance.GetFinalColorPercentage(brushes[i].GetBrushColor());
      if (scores[i] > highestScore) { highestScore = scores[i]; winnerIndex = i; }
    }

    roundWins[winnerIndex]++;

    for (int i = 0; i < brushes.Count; i++)
      Debug.Log($"Fırça {i + 1}: %{scores[i] * 100f:F1}");

    Debug.Log($"Round {CurrentRound} kazananı: Fırça {winnerIndex + 1}");

    if (CurrentRound >= totalRounds) EndGame();
    else NextRound();
  }

  private void NextRound()
  {
    CurrentRound++;
    StartRound();
  }

  private void EndGame()
  {
    CurrentState = GameState.GameEnd;

    int maxWins = 0;
    int gameWinner = 0;

    for (int i = 0; i < roundWins.Length; i++)
      if (roundWins[i] > maxWins) { maxWins = roundWins[i]; gameWinner = i; }

    Debug.Log($"Oyun kazananı: Fırça {gameWinner + 1} ({maxWins} round)");
  }

  /// <summary>
  /// Her fırçayı köşe pozisyonuna yerleştirir ve merkeze döndürür.
  /// Kamera boyutundan dinamik hesaplanır.
  /// </summary>
  private void RepositionBrushes()
  {
    if (brushes.Count == 0) return;

    Camera cam = Camera.main;
    float halfHeight = cam.orthographicSize;
    float halfWidth = halfHeight * cam.aspect;

    for (int i = 0; i < brushes.Count; i++)
    {
      if (i >= cornerOffsets.Length) break;

      Vector3 startPos = new Vector3(
          cam.transform.position.x + halfWidth * cornerOffsets[i].x,
          cam.transform.position.y + halfHeight * cornerOffsets[i].y,
          0f
      );

      brushes[i].transform.position = startPos;

      // Merkeze bak
      Vector2 dirToCenter = (Vector2)cam.transform.position - (Vector2)startPos;
      float angle = Mathf.Atan2(dirToCenter.y, dirToCenter.x) * Mathf.Rad2Deg - 90f;
      brushes[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
  }

  public List<BrushController> GetBrushes() => brushes;
  public int[] GetRoundWins() => roundWins;
  public int GetTotalRounds() => totalRounds;

  /// <summary>
  /// Tüm fırçaların güncel boyama yüzdelerini döndürür.
  /// BrushAI Superhuman modu tarafından kullanılır.
  /// </summary>
  public float[] GetAllScores()
  {
    float[] scores = new float[brushes.Count];
    for (int i = 0; i < brushes.Count; i++)
      scores[i] = PaintManager.Instance != null
          ? PaintManager.Instance.GetColorPercentage(brushes[i].GetBrushColor())
          : 0f;
    return scores;
  }
}