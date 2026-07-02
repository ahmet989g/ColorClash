using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fırçanın aktif boost state'ini yönetir (kök objede).
///
/// Kurallar:
///  - Yeni boost alınınca eski süreli boost İPTAL olur (Remove çağrılır).
///  - Çarpışınca aktif boost iptal olur (BrushCollision.OnCollision'a abone).
///  - Anlık boostlar (bomba) state tutmaz, Apply çalışır ve biter.
///
/// Strategy pattern: gelen BoostType'a göre doğru IBoostEffect sınıfını
/// oluşturur. Yeni boost eklemek = CreateEffect'e bir case + yeni sınıf.
///
/// NOT: Effect sınıfları henüz yazılmadı — bunları tek tek boost yaparken
/// dolduracağız. CreateEffect şimdilik iskelet/placeholder.
/// </summary>
[RequireComponent(typeof(BrushController))]
public class BrushBoostState : MonoBehaviour
{
    private BrushController brushController;
    private BrushCollision brushCollision;

    // Şu an aktif olan süreli boost (yoksa null).
    private IBoostEffect activeBoost;

    private void Awake()
    {
        brushController = GetComponent<BrushController>();
        brushCollision = GetComponent<BrushCollision>();
    }

    private void OnEnable()
    {
        // Çarpışınca aktif boost iptal olsun.
        if (brushCollision != null)
            brushCollision.OnCollision += CancelActiveBoost;
    }

    private void OnDisable()
    {
        if (brushCollision != null)
            brushCollision.OnCollision -= CancelActiveBoost;
    }

    private void Update()
    {
        // Süreli boost varsa her frame ilerlet.
        activeBoost?.Tick(Time.deltaTime);
    }

    /// <summary>
    /// Bir boost uygular. BoostPickup tarafından çağrılır.
    ///
    /// Süreli boost gelirse: önce eskisini iptal et, sonra yenisini kur.
    /// Anlık boost gelirse: Apply çalışır, state'e kaydedilmez.
    /// </summary>
    public void ApplyBoost(BoostType type)
    {
        var brushes = GameManager.Instance?.GetBrushes();
        if (brushes == null) return;

        IBoostEffect effect = CreateEffect(type);
        if (effect == null) return;

        if (effect.IsInstant)
        {
            // Anlık: uygula ve unut. State tutma.
            effect.Apply(brushController, brushes);
        }
        else
        {
            // Süreli: önce eski boostu iptal et (kural: yeni boost eskisini ezer).
            CancelActiveBoost();

            activeBoost = effect;
            activeBoost.Apply(brushController, brushes);
        }
    }

    /// <summary>
    /// Aktif süreli boostu iptal eder (Remove ile geri alır).
    /// Çarpışmada ve yeni boost alımında çağrılır.
    /// </summary>
    public void CancelActiveBoost()
    {
        if (activeBoost == null) return;

        activeBoost.Remove();
        activeBoost = null;
    }

    /// <summary>
    /// BoostType → IBoostEffect eşlemesi (Strategy factory).
    ///
    /// PLACEHOLDER: Effect sınıfları tek tek boost yaparken yazılacak.
    /// Şimdilik hepsi null döner — iskelet derlensin diye.
    /// Her boost'u yaptığımızda buraya ilgili case eklenecek:
    ///   case BoostType.Speed: return new SpeedBoostEffect();
    /// </summary>
    private IBoostEffect CreateEffect(BoostType type)
    {
        switch (type)
        {
            // case BoostType.Speed:      return new SpeedBoostEffect();
            // case BoostType.PaintArea:  return new PaintAreaBoostEffect();
            // case BoostType.SingleBomb: return new SingleBombBoostEffect();
            // case BoostType.MultiBomb:  return new MultiBombBoostEffect();
            // case BoostType.Freeze:     return new FreezeBoostEffect();
            // case BoostType.ColorSteal: return new ColorStealBoostEffect();

            default:
                Debug.LogWarning($"BoostEffect henüz yazılmadı: {type}");
                return null;
        }
    }

    /// <summary>
    /// Şu an süreli bir boost aktif mi? UI/AI için.
    /// </summary>
    public bool HasActiveBoost => activeBoost != null;
}