using UnityEngine;

/// <summary>
/// Ekrandaki fiziksel boost item'ı. Tek görevi: bir fırça değince
/// hangi boost tipi olduğunu bildirip kendini yok etmek.
///
/// Etkiyi KENDİ uygulamaz — etki uygulama BrushBoostState'in işi
/// (sorumluluk ayrımı). Pickup sadece "bu tip boost alındı" der.
///
/// Görsel/animasyon (dönme, parlama) buraya eklenecek — her boost
/// tipinin kendi sprite'ı olacak, tek tek boost yaparken detaylandırılır.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BoostPickup : MonoBehaviour
{
    [Header("Görsel")]
    [SerializeField] private SpriteRenderer iconRenderer;

    // Bu pickup hangi boost tipini taşıyor.
    private BoostType boostType;

    /// <summary>
    /// BoostManager tarafından spawn sonrası çağrılır. Tipi belirler.
    /// </summary>
    public void Initialize(BoostType type)
    {
        boostType = type;
        // İleride: tipe göre sprite/renk ata (tek tek boost yaparken).
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Sadece fırçalar boost alabilir.
        if (!other.CompareTag("Brush")) return;

        BrushController brush = other.GetComponent<BrushController>();
        if (brush == null) return;

        // Etki uygulamayı fırçanın boost state'ine devret.
        BrushBoostState boostState = other.GetComponent<BrushBoostState>();
        if (boostState != null)
            boostState.ApplyBoost(boostType);

        // Yönetici listeden çıkarsın, sonra kendini yok et.
        if (BoostManager.Instance != null)
            BoostManager.Instance.OnPickupCollected(this);

        Destroy(gameObject);
    }

    /// <summary>
    /// Bu pickup'ın boost tipi. Debug/UI için.
    /// </summary>
    public BoostType GetBoostType() => boostType;
}