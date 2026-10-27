namespace NeuralQuest.Controls;

public partial class NeonBilgiPaneli : ContentView
{
    private int _gosterim;
    private bool _kompakt;
    private bool _boyutlandiriliyor;
    private readonly HudKapanisEfekti _kapanis = new();
    public bool Acik { get; private set; }
    public event EventHandler? Kapandi;

    public NeonBilgiPaneli() => InitializeComponent();

    public void Goster(string baslik, string altBaslik, IEnumerable<View> icerik,
        string kapatMetni = "KAPAT", bool kompakt = false)
    {
        if (Acik) return;
        _kapanis.Durdur();
        HudGecisi.Temizle(Panel);
        Acik = true;
        InputTransparent = false;
        PanelAlani.InputTransparent = false;
        KapatDugmesi.IsEnabled = true;
        _kompakt = kompakt;
        Baslik.Text = baslik;
        AltBaslik.Text = altBaslik;
        AltBaslik.IsVisible = !string.IsNullOrWhiteSpace(altBaslik);
        Icerik.Children.Clear();
        foreach (var gorunum in icerik) Icerik.Children.Add(gorunum);
        KapatDugmesi.Text = kapatMetni;
        IsVisible = true;
        int gosterim = ++_gosterim;
        Dispatcher.Dispatch(() =>
        {
            if (Acik && gosterim == _gosterim)
                _ = IcerikKaydirma.ScrollToAsync(0, 0, animated: false);
        });
        PaneliBoyutlandir();
        HudGecisi.Ac(Panel);
    }

    public void Kapat(bool animasyonlu = true)
    {
        bool acikti = Acik;
        Acik = false;
        InputTransparent = true;
        PanelAlani.InputTransparent = true;
        KapatDugmesi.IsEnabled = false;
        int gosterim = ++_gosterim;
        _kapanis.Durdur();
        HudGecisi.Temizle(Panel);
        IsVisible = false;
        // Menü etkileşimi kapanış animasyonunun bitmesini beklemez.
        if (acikti)
        {
            Kapandi?.Invoke(this, EventArgs.Empty);
            if (animasyonlu) _kapanis.Baslat(Panel, this);
#if DEBUG && ANDROID
            // UI testleri sabit bekleme yerine kapanış olayının işlendiğini gözler.
            Android.Util.Log.Debug("NeuralQuestHud", $"KAPANIYOR|{gosterim}|{Baslik.Text}");
#endif
        }
        Icerik.Children.Clear();
    }

    private void Kapat_Clicked(object? sender, EventArgs e) => Kapat();
    private void PanelAlani_SizeChanged(object? sender, EventArgs e) => PaneliBoyutlandir();

    private void PaneliBoyutlandir()
    {
        if (_boyutlandiriliyor || Panel is null || GuvenliPanelAlani.Width <= 0 || GuvenliPanelAlani.Height <= 0) return;
        _boyutlandiriliyor = true;
        try
        {
            // İç Grid yalnızca güvenli alanı kapsar; sistem çubuklarına taşılmaz.
            Panel.WidthRequest = Math.Min(480, Math.Max(1, GuvenliPanelAlani.Width - 24));
            double ustSinir = Math.Min(720, Math.Max(1, GuvenliPanelAlani.Height - 24));
            // ScrollView içerik yüksekliğini görünür alan kadar büyütebilir. Doğal ölçüyü kullan.
            double genislik = Math.Max(1, Panel.WidthRequest - 40);
            double dogalYukseklik = _kompakt ? Icerik.Measure(genislik, double.PositiveInfinity).Height +
                BaslikAlani.Measure(genislik, double.PositiveInfinity).Height + 48 + 40 + 24 : ustSinir;
            double yukseklik = _kompakt ? Math.Min(ustSinir, Math.Max(240, dogalYukseklik)) : ustSinir;
            if (Math.Abs(Panel.HeightRequest - yukseklik) > 0.5) Panel.HeightRequest = yukseklik;
        }
        finally { _boyutlandiriliyor = false; }
    }
}
