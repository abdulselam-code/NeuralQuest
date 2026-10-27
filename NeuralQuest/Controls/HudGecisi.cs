namespace NeuralQuest.Controls;

// Tek animasyon adı: yeni geçiş eskisini iptal eder. Tamamlama iş kurallarını bekletmez.
internal static class HudGecisi
{
    private const string Ad = "HudGecisi";

    public static void OneAl(View gorunum)
    {
#if ANDROID
        // ZIndex sonraki yerleşimi korur; yerel sıra mevcut dokunma olayı için de güncellenir.
        var handler = gorunum.Handler;
        if ((handler?.ContainerView ?? handler?.PlatformView) is Android.Views.View yerel)
        {
            // Android dokunma sırası çocuk sırasından önce yerel Z değerini dikkate alır.
            if (yerel.Parent is Android.Views.ViewGroup ebeveyn)
                for (int i = 0; i < ebeveyn.ChildCount; i++)
                    if (ebeveyn.GetChildAt(i) is Android.Views.View kardes) kardes.TranslationZ = 0;
            yerel.TranslationZ = 1;
            yerel.BringToFront();
        }
#endif
    }

    public static void Ac(HudCerceve panel)
    {
        panel.AbortAnimation(Ad);
        new Animation(t =>
        {
            panel.Opacity = 0.55 + 0.45 * t;
            panel.Scale = 0.978 + 0.022 * t;
            panel.CizgiyiGuncelle(t);
        }).Commit(panel, Ad, rate: 16, length: 200, easing: Easing.CubicOut,
            finished: (_, iptal) => { if (!iptal) GorunumuSifirla(panel); });
    }

    public static void Temizle(HudCerceve panel)
    {
        panel.AbortAnimation(Ad);
        GorunumuSifirla(panel);
    }

    private static void GorunumuSifirla(HudCerceve panel)
    {
        panel.Opacity = panel.Scale = 1;
        panel.CizgiyiGuncelle(0);
    }
}
