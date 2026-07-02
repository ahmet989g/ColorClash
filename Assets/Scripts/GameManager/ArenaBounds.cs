using UnityEngine;

/// <summary>
/// Oynanabilir arena alanının sınırlarını TEK merkezden hesaplar.
///
/// Neden var: "TopBar hariç oynanabilir alan" hesabı birden fazla script'te (BrushController, ArenaScaler, PaintManager) lazım.
/// Aynı hesabı kopyalamak yerine burada topluyoruz → tek doğruluk kaynağı. Biri değişirse hepsi tutarlı kalır.
///
/// Statik ve saf: sahne objesi değil, MonoBehaviour değil. Sadece kamera + TopBar verilir, sınırları döndürür. Global state yok.
/// </summary>
public static class ArenaBounds
{
    /// <summary>
    /// Oynanabilir alanın dünya-uzayı sınırlarını döndürür.
    /// Üst kenar TopBar'ın altına çekilir; diğer kenarlar kameranın kenarı.
    /// </summary>
    /// <param name="cam">Ortografik oyun kamerası</param>
    /// <param name="topBarRect">Üst bar (null ise üst = ekran tepesi)</param>
    /// <param name="padding">Kenarlardan içeri boşluk (0 = tam kenar)</param>
    /// <returns>Sınırlar: minX, maxX, minY, maxY</returns>
    public static Bounds2D Calculate(Camera cam, RectTransform topBarRect, float padding = 0f)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float minX = cam.transform.position.x - halfWidth + padding;
        float maxX = cam.transform.position.x + halfWidth - padding;
        float minY = cam.transform.position.y - halfHeight + padding;

        // Üst kenar: TopBar'ın dünyadaki alt kenarı (piksel değil, ölçüm).
        float topEdge = GetTopBarBottomWorldY(cam, topBarRect);
        float maxY = topEdge - padding;

        return new Bounds2D(minX, maxX, minY, maxY);
    }

    /// <summary>
    /// TopBar'ın dünya-uzayındaki alt kenarının Y değerini döndürür.
    /// Canvas modundan bağımsız çalışır (Overlay / Screen Space - Camera).
    /// </summary>
    public static float GetTopBarBottomWorldY(Camera cam, RectTransform topBarRect)
    {
        // TopBar yoksa üst sınır = ekranın tepesi.
        if (topBarRect == null)
            return cam.transform.position.y + cam.orthographicSize;

        Vector3[] corners = new Vector3[4];
        topBarRect.GetWorldCorners(corners);
        // corners: [0]=sol-alt, [1]=sol-üst, [2]=sağ-üst, [3]=sağ-alt

        Canvas canvas = topBarRect.GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Overlay: corners ekran pikseli → dünyaya çevir.
            Vector3 screenBottom = corners[0];
            Vector3 worldBottom = cam.ScreenToWorldPoint(
                new Vector3(screenBottom.x, screenBottom.y, -cam.transform.position.z)
            );
            return worldBottom.y;
        }

        // Screen Space - Camera / World Space: corners zaten dünya birimi.
        return corners[0].y;
    }
}

/// <summary>
/// 2D sınır kutusu — minX, maxX, minY, maxY.
/// Arena sınırlarını taşımak için basit değer tipi.
/// </summary>
public struct Bounds2D
{
    public float minX, maxX, minY, maxY;

    public Bounds2D(float minX, float maxX, float minY, float maxY)
    {
        this.minX = minX;
        this.maxX = maxX;
        this.minY = minY;
        this.maxY = maxY;
    }

    // Oynanabilir alanın genişliği ve yüksekliği (dünya birimi).
    public float Width => maxX - minX;
    public float Height => maxY - minY;

    // Merkez noktası — arena ortalama/konumlandırma için.
    public Vector2 Center => new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
}