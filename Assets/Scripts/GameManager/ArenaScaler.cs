using UnityEngine;

/// <summary>
/// Arena zeminini oynanabilir alana (TopBar hariç) göre ölçekler.
/// Sınır hesabını ArenaBounds'tan alır — tek doğruluk kaynağı.
/// Böylece zemin, fırçanın erişebildiği alanla birebir örtüşür;
/// TopBar altında boyanamayan ölü bölge kalmaz.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScaler : MonoBehaviour
{
  [Header("Sınır Referansı")]
  [Tooltip("Üst bar. Zeminin üst kenarı bunun altına hizalanır.")]
  [SerializeField] private RectTransform topBarRect;

  private void Start()
  {
    ScaleToPlayableArea();
  }

  /// <summary>
  /// Sprite'ı oynanabilir alanı (TopBar hariç) kaplayacak şekilde
  /// ölçekler ve o alanın merkezine konumlar.
  /// </summary>
  private void ScaleToPlayableArea()
  {
    Camera cam = Camera.main;
    SpriteRenderer sr = GetComponent<SpriteRenderer>();

    // Oynanabilir alan sınırları (TopBar hariç). padding = 0 çünkü
    // zemin tam kenara kadar uzansın, sadece fırça içeride dursun.
    Bounds2D area = ArenaBounds.Calculate(cam, topBarRect, 0f);

    // Sprite'ın orijinal boyutları
    float spriteWidth = sr.sprite.bounds.size.x;
    float spriteHeight = sr.sprite.bounds.size.y;

    // Oynanabilir alanı kaplayacak ölçek
    transform.localScale = new Vector3(
        area.Width / spriteWidth,
        area.Height / spriteHeight,
        1f
    );

    // Oynanabilir alanın merkezine konumla (TopBar hariç olduğu için
    // merkez, ekran merkezinden biraz aşağıda olur).
    transform.position = new Vector3(area.Center.x, area.Center.y, 0f);
  }
}