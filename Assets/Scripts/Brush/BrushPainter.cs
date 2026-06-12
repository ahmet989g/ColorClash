using UnityEngine;

/// <summary>
/// Fırçanın hareket ederken arkasında boya bırakmasını sağlar.
/// BrushController ile aynı obje üzerinde çalışır.
/// </summary>
[RequireComponent(typeof(BrushController))]
public class BrushPainter : MonoBehaviour
{
  [Header("Boyama Ayarları")]
  [SerializeField] private float paintInterval = 0.02f;  // Kaç saniyede bir boya bırakır
  [SerializeField] private float sizeMultiplier = 1f;    // Büyük fırça boost için

  private BrushController brushController;
  private float paintTimer;

  private void Awake()
  {
    brushController = GetComponent<BrushController>();
  }

  private void Update()
  {
    paintTimer += Time.deltaTime;

    // Her paintInterval saniyede bir boya bırak
    if (paintTimer >= paintInterval)
    {
      paintTimer = 0f;
      DoPaint();
    }
  }

  /// <summary>
  /// PaintManager'a boyama isteği gönderir.
  /// </summary>
  private void DoPaint()
  {
    if (PaintManager.Instance == null)
    {
      Debug.LogError("PaintManager bulunamadı!");
      return;
    }

    PaintManager.Instance.Paint(
        transform.position,
        brushController.GetBrushColor(),
        sizeMultiplier
    );
  }

  /// <summary>
  /// Büyük Fırça boost item tarafından çağrılır.
  /// </summary>
  public void SetSizeMultiplier(float multiplier)
  {
    sizeMultiplier = multiplier;
  }
}