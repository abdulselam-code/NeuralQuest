namespace NeuralQuest.Models;

public sealed class HafizaKarti
{
    public int Kimlik { get; }
    public int ModulKimligi { get; }
    public string Gorsel { get; }
    public string Baslik { get; }
    public bool Acik { get; internal set; }
    public bool Eslesmis { get; internal set; }

    internal HafizaKarti(int kimlik, int modulKimligi, string gorsel, string baslik)
    {
        Kimlik = kimlik;
        ModulKimligi = modulKimligi;
        Gorsel = gorsel;
        Baslik = baslik;
    }
}

public enum OyunSeviyesi { Kolay, Orta, Zor }
public enum KartAcmaSonucu { Reddedildi, IlkKart, Eslesme, FarkliKartlar }

public sealed record OyunSonucu(
    Guid OyunKimligi, OyunSeviyesi Seviye, TimeSpan Sure,
    int Hamle, int KurtarilanModul, int Skor, bool? TaramaKullanildi = null);

