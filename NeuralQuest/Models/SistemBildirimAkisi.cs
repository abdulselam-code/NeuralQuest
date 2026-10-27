namespace NeuralQuest.Models;

// Yalnızca aktif oyun zamanı ilerler. Gecikmiş Task/callback yoktur; her oyun kendi kuyruğunu taşır.
public sealed class SistemBildirimAkisi
{
    public const string TamamlanmaMesaji = "SİSTEM HAFIZASI BAŞARIYLA ONARILDI!";
    public static readonly TimeSpan BelirmeSuresi = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan BeklemeSuresi = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SonmeSuresi = TimeSpan.FromMilliseconds(220);
    public static TimeSpan ToplamSure => BelirmeSuresi + BeklemeSuresi + SonmeSuresi;

    private static readonly (int Yuzde, string Metin)[] Esikler =
    [
        (25, "VERİLER KURTARILIYOR..."),
        (50, "SİNİR AĞLARI YENİDEN BAĞLANIYOR."),
        (75, "SİSTEM KARARLILIĞI SAĞLANIYOR.")
    ];
    private readonly TimeProvider _saat;
    private readonly Queue<string> _kuyruk = new();
    private TimeSpan _gecen;
    private long _sonAn;
    private int _sonrakiEsik;
    private bool _calisiyor;
    private bool _sonlandi;

    public string? Metin => _kuyruk.TryPeek(out var metin) ? metin : null;
    public int BekleyenSayisi => _kuyruk.Count;
    public double Gorunurluk
    {
        get
        {
            double giris = Math.Clamp(_gecen / BelirmeSuresi, 0, 1);
            double cikis = Math.Clamp((ToplamSure - _gecen) / SonmeSuresi, 0, 1);
            double t = Math.Min(giris, cikis);
            return t * t * (3 - 2 * t);
        }
    }

    public SistemBildirimAkisi(TimeProvider? saat = null)
    {
        _saat = saat ?? TimeProvider.System;
        _kuyruk.Enqueue("KRİTİK HATA! Hafıza modülleri hasarlı.");
    }

    public void IlerlemeyiBildir(int eslesenCift, int toplamCift)
    {
        if (toplamCift <= 0 || eslesenCift < 0 || eslesenCift > toplamCift)
            throw new ArgumentOutOfRangeException(nameof(eslesenCift));
        if (_sonlandi)
            return;
        Guncelle();
        // Tam sayıya yuvarlama veya tam eşitlik yerine gerçek çift oranıyla eşik geçişi.
        while (_sonrakiEsik < Esikler.Length &&
               (long)eslesenCift * 100 >= (long)toplamCift * Esikler[_sonrakiEsik].Yuzde)
            _kuyruk.Enqueue(Esikler[_sonrakiEsik++].Metin);
        // %100 ayrı bildirim açmaz; sonuç paneli TamamlanmaMesaji'nı gösterir.
    }

    public void DevamEt()
    {
        if (_sonlandi || _calisiyor)
            return;
        _sonAn = _saat.GetTimestamp();
        _calisiyor = true;
    }

    public void Duraklat()
    {
        Guncelle();
        _calisiyor = false;
    }

    public void Guncelle()
    {
        if (!_calisiyor || _sonlandi)
            return;
        long simdi = _saat.GetTimestamp();
        if (_kuyruk.Count > 0)
        {
            _gecen += _saat.GetElapsedTime(_sonAn, simdi);
            if (_gecen >= ToplamSure)
            {
                _kuyruk.Dequeue();
                // Sonraki mesaj ilk karesinden başlar; uzun bir UI karesi mesaj atlatamaz.
                _gecen = TimeSpan.Zero;
            }
        }
        _sonAn = simdi;
    }

    public void Sonlandir()
    {
        _sonlandi = true;
        _calisiyor = false;
        _kuyruk.Clear();
        _gecen = TimeSpan.Zero;
    }
}
