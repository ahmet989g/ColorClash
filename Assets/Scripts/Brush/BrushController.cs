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

  // Dinamik hesaplanan sınırlar
  private float minX, maxX, minY, maxY;

  // Hız çarpanı
  private float speedMultiplier = 1f;

  // Input
  private Keyboard keyboard;

  private void Awake()
  {
    keyboard = Keyboard.current;
    CalculateBounds();
  }

  /// <summary>
  /// Kamera boyutuna göre arena sınırlarını hesaplar.
  /// Ekran boyutu veya aspect ratio değişse bile doğru çalışır.
  /// </summary>
  private void CalculateBounds()
  {
    Camera cam = Camera.main;

    float halfHeight = cam.orthographicSize;
    float halfWidth = halfHeight * cam.aspect;

    // Üst sınırı TopBar kadar aşağı çek
    // 60px UI → dünya koordinatına çevir
    float topBarWorldHeight = (60f / Screen.height) * halfHeight * 2f;

    minX = cam.transform.position.x - halfWidth + borderPadding;
    maxX = cam.transform.position.x + halfWidth - borderPadding;
    minY = cam.transform.position.y - halfHeight + borderPadding;
    maxY = cam.transform.position.y + halfHeight - topBarWorldHeight - borderPadding;
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
    float speed = moveSpeed * speedMultiplier;
    Vector3 nextPosition = transform.position + transform.up * speed * Time.deltaTime;

    // Arena sınır kontrolü
    nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
    nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

    // Diğer fırçalarla çakışma kontrolü
    float brushRadius = 0.25f;
    Collider2D[] hits = Physics2D.OverlapCircleAll(nextPosition, brushRadius, LayerMask.GetMask("Default"));

    foreach (Collider2D hit in hits)
    {
      // Kendisi değil ve Brush tag'li ise
      if (hit.gameObject == gameObject) continue;
      if (!hit.CompareTag("Brush")) continue;

      // O yöne gitme — pozisyonu güncelleme
      return;
    }

    transform.position = nextPosition;
  }

  private void Rotate(float direction)
  {
    transform.Rotate(0f, 0f, direction * turnSpeed * Time.deltaTime);
  }

  public void SetSpeedMultiplier(float multiplier) => speedMultiplier = multiplier;
  public Color GetBrushColor() => brushColor;
  public float GetMoveSpeed() => moveSpeed * speedMultiplier;

  /// <summary>
  /// Yapay zeka tarafından çağrılır. HandleInput'tan bağımsız çalışır.
  /// </summary>
  /// <param name="direction">1f = sol, -1f = sağ</param>
  public void TurnAI(float direction)
  {
    Rotate(direction);
  }
}