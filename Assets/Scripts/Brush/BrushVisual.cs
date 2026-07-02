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

  [Header("Çarpışma Zıplaması (Hop)")]
  [Tooltip("Zıplamanın ulaşacağı ekstra yükseklik (dünya birimi).")]
  [SerializeField] private float hopHeight = 0.5f;
  [Tooltip("Zıplamanın toplam süresi (sn) — çıkış + iniş.")]
  [SerializeField] private float hopDuration = 0.35f;
  [Tooltip("Zıplarken gölge ne kadar küçülsün (1 = değişmez, 0.7 = %30 küçülür).")]
  [SerializeField] private float shadowShrinkOnHop = 0.7f;

  [Header("Yara Bandı")]
  [Tooltip("Çarpışma sonrası gösterilecek yara bandı SpriteRenderer'ı. Normalde kapalı.")]
  [SerializeField] private SpriteRenderer bandageRenderer;

  // Aktif zıplamanın o anki ekstra yüksekliği. 0 = yerde.
  private float currentHopOffset = 0f;
  // Zıplama ilerlemesi: 0 (başladı) → 1 (bitti). 1 iken zıplama yok.
  private float hopProgress = 1f;

  // Gölgenin başlangıç ölçeği (zıplama bitince geri dönmek için).
  private Vector3 shadowBaseScale = Vector3.one;

  // Kök objenin dünya dönüşünü iptal etmek için sabit hedef:
  // her zaman sıfır rotasyon (dünyaya göre dik).
  private static readonly Quaternion UprightRotation = Quaternion.identity;

  // Çarpışma event'ine abone olmak için kök objedeki collision referansı.
  private BrushCollision brushCollision;

  private void Awake()
  {
    // Gölgenin orijinal ölçeğini sakla — zıplama bitince geri yüklenecek.
    if (shadow != null)
      shadowBaseScale = shadow.localScale;

    // Yara bandı başlangıçta gizli.
    if (bandageRenderer != null)
      bandageRenderer.enabled = false;

    // Kök objedeki BrushCollision'ı bul (görsel, kökün çocuğu).
    brushCollision = GetComponentInParent<BrushCollision>();
  }

  private void OnEnable()
  {
    // Çarpışma event'ine abone ol — çarpışınca zıpla.
    if (brushCollision != null)
      brushCollision.OnCollision += TriggerHop;
  }

  private void OnDisable()
  {
    // Aboneliği bırak — bellek sızıntısı / hayalet çağrı olmasın.
    if (brushCollision != null)
      brushCollision.OnCollision -= TriggerHop;
  }

  private void LateUpdate()
  {
    // LateUpdate kullanıyoruz: kök obje Update içinde döndükten SONRA
    // çalışır, böylece görsel tek frame bile eğik görünmez.

    // 0) Zıplama ilerlemesini güncelle.
    UpdateHop();

    // 1) Görseli dünyaya göre dik tut — kök ne kadar dönerse dönsün
    //    bu obje her zaman yukarı bakar.
    transform.rotation = UprightRotation;

    // 2) Görseli kök pozisyonun biraz üzerine yerleştir.
    //    localPosition kullanmıyoruz çünkü kök dönünce local eksen de
    //    döner — bunun yerine dünya pozisyonunu doğrudan ayarlıyoruz.
    //    heightOffset (sabit yükseklik) + currentHopOffset (zıplama) birlikte.
    Vector3 basePos = transform.parent.position;
    transform.position = new Vector3(
        basePos.x,
        basePos.y + heightOffset + currentHopOffset,
        basePos.z
    );

    // 3) Gölge (varsa): zemine sabit, dik durur, fırçayla dönmez.
    //    Görselin offseti kadar yukarı çıkmaz — yerde kalarak
    //    derinlik hissi verir. Zıplarken hafif küçülür (yükseğe çıkınca
    //    gölge küçülür mantığı → gerçekçilik).
    if (shadow != null)
    {
      shadow.rotation = UprightRotation;
      shadow.position = new Vector3(
          basePos.x,
          basePos.y + shadowYOffset,
          basePos.z
      );

      // Zıplama yüksekliğine göre gölgeyi küçült: yerdeyken tam boy,
      // zirvede en küçük.
      float hopAmount = currentHopOffset / Mathf.Max(hopHeight, 0.0001f);
      float scaleFactor = Mathf.Lerp(1f, shadowShrinkOnHop, hopAmount);
      shadow.localScale = shadowBaseScale * scaleFactor;
    }
  }

  /// <summary>
  /// Zıplama ilerlemesini her frame günceller ve currentHopOffset'i hesaplar.
  /// Sinüs eğrisi: 0'dan başlar, ortada zirve yapar, sonda 0'a döner —
  /// yumuşak bir parabol gibi çıkış/iniş.
  /// </summary>
  private void UpdateHop()
  {
    if (hopProgress >= 1f)
    {
      currentHopOffset = 0f;
      return;
    }

    hopProgress += Time.deltaTime / hopDuration;
    hopProgress = Mathf.Clamp01(hopProgress);

    // sin(0)=0, sin(π/2)=1, sin(π)=0 → 0→1→0 yumuşak yay
    currentHopOffset = Mathf.Sin(hopProgress * Mathf.PI) * hopHeight;
  }

  /// <summary>
  /// Bir zıplama başlatır. Çarpışma event'i tetiklenince çağrılır.
  /// Zaten zıplıyorsa baştan başlatır (üst üste çarpışmada resetlenir).
  /// </summary>
  public void TriggerHop()
  {
    hopProgress = 0f;
  }

  /// <summary>
  /// Yara bandını gösterir veya gizler. Süreyi BU script tutmaz —
  /// çarpışma cezasıyla senkron olması için zamanlamayı BrushCollision
  /// yönetir, burası sadece görünürlüğü değiştirir (tek görev: görsel).
  /// </summary>
  public void SetBandage(bool visible)
  {
    if (bandageRenderer != null)
      bandageRenderer.enabled = visible;
  }

  /// <summary>
  /// Boost (Büyük Fırça) sırasında görseli büyütmek için kullanılabilir.
  /// </summary>
  public void SetVisualScale(float scale)
  {
    transform.localScale = Vector3.one * scale;
  }
}