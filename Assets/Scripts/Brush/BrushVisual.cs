using UnityEngine;

/// <summary>
/// Fırça görselini kök objenin dönüşünden bağımsız tutar.
///
/// Kök obje (Brush) boyama yönü için döner, ancak bu script'in
/// bağlı olduğu görsel alt-obje HER ZAMAN dünya-dik durur — fırçayla
/// dönmez. Ayrıca görseli zeminden (boyama noktasından) yukarı
/// kaydırarak "boyanın üzerinde değil, biraz uzağında" hissini verir.
///
/// Tek görevi: görselin yönünü ve konum offsetini yönetmek.
/// Hareket/boyama mantığına KARIŞMAZ — o BrushController'da kalır.
/// </summary>
public class BrushVisual : MonoBehaviour
{
  [Header("Konum Offseti")]
  [Tooltip("Görselin zeminden (boyama noktasından) ne kadar yukarıda duracağı (dünya birimi).")]
  [SerializeField] private float heightOffset = 0.35f;

  [Header("Gölge (opsiyonel)")]
  [Tooltip("Gölge sprite'ının Transform'u. Atanmazsa gölge yönetilmez.")]
  [SerializeField] private Transform shadow;
  [Tooltip("Gölgenin zemine (boyama noktasına) göre dikey konumu. 0 = tam zeminde.")]
  [SerializeField] private float shadowYOffset = 0f;

  // Kök objenin dünya dönüşünü iptal etmek için sabit hedef:
  // her zaman sıfır rotasyon (dünyaya göre dik).
  private static readonly Quaternion UprightRotation = Quaternion.identity;

  private void LateUpdate()
  {
    // LateUpdate kullanıyoruz: kök obje Update içinde döndükten SONRA
    // çalışır, böylece görsel tek frame bile eğik görünmez.

    // 1) Görseli dünyaya göre dik tut — kök ne kadar dönerse dönsün
    //    bu obje her zaman yukarı bakar.
    transform.rotation = UprightRotation;

    // 2) Görseli kök pozisyonun biraz üzerine yerleştir.
    //    localPosition kullanmıyoruz çünkü kök dönünce local eksen de
    //    döner — bunun yerine dünya pozisyonunu doğrudan ayarlıyoruz.
    Vector3 basePos = transform.parent.position;
    transform.position = new Vector3(
        basePos.x,
        basePos.y + heightOffset,
        basePos.z
    );

    // 3) Gölge (varsa): zemine sabit, dik durur, fırçayla dönmez.
    //    Görselin offseti kadar yukarı çıkmaz — yerde kalarak
    //    derinlik hissi verir.
    if (shadow != null)
    {
      shadow.rotation = UprightRotation;
      shadow.position = new Vector3(
          basePos.x,
          basePos.y + shadowYOffset,
          basePos.z
      );
    }
  }

  /// <summary>
  /// Boost (Büyük Fırça) sırasında görseli büyütmek için kullanılabilir.
  /// </summary>
  public void SetVisualScale(float scale)
  {
    transform.localScale = Vector3.one * scale;
  }
}