using UnityEngine;

/// <summary>
/// Arena zeminini kamera boyutuna göre otomatik ölçeklendirir.
/// Tüm ekran boyutlarını ve aspect ratio'ları destekler.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScaler : MonoBehaviour
{
  private void Start()
  {
    ScaleToCamera();
  }

  /// <summary>
  /// Sprite'ı kameranın tam görüş alanını kaplayacak şekilde ölçekler.
  /// </summary>
  private void ScaleToCamera()
  {
    Camera cam = Camera.main;
    SpriteRenderer sr = GetComponent<SpriteRenderer>();

    // Kameranın gördüğü dünya boyutları
    float camHeight = cam.orthographicSize * 2f;
    float camWidth = camHeight * cam.aspect;

    // Sprite'ın orijinal boyutları
    float spriteHeight = sr.sprite.bounds.size.y;
    float spriteWidth = sr.sprite.bounds.size.x;

    // Scale hesapla
    transform.localScale = new Vector3(
        camWidth / spriteWidth,
        camHeight / spriteHeight,
        1f
    );

    // Kamera merkezine konumla
    transform.position = new Vector3(
        cam.transform.position.x,
        cam.transform.position.y,
        0f
    );
  }
}