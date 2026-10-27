namespace NeuralQuest.Models;

public enum OyunPaneli { Kapali, Duraklatildi, YenidenBaslat, Cikis }

// Tek bir modal durum vardır; geri tuşu hiçbir zaman ikinci bir panel açmaz.
public sealed class OyunPanelAkisi
{
    private OyunPaneli _cikisOncesi;
    public OyunPaneli Durum { get; private set; }
    public bool Acik => Durum != OyunPaneli.Kapali;

    public void Duraklat()
    {
        if (!Acik)
            Durum = OyunPaneli.Duraklatildi;
    }

    public void YenidenBaslatOnayi() => Durum = OyunPaneli.YenidenBaslat;

    public void CikisOnayi()
    {
        if (Durum == OyunPaneli.Cikis)
            return;
        _cikisOncesi = Durum;
        Durum = OyunPaneli.Cikis;
    }

    public void Geri()
    {
        Durum = Durum switch
        {
            OyunPaneli.YenidenBaslat => OyunPaneli.Duraklatildi,
            OyunPaneli.Cikis => _cikisOncesi,
            _ => OyunPaneli.Kapali
        };
    }

    public void Kapat() => Durum = OyunPaneli.Kapali;
}
