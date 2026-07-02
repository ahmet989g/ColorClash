using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tek bir boost'un spawn planı: ne zaman, nerede, hangi tip.
/// Deterministik plan sayesinde hem BoostManager hem BrushAI aynı
/// bilgiyi okur — AI önceden nereye gideceğini bilir.
/// </summary>
public class BoostSpawnInfo
{
    // Kaç saniye KALA spawn olacak (45/30/15).
    public float spawnAtRemaining;

    // Dünya konumu (arena içinde rastgele nokta).
    public Vector2 position;

    // Hangi boost tipi.
    public BoostType type;

    // Bu boost spawn edildi mi? (BoostManager işaretler, tekrar spawn olmasın)
    public bool spawned;

    public BoostSpawnInfo(float remaining, Vector2 pos, BoostType t)
    {
        spawnAtRemaining = remaining;
        position = pos;
        type = t;
        spawned = false;
    }
}

/// <summary>
/// Round başında boost spawn PLANINI üretir. Tek görevi plan kurmak:
/// hangi saniyede, arenanın neresinde, hangi boost çıkacağını rastgele
/// belirler. Spawn etmez, etki uygulamaz — sadece planı döndürür.
///
/// Plan deterministiktir: bir kez üretilince sabittir. Böylece AI
/// plandan okuyup boost çıkmadan önce yola çıkabilir.
/// </summary>
public class BoostScheduler : MonoBehaviour
{
    [Header("Spawn Zamanları")]
    [Tooltip("Bu saniyeler KALA boost spawn olur.")]
    [SerializeField] private float[] spawnTimes = { 45f, 30f, 15f };

    [Header("Sınır Referansı")]
    [Tooltip("Boostların spawn olacağı alanı sınırlamak için TopBar.")]
    [SerializeField] private RectTransform topBarRect;

    [Tooltip("Boostlar kenardan ne kadar içeride spawn olsun (dünya birimi).")]
    [SerializeField] private float edgePadding = 1f;

    /// <summary>
    /// Yeni bir spawn planı üretir. Round başında çağrılır.
    /// Her çağrıda farklı (rastgele) bir plan döner.
    /// </summary>
    public List<BoostSpawnInfo> GeneratePlan()
    {
        var plan = new List<BoostSpawnInfo>();
        Camera cam = Camera.main;

        // Oynanabilir alan sınırları (TopBar hariç) — ArenaBounds'tan.
        Bounds2D area = ArenaBounds.Calculate(cam, topBarRect, edgePadding);

        foreach (float t in spawnTimes)
        {
            Vector2 pos = new Vector2(
                Random.Range(area.minX, area.maxX),
                Random.Range(area.minY, area.maxY)
            );

            BoostType type = GetRandomBoostType();

            plan.Add(new BoostSpawnInfo(t, pos, type));
        }

        return plan;
    }

    /// <summary>
    /// Rastgele bir boost tipi seçer. Tüm tipler eşit olasılıkta.
    /// (İleride ağırlıklı seçim istenirse burası değişir.)
    /// </summary>
    private BoostType GetRandomBoostType()
    {
        var values = System.Enum.GetValues(typeof(BoostType));
        return (BoostType)values.GetValue(Random.Range(0, values.Length));
    }
}