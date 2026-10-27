using NeuralQuest.Models;
using NeuralQuest.Services;
using NeuralQuest.Controls;

namespace NeuralQuest;

public partial class MainPage : ContentPage
{
    private int _kartSayisi = 12;
    private string _seviye = "Kolay";
    private bool _oyunAciliyor;

    public MainPage()
    {
        InitializeComponent();
        SecimiGuncelle();
    }

    private bool _updatingLayout;

    // Fiziksel ekran çözünürlüğü yerine kullanılabilir güvenli ekran alanını esas al.
    // Metinler ve dokunma alanları doğal boyutlarını korur; görseller kalan alanı kullanır.
    private void MenuViewport_SizeChanged(object? sender, EventArgs e)
    {
        if (_updatingLayout || MenuScroll is null || MenuContent is null ||
            MenuScroll.Width <= 0 || MenuScroll.Height <= 0)
            return;

        _updatingLayout = true;
        try
        {
            double width = Math.Min(Math.Max(48, MenuScroll.Width - 24), 480);
            MenuContent.WidthRequest = width;
            AltMenuyuBoyutlandir();

            double textHeight = Math.Max(48, HeaderBar.Height) +
                                Math.Max(18, ProtocolLabel.Height) +
                                Math.Max(18, SeciliSeviyeLabel.Height);
            double available = MenuScroll.Height - MenuContent.Padding.VerticalThickness -
                               MenuContent.Spacing * (MenuContent.Children.Count - 1) - textHeight;

            // Yükseklikler görsel oranlarına göre belirlenir. LogoArea, StatusPanels ve StartArea
            // içinde PNG'lerin yalnızca boş kenarları kırpılır. Seviye kartları daima tam gösterilir.
            double minLogoWidth = Math.Min(width, 300);
            double minPanelWidth = Math.Min(width, 300);
            double minLevelWidth = Math.Min(width, 258);
            double minStartWidth = Math.Min(width, 280);
            double minCoreSize = Math.Min(width, 80);
            double idealCoreSize = Math.Min(width * 0.58, 230);
            double minHeight = minLogoWidth * 0.23 + minPanelWidth * 0.20 +
                               LevelHeight(minLevelWidth) + minStartWidth * 0.22 + minCoreSize;
            double idealHeight = width * (0.23 + 0.20 + 0.22) +
                                 LevelHeight(width) + idealCoreSize;
            double fraction = idealHeight > minHeight
                ? Math.Clamp((available - minHeight) / (idealHeight - minHeight), 0, 1)
                : 0;

            SetAreaSize(LogoArea, Interpolate(minLogoWidth, width, fraction), 0.23);
            SetAreaSize(StatusPanels, Interpolate(minPanelWidth, width, fraction), 0.20);
            SetAreaSize(StartArea, Interpolate(minStartWidth, width, fraction), 0.22);
            LevelsGrid.WidthRequest = Interpolate(minLevelWidth, width, fraction);
            LevelsGrid.HeightRequest = LevelHeight(LevelsGrid.WidthRequest);
            // Her PNG'nin farklı şeffaf alt payını dikkate al; çizgiyi görünür kenardan 1 birim ayır.
            GostergeyiHizala(KolayIndicator, LevelsGrid.HeightRequest, 1328.0 / 1448);
            GostergeyiHizala(OrtaIndicator, LevelsGrid.HeightRequest, 1353.0 / 1448);
            GostergeyiHizala(ZorIndicator, LevelsGrid.HeightRequest, 1366.0 / 1448);
            CoreArea.WidthRequest = CoreArea.HeightRequest =
                Interpolate(minCoreSize, idealCoreSize, fraction);
            // Okunabilir en küçük menü ekrana sığmadığında ScrollView, yatay kullanımda
            // ve büyük yazı boyutlarında da tüm içeriğe erişim sağlar.
        }
        finally
        {
            _updatingLayout = false;
        }
    }

    private void AltMenuyuBoyutlandir()
    {
        const double imageRatio = 1994.0 / 789;
        // Dört sütun her genişlikte tek satırda kalır; PNG'nin tamamı sütun içine oranlı sığar.
        double footerWidth = Math.Min(Math.Max(48, MenuScroll.Width - 8), 560);
        double columnWidth = (footerWidth - NavigationGrid.ColumnSpacing * 3) / 4;
        double imageHeight = Math.Min(48, columnWidth / imageRatio);
        NavigationGrid.WidthRequest = footerWidth;

        foreach (var item in NavigationGrid.Children.OfType<Grid>())
            item.Children.OfType<Image>().Single().HeightRequest = imageHeight;
    }

    private static void GostergeyiHizala(BoxView indicator, double imageHeight, double bottomEdgeRatio)
    {
        double bottomMargin = Math.Max(0, imageHeight * (1 - bottomEdgeRatio) - indicator.HeightRequest - 1);
        indicator.Margin = new Thickness(0, 0, 0, bottomMargin);
    }

    private static double LevelHeight(double width) =>
        (width - 12) / 3 * (1448.0 / 1086);

    private static double Interpolate(double minimum, double ideal, double fraction) =>
        minimum + (ideal - minimum) * fraction;

    private static void SetAreaSize(Grid area, double width, double heightRatio)
    {
        area.WidthRequest = width;
        area.HeightRequest = width * heightRatio;
    }

    // SEVİYE SEÇİMLERİ
    private void Kolay_Tapped(
        object? sender,
        EventArgs e)
    {
        SeviyeSec("Kolay", 12);
    }

    private void Orta_Tapped(
        object? sender,
        EventArgs e)
    {
        SeviyeSec("Orta", 16);
    }

    private void Zor_Tapped(
        object? sender,
        EventArgs e)
    {
        SeviyeSec("Zor", 20);
    }

    private void SeviyeSec(
        string seviye,
        int kartSayisi)
    {
        if (BilgiPaneli.Acik || _oyunAciliyor)
            return;
        _seviye = seviye;
        _kartSayisi = kartSayisi;

        SecimiGuncelle();
    }

    // PNG'lerin doğal parlaklığını koru; seçimi yalnızca ince neon alt çizgiyle belirt.
    private void SecimiGuncelle()
    {
        bool kolay = _kartSayisi == 12;
        bool orta = _kartSayisi == 16;
        bool zor = _kartSayisi == 20;

        KolayIndicator.Opacity = kolay ? 1 : 0;
        OrtaIndicator.Opacity = orta ? 1 : 0;
        ZorIndicator.Opacity = zor ? 1 : 0;
        SemanticProperties.SetDescription(KolaySecim,
            "Kolay, 12 kart, 6 eşleşme" + (kolay ? ", seçili" : ""));
        SemanticProperties.SetDescription(OrtaSecim,
            "Orta, 16 kart, 8 eşleşme" + (orta ? ", seçili" : ""));
        SemanticProperties.SetDescription(ZorSecim,
            "Zor, 20 kart, 10 eşleşme" + (zor ? ", seçili" : ""));

        SeciliSeviyeLabel.Text =
            $"{_seviye.ToUpperInvariant()} PROTOKOL" +
            $" • {_kartSayisi} KART";

        SeciliSeviyeLabel.TextColor = Color.FromArgb(
            kolay ? "#58E6FF" :
            orta ? "#CF91FF" :
                   "#FF789C");
    }

    // OYUNU BAŞLAT
    private async void Baslat_Tapped(
        object? sender,
        EventArgs e)
    {
        if (_oyunAciliyor || BilgiPaneli.Acik || (Shell.Current?.CurrentPage is Page sayfa && sayfa != this))
            return;
        _oyunAciliyor = true;
        try
        {
            await Navigation.PushAsync(new OyunPage(_kartSayisi));
        }
        catch (Exception hata)
        {
            System.Diagnostics.Debug.WriteLine(hata);
            // Hata yolu da aynı neon paneli kullanır.
            _oyunAciliyor = false;
            BilgiGoster("OYUN AÇILAMADI", "", [Metin("Sistem onarımı başlatılamadı. Tekrar deneyebilirsin.")]);
        }
        finally { _oyunAciliyor = false; }
    }

    protected override bool OnBackButtonPressed()
    {
        if (!BilgiPaneli.Acik)
            return base.OnBackButtonPressed();
        BilgiPaneli.Kapat();
        return true;
    }

    protected override void OnDisappearing()
    {
        BilgiPaneli.Kapat(animasyonlu: false);
        base.OnDisappearing();
    }

    private void BilgiPaneli_Kapandi(object? sender, EventArgs e)
    {
        MenuEtkilesimAlani.IsEnabled = true;
        // Android kapanan yerel katmanı hâlâ hit-test edebilir. Etkin menüyü öne al;
        // HUD arkasında sönmeye devam eder ve yeni dokunmayı tutamaz.
        MenuEtkilesimAlani.ZIndex = 21;
        HudGecisi.OneAl(MenuEtkilesimAlani);
    }

    private void BilgiGoster(string baslik, string altBaslik, IEnumerable<View> icerik,
        string kapat = "KAPAT", bool kompakt = false)
    {
        if (BilgiPaneli.Acik || _oyunAciliyor) return;
        MenuEtkilesimAlani.ZIndex = 0;
        MenuEtkilesimAlani.IsEnabled = false;
        BilgiPaneli.Goster(baslik, altBaslik, icerik, kapat, kompakt);
        HudGecisi.OneAl(BilgiPaneli);
    }

    private static Label Metin(string metin, double boyut = 14, string renk = "#D8E8F9", bool kalin = false) => new()
    {
        Text = metin, FontSize = boyut, TextColor = Color.FromArgb(renk),
        FontAttributes = kalin ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap
    };

    private static HudCerceve BilgiKutusu(View icerik, string renk = "#52DCFF", double enerji = 0.8) => new()
    {
        IcerikPayi = 16, Vurgu = Color.FromArgb(renk), Enerji = enerji, Govde = icerik
    };

    private static Label TeknikEtiket(string yazi, string renk = "#AB92D5")
    {
        var etiket = Metin(yazi, 10, renk, true);
        etiket.CharacterSpacing = 1.2;
        return etiket;
    }

    private static View Numara(string deger, string renk, double enerji = 1)
    {
        var yazi = Metin(deger, 17, renk, true);
        yazi.HorizontalTextAlignment = TextAlignment.Center;
        yazi.VerticalTextAlignment = TextAlignment.Center;
        return new HudCerceve { WidthRequest = 42, HeightRequest = 46, IcerikPayi = 6,
            Vurgu = Color.FromArgb(renk), Enerji = enerji, Govde = yazi, VerticalOptions = LayoutOptions.Start };
    }

    private void Ayarlar_Tapped(object? sender, EventArgs e)
    {
        var bilgiler = new VerticalStackLayout { Spacing = 10 };
        bilgiler.Children.Add(TeknikEtiket("YAPILANDIRMA / YEREL SİSTEM"));
        bilgiler.Children.Add(Metin("Bu sürümde değiştirilebilir ses veya titreşim ayarı bulunmuyor."));
        bilgiler.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#29445D"), InputTransparent = true });
        bilgiler.Children.Add(Metin("Oyun çevrimdışı çalışır; Sistem Taraması her oyun için bir kez kullanılabilir.", 13, "#AECDE1"));
        BilgiGoster("AYARLAR", "SİSTEM YAPILANDIRMASI", [BilgiKutusu(bilgiler)], kompakt: true);
    }

    private void Basarilar_Tapped(object? sender, EventArgs e)
    {
        var basarilar = OyunBasarilari.Hesapla(CihazKayitlari.Istatistikler.Oku());
        var gorunumler = new List<View>();
        int numara = 0;
        foreach (var basari in basarilar)
        {
            string renk = basari.Kazanildi ? "#6BF3D0" : "#9985BD";
            var satir = new Grid { ColumnDefinitions = { new(42), new(GridLength.Star) }, ColumnSpacing = 10 };
            satir.Add(Numara((++numara).ToString("00"), renk, basari.Kazanildi ? 1 : 0.4));
            var metinler = new VerticalStackLayout { Spacing = 6 };
            metinler.Children.Add(TeknikEtiket($"ARŞİV / {numara:00}", "#8CA9C4"));
            metinler.Children.Add(Metin(basari.Baslik, 16, basari.Kazanildi ? "#E8FAFF" : "#C0C9DC", true));
            metinler.Children.Add(Metin(basari.Aciklama, 13, "#BBD0E3"));
            var durum = new Grid { ColumnDefinitions = { new(6), new(GridLength.Star) }, ColumnSpacing = 7 };
            durum.Add(new BoxView { Color = Color.FromArgb(renk), HeightRequest = 6, WidthRequest = 6,
                VerticalOptions = LayoutOptions.Center, Opacity = basari.Kazanildi ? 1 : 0.45, InputTransparent = true });
            durum.Add(Metin(basari.Kazanildi ? "KAZANILDI" : "KİLİTLİ", 10, renk, true), 1);
            metinler.Children.Add(durum);
            satir.Add(metinler, 1);
            gorunumler.Add(BilgiKutusu(satir, renk, basari.Kazanildi ? 1 : 0.4));
        }
        gorunumler.Add(Metin("Başarılar bu cihazdaki doğrulanmış oyun kayıtlarından hesaplanır. Eski oyunların tarama bilgisi bilinmiyorsa Mükemmel Onarım kazanılmış sayılmaz.", 12, "#9DB8D1"));
        BilgiGoster("BAŞARILAR", "KURTARMA PROTOKOLÜ KAYITLARI", gorunumler);
    }

    private static View OzetKutusu(string baslik, string deger, string renk)
    {
        var sutun = new VerticalStackLayout { Spacing = 7 };
        sutun.Children.Add(Metin(baslik, 10, "#B6DAEE", true));
        sutun.Children.Add(Metin(deger, 25, renk, true));
        sutun.Children.Add(new BoxView { HeightRequest = 2, WidthRequest = 28, HorizontalOptions = LayoutOptions.Start,
            Color = Color.FromArgb(renk), InputTransparent = true });
        return BilgiKutusu(sutun, renk);
    }

    private static string SeviyeRengi(OyunSeviyesi seviye) => seviye switch
    {
        OyunSeviyesi.Kolay => "#65E6FF", OyunSeviyesi.Orta => "#C197FF", _ => "#FF789C"
    };

    private void Istatistik_Tapped(object? sender, EventArgs e)
    {
        var kayit = CihazKayitlari.Istatistikler.Oku();
        var gorunumler = new List<View>();
        var ozet = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        ozet.Add(OzetKutusu("TOPLAM OYUN", kayit.ToplamTamamlananOyun.ToString(), "#80EEFF"), 0);
        ozet.Add(OzetKutusu("TOPLAM SKOR", kayit.ToplamSkor.ToString("N0"), "#C5A0FF"), 1);
        gorunumler.Add(ozet);
        int tamamlanan = Enum.GetValues<OyunSeviyesi>().Count(s => kayit.Seviyeler.GetValueOrDefault(s.ToString())?.TamamlanmaSayisi > 0);
        var ilerleme = new VerticalStackLayout { Spacing = 10 };
        ilerleme.Children.Add(TeknikEtiket($"GENEL İLERLEME • {tamamlanan} / 3 PROTOKOL"));
        var hat = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 5 };
        foreach (var seviye in Enum.GetValues<OyunSeviyesi>())
        {
            bool bitti = kayit.Seviyeler.GetValueOrDefault(seviye.ToString())?.TamamlanmaSayisi > 0;
            hat.Add(new BoxView { HeightRequest = 5, Color = Color.FromArgb(bitti ? SeviyeRengi(seviye) : "#26354F"),
                InputTransparent = true }, (int)seviye);
        }
        SemanticProperties.SetDescription(hat, $"Tamamlanan protokol: {tamamlanan} / 3");
        ilerleme.Children.Add(hat);
        gorunumler.Add(BilgiKutusu(ilerleme, "#9A7BD8", 0.6));
        foreach (var seviye in Enum.GetValues<OyunSeviyesi>())
        {
            var bilgi = kayit.Seviyeler.GetValueOrDefault(seviye.ToString());
            string renk = SeviyeRengi(seviye);
            var detay = new VerticalStackLayout { Spacing = 10 };
            detay.Children.Add(TeknikEtiket($"ANALİZ / {seviye.ToString().ToUpperInvariant()}", renk));
            detay.Children.Add(Metin($"{seviye.ToString().ToUpperInvariant()} PROTOKOLÜ", 16, "#DFEFFA", true));
            var veriler = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, RowSpacing = 7, ColumnSpacing = 10 };
            void Satir(string ad, string deger)
            {
                int row = veriler.RowDefinitions.Count;
                veriler.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                veriler.Add(Metin(ad, 12, "#ABC6DE"), 0, row);
                var sayi = Metin(deger, 13, renk, true);
                sayi.HorizontalTextAlignment = TextAlignment.End;
                veriler.Add(sayi, 1, row);
            }
            Satir("Tamamlanan oyun", (bilgi?.TamamlanmaSayisi ?? 0).ToString());
            if (bilgi is null || bilgi.TamamlanmaSayisi == 0) detay.Children.Add(Metin("Henüz tamamlanmadı.", 13));
            else
            {
                Satir("En iyi süre", bilgi.EnIyiSureTicks is long ticks ? OyunOturumu.SureMetni(TimeSpan.FromTicks(ticks)) : "—");
                Satir("En az hamle", bilgi.EnAzHamle?.ToString() ?? "—");
                Satir("En iyi skor", $"{bilgi.EnIyiSkor:N0}");
            }
            detay.Children.Add(veriler);
            gorunumler.Add(BilgiKutusu(detay, renk));
        }
        BilgiGoster("İSTATİSTİKLER", "BU CİHAZDAKİ KALICI ONARIM KAYITLARI", gorunumler);
    }

    private void Yardim_Tapped(object? sender, EventArgs e)
    {
        string[] kurallar = ["Bir hafıza kartına dokunarak aç.", "İkinci kartı aç ve aynı güvenlik modülünü bul.",
            "Doğru eşleşmeler yapay zekâ çekirdeğini onarır.", "Yanlış eşleşen kartlar yeniden kapanır.",
            "Bütün çiftleri bularak sistemi %100 onar.", "Sistem Taraması ipucunu oyun başına yalnızca bir kez kullanabilirsin."];
        var gorunumler = new List<View>();
        for (int i = 0; i < kurallar.Length; i++)
        {
            var adim = new Grid { ColumnDefinitions = { new(42), new(GridLength.Star) }, ColumnSpacing = 10 };
            adim.Add(Numara((i + 1).ToString("00"), "#72E6FF"));
            var aciklama = new VerticalStackLayout { Spacing = 5 };
            aciklama.Children.Add(TeknikEtiket($"İŞLEM / {i + 1:00}"));
            aciklama.Children.Add(Metin(kurallar[i], 14));
            adim.Add(aciklama, 1);
            gorunumler.Add(BilgiKutusu(adim, "#52BCDC", 0.65));
            if (i < kurallar.Length - 1)
                gorunumler.Add(new BoxView { WidthRequest = 1, HeightRequest = 8, Margin = new Thickness(36, -7, 0, -7),
                    HorizontalOptions = LayoutOptions.Start, Color = Color.FromArgb("#507D9F"), InputTransparent = true });
        }
        gorunumler.Add(TeknikEtiket("SEVİYE / MODÜL KAPASİTESİ"));
        foreach (var seviye in Enum.GetValues<OyunSeviyesi>())
        {
            int kart = OyunOturumu.KartSayisi(seviye);
            gorunumler.Add(BilgiKutusu(Metin($"{seviye.ToString().ToUpperInvariant()}: {kart} kart / {kart / 2} çift",
                13, SeviyeRengi(seviye), true), SeviyeRengi(seviye), 0.7));
        }
        BilgiGoster("NASIL OYNANIR?", "HAFIZA MODÜLLERİNİ KURTAR", gorunumler, "ANLADIM");
    }

    private void Hakkinda_Tapped(object? sender, EventArgs e)
    {
        var tanim = new VerticalStackLayout { Spacing = 9 };
        tanim.Children.Add(TeknikEtiket("NQ / SİSTEM KİMLİĞİ"));
        tanim.Children.Add(Metin("Neural Quest, yapay zekâ hafızasının ve siber güvenlik modüllerinin kurtarılması temasına sahip bir hafıza eşleştirme oyunudur."));
        tanim.Children.Add(Metin("Oyuncu güvenlik modüllerini eşleştirerek hasarlı yapay zekâ çekirdeğini onarır.", 13, "#AFC8DE"));
        var teknik = new VerticalStackLayout { Spacing = 7 };
        teknik.Children.Add(Metin("Teknoloji: .NET MAUI / C#", 13, "#8DEEFF", true));
        teknik.Children.Add(Metin("Oyun çevrimdışı çalışır.", 13));
        teknik.Children.Add(Metin($"Sürüm: {AppInfo.Current.VersionString}", 12, "#BFA3F5"));
        BilgiGoster("NEURAL QUEST", "DİJİTAL HAFIZA • SİSTEM KURTARMA",
            [BilgiKutusu(tanim), BilgiKutusu(teknik, "#AC83E3", 0.7)], kompakt: true);
    }
}

