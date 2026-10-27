namespace NeuralQuest.Models;

// Oyun kuralları arayüzden bağımsızdır; tüm geçişler tek bir oturum içinde yapılır.
public sealed class OyunOturumu
{
    private static readonly (string Gorsel, string Baslik)[] Moduller =
    [
        ("card_shield.png", "Güvenlik Kalkanı"),
        ("card_lock.png", "Şifreleme Kilidi"),
        ("card_ai_core.png", "Yapay Zekâ Çekirdeği"),
        ("card_firewall.png", "Güvenlik Duvarı"),
        ("card_auth.png", "Kimlik Doğrulama"),
        ("card_recovery.png", "Veri Kurtarma"),
        ("card_network.png", "Ağ İzleme"),
        ("card_malware.png", "Zararlı Yazılım Tespiti"),
        ("card_server.png", "Güvenli Sunucu"),
        ("card_threat.png", "Tehdit Analizi")
    ];

    private readonly TimeProvider _saat;
    private readonly HafizaKarti[] _kartlar;
    private HafizaKarti? _ilkKart;
    private TimeSpan _birikenSure;
    private long _baslangic;
    private bool _sureCalisiyor;
    private bool _kapandi;

    public Guid OyunKimligi { get; } = Guid.NewGuid();
    public OyunSeviyesi Seviye { get; }
    public IReadOnlyList<HafizaKarti> Kartlar { get; }
    public int Sutun => Seviye == OyunSeviyesi.Kolay ? 3 : 4;
    public int Satir => Seviye == OyunSeviyesi.Zor ? 5 : 4;
    public int ToplamCift => _kartlar.Length / 2;
    public int Hamle { get; private set; }
    public int EslesenCift { get; private set; }
    public bool Duraklatildi { get; private set; }
    public bool Tamamlandi => EslesenCift == ToplamCift;
    public bool IslemBekleniyor { get; private set; }
    public bool Taraniyor { get; private set; }
    public bool TaramaKullanildi { get; private set; }
    public bool EtkilesimeAcik => !_kapandi && !Duraklatildi && !Tamamlandi && !IslemBekleniyor;
    public double Onarim => (double)EslesenCift / ToplamCift;
    public int OnarimYuzdesi => (int)Math.Round(Onarim * 100, MidpointRounding.AwayFromZero);
    public TimeSpan Sure => _birikenSure +
        (_sureCalisiyor ? _saat.GetElapsedTime(_baslangic) : TimeSpan.Zero);

    // Her gereksiz hamle 100, her aktif saniye 5 puan azaltır; skor sıfırın altına inmez.
    public int Skor => (int)Math.Max(0,
        ToplamCift * 1000L - Math.Max(0, Hamle - ToplamCift) * 100L - (long)Sure.TotalSeconds * 5);

    public OyunOturumu(int kartSayisi, Random? rastgele = null, TimeProvider? saat = null)
    {
        Seviye = kartSayisi switch
        {
            12 => OyunSeviyesi.Kolay,
            16 => OyunSeviyesi.Orta,
            20 => OyunSeviyesi.Zor,
            _ => throw new ArgumentOutOfRangeException(nameof(kartSayisi), "Kart sayısı 12, 16 veya 20 olmalıdır.")
        };
        _saat = saat ?? TimeProvider.System;
        _kartlar = Enumerable.Range(0, kartSayisi).Select(index =>
        {
            int modul = index / 2;
            return new HafizaKarti(index, modul, Moduller[modul].Gorsel, Moduller[modul].Baslik);
        }).ToArray();

        // Fisher–Yates: her adımda kalan aralıktan eşit olasılıklı seçim yapılır.
        var random = rastgele ?? Random.Shared;
        for (int index = _kartlar.Length - 1; index > 0; index--)
        {
            int secilen = random.Next(index + 1);
            (_kartlar[index], _kartlar[secilen]) = (_kartlar[secilen], _kartlar[index]);
        }
        Kartlar = Array.AsReadOnly(_kartlar);
        SaatiBaslat();
    }

    public KartAcmaSonucu KartAc(int kimlik)
    {
        if (!EtkilesimeAcik)
            return KartAcmaSonucu.Reddedildi;

        var kart = Array.Find(_kartlar, item => item.Kimlik == kimlik);
        if (kart is null || kart.Eslesmis || kart.Acik)
            return KartAcmaSonucu.Reddedildi;

        kart.Acik = true;
        if (_ilkKart is null)
        {
            _ilkKart = kart;
            return KartAcmaSonucu.IlkKart;
        }

        Hamle++;
        IslemBekleniyor = true;
        if (_ilkKart.ModulKimligi != kart.ModulKimligi)
            return KartAcmaSonucu.FarkliKartlar;

        // Eşleşme bir kez kaydedilir; iki ön yüz animasyon bitene kadar açık kalır.
        _ilkKart.Eslesmis = kart.Eslesmis = true;
        EslesenCift++;
        if (Tamamlandi)
            SaatiDurdur();
        return KartAcmaSonucu.Eslesme;
    }

    public bool TaramayiBaslat()
    {
        if (!EtkilesimeAcik || TaramaKullanildi)
            return false;

        // Yarım kalan seçim taramaya taşınmaz; önceden bulunan modüller geri gelmez.
        GecisiTamamla();
        TaramaKullanildi = Taraniyor = IslemBekleniyor = true;
        foreach (var kart in _kartlar.Where(kart => !kart.Eslesmis))
            kart.Acik = true;
        return true;
    }

    public void GecisiTamamla()
    {
        foreach (var kart in _kartlar)
            kart.Acik = false;
        _ilkKart = null;
        Taraniyor = IslemBekleniyor = false;
    }

    public void Duraklat()
    {
        if (_kapandi || Duraklatildi)
            return;
        SaatiDurdur();
        Duraklatildi = true;
        // İptal edilen animasyon/bekleme sonrasında açık kart veya giriş kilidi kalmaz.
        GecisiTamamla();
    }

    public void DevamEt()
    {
        if (_kapandi || !Duraklatildi)
            return;
        Duraklatildi = false;
        if (!Tamamlandi)
            SaatiBaslat();
    }

    public void Sonlandir()
    {
        SaatiDurdur();
        GecisiTamamla();
        _kapandi = true;
    }

    public OyunSonucu SonucuAl()
    {
        if (!Tamamlandi)
            throw new InvalidOperationException("Oyun henüz tamamlanmadı.");
        return new(OyunKimligi, Seviye, Sure, Hamle, EslesenCift, Skor, TaramaKullanildi);
    }

    public static int KartSayisi(OyunSeviyesi seviye) => seviye switch
    {
        OyunSeviyesi.Kolay => 12,
        OyunSeviyesi.Orta => 16,
        OyunSeviyesi.Zor => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(seviye))
    };

    public static string SureMetni(TimeSpan sure) =>
        $"{(long)sure.TotalMinutes:00}:{sure.Seconds:00}";

    private void SaatiBaslat()
    {
        _baslangic = _saat.GetTimestamp();
        _sureCalisiyor = true;
    }

    private void SaatiDurdur()
    {
        if (!_sureCalisiyor)
            return;
        _birikenSure += _saat.GetElapsedTime(_baslangic);
        _sureCalisiyor = false;
    }
}

