using System;
using UnityEngine;

/// <summary>
/// Fırçalar arası çarpışmayı yönetir.
///
/// Görevi çarpışma ve sonrası: iki fırça değdiğinde İKİSİNİ BİRDEN
/// (simetrik) zıt yönlere sıçratır, sesi çalar, OnCollision event'ini
/// yayar VE çarpışma sonrası hız cezası + yara bandı süresini yönetir.
///
/// Hız cezası ve yara bandı AYNI timer'a bağlıdır → senkron biter.
/// Ceza, boost'tan bağımsız bir katmandır (BrushController'da ayrı
/// penaltyMultiplier). Boost gibi durumlar cezayı iptal edebilir.
///
/// Görsel zıplama ve yara bandı gösterimi gevşek bağla BrushVisual'da;
/// buradan sadece tetiklenir.
/// </summary>
[RequireComponent(typeof(BrushController))]
public class BrushCollision : MonoBehaviour
{
  [Header("Çarpışma Ayarları")]
  [Tooltip("İki fırça merkezinin çarpışma sayılacağı mesafe.")]
  [SerializeField] private float collisionRadius = 0.4f;

  [Tooltip("Çarpışma anında uygulanan sıçrama gücü.")]
  [SerializeField] private float knockbackForce = 6f;

  [Tooltip("Aynı çarpışmanın tekrar tetiklenmemesi için bekleme süresi (sn).")]
  [SerializeField] private float collisionCooldown = 0.4f;

  [Header("Hız Cezası")]
  [Tooltip("Çarpışma sonrası hız çarpanı (0.5 = yarı hız).")]
  [SerializeField] private float penaltyMultiplier = 0.5f;
  [Tooltip("Cezanın (ve yara bandının) süresi (sn).")]
  [SerializeField] private float penaltyDuration = 5f;

  [Header("Ses")]
  [Tooltip("Çarpışma anında çalınacak ses. AudioSource gerektirir.")]
  [SerializeField] private AudioClip collisionSound;
  [SerializeField] private AudioSource audioSource;

  [Header("Görsel Referansı")]
  [Tooltip("Yara bandını göster/gizle için. Atanmazsa çocukta aranır.")]
  [SerializeField] private BrushVisual brushVisual;

  /// <summary>
  /// Bu fırça çarpıştığında tetiklenir. BrushVisual (zıplama) buna abone.
  /// </summary>
  public event Action OnCollision;

  private BrushController brushController;

  // Çarpışma cooldown'u: tek çarpışmanın çift sayılmasını önler.
  private float cooldownTimer;

  // Ceza sayacı: > 0 iken fırça cezalı (yavaş + yara bandı görünür).
  // Hız cezası ve yara bandı bu TEK sayaca bağlı → senkron biter.
  private float penaltyTimer;

  // Tahsisatsız (allocation-free) tarama için önceden ayrılmış tampon.
  private static readonly Collider2D[] overlapBuffer = new Collider2D[8];

  private void Awake()
  {
    brushController = GetComponent<BrushController>();

    // Görsel referansı atanmadıysa çocuk objelerde ara.
    if (brushVisual == null)
      brushVisual = GetComponentInChildren<BrushVisual>();
  }

  private void Update()
  {
    if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

    UpdatePenalty();

    if (cooldownTimer > 0f)
    {
      cooldownTimer -= Time.deltaTime;
      return;
    }

    CheckCollision();
  }

  /// <summary>
  /// Ceza süresini sayar. Süre bitince hem hız cezasını hem yara
  /// bandını AYNI ANDA kaldırır (senkron bitiş).
  /// </summary>
  private void UpdatePenalty()
  {
    if (penaltyTimer <= 0f) return;

    penaltyTimer -= Time.deltaTime;

    if (penaltyTimer <= 0f)
    {
      penaltyTimer = 0f;
      brushController.ClearPenalty();
      brushVisual?.SetBandage(false);
    }
  }

  /// <summary>
  /// Yakındaki diğer fırçaları tarar; değen ilk fırçayla simetrik
  /// çarpışmayı çözer.
  /// </summary>
  private void CheckCollision()
  {
    int count = Physics2D.OverlapCircleNonAlloc(
        transform.position,
        collisionRadius,
        overlapBuffer,
        LayerMask.GetMask("Default")
    );

    for (int i = 0; i < count; i++)
    {
      Collider2D other = overlapBuffer[i];

      if (other.gameObject == gameObject) continue;
      if (!other.CompareTag("Brush")) continue;

      BrushCollision otherCollision = other.GetComponent<BrushCollision>();
      if (otherCollision == null) continue;

      ResolveSymmetric(otherCollision);
      return;
    }
  }

  /// <summary>
  /// İki fırçayı birbirinin zıt yönüne iter ve iki tarafta da
  /// çarpışma tepkisini (event + ses + ceza) tetikler.
  /// </summary>
  private void ResolveSymmetric(BrushCollision other)
  {
    Vector2 myPos = transform.position;
    Vector2 otherPos = other.transform.position;

    Vector2 axis = myPos - otherPos;

    if (axis.sqrMagnitude < 0.0001f)
      axis = UnityEngine.Random.insideUnitCircle.normalized;
    else
      axis = axis.normalized;

    this.ReceiveCollision(axis * knockbackForce);
    other.ReceiveCollision(-axis * knockbackForce);
  }

  /// <summary>
  /// Dışarıdan (karşı fırçadan) tetiklenen çarpışma tepkisi.
  /// Knockback + ses + event + hız cezası uygular, cooldown başlatır.
  /// </summary>
  public void ReceiveCollision(Vector2 knockback)
  {
    if (cooldownTimer > 0f) return;

    brushController.AddKnockback(knockback);

    if (collisionSound != null && audioSource != null)
      audioSource.PlayOneShot(collisionSound);

    ApplyPenalty();

    OnCollision?.Invoke();

    cooldownTimer = collisionCooldown;
  }

  /// <summary>
  /// Hız cezasını uygular ve yara bandını gösterir.
  ///
  /// Kural: Zaten cezalıysa çarpan TEKRAR yarıya inmez (0.5×0.5=0.25
  /// olmaz, 0.5'te kalır) — sadece süre sıfırdan başlar. Cezalı
  /// değilse çarpanı uygular.
  /// </summary>
  private void ApplyPenalty()
  {
    // Zaten cezalı değilse çarpanı uygula. Cezalıysa dokunma.
    if (penaltyTimer <= 0f)
    {
      brushController.SetPenaltyMultiplier(penaltyMultiplier);
      brushVisual?.SetBandage(true);
    }

    // Her durumda süreyi sıfırdan başlat.
    penaltyTimer = penaltyDuration;
  }

  /// <summary>
  /// Cezayı dışarıdan anında iptal eder (örn. hızlanma boost'u alınınca).
  /// Hem hız cezasını hem yara bandını kaldırır.
  /// Boost sistemi bunu çağıracak.
  /// </summary>
  public void CancelPenalty()
  {
    penaltyTimer = 0f;
    brushController.ClearPenalty();
    brushVisual?.SetBandage(false);
  }
}