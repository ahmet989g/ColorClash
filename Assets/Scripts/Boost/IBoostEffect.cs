using UnityEngine;

/// <summary>
/// Tüm boost etkilerinin ortak arayüzü (Strategy pattern).
///
/// Her boost tipi bu arayüzü implemente eden ayrı bir sınıftır.
/// Yeni boost eklemek = yeni sınıf yazmak; mevcut kod bozulmaz.
///
/// Yaşam döngüsü:
///   Apply()   → boost alındığı an bir kez çalışır.
///   Tick()    → süreli boostlar için her frame çağrılır (opsiyonel).
///   Remove()  → boost bittiğinde/iptal edildiğinde temizlik yapar.
///
/// Anlık boostlar (bombalar) sadece Apply'ı kullanır, IsInstant=true
/// döner ve Tick/Remove'da bir şey yapmaz. Süreli boostlar (hız,
/// dondurma) state tutar ve Remove'da geri alır.
/// </summary>
public interface IBoostEffect
{
    /// <summary>
    /// Boost tipi — plan ve UI için kimlik.
    /// </summary>
    BoostType Type { get; }

    /// <summary>
    /// Anlık mı? true ise Apply çalışır ve boost hemen biter (Tick/Remove yok).
    /// false ise süreli — çarpışana veya yeni boost alana kadar sürer.
    /// </summary>
    bool IsInstant { get; }

    /// <summary>
    /// Boost alındığı an bir kez çalışır.
    /// </summary>
    /// <param name="owner">Boost'u alan fırça</param>
    /// <param name="allBrushes">Tüm fırçalar (rakiplere etki edenler için)</param>
    void Apply(BrushController owner, System.Collections.Generic.List<BrushController> allBrushes);

    /// <summary>
    /// Süreli boostlar için her frame çağrılır. Anlık boostlarda boş.
    /// </summary>
    void Tick(float deltaTime);

    /// <summary>
    /// Boost bittiğinde/iptal edildiğinde çağrılır. Etkiyi geri alır
    /// (hızı normale döndür, dondurmayı kaldır vb.). Anlık boostlarda boş.
    /// </summary>
    void Remove();
}

/// <summary>
/// Boost tipleri. Plan üretimi, spawn ve UI bu enum'u kullanır.
/// </summary>
public enum BoostType
{
    Speed,        // Hız x2 (süreli)
    PaintArea,    // Boyama alanı x2 (süreli)
    SingleBomb,   // Tek daire boya (anlık)
    MultiBomb,    // 20 noktaya minik boya (anlık)
    Freeze,       // Rakipleri 7 sn dondur (süreli)
    ColorSteal    // Rakiplerin boyamasını pasif et (süreli)
}