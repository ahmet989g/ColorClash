using UnityEngine;

/// <summary>
/// Texture2D üzerine direkt piksel yazarak boyama yapar.
/// Performans optimizasyonları:
/// - Color32 kullanır (Color'dan 4x hızlı)
/// - isDirty flag ile sadece değişince GPU'ya yükler
/// - Skor hesabı cache'lenir, her frame yapılmaz
/// </summary>
public class PaintManager : MonoBehaviour
{
  public static PaintManager Instance { get; private set; }

  [Header("Referanslar")]
  [SerializeField] private SpriteRenderer paintSurface;

  [Tooltip("Üst bar. Boyama alanı bunun altına hizalanır.")]
  [SerializeField] private RectTransform topBarRect;

  [Header("Boyama Ayarları")]
  [Tooltip("Texture'ın DİKEY çözünürlüğü (sabit). Yatay, ekran oranına göre türetilir.")]
  [SerializeField] private int textureBaseHeight = 540;
  [Tooltip("Fırça yarıçapı (piksel).")]
  [SerializeField] private int brushRadius = 16;

  // Türetilen çözünürlük (Start'ta hesaplanır)
  private int textureWidth;
  private int textureHeight;

  // Oynanabilir alan sınırları — bir kez hesaplanıp saklanır.
  // WorldToPixel/PixelToWorld bunu kullanır (her seferinde ölçüm yok).
  private Bounds2D playableArea;

  // Color32 kullanıyoruz — Color (float×4) yerine byte×4, 4x daha hızlı
  private Texture2D paintTexture;
  private Color32[] pixels;
  private bool isDirty = false;

  // Skor cache — her frame hesaplamak yerine belirli aralıkla güncellenir
  private float[] cachedScores;
  private float scoreCacheTimer = 0f;
  private const float ScoreCacheInterval = 2f; // 2 saniyede bir güncelle

  private void Awake()
  {
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
  }

  private void Start()
  {
    InitializeTexture();
    cachedScores = new float[4];
  }

  private void LateUpdate()
  {
    // Sadece değişiklik varsa GPU'ya yükle
    if (!isDirty) return;

    paintTexture.SetPixels32(pixels);
    paintTexture.Apply(false); // false = mipmap yenileme, performans için
    isDirty = false;
  }

  private void Update()
  {
    // Skoru belirli aralıklarla cache'le
    // Sürekli 2M piksel taramak yerine 2 saniyede bir hesapla
    if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

    scoreCacheTimer += Time.deltaTime;
    if (scoreCacheTimer >= ScoreCacheInterval)
    {
      scoreCacheTimer = 0f;
      RefreshScoreCache();
    }
  }

  /// <summary>
  /// Texture'ı oluşturur ve PaintSurface sprite'ına atar.
  /// </summary>
  private void InitializeTexture()
  {
    Camera cam = Camera.main;

    // Oynanabilir alanı bir kez hesapla ve sakla.
    // padding = 0: texture tam alanı kaplasın (fırça zaten içeride durur).
    playableArea = ArenaBounds.Calculate(cam, topBarRect, 0f);

    // Çözünürlüğü oynanabilir alanın oranına göre türet.
    // Yükseklik taban değer, genişlik alanın en-boy oranına göre.
    textureHeight = textureBaseHeight;
    float areaAspect = playableArea.Width / playableArea.Height;
    textureWidth = Mathf.RoundToInt(textureHeight * areaAspect);

    // Performans koruması
    textureWidth = Mathf.Clamp(textureWidth, 256, 2048);

    paintTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
    paintTexture.filterMode = FilterMode.Bilinear;
    paintTexture.wrapMode = TextureWrapMode.Clamp;

    pixels = new Color32[textureWidth * textureHeight];
    ClearCanvas();

    // Sprite'ın PPU'su: texture genişliği / alan genişliği (dünya birimi).
    float ppu = textureWidth / playableArea.Width;

    Sprite paintSprite = Sprite.Create(
        paintTexture,
        new Rect(0, 0, textureWidth, textureHeight),
        new Vector2(0.5f, 0.5f),
        ppu
    );

    if (paintSurface != null)
    {
      paintSurface.sprite = paintSprite;
      // Boyama yüzeyini oynanabilir alanın merkezine konumla —
      // ArenaScaler zeminiyle birebir üst üste gelsin.
      paintSurface.transform.position = new Vector3(
          playableArea.Center.x, playableArea.Center.y, paintSurface.transform.position.z);
    }
    else
    {
      Debug.LogError("PaintManager: PaintSurface atanmadı!");
    }
  }

  /// <summary>
  /// Verilen dünya pozisyonuna daire şeklinde renk boyar.
  /// </summary>
  public void Paint(Vector2 worldPos, Color color, float sizeMultiplier = 1f)
  {
    Vector2Int pixelPos = WorldToPixel(worldPos);

    // Color → Color32 dönüşümü (byte tabanlı, çok daha hızlı)
    Color32 c32 = color;

    int radius = Mathf.RoundToInt(brushRadius * sizeMultiplier);
    int radiusSq = radius * radius;

    for (int x = -radius; x <= radius; x++)
    {
      for (int y = -radius; y <= radius; y++)
      {
        if (x * x + y * y > radiusSq) continue;

        int px = pixelPos.x + x;
        int py = pixelPos.y + y;

        if (px < 0 || px >= textureWidth) continue;
        if (py < 0 || py >= textureHeight) continue;

        pixels[py * textureWidth + px] = c32;
      }
    }

    isDirty = true;
  }

  /// <summary>
  /// Cache'lenmiş skoru döndürür.
  /// AI ve UI tarafından kullanılır — her çağrıda hesap yapmaz.
  /// </summary>
  public float GetColorPercentage(Color targetColor)
  {
    if (GameManager.Instance == null) return 0f;

    var brushes = GameManager.Instance.GetBrushes();
    for (int i = 0; i < brushes.Count; i++)
    {
      if (brushes[i].GetBrushColor() == targetColor)
        return i < cachedScores.Length ? cachedScores[i] : 0f;
    }

    // Cache'de yoksa direkt hesapla (round sonu için)
    return CalculateColorPercentage(targetColor);
  }

  /// <summary>
  /// Round bitiminde kesin skor için direkt hesaplar.
  /// Oyun içinde değil, sadece round sonunda çağrılmalı.
  /// </summary>
  public float GetFinalColorPercentage(Color targetColor)
  {
    return CalculateColorPercentage(targetColor);
  }

  /// <summary>
  /// Verilen pozisyonun boyasız olup olmadığını döndürür.
  /// Piksel array'den direkt okur — hızlı.
  /// </summary>
  public bool IsPositionUnpainted(Vector2 worldPos)
  {
    Vector2Int p = WorldToPixel(worldPos);
    int index = p.y * textureWidth + p.x;

    if (index < 0 || index >= pixels.Length) return false;
    return pixels[index].a < 10; // Color32'de alpha 0-255
  }

  /// <summary>
  /// Canvas'ı temizler. Round başlangıcında çağrılır.
  /// </summary>
  public void ClearCanvas()
  {
    Color32 clear = new Color32(0, 0, 0, 0);
    for (int i = 0; i < pixels.Length; i++)
      pixels[i] = clear;

    isDirty = true;
  }

  /// <summary>
  /// Tüm fırçaların skorunu toplu hesaplar.
  /// 2 saniyede bir çağrılır — sürekli değil.
  /// </summary>
  private void RefreshScoreCache()
  {
    if (GameManager.Instance == null) return;

    var brushes = GameManager.Instance.GetBrushes();

    for (int i = 0; i < brushes.Count && i < cachedScores.Length; i++)
    {
      cachedScores[i] = CalculateColorPercentage(brushes[i].GetBrushColor());
    }
  }

  /// <summary>
  /// Belirli bir rengin piksel oranını hesaplar.
  /// Pahalı işlem — direkt çağırmak yerine cache kullan.
  /// </summary>
  private float CalculateColorPercentage(Color targetColor)
  {
    Color32 target32 = targetColor;
    int matchCount = 0;
    byte tolerance = 30;

    foreach (Color32 pixel in pixels)
    {
      if (pixel.a < 10) continue; // Boyasız piksel atla

      int diff = Mathf.Abs(pixel.r - target32.r)
               + Mathf.Abs(pixel.g - target32.g)
               + Mathf.Abs(pixel.b - target32.b);

      if (diff < tolerance) matchCount++;
    }

    // Toplam alanın yüzdesi (Splatoon mantığı): boş alan da paydada.
    // Artık ölü bölge olmadığı için tüm pikseller gerçekten boyanabilir.
    return (float)matchCount / pixels.Length;
  }

  private Vector2Int WorldToPixel(Vector2 worldPos)
  {
    // Dünya pozisyonunu oynanabilir alan içinde 0..1'e normalize et.
    float normalizedX = (worldPos.x - playableArea.minX) / playableArea.Width;
    float normalizedY = (worldPos.y - playableArea.minY) / playableArea.Height;

    int px = Mathf.Clamp(Mathf.RoundToInt(normalizedX * textureWidth), 0, textureWidth - 1);
    int py = Mathf.Clamp(Mathf.RoundToInt(normalizedY * textureHeight), 0, textureHeight - 1);

    return new Vector2Int(px, py);
  }

  /// <summary>
  /// Cache'lenmiş skorları döndürür. BrushAI Superhuman için.
  /// </summary>
  public float[] GetCachedScores() => cachedScores;

  /// <summary>
  /// Verilen pozisyonun belirtilen renkle boyalı olup olmadığını döndürür.
  /// Superhuman AI'ın "liderin boyasını" bulması için kullanılır.
  /// IsPositionUnpainted'in tersi mantık + renk eşleştirme.
  /// </summary>
  /// <param name="worldPos">Kontrol edilecek dünya pozisyonu</param>
  /// <param name="targetColor">Aranan renk (örn. liderin rengi)</param>
  /// <returns>O piksel hedef renkle boyalıysa true</returns>
  public bool IsPositionPaintedWith(Vector2 worldPos, Color targetColor)
  {
    Vector2Int p = WorldToPixel(worldPos);
    int index = p.y * textureWidth + p.x;

    if (index < 0 || index >= pixels.Length) return false;

    Color32 pixel = pixels[index];
    if (pixel.a < 10) return false; // Boyasız

    // Renk toleranslı karşılaştırma (boyamadaki anti-alias için)
    Color32 target32 = targetColor;
    int diff = Mathf.Abs(pixel.r - target32.r)
             + Mathf.Abs(pixel.g - target32.g)
             + Mathf.Abs(pixel.b - target32.b);

    return diff < 30; // CalculateColorPercentage ile aynı tolerans
  }

  /// <summary>
  /// Verilen pozisyona en yakın, hedef renkle boyalı dünya noktasını bulur.
  /// Tüm texture'ı taramak yerine, merkezden dışa doğru genişleyen
  /// halkalar halinde örnekleme yapar (spiral arama) — performanslı.
  ///
  /// Superhuman AI bunu kullanarak liderin boyadığı en yakın bölgeyi
  /// hedefler ve üstüne kendi rengiyle yazar.
  /// </summary>
  /// <param name="fromWorldPos">Arama merkezi (genelde AI'ın konumu)</param>
  /// <param name="targetColor">Aranan boya rengi (liderin rengi)</param>
  /// <param name="maxSearchRadius">Maksimum arama yarıçapı (dünya birimi)</param>
  /// <param name="hitPos">Bulunan nokta (out)</param>
  /// <returns>Hedef renkle boyalı bir nokta bulunduysa true</returns>
  public bool TryFindNearestPaintedPosition(
      Vector2 fromWorldPos,
      Color targetColor,
      float maxSearchRadius,
      out Vector2 hitPos)
  {
    hitPos = fromWorldPos;

    Color32 target32 = targetColor;

    // Dünya birimini piksele çevirmek için ölçek
    Camera cam = Camera.main;
    float worldWidth = cam.orthographicSize * 2f * cam.aspect;
    float pixelsPerWorldUnit = textureWidth / worldWidth;

    int maxRadiusPx = Mathf.RoundToInt(maxSearchRadius * pixelsPerWorldUnit);
    Vector2Int center = WorldToPixel(fromWorldPos);

    // Merkezden dışa doğru halka halka tara.
    // Adım > 1 → her pikseli değil, seyrek örnekle (performans).
    const int ringStep = 4;   // Halkalar arası piksel atlaması
    const int angleStep = 12; // Halka üzerinde derece atlaması

    for (int r = ringStep; r <= maxRadiusPx; r += ringStep)
    {
      for (int deg = 0; deg < 360; deg += angleStep)
      {
        float rad = deg * Mathf.Deg2Rad;
        int px = center.x + Mathf.RoundToInt(Mathf.Cos(rad) * r);
        int py = center.y + Mathf.RoundToInt(Mathf.Sin(rad) * r);

        if (px < 0 || px >= textureWidth) continue;
        if (py < 0 || py >= textureHeight) continue;

        Color32 pixel = pixels[py * textureWidth + px];
        if (pixel.a < 10) continue; // Boyasız

        int diff = Mathf.Abs(pixel.r - target32.r)
                 + Mathf.Abs(pixel.g - target32.g)
                 + Mathf.Abs(pixel.b - target32.b);

        if (diff < 30)
        {
          // Pikseli dünya pozisyonuna geri çevir
          hitPos = PixelToWorld(px, py);
          return true; // En yakın halkadaki ilk eşleşme yeterli
        }
      }
    }

    return false; // Arama yarıçapında liderin boyası yok
  }

  /// <summary>
  /// Piksel koordinatını dünya pozisyonuna çevirir.
  /// WorldToPixel'in tersi.
  /// </summary>
  private Vector2 PixelToWorld(int px, int py)
  {
    float normalizedX = (float)px / textureWidth;
    float normalizedY = (float)py / textureHeight;

    float worldX = playableArea.minX + normalizedX * playableArea.Width;
    float worldY = playableArea.minY + normalizedY * playableArea.Height;

    return new Vector2(worldX, worldY);
  }
}