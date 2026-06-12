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

  [Header("Boyama Ayarları")]
  [SerializeField] private int textureWidth = 512;
  [SerializeField] private int textureHeight = 288;
  [SerializeField] private int brushRadius = 5;

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
    paintTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
    paintTexture.filterMode = FilterMode.Bilinear;
    paintTexture.wrapMode = TextureWrapMode.Clamp;

    pixels = new Color32[textureWidth * textureHeight];
    ClearCanvas();

    Camera cam = Camera.main;
    float camWidth = cam.orthographicSize * 2f * cam.aspect;
    float ppu = textureWidth / camWidth;

    Sprite paintSprite = Sprite.Create(
        paintTexture,
        new Rect(0, 0, textureWidth, textureHeight),
        new Vector2(0.5f, 0.5f),
        ppu
    );

    if (paintSurface != null)
      paintSurface.sprite = paintSprite;
    else
      Debug.LogError("PaintManager: PaintSurface atanmadı!");
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
    int totalPainted = 0;
    byte tolerance = 30; // 0-255 arası

    foreach (Color32 pixel in pixels)
    {
      if (pixel.a < 10) continue;
      totalPainted++;

      int diff = Mathf.Abs(pixel.r - target32.r)
               + Mathf.Abs(pixel.g - target32.g)
               + Mathf.Abs(pixel.b - target32.b);

      if (diff < tolerance) matchCount++;
    }

    if (totalPainted == 0) return 0f;
    return (float)matchCount / pixels.Length;
  }

  private Vector2Int WorldToPixel(Vector2 worldPos)
  {
    Camera cam = Camera.main;
    float halfHeight = cam.orthographicSize;
    float halfWidth = halfHeight * cam.aspect;

    float normalizedX = (worldPos.x - cam.transform.position.x + halfWidth) / (halfWidth * 2f);
    float normalizedY = (worldPos.y - cam.transform.position.y + halfHeight) / (halfHeight * 2f);

    int px = Mathf.Clamp(Mathf.RoundToInt(normalizedX * textureWidth), 0, textureWidth - 1);
    int py = Mathf.Clamp(Mathf.RoundToInt(normalizedY * textureHeight), 0, textureHeight - 1);

    return new Vector2Int(px, py);
  }

  /// <summary>
  /// Cache'lenmiş skorları döndürür. BrushAI Superhuman için.
  /// </summary>
  public float[] GetCachedScores() => cachedScores;
}