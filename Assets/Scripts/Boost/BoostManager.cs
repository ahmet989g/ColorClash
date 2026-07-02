using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boost spawn planını yürütür. Tek görevi: zamanı geldikçe plandaki
/// boostları ekranda fiziksel item (BoostPickup) olarak oluşturmak.
///
/// Planı BoostScheduler üretir; BoostManager sadece uygular. Planı
/// dışarı açar (GetActivePlan) → BrushAI Superhuman önceden okuyup
/// boost çıkmadan yola çıkar.
///
/// Round başında GameManager tarafından StartNewRound ile tetiklenir.
/// </summary>
public class BoostManager : MonoBehaviour
{
    public static BoostManager Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] private BoostScheduler scheduler;
    [Tooltip("Ekranda spawn edilecek boost item prefab'ı (BoostPickup içerir).")]
    [SerializeField] private GameObject boostPickupPrefab;

    // Aktif round'un spawn planı. AI ve spawn mantığı buradan okur.
    private List<BoostSpawnInfo> currentPlan = new List<BoostSpawnInfo>();

    // Şu an ekranda duran (alınmamış) boost item'ları.
    private readonly List<BoostPickup> activePickups = new List<BoostPickup>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Round başında çağrılır. Yeni plan üretir, eski item'ları temizler.
    /// </summary>
    public void StartNewRound()
    {
        ClearActivePickups();
        currentPlan = scheduler.GeneratePlan();
    }

    private void Update()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

        float remaining = GameManager.Instance.RemainingTime;

        // Zamanı gelen boostları spawn et.
        foreach (var info in currentPlan)
        {
            if (info.spawned) continue;

            // Kalan süre spawn zamanına ulaştıysa (veya altına indiyse) spawn et.
            if (remaining <= info.spawnAtRemaining)
            {
                SpawnBoost(info);
                info.spawned = true;
            }
        }
    }

    /// <summary>
    /// Plandaki bir boostu ekranda fiziksel item olarak oluşturur.
    /// </summary>
    private void SpawnBoost(BoostSpawnInfo info)
    {
        if (boostPickupPrefab == null)
        {
            Debug.LogError("BoostManager: boostPickupPrefab atanmadı!");
            return;
        }

        GameObject obj = Instantiate(
            boostPickupPrefab,
            new Vector3(info.position.x, info.position.y, 0f),
            Quaternion.identity
        );

        BoostPickup pickup = obj.GetComponent<BoostPickup>();
        if (pickup != null)
        {
            pickup.Initialize(info.type);
            activePickups.Add(pickup);
        }
    }

    /// <summary>
    /// Bir pickup alındığında BoostPickup tarafından çağrılır — listeden çıkar.
    /// </summary>
    public void OnPickupCollected(BoostPickup pickup)
    {
        activePickups.Remove(pickup);
    }

    /// <summary>
    /// Ekrandaki tüm alınmamış boostları temizler (round sonu/başı).
    /// </summary>
    private void ClearActivePickups()
    {
        foreach (var p in activePickups)
            if (p != null) Destroy(p.gameObject);

        activePickups.Clear();
    }

    /// <summary>
    /// Aktif planı döndürür. BrushAI Superhuman önceden okumak için kullanır.
    /// </summary>
    public List<BoostSpawnInfo> GetActivePlan() => currentPlan;
}