using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fırçanın hareket ve yön kontrolünü yönetir.
/// Arena sınırları kamera boyutundan dinamik hesaplanır.
/// Her ekran boyutunda doğru çalışır.
/// </summary>
public class BrushController : MonoBehaviour
{
  [Header("Hareket Ayarları")]
  [SerializeField] private float moveSpeed = 3f;
  [SerializeField] private float turnSpeed = 120f;

  [Header("Oyuncu Ayarları")]
  [SerializeField] private bool isHumanPlayer = false;
  [SerializeField] private Color brushColor = Color.red;

  [Header("Sınır Ayarı")]
  [SerializeField] private float borderPadding = 0.2f; // Sınırdan ne kadar içeride dursun

  [Tooltip("Üstteki bar'ın RectTransform'u. Fırçanın üst sınırı bunun dünyadaki alt kenarına göre hesaplanır. Boş bırakılırsa üst sınır ekranın tepesi olur.")]
  [SerializeField] private RectTransform topBarRect;

  // Dinamik hesaplanan sınırlar
  private float minX, maxX, minY, maxY;

  // Hız çarpanı
  private float speedMultiplier = 1f;

  // Çarpışma hız cezası. 1.0 = ceza yok, 0.5 = yarı hız.
  // speedMultiplier (boost) ile ÇARPILARAK birleşir, onu ezmez.
  private float penaltyMultiplier = 1f;

  private Vector2 knockbackVelocity = Vector2.zero;

  [Header("Çarpışma Sıçraması")]
  [Tooltip("Sıçramanın ne kadar hızlı söneceği. Yüksek = çabuk durur.")]
  [SerializeField] private float knockbackDamping = 8f;

  // Input
  private Keyboard keyboard;

  private void Awake()
  {
    keyboard = Keyboard.current;
  }

  private void Start()
  {
    CalculateBounds();
  }

  /// <summary>
  /// Kamera boyutuna göre arena sınırlarını hesaplar.
  /// Ekran boyutu veya aspect ratio değişse bile doğru çalışır.
  /// </summary>
  private void CalculateBounds()
  {
    Bounds2D area = ArenaBounds.Calculate(Camera.main, topBarRect, borderPadding);
    minX = area.minX; maxX = area.maxX;
    minY = area.minY; maxY = area.maxY;
  }
  
  private void Update()
  {
    HandleInput();
    Move();
  }

  private void HandleInput()
  {
    if (!isHumanPlayer) return;

    if (keyboard == null)
    {
      keyboard = Keyboard.current;
      return;
    }

    if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
      Rotate(1f);
    else if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
      Rotate(-1f);
  }

  /// <summary>
  /// Fırçayı ilerletir. Sınır ve diğer fırçaları kontrol eder.
  /// Çarpışma efektleri ileride BrushCollision scripti ile eklenecek.
  /// </summary>
  private void Move()
  {
    // Final hız = temel hız × boost çarpanı × ceza çarpanı
    // Katmanlar birbirini ezmez, çarpım olarak birleşir.
    float speed = moveSpeed * speedMultiplier * penaltyMultiplier;
    Vector3 nextPosition = transform.position + transform.up * speed * Time.deltaTime;

    // Arena sınır kontrolü
    nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
    nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

    // Normal hareketi uygula.
    // NOT: Eski "diğer fırçaya değince return" bloğu KALDIRILDI.
    // Fırçaların ayrılması artık BrushCollision'ın knockback'i ile olur;
    // eski blok fırçaları kilitliyordu.
    transform.position = nextPosition;

    // Knockback (sıçrama) varsa uygula ve zamanla söndür.
    if (knockbackVelocity.sqrMagnitude > 0.0001f)
    {
      Vector3 knockedPos = transform.position + (Vector3)knockbackVelocity * Time.deltaTime;

      // Sıçrama da arena dışına taşmasın
      knockedPos.x = Mathf.Clamp(knockedPos.x, minX, maxX);
      knockedPos.y = Mathf.Clamp(knockedPos.y, minY, maxY);
      transform.position = knockedPos;

      // Üstel sönümleme — yumuşak yavaşlama
      knockbackVelocity = Vector2.Lerp(
          knockbackVelocity,
          Vector2.zero,
          knockbackDamping * Time.deltaTime
      );
    }
  }

  private void Rotate(float direction)
  {
    transform.Rotate(0f, 0f, direction * turnSpeed * Time.deltaTime);
  }

  public void SetSpeedMultiplier(float multiplier) => speedMultiplier = multiplier;

  /// <summary>
  /// Hız cezası uygular (örn. çarpışma sonrası 0.5 = yarı hız).
  /// Boost çarpanından bağımsızdır, onunla çarpılarak birleşir.
  /// </summary>
  public void SetPenaltyMultiplier(float multiplier)
  {
    penaltyMultiplier = multiplier;
  }

  /// <summary>
  /// Hız cezasını kaldırır (çarpanı 1.0'a döndürür).
  /// Ceza süresi bitince VEYA boost gibi bir durum cezayı iptal
  /// ettiğinde çağrılır.
  /// </summary>
  public void ClearPenalty()
  {
    penaltyMultiplier = 1f;
  }

  /// <summary>
  /// Şu an ceza uygulanıyor mu? Boost sistemi "cezalı mı" diye
  /// sorup iptal etmek isteyebilir.
  /// </summary>
  public bool HasPenalty => penaltyMultiplier < 1f;
  public Color GetBrushColor() => brushColor;
  public float GetMoveSpeed() => moveSpeed * speedMultiplier * penaltyMultiplier;

  /// <summary>
  /// Yapay zeka tarafından çağrılır. HandleInput'tan bağımsız çalışır.
  /// </summary>
  /// <param name="direction">1f = sol, -1f = sağ</param>
  public void TurnAI(float direction)
  {
    Rotate(direction);
  }

  /// <summary>
  /// AI'ın duvar kaçınması için arena sınırlarını döndürür.
  /// x = minX, y = maxX, z = minY, w = maxY
  /// </summary>
  public Vector4 GetBounds() => new Vector4(minX, maxX, minY, maxY);

  /// <summary>
  /// Çarpışma sıçraması ekler. BrushCollision tarafından çağrılır.
  /// Bu script knockback'i HESAPLAMAZ, sadece uygular ve söndürür.
  /// </summary>
  /// <param name="velocity">Sıçrama yönü ve şiddeti (yön * güç)</param>
  public void AddKnockback(Vector2 velocity)
  {
    knockbackVelocity = velocity;
  }
}