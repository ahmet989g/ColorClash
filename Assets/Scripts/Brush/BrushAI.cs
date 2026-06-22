using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gelişmiş yapay zeka fırça kontrolcüsü — v2 (akıcı hareket).
///
/// v1'e göre değişiklikler:
/// - Dönüş artık -1/0/1 değil, -1..1 arası SÜREKLİ değer (analog steering)
/// - Dönüş komutu yumuşatılarak uygulanır → ani kırılmalar yok
/// - Perlin gürültüsü ile organik sapma → robotik düz çizgiler yok
/// - Öngörülü duvar kaçınma → fırça duvara sürtünmez
/// - Pencere bazlı sıkışma tespiti + kaçış modu → kilitlenme yok
///
/// Easy       — Çok gürültülü, yavaş karar, kısa görüş
/// Medium     — Boyasız alana yönelir, orta tepki
/// Hard       — Agresif, rakip boyasına girer, hızlı tepki
/// Superhuman — Skor takibi yapar, lideri hedefler, neredeyse sıfır gürültü
/// </summary>
[RequireComponent(typeof(BrushController))]
public class BrushAI : MonoBehaviour
{
  public enum Difficulty { Easy, Medium, Hard, Superhuman }

  [Header("Yapay Zeka Ayarları")]
  [SerializeField] private Difficulty difficulty = Difficulty.Medium;

  [Header("Hareket Yumuşatma")]
  [Tooltip("Dönüş komutunun saniyede ne kadar değişebileceği. Düşük = yumuşak, yüksek = keskin.")]
  [SerializeField] private float steerResponse = 5f;

  [Header("Duvar Kaçınma")]
  [Tooltip("Fırçanın kaç saniye ilerisini kontrol edeceği (hızla çarpılır).")]
  [SerializeField] private float wallLookAheadTime = 0.6f;

  [Header("Sıkışma Tespiti")]
  [Tooltip("Mesafe ölçüm penceresi (saniye).")]
  [SerializeField] private float stuckCheckWindow = 0.5f;
  [Tooltip("Beklenen mesafenin bu oranından az gidildiyse sıkışmış sayılır.")]
  [SerializeField, Range(0.1f, 0.9f)] private float stuckThreshold = 0.35f;
  [Tooltip("Kaçış modunun süresi (saniye).")]
  [SerializeField] private float escapeDuration = 0.6f;

  private BrushController brushController;

  // ── Yönlendirme (sürekli değerler, -1..1) ──────────────────
  private float steerTarget;    // Karar mekanizmasının istediği dönüş
  private float steerCurrent;   // Yumuşatılmış, fiilen uygulanan dönüş
  private float noiseSeed;      // Her fırçanın kendi Perlin tohumu

  // ── Karar zamanlayıcısı ────────────────────────────────────
  private float decisionTimer;

  // ── Sıkışma tespiti (pencere bazlı) ────────────────────────
  private float windowTimer;
  private float windowDistance;
  private Vector3 lastPosition;
  private float escapeTimer;      // > 0 ise kaçış modunda
  private float escapeDirection;  // Kaçış sırasında dönülecek yön

  // ── Zorluk bazlı ayarlar (Easy, Medium, Hard, Superhuman) ──
  private readonly float[] decisionIntervals = { 0.80f, 0.45f, 0.25f, 0.12f }; // Karar sıklığı (sn)
  private readonly float[] strategyChance = { 0.25f, 0.55f, 0.85f, 1.00f }; // Strateji kullanma olasılığı
  private readonly float[] wanderNoise = { 0.45f, 0.28f, 0.15f, 0.05f }; // Organik sapma miktarı
  private readonly float[] lookDistances = { 1.0f, 1.6f, 2.2f, 3.0f };     // Boyasız alan görüş mesafesi

  // Boyasız alan taramasında kullanılan açılar.
  // Öne yakın açılar önce kontrol edilir → gereksiz sert dönüş yapılmaz.
  private static readonly float[] ScanAngles = { 0f, 25f, -25f, 55f, -55f };

  private void Awake()
  {
    brushController = GetComponent<BrushController>();
  }

  private void Start()
  {
    lastPosition = transform.position;

    // Her fırça farklı Perlin örüntüsü kullansın
    noiseSeed = Random.Range(0f, 1000f);

    // Aynı anda karar vermesinler — başlangıçta küçük faz farkı
    decisionTimer = Random.Range(0f, GetInterval());
  }

  private void Update()
  {
    if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

    float dt = Time.deltaTime;

    UpdateStuckWindow(dt);

    if (escapeTimer > 0f)
    {
      // 1) KAÇIŞ MODU — her şeyi ezer, sabit yönde tam güçle dön
      escapeTimer -= dt;
      steerTarget = escapeDirection;
    }
    else if (AvoidWalls())
    {
      // 2) DUVAR KAÇINMA — kararlardan önceliklidir
      // steerTarget AvoidWalls içinde ayarlandı, karar verme atlanır.
      // Karar zamanlayıcısını sıfırlamıyoruz; duvardan çıkınca normal akış devam eder.
    }
    else
    {
      // 3) NORMAL KARAR AKIŞI
      decisionTimer += dt;
      if (decisionTimer >= GetInterval())
      {
        decisionTimer = 0f;
        MakeDecision();
      }
    }

    ApplySteering(dt);
  }

  // ═══════════════════════════════════════════════════════════
  //  YÖNLENDİRME KATMANI
  // ═══════════════════════════════════════════════════════════

  /// <summary>
  /// Hedef dönüş değerine yumuşak geçiş yapar ve uygular.
  /// Perlin gürültüsü ile organik sapma ekler.
  /// </summary>
  private void ApplySteering(float dt)
  {
    // Perlin -1..1 aralığına çekilir; zorluk düştükçe gürültü artar.
    // Kaçış modunda gürültü kapatılır — net manevra gerekir.
    float noise = 0f;
    if (escapeTimer <= 0f)
    {
      noise = (Mathf.PerlinNoise(noiseSeed, Time.time * 0.5f) * 2f - 1f)
              * wanderNoise[(int)difficulty];
    }

    float desired = Mathf.Clamp(steerTarget + noise, -1f, 1f);

    // Ani yön değişimi yerine yumuşak geçiş — robotik kırılmaları önler
    steerCurrent = Mathf.MoveTowards(steerCurrent, desired, steerResponse * dt);

    if (Mathf.Abs(steerCurrent) > 0.01f)
      brushController.TurnAI(steerCurrent);
  }

  /// <summary>
  /// Hedefe ORANTILI döner: hedef öndeyse az, yandaysa sert dönüş.
  /// Eski cross-product eşiği hedef etrafında titreşime sebep oluyordu.
  /// </summary>
  private void SteerToward(Vector2 targetPos, float strength = 1f)
  {
    Vector2 dir = (targetPos - (Vector2)transform.position).normalized;

    // SignedAngle: pozitif = saat yönünün tersi (sol) = TurnAI(+1) ile aynı yön
    float angle = Vector2.SignedAngle(transform.up, dir);

    // 45° içinde orantılı, dışında tam dönüş
    steerTarget = Mathf.Clamp(angle / 45f * strength, -1f, 1f);
  }

  // ═══════════════════════════════════════════════════════════
  //  DUVAR KAÇINMA
  // ═══════════════════════════════════════════════════════════

  /// <summary>
  /// Fırçanın hızına göre ilerisini kontrol eder.
  /// Sınır dışına çıkacaksa arena merkezine doğru döner.
  /// </summary>
  /// <returns>Duvar tehlikesi var mı?</returns>
  private bool AvoidWalls()
  {
    float lookAhead = brushController.GetMoveSpeed() * wallLookAheadTime;
    Vector2 ahead = (Vector2)transform.position + (Vector2)transform.up * lookAhead;

    // x = minX, y = maxX, z = minY, w = maxY
    Vector4 b = brushController.GetBounds();

    bool danger = ahead.x < b.x || ahead.x > b.y ||
                  ahead.y < b.z || ahead.y > b.w;

    if (!danger) return false;

    // Merkeze dönmek köşe tuzaklarını da çözer
    Vector2 center = new Vector2((b.x + b.y) * 0.5f, (b.z + b.w) * 0.5f);
    SteerToward(center, 1.4f);
    return true;
  }

  // ═══════════════════════════════════════════════════════════
  //  SIKIŞMA TESPİTİ
  // ═══════════════════════════════════════════════════════════

  /// <summary>
  /// Pencere bazlı tespit: son X saniyede kat edilen mesafe,
  /// beklenenin altındaysa fırça sıkışmış demektir.
  /// Duvar sürtünmesini ve fırça-fırça kilitlenmesini de yakalar
  /// (anlık pozisyon farkı bunları kaçırıyordu).
  /// </summary>
  private void UpdateStuckWindow(float dt)
  {
    windowDistance += Vector3.Distance(transform.position, lastPosition);
    lastPosition = transform.position;
    windowTimer += dt;

    if (windowTimer < stuckCheckWindow) return;

    float expected = brushController.GetMoveSpeed() * stuckCheckWindow;
    if (windowDistance < expected * stuckThreshold)
      StartEscape();

    windowTimer = 0f;
    windowDistance = 0f;
  }

  /// <summary>
  /// Kaçış modunu başlatır: arena merkezi hangi taraftaysa
  /// o yöne tam güçle dönerek açık alana çıkar.
  /// </summary>
  private void StartEscape()
  {
    Vector4 b = brushController.GetBounds();
    Vector2 center = new Vector2((b.x + b.y) * 0.5f, (b.z + b.w) * 0.5f);

    Vector2 dir = (center - (Vector2)transform.position).normalized;
    float angle = Vector2.SignedAngle(transform.up, dir);

    escapeDirection = angle >= 0f ? 1f : -1f;
    escapeTimer = escapeDuration;
  }

  // ═══════════════════════════════════════════════════════════
  //  KARAR MEKANİZMASI (zorluk seviyeleri)
  // ═══════════════════════════════════════════════════════════

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

  /// <summary>Çoğunlukla rastgele gezinir, nadiren strateji kullanır.</summary>
  private void DecideEasy()
  {
    if (Random.value < strategyChance[(int)difficulty])
      TurnTowardUnpainted(lookDistances[(int)difficulty]);
    else
      RandomTurn(0.6f);
  }

  /// <summary>Boyasız alana yönelmeye çalışır.</summary>
  private void DecideMedium()
  {
    if (Random.value < strategyChance[(int)difficulty])
    {
      if (!TurnTowardUnpainted(lookDistances[(int)difficulty]))
        RandomTurn(0.4f); // Bulamazsa boş boş düz gitmesin
    }
    else
    {
      RandomTurn(0.4f);
    }
  }

  /// <summary>Agresif: boyasız alan yoksa rakip boyasının üstüne yazar.</summary>
  private void DecideHard()
  {
    if (Random.value < strategyChance[(int)difficulty])
    {
      if (!TurnTowardUnpainted(lookDistances[(int)difficulty]))
        TurnTowardRivalPaint();
    }
    else
    {
      RandomTurn(0.2f);
    }
  }

  /// <summary>
  /// Skor takibi yapar: lider belirgin öndeyse onun bölgesini hedefler,
  /// fark azsa boyasız alana yönelir.
  /// </summary>
  private void DecideSuperhuman()
  {
    if (GameManager.Instance == null || PaintManager.Instance == null) return;

    var brushes = GameManager.Instance.GetBrushes();

    // Cache'den oku — 2 saniyede bir güncellenir, her frame hesaplanmaz
    float[] scores = PaintManager.Instance.GetCachedScores();
    if (scores == null) return;

    int myIndex = brushes.IndexOf(brushController);
    if (myIndex < 0 || myIndex >= scores.Length) return;

    float myScore = scores[myIndex];

    // En yüksek skorlu rakibi (lideri) bul
    int leaderIndex = -1;
    float leaderScore = -1f;

    for (int i = 0; i < scores.Length && i < brushes.Count; i++)
    {
      if (i == myIndex) continue;
      if (scores[i] > leaderScore) { leaderScore = scores[i]; leaderIndex = i; }
    }

    if (leaderIndex < 0) return;

    Color leaderColor = brushes[leaderIndex].GetBrushColor();
    bool leaderClearlyAhead = (leaderScore - myScore) > 0.05f;

    if (leaderClearlyAhead)
    {
      // Lider önde → boyasını ez. Bulamazsan boş alana git.
      if (!TurnTowardEnemyPaint(leaderColor))
        TurnTowardUnpainted(lookDistances[(int)difficulty]);
    }
    else
    {
      // Fark az → önce verimli puan (boş alan), yoksa liderin boyasını ez
      if (!TurnTowardUnpainted(lookDistances[(int)difficulty]))
        TurnTowardEnemyPaint(leaderColor);
    }
  }

  /// <summary>
  /// Belirtilen renkle (genelde liderin rengiyle) boyalı, en yakın
  /// alanı bulur ve oraya doğru orantılı döner. Üstüne kendi rengiyle
  /// yazarak rakibin yüzdesini düşürmek için kullanılır.
  ///
  /// Lideri DEĞİL, liderin boyadığı PİKSEL alanını hedefler.
  /// </summary>
  /// <param name="enemyColor">Hedeflenecek rakip boyasının rengi</param>
  /// <returns>Hedef renkle boyalı alan bulunup yönelinebildiyse true</returns>
  private bool TurnTowardEnemyPaint(Color enemyColor)
  {
    // Görüş mesafesinin birkaç katı kadar geniş tara — boya alanı
    // fırçadan uzakta olabilir.
    float searchRadius = lookDistances[(int)difficulty] * 4f;

    if (PaintManager.Instance.TryFindNearestPaintedPosition(
            transform.position, enemyColor, searchRadius, out Vector2 hitPos))
    {
      SteerToward(hitPos);
      return true;
    }

    return false;
  }

  // ═══════════════════════════════════════════════════════════
  //  YARDIMCI METODLAR
  // ═══════════════════════════════════════════════════════════

  /// <summary>
  /// 5 açıda boyasız alan tarar (0°, ±25°, ±55°).
  /// Öne yakın açılar öncelikli — fırça gereksiz sert dönüş yapmaz.
  /// Bulunan açıya ORANTILI döner.
  /// </summary>
  /// <returns>Boyasız alan bulundu mu?</returns>
  private bool TurnTowardUnpainted(float lookDistance)
  {
    foreach (float angle in ScanAngles)
    {
      Vector2 dir = Quaternion.Euler(0f, 0f, angle) * transform.up;
      Vector2 samplePos = (Vector2)transform.position + dir * lookDistance;

      if (CheckUnpainted(samplePos))
      {
        // Açı küçükse az, büyükse çok dön
        steerTarget = Mathf.Clamp(angle / 45f, -1f, 1f);
        return true;
      }
    }

    return false;
  }

  /// <summary>En yakın rakibin pozisyonuna döner (boyasının üstüne yazmak için).</summary>
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
      SteerToward(nearest.transform.position);
  }

  /// <summary>Verilen pozisyonun boyasız olup olmadığını kontrol eder.</summary>
  private bool CheckUnpainted(Vector2 worldPos)
  {
    return PaintManager.Instance != null &&
           PaintManager.Instance.IsPositionUnpainted(worldPos);
  }

  /// <summary>
  /// Rastgele SÜREKLİ dönüş değeri seçer (eski -1/0/1 yerine).
  /// straightChance ile düz gitme olasılığı ayarlanır.
  /// </summary>
  private void RandomTurn(float straightChance)
  {
    if (Random.value < straightChance)
      steerTarget = 0f;
    else
      steerTarget = Random.Range(0.3f, 1f) * (Random.value > 0.5f ? 1f : -1f);
  }

  private float GetInterval() => decisionIntervals[(int)difficulty];

  public Difficulty GetDifficulty() => difficulty;
  public void SetDifficulty(Difficulty d) => difficulty = d;
}