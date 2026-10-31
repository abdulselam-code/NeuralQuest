using Microsoft.Maui.Dispatching;
using NeuralQuest.Controls;
using NeuralQuest.Effects;
using NeuralQuest.Models;
using NeuralQuest.Services;

namespace NeuralQuest;

public partial class OyunPage : ContentPage
{
    private readonly Dictionary<int, (Image Gorsel, Button Dugme)> _kartGorunumleri = new();
    private OyunOturumu _oyun = null!;
    private CancellationTokenSource _islemKaynak = new();
    private IDispatcherTimer? _zamanlayici;
    private IDispatcherTimer? _bildirimZamanlayici;
    private SistemBildirimAkisi _bildirimAkisi = null!;
    private KartCozulmeEfekti? _eslesmeEfekti;
    private Window? _pencere;
    private Guid? _kaydedilenOyun;
    private bool _gorunur;
    private bool _ilkGorunum = true;
    private bool _boyutlandiriliyor;
    private readonly OyunPanelAkisi _panelAkisi = new();
    private bool _dialogAcik => _panelAkisi.Acik;
    private bool _donuluyor;
    private bool _modalAcik;
    private readonly HudKapanisEfekti _modalKapanis = new();

    private int? _hikayeSeviyesi;

    public OyunPage(int kartSayisi, int? hikayeSeviyesi = null)
    {
        _hikayeSeviyesi = hikayeSeviyesi;
        InitializeComponent();
        Loaded += Sayfa_Loaded;
        YeniOyunBaslat(kartSayisi);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _gorunur = true;
        PencereyiBagla();

        _zamanlayici = Dispatcher.CreateTimer();
        _zamanlayici.Interval = TimeSpan.FromMilliseconds(250);
        _zamanlayici.Tick += Sure_Tick;
        _zamanlayici.Start();
        if (_ilkGorunum)
        {
            _ilkGorunum = false;
            _oyun.DevamEt();
        }
        if (_oyun.Duraklatildi && !_oyun.Tamamlandi)
            _panelAkisi.Duraklat();
        PaneliGuncelle();
        KartlariGuncelle();
        BilgileriGuncelle();
        if (_oyun.Tamamlandi)
            SonucuGoster();
        BildirimleriBaslat();
        YerlesimiGuncelle();
    }

    protected override void OnDisappearing()
    {
        _gorunur = false;
        BildirimleriTemizle();
        IslemleriIptalEt();
        _oyun.Duraklat();
        OyunModaliniKapat(animasyonlu: false);
        if (_zamanlayici is not null)
        {
            _zamanlayici.Stop();
            _zamanlayici.Tick -= Sure_Tick;
            _zamanlayici = null;
        }
        PencereBaglantisiniKaldir();
        base.OnDisappearing();
    }

    private void Sayfa_Loaded(object? sender, EventArgs e) => PencereyiBagla();

    private void PencereyiBagla()
    {
        if (!_gorunur)
            return;
        // Shell tarafından itilen sayfanın Window bağlantısı Appearing anında henüz kurulmamış olabilir.
        var pencere = Window ?? Shell.Current?.Window;
        if (pencere is null || ReferenceEquals(pencere, _pencere))
            return;
        PencereBaglantisiniKaldir();
        _pencere = pencere;
        _pencere.Deactivated += Pencere_Durakladi;
        _pencere.Stopped += Pencere_Durdu;
        _pencere.Activated += Pencere_Etkinlesti;
        _pencere.Destroying += Pencere_Kapaniyor;
    }

    private void PencereBaglantisiniKaldir()
    {
        if (_pencere is null)
            return;
        _pencere.Deactivated -= Pencere_Durakladi;
        _pencere.Stopped -= Pencere_Durdu;
        _pencere.Activated -= Pencere_Etkinlesti;
        _pencere.Destroying -= Pencere_Kapaniyor;
        _pencere = null;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_donuluyor)
            return true;
        if (_panelAkisi.Acik)
        {
            _panelAkisi.Geri();
            if (!_panelAkisi.Acik)
                DevamEt();
            else
                PaneliGuncelle();
        }
        else
            _ = AnaMenuyeDonAsync();
        return true;
    }

    private void YeniOyunBaslat(int kartSayisi)
    {
        BildirimleriTemizle();
        IslemleriIptalEt();
        _oyun?.Sonlandir();
        _oyun = new OyunOturumu(kartSayisi, hikayeSeviyesi: _hikayeSeviyesi);
        _bildirimAkisi = new SistemBildirimAkisi();
        if (!_gorunur)
            _oyun.Duraklat();
        _kaydedilenOyun = null;
        _panelAkisi.Kapat();
        SonucKatmani.IsVisible = false;
        KartAlani.Children.Clear();
        KartAlani.ColumnDefinitions.Clear();
        KartAlani.RowDefinitions.Clear();
        _kartGorunumleri.Clear();

        for (int index = 0; index < _oyun.Sutun; index++)
            KartAlani.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        for (int index = 0; index < _oyun.Satir; index++)
            KartAlani.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        for (int index = 0; index < _oyun.Kartlar.Count; index++)
        {
            var kart = _oyun.Kartlar[index];
            var gorsel = new Image { Source = "card_back.png", Aspect = Aspect.AspectFit, InputTransparent = true };
            AutomationProperties.SetIsInAccessibleTree(gorsel, false);
            var dugme = new Button
            {
                BackgroundColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 0,
                Padding = 0, MinimumWidthRequest = 48, MinimumHeightRequest = 48,
                CommandParameter = kart.Kimlik
            };
            dugme.Clicked += Kart_Clicked;
            var hucre = new Grid { SafeAreaEdges = SafeAreaEdges.None };
            hucre.Children.Add(gorsel);
            hucre.Children.Add(dugme);
            Grid.SetColumn(hucre, index % _oyun.Sutun);
            Grid.SetRow(hucre, index / _oyun.Sutun);
            KartAlani.Children.Add(hucre);
            _kartGorunumleri.Add(kart.Kimlik, (gorsel, dugme));
        }

        ProtokolBasligi.Text = _hikayeSeviyesi is int seviye ? $"HİKÂYE MODU • SEVİYE {seviye} / 9" : $"{_oyun.Seviye.ToString().ToUpperInvariant()} PROTOKOLÜ";
        ProtokolDetayi.Text = $"{kartSayisi} KART • {_oyun.ToplamCift} MODÜL";
        DurumLabel.Text = "İki kart açarak modülleri eşleştir.";
        PaneliGuncelle();
        KartlariGuncelle();
        BilgileriGuncelle();
        YerlesimiGuncelle();
        if (_gorunur)
        {
            _zamanlayici?.Start();
            BildirimleriBaslat();
        }
    }

    private async void Kart_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || sender is not Button { CommandParameter: int kimlik })
            return;
        var oyun = _oyun;
        var sonuc = oyun.KartAc(kimlik);
        if (sonuc == KartAcmaSonucu.Reddedildi)
            return;

        var token = _islemKaynak.Token;
        KartlariGuncelle();
        BilgileriGuncelle(sonuc == KartAcmaSonucu.Eslesme);
        if (sonuc == KartAcmaSonucu.IlkKart)
            return;

        try
        {
            if (sonuc == KartAcmaSonucu.Eslesme)
            {
                _bildirimAkisi.IlerlemeyiBildir(oyun.EslesenCift, oyun.ToplamCift);
                BildirimleriBaslat();
                DurumLabel.Text = "Güvenlik modülü kurtarıldı!";
                if (oyun.Tamamlandi)
                    TamamlananOyunuKaydet();

                await Task.Delay(120, token);
                token.ThrowIfCancellationRequested();
                var gorseller = oyun.Kartlar.Where(kart => kart.Acik && kart.Eslesmis)
                    .Select(kart => _kartGorunumleri[kart.Kimlik].Gorsel).ToArray();
                await EslesmeAnimasyonuAsync(gorseller, token);
            }
            else
            {
                DurumLabel.Text = "Eşleşmedi. Modülleri hatırla.";
                await Task.Delay(900, token);
            }

            token.ThrowIfCancellationRequested();
            if (!_gorunur || !ReferenceEquals(oyun, _oyun))
                return;
            oyun.GecisiTamamla();
            KartlariGuncelle();
            BilgileriGuncelle();
            if (oyun.Tamamlandi)
                SonucuGoster();
        }
        catch (OperationCanceledException)
        {
            // Duraklatma, yeniden başlatma ve sayfadan çıkış eski oturumun devamını engeller.
        }
        catch (Exception hata)
        {
            System.Diagnostics.Debug.WriteLine(hata);
            if (_gorunur && ReferenceEquals(oyun, _oyun))
            {
                Duraklat();
                if (oyun.Tamamlandi)
                    KayitDurumu.Text = "Görsel işlem tamamlanamadı; oyun sonucu korundu.";
                else
                    PanelAciklamasi.Text = "Görsel işlem tamamlanamadı. Devam ederek oyunu sürdürebilirsin.";
            }
        }
    }

    private async Task EslesmeAnimasyonuAsync(Image[] gorseller, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var efekt = new KartCozulmeEfekti(KartEfektKatmani, KartAlani, gorseller);
        _eslesmeEfekti = efekt;
        try
        {
            await efekt.OynatAsync();
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            efekt.Durdur();
            if (ReferenceEquals(_eslesmeEfekti, efekt))
                _eslesmeEfekti = null;
        }
    }

    private async void Tarama_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || !_oyun.TaramayiBaslat())
            return;
        var oyun = _oyun;
        var token = _islemKaynak.Token;
        DurumLabel.Text = "Sistem taraması: modülleri hatırla.";
        KartlariGuncelle();
        BilgileriGuncelle();
        try
        {
            await Task.Delay(1000, token);
            token.ThrowIfCancellationRequested();
            if (!_gorunur || !ReferenceEquals(oyun, _oyun))
                return;
            oyun.GecisiTamamla();
            DurumLabel.Text = "Tarama tamamlandı. Eşleştirmeye devam et.";
            KartlariGuncelle();
            BilgileriGuncelle();
        }
        catch (OperationCanceledException)
        {
            // İpucu kullanılmış kalır; iptal edilen tarama hiçbir kartı yeniden açamaz.
        }
    }

    private void KartlariGuncelle()
    {
        foreach (var kart in _oyun.Kartlar)
        {
            var (gorsel, dugme) = _kartGorunumleri[kart.Kimlik];
            gorsel.Scale = 1;
            gorsel.Opacity = kart.Eslesmis && !kart.Acik ? 0 : 1;
            gorsel.Source = kart.Acik ? kart.Gorsel : "card_back.png";
            dugme.IsEnabled = _gorunur && _oyun.EtkilesimeAcik && !kart.Eslesmis && !kart.Acik;
            SemanticProperties.SetDescription(dugme,
                kart.Eslesmis ? $"{kart.Baslik}, kurtarıldı" :
                kart.Acik ? kart.Baslik : $"Kapalı kart {kart.Kimlik + 1}");
        }
    }

    private void BilgileriGuncelle(bool onarimiCanlandir = false)
    {
        SureLabel.Text = OyunOturumu.SureMetni(_oyun.Sure);
        HamleLabel.Text = _oyun.Hamle.ToString();
        EslesmeLabel.Text = $"{_oyun.EslesenCift} / {_oyun.ToplamCift}";
        OnarimLabel.Text = $"ÇEKİRDEK ONARIMI • %{_oyun.OnarimYuzdesi}";
        SemanticProperties.SetDescription(OnarimCubugu, $"Çekirdek onarımı yüzde {_oyun.OnarimYuzdesi}");
        OnarimCubugu.AbortAnimation("Onarim");
        if (onarimiCanlandir && _gorunur)
        {
            new Animation(deger => OnarimCubugu.Progress = deger, OnarimCubugu.Progress, _oyun.Onarim)
                .Commit(OnarimCubugu, "Onarim", length: 240, easing: Easing.CubicOut);
        }
        else
            OnarimCubugu.Progress = _oyun.Onarim;

        CekirdekGorseli.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#54E8FF")), Offset = new Point(0, 0),
            Radius = (float)(6 + 14 * _oyun.Onarim), Opacity = (float)(0.15 + 0.6 * _oyun.Onarim)
        };
        TaramaDugmesi.Text = _oyun.TaramaKullanildi ? "TARAMA KULLANILDI" : "SİSTEM TARAMASI";
        TaramaDugmesi.IsEnabled = _gorunur && _oyun.EtkilesimeAcik && !_oyun.TaramaKullanildi;
        DuraklatDugmesi.IsEnabled = !_oyun.Tamamlandi;
        if (_oyun.Tamamlandi)
            _zamanlayici?.Stop();
    }

    private void Sure_Tick(object? sender, EventArgs e)
    {
        if (_gorunur)
            SureLabel.Text = OyunOturumu.SureMetni(_oyun.Sure);
    }

    private void BildirimleriBaslat()
    {
        if (!_gorunur || _oyun.Duraklatildi || SonucKatmani.IsVisible || _donuluyor)
            return;
        _bildirimAkisi.DevamEt();
        if (_bildirimZamanlayici is null)
        {
            _bildirimZamanlayici = Dispatcher.CreateTimer();
            _bildirimZamanlayici.Interval = TimeSpan.FromMilliseconds(33);
            _bildirimZamanlayici.Tick += Bildirim_Tick;
        }
        _bildirimZamanlayici.Start();
        Bildirim_Tick(this, EventArgs.Empty);
    }

    private void Bildirim_Tick(object? sender, EventArgs e)
    {
        if (!_gorunur || _oyun.Duraklatildi || SonucKatmani.IsVisible || _donuluyor)
            return;
        _bildirimAkisi.Guncelle();
        string? metin = _bildirimAkisi.Metin;
        SistemBildirimPaneli.IsVisible = metin is not null;
        if (metin is null)
        {
            _bildirimZamanlayici?.Stop();
            return;
        }
        SistemBildirimMetni.Text = metin;
        SistemBildirimPaneli.Opacity = _bildirimAkisi.Gorunurluk;
        SistemBildirimPaneli.TranslationY = 3 * (1 - _bildirimAkisi.Gorunurluk);
    }

    private void BildirimleriDuraklat()
    {
        _bildirimAkisi?.Duraklat();
        _bildirimZamanlayici?.Stop();
        SistemBildirimPaneli.IsVisible = false;
    }

    private void BildirimleriTemizle()
    {
        _bildirimAkisi?.Sonlandir();
        if (_bildirimZamanlayici is not null)
        {
            _bildirimZamanlayici.Stop();
            _bildirimZamanlayici.Tick -= Bildirim_Tick;
            _bildirimZamanlayici = null;
        }
        SistemBildirimPaneli.IsVisible = false;
        SistemBildirimPaneli.Opacity = 0;
        SistemBildirimPaneli.TranslationY = 0;
        SistemBildirimMetni.Text = string.Empty;
    }

    private void IslemleriIptalEt()
    {
        _islemKaynak.Cancel();
        _islemKaynak.Dispose();
        _islemKaynak = new CancellationTokenSource();
        _eslesmeEfekti?.Durdur();
        _eslesmeEfekti = null;
        foreach (var gorunum in _kartGorunumleri.Values)
            gorunum.Gorsel.CancelAnimations();
        OnarimCubugu.AbortAnimation("Onarim");
        SonucPaneli.AbortAnimation("Kutlama");
        SonucPaneli.Scale = 1;
        HudGecisi.Temizle(OyunModalPaneli);
        _modalKapanis.Durdur();
    }

    private void Duraklat()
    {
        BildirimleriDuraklat();
        IslemleriIptalEt();
        _oyun.Duraklat();
        KartlariGuncelle();
        BilgileriGuncelle();
        if (!_oyun.Tamamlandi)
        {
            _panelAkisi.Duraklat();
            PaneliGuncelle();
        }
        if (_oyun.Tamamlandi)
            SonucuGoster();
    }

    private void DevamEt()
    {
        if (!_gorunur)
            return;
        _panelAkisi.Kapat();
        OyunModaliniKapat();
        OyunGorunumu.IsEnabled = true;
        _oyun.DevamEt();
        KartlariGuncelle();
        BilgileriGuncelle();
        if (_oyun.Tamamlandi)
            SonucuGoster();
        else
        {
            _zamanlayici?.Start();
            BildirimleriBaslat();
        }
    }

    private void Duraklat_Clicked(object? sender, EventArgs e) => Duraklat();
    private void Pencere_Durakladi(object? sender, EventArgs e) { if (_gorunur) Duraklat(); }
    private void Pencere_Durdu(object? sender, EventArgs e) { if (_gorunur) Duraklat(); _zamanlayici?.Stop(); }
    private void Pencere_Etkinlesti(object? sender, EventArgs e) { if (_gorunur && !_oyun.Tamamlandi) _zamanlayici?.Start(); }
    private void Pencere_Kapaniyor(object? sender, EventArgs e)
    {
        _gorunur = false;
        BildirimleriTemizle();
        IslemleriIptalEt();
        _oyun.Sonlandir();
        _zamanlayici?.Stop();
    }

    private void PaneliGuncelle()
    {
        OyunGorunumu.IsEnabled = !_panelAkisi.Acik && !_donuluyor;
        if (!_panelAkisi.Acik)
        {
            OyunModaliniKapat();
            return;
        }
        bool aciliyor = !_modalAcik;
        _modalAcik = true;
        _modalKapanis.Durdur();
        OyunGorunumu.ZIndex = 0;
        DuraklatmaKatmani.IsVisible = true;
        DuraklatmaKatmani.InputTransparent = false;
        HudGecisi.OneAl(DuraklatmaKatmani);
        PanelUcuncul.IsVisible = _panelAkisi.Durum == OyunPaneli.Duraklatildi;
        (PanelBasligi.Text, PanelAciklamasi.Text, PanelBirincil.Text, PanelIkincil.Text) = _panelAkisi.Durum switch
        {
            OyunPaneli.YenidenBaslat => ("PROTOKOL YENİDEN BAŞLATILSIN MI?",
                "Mevcut ilerlemen sıfırlanacak ve kartlar yeniden karıştırılacak.", "VAZGEÇ", "YENİDEN BAŞLAT"),
            OyunPaneli.Cikis => ("SİSTEMDEN AYRIL?", "Tamamlanmamış oyun ilerlemen kaybolacak.", "DEVAM ET", "ANA MENÜ"),
            _ => ("SİSTEM DURAKLATILDI", "Onarım işlemi geçici olarak durduruldu.", "DEVAM ET", "YENİDEN BAŞLAT")
        };
        bool onay = _panelAkisi.Durum != OyunPaneli.Duraklatildi;
        string vurgu = _panelAkisi.Durum == OyunPaneli.Cikis ? "#FF789C" : onay ? "#C197FF" : "#65E6FF";
        PanelTeknikEtiketi.Text = _panelAkisi.Durum switch
        {
            OyunPaneli.YenidenBaslat => "NQ / PROTOKOL SIFIRLAMA",
            OyunPaneli.Cikis => "NQ / OTURUM SONLANDIRMA",
            _ => "NQ / KONTROL TERMİNALİ"
        };
        OyunModalPaneli.Vurgu = Color.FromArgb(vurgu);
        PanelBasligi.TextColor = Color.FromArgb(vurgu);
        PanelBirincil.BackgroundColor = Color.FromArgb(onay ? "#111F33" : "#164359");
        PanelBirincil.BorderColor = Color.FromArgb(onay ? "#607992" : "#65E6FF");
        PanelIkincil.BackgroundColor = Color.FromArgb(_panelAkisi.Durum == OyunPaneli.Cikis ? "#542039" : onay ? "#382953" : "#1A2039");
        PanelIkincil.BorderColor = Color.FromArgb(onay ? vurgu : "#9275C3");
        if (aciliyor && _gorunur) HudGecisi.Ac(OyunModalPaneli);
    }

    private void OyunModaliniKapat(bool animasyonlu = true)
    {
        bool acikti = DuraklatmaKatmani.IsVisible;
        _modalAcik = false;
        DuraklatmaKatmani.IsVisible = false;
        DuraklatmaKatmani.InputTransparent = true;
        OyunGorunumu.ZIndex = 11;
        HudGecisi.OneAl(OyunGorunumu);
        HudGecisi.Temizle(OyunModalPaneli);
        if (animasyonlu && _gorunur && acikti) _modalKapanis.Baslat(OyunModalPaneli, DuraklatmaKatmani);
        else if (!animasyonlu) _modalKapanis.Durdur();
    }

    private void PanelBirincil_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || _donuluyor)
            return;
        if (_panelAkisi.Durum == OyunPaneli.YenidenBaslat)
        {
            _panelAkisi.Geri();
            PaneliGuncelle();
        }
        else
            DevamEt();
    }

    private async void PanelIkincil_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || _donuluyor)
            return;
        switch (_panelAkisi.Durum)
        {
            case OyunPaneli.Duraklatildi:
                YenidenBaslat_Clicked(sender, e);
                break;
            case OyunPaneli.YenidenBaslat:
                YeniOyunBaslat(_oyun.Kartlar.Count);
                PaneliGuncelle();
                break;
            case OyunPaneli.Cikis:
                await GuvenliCikAsync();
                break;
        }
    }

    private void YenidenBaslat_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || _donuluyor)
            return;
        if (_oyun.Tamamlandi)
        {
            YeniOyunBaslat(_oyun.Kartlar.Count);
            return;
        }

        Duraklat();
        _panelAkisi.YenidenBaslatOnayi();
        PaneliGuncelle();
    }

    private async void AnaMenu_Clicked(object? sender, EventArgs e) => await AnaMenuyeDonAsync();

    private async Task AnaMenuyeDonAsync()
    {
        if (!_gorunur || _donuluyor)
            return;
        if (_oyun.Tamamlandi)
        {
            await GuvenliCikAsync();
            return;
        }
        // Önceki modal durumunu kaydet; geri tuşu o durumu geri yükler.
        _panelAkisi.CikisOnayi();
        Duraklat();
        PaneliGuncelle();
    }

    private async Task GuvenliCikAsync()
    {
        if (!_gorunur || _donuluyor)
            return;
        if (_oyun.Tamamlandi) TamamlananOyunuKaydet();
        _donuluyor = true;
        BildirimleriDuraklat();
        IslemleriIptalEt();
        _oyun.Duraklat();
        PanelBirincil.IsEnabled = PanelIkincil.IsEnabled = PanelUcuncul.IsEnabled = false;
        try
        {
            await Navigation.PopAsync();
            _oyun.Sonlandir();
        }
        catch (Exception hata)
        {
            System.Diagnostics.Debug.WriteLine(hata);
            if (_gorunur)
            {
                _panelAkisi.Kapat();
                Duraklat();
                if (_oyun.Tamamlandi)
                    KayitDurumu.Text = "Ana menüye dönülemedi. Tekrar deneyebilirsin.";
                else
                    PanelAciklamasi.Text = "Ana menüye dönülemedi. Tekrar deneyebilirsin.";
            }
        }
        finally
        {
            _donuluyor = false;
            PanelBirincil.IsEnabled = PanelIkincil.IsEnabled = PanelUcuncul.IsEnabled = true;
        }
    }

    private void TamamlananOyunuKaydet()
    {
        if (!_oyun.Tamamlandi || _kaydedilenOyun == _oyun.OyunKimligi)
            return;
        try
        {
            if (_hikayeSeviyesi.HasValue) CihazKayitlari.Hikaye.Kaydet(_oyun);
            else CihazKayitlari.Istatistikler.Kaydet(_oyun.SonucuAl());
            _kaydedilenOyun = _oyun.OyunKimligi;
            KayitDurumu.Text = _hikayeSeviyesi.HasValue ? "Hikâye ilerlemesi bu cihazda saklandı." : "İstatistikler bu cihazda saklandı.";
        }
        catch (Exception hata)
        {
            System.Diagnostics.Debug.WriteLine(hata);
            KayitDurumu.Text = "Kayıt cihazda saklanamadı. Sonraki seviye veya ana menü ile tekrar denenecek.";
        }
    }

    private void SonucuGoster()
    {
        // Bekleyen ilerleme mesajları sonuç panelinin üstüne taşınmaz.
        BildirimleriTemizle();
        TamamlanmaBasligi.Text = _hikayeSeviyesi == 9 ? "TÜM SEVİYELER TAMAMLANDI!" : _hikayeSeviyesi is int seviye ? $"SEVİYE {seviye} TAMAMLANDI!" : SistemBildirimAkisi.TamamlanmaMesaji;
        TamamlananOyunuKaydet();
        var sonuc = _oyun.SonucuAl();
        _panelAkisi.Kapat();
        OyunModaliniKapat(animasyonlu: false);
        bool ilkAciliyor = !SonucKatmani.IsVisible;
        OyunGorunumu.ZIndex = 0;
        SonucBilgisi.Text = (_hikayeSeviyesi is int hikaye ? $"Hikâye Modu: Seviye {hikaye} / 9\n" : $"Protokol: {sonuc.Seviye}\n") + $"Süre: {OyunOturumu.SureMetni(sonuc.Sure)}\n" +
                           $"Hamle: {sonuc.Hamle}\nSkor: {sonuc.Skor:N0}\nKurtarılan modül: {sonuc.KurtarilanModul}";
        SonrakiSeviyeDugmesi.IsVisible = _hikayeSeviyesi.HasValue ? _oyun.SonrakiHikayeSeviyesiVar : sonuc.Seviye != OyunSeviyesi.Zor;
        SonucKatmani.IsVisible = true;
        HudGecisi.OneAl(SonucKatmani);
        SonucPaneli.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#B77AFF")),
            Offset = new Point(0, 0), Radius = 24, Opacity = 0.65f
        };
        if (ilkAciliyor && _gorunur)
            new Animation(deger => SonucPaneli.Scale = deger, 0.96, 1)
                .Commit(SonucPaneli, "Kutlama", length: 320, easing: Easing.CubicOut);
    }

    private void TekrarOyna_Clicked(object? sender, EventArgs e)
    {
        if (_gorunur && !_dialogAcik && _oyun.Tamamlandi)
        {
            TamamlananOyunuKaydet();
            if (_hikayeSeviyesi.HasValue && _kaydedilenOyun != _oyun.OyunKimligi) return;
            YeniOyunBaslat(_oyun.Kartlar.Count);
        }
    }

    private void SonrakiSeviye_Clicked(object? sender, EventArgs e)
    {
        if (!_gorunur || _dialogAcik || !_oyun.Tamamlandi) return;
        TamamlananOyunuKaydet();
        if (_kaydedilenOyun != _oyun.OyunKimligi) return;
        if (_hikayeSeviyesi.HasValue)
        {
            if (!_oyun.SonrakiHikayeSeviyesiVar) return;
            _hikayeSeviyesi++;
            YeniOyunBaslat(HikayeIlerlemeServisi.KartSayisi(_hikayeSeviyesi.Value));
        }
        else if (_oyun.Seviye != OyunSeviyesi.Zor)
            YeniOyunBaslat(_oyun.Seviye == OyunSeviyesi.Kolay ? 16 : 20);
    }

    private void Yerlesim_SizeChanged(object? sender, EventArgs e) => YerlesimiGuncelle();

    private void YerlesimiGuncelle()
    {
        if (_boyutlandiriliyor || _oyun is null || OyunGorunumu.Width <= 0 || OyunGorunumu.Height <= 0)
            return;
        _boyutlandiriliyor = true;
        try
        {
            const double oran = 1122.0 / 1402;
            double bosluk = Math.Clamp(OyunGorunumu.Width * 0.012, 2, 6);
            KartAlani.ColumnSpacing = KartAlani.RowSpacing = bosluk;
            double minGenislik = _oyun.Sutun * 48 + (_oyun.Sutun - 1) * bosluk + OyunDuzeni.Padding.HorizontalThickness;
            double genislik = Math.Max(OyunGorunumu.Width, minGenislik);
            OyunGorunumu.Orientation = genislik > OyunGorunumu.Width + 0.5 ? ScrollOrientation.Both : ScrollOrientation.Vertical;
            OyunDuzeni.WidthRequest = genislik;
            double sabitYukseklik = Math.Max(48, UstAlan.Height) + Math.Max(44, SayacAlani.Height) +
                                    Math.Max(48, OnarimAlani.Height) + Math.Max(54, BildirimAlani.Height) +
                                    Math.Max(48, KontrolAlani.Height) +
                                    OyunDuzeni.Padding.VerticalThickness + OyunDuzeni.RowSpacing * 5;
            double minTahta = _oyun.Satir * (48 / oran) + (_oyun.Satir - 1) * bosluk;
            OyunDuzeni.HeightRequest = Math.Max(OyunGorunumu.Height, sabitYukseklik + minTahta);
            double kartYuksekligi = Math.Min(
                (OyunDuzeni.HeightRequest - sabitYukseklik - (_oyun.Satir - 1) * bosluk) / _oyun.Satir,
                (genislik - OyunDuzeni.Padding.HorizontalThickness - (_oyun.Sutun - 1) * bosluk) / _oyun.Sutun / oran);
            if (_hikayeSeviyesi.HasValue) kartYuksekligi = Math.Min(kartYuksekligi, 112);
            KartAlani.WidthRequest = _oyun.Sutun * kartYuksekligi * oran + (_oyun.Sutun - 1) * bosluk;
            KartAlani.HeightRequest = _oyun.Satir * kartYuksekligi + (_oyun.Satir - 1) * bosluk;
            // Son satır dolu değilse kalan kartları da tahta merkezine hizala.
            int sonSatirKartlari = _oyun.Kartlar.Count % _oyun.Sutun;
            foreach (var hucre in KartAlani.Children.OfType<Grid>())
                hucre.TranslationX = _hikayeSeviyesi.HasValue && sonSatirKartlari > 0 && Grid.GetRow(hucre) == _oyun.Satir - 1
                    ? (_oyun.Sutun - sonSatirKartlari) * (kartYuksekligi * oran + bosluk) / 2 : 0;
            // Genişliğe göre sınırlanan dört satırlı tahtalarda fazla üst boşluğun
            // yalnız bir bölümünü al. Kart boyutu, bildirim alanı ve Zor düzeni korunur.
            double artanAlan = Math.Max(0, OyunDuzeni.HeightRequest - sabitYukseklik - KartAlani.HeightRequest);
            bool yukariDengele = !_hikayeSeviyesi.HasValue && _oyun.Seviye != OyunSeviyesi.Zor && artanAlan > 0.5;
            KartAlani.VerticalOptions = yukariDengele ? LayoutOptions.Start : LayoutOptions.Center;
            KartAlani.Margin = yukariDengele
                ? new Thickness(0, artanAlan / 2 - Math.Min(24, artanAlan / 4), 0, 0)
                : new Thickness(0);
        }
        finally { _boyutlandiriliyor = false; }
    }
}

