using NeuralQuest.Models;
using NeuralQuest.Services;

// Harici test paketi gerektirmeden gerçek oyun ve kayıt sınıfları çalıştırılır.
int basarili = 0;
void Dogrula(bool kosul, string mesaj)
{
    if (!kosul)
        throw new InvalidOperationException(mesaj);
}
void Test(string ad, Action islem)
{
    islem();
    Console.WriteLine($"BAŞARILI: {ad}");
    basarili++;
}
HafizaKarti[] Cift(OyunOturumu oyun, int modul = 0) =>
    oyun.Kartlar.Where(kart => kart.ModulKimligi == modul).ToArray();
void Esles(OyunOturumu oyun, int modul)
{
    var cift = Cift(oyun, modul);
    Dogrula(oyun.KartAc(cift[0].Kimlik) == KartAcmaSonucu.IlkKart, "İlk kart açılamadı.");
    Dogrula(oyun.KartAc(cift[1].Kimlik) == KartAcmaSonucu.Eslesme, "Çift eşleşmedi.");
    oyun.GecisiTamamla();
}
void Tamamla(OyunOturumu oyun)
{
    for (int modul = 0; modul < oyun.ToplamCift; modul++)
        Esles(oyun, modul);
}

foreach (int adet in new[] { 12, 16, 20 })
{
    Test($"{adet} kart, çiftler, ilk modüller ve tahta düzeni", () =>
    {
        var oyun = new OyunOturumu(adet, new Random(12));
        Dogrula(oyun.Kartlar.Count == adet, "Kart sayısı yanlış.");
        Dogrula(oyun.Sutun * oyun.Satir == adet, "Satır/sütun düzeni yanlış.");
        Dogrula(oyun.Sutun == (adet == 12 ? 3 : 4) && oyun.Satir == (adet == 20 ? 5 : 4), "Düzen beklenenden farklı.");
        Dogrula(oyun.Kartlar.Select(k => k.Kimlik).Distinct().Count() == adet, "Kart kimlikleri benzersiz değil.");
        Dogrula(oyun.Kartlar.GroupBy(k => k.ModulKimligi).All(g => g.Count() == 2), "Her modülden iki kart olmalı.");
        Dogrula(oyun.Kartlar.All(k => !k.Acik && !k.Eslesmis && k.ModulKimligi < adet / 2), "Başlangıç durumu veya modül seçimi yanlış.");
    });
    Test($"{adet} kartlık oyunun son eşleşmeye kadar tamamlanması", () =>
    {
        var saat = new DenemeSaati();
        var oyun = new OyunOturumu(adet, new Random(2), saat);
        saat.Ilerle(24);
        Tamamla(oyun);
        Dogrula(oyun.Tamamlandi && oyun.OnarimYuzdesi == 100 && oyun.Onarim == 1, "Onarım tamamlanmadı.");
        Dogrula(oyun.Hamle == adet / 2 && oyun.EslesenCift == adet / 2, "Sonuç sayaçları yanlış.");
        Dogrula(oyun.Kartlar.All(k => k.Eslesmis && !k.Acik), "Eşleşen kartların hücreleri kaybolmalı.");
        var sonuc = oyun.SonucuAl();
        Dogrula(sonuc.KurtarilanModul == adet / 2 && sonuc.Sure == TimeSpan.FromSeconds(24), "Sonuç bilgileri yanlış.");
        Dogrula(!oyun.TaramayiBaslat() && oyun.KartAc(0) == KartAcmaSonucu.Reddedildi, "Bitmiş oyun etkileşim aldı.");
        saat.Ilerle(300);
        Dogrula(oyun.Sure == sonuc.Sure && oyun.Skor == sonuc.Skor, "Bitmiş oyunda süre/skor değişti.");
    });
}

Test("Fisher–Yates ve yeniden başlatmada yeni karışım", () =>
{
    var random = new Random(23);
    var ilk = new OyunOturumu(20, random);
    var yeni = new OyunOturumu(20, random);
    Dogrula(!ilk.Kartlar.Select(k => k.Kimlik).SequenceEqual(yeni.Kartlar.Select(k => k.Kimlik)), "Yeni destenin sırası değişmedi.");
    Dogrula(ilk.OyunKimligi != yeni.OyunKimligi && yeni.Hamle == 0 && !yeni.TaramaKullanildi, "Yeni oturum temiz başlamadı.");
});

Test("Aynı karta çift dokunma ve geçersiz kimlik", () =>
{
    var oyun = new OyunOturumu(12);
    Dogrula(oyun.KartAc(-1) == KartAcmaSonucu.Reddedildi, "Geçersiz kimlik kabul edildi.");
    oyun.KartAc(0);
    Dogrula(oyun.KartAc(0) == KartAcmaSonucu.Reddedildi && oyun.Hamle == 0, "Aynı kart hamle sayıldı.");
    Dogrula(oyun.Kartlar.Count(k => k.Acik) == 1, "Birden fazla kart açıldı.");
});

Test("Yanlış eşleşme sırasında hızlı üçüncü dokunma engellenir", () =>
{
    var oyun = new OyunOturumu(12);
    oyun.KartAc(Cift(oyun, 0)[0].Kimlik);
    Dogrula(oyun.KartAc(Cift(oyun, 1)[0].Kimlik) == KartAcmaSonucu.FarkliKartlar, "Yanlış çift algılanmadı.");
    for (int i = 0; i < 100; i++)
        Dogrula(oyun.KartAc(i % 12) == KartAcmaSonucu.Reddedildi, "Bekleme sırasında dokunma kabul edildi.");
    Dogrula(oyun.Hamle == 1 && oyun.EslesenCift == 0 && oyun.Kartlar.Count(k => k.Acik) == 2, "Yanlış eşleşme sayaçları bozuldu.");
    Dogrula(!oyun.TaramayiBaslat() && !oyun.TaramaKullanildi, "Bekleme sırasında ipucu harcandı.");
    oyun.GecisiTamamla();
    Dogrula(oyun.Kartlar.All(k => !k.Acik) && oyun.EtkilesimeAcik, "Yanlış kartlar kapanmadı.");
});

Test("Doğru eşleşme, yüzde 17 ve hücrelerin sırası", () =>
{
    var oyun = new OyunOturumu(12);
    int[] sira = oyun.Kartlar.Select(k => k.Kimlik).ToArray();
    var cift = Cift(oyun);
    oyun.KartAc(cift[0].Kimlik);
    oyun.KartAc(cift[1].Kimlik);
    Dogrula(oyun.Kartlar.Count(k => k.Acik) == 2 && oyun.IslemBekleniyor, "Eşleşme ön yüzleri beklemedi.");
    Dogrula(oyun.OnarimYuzdesi == 17, "Yüzde yuvarlaması yanlış.");
    Dogrula(oyun.KartAc(Cift(oyun, 1)[0].Kimlik) == KartAcmaSonucu.Reddedildi, "Eşleşme animasyonunda üçüncü kart açıldı.");
    oyun.GecisiTamamla();
    Dogrula(oyun.KartAc(cift[0].Kimlik) == KartAcmaSonucu.Reddedildi, "Bulunmuş karta tekrar dokunuldu.");
    Dogrula(sira.SequenceEqual(oyun.Kartlar.Select(k => k.Kimlik)), "Eşleşme kartların yerini değiştirdi.");
});

Test("Tarama yarım seçimi temizler, eşleşenleri getirmez, sayaçları değiştirmez", () =>
{
    var oyun = new OyunOturumu(16);
    Esles(oyun, 0);
    oyun.KartAc(Cift(oyun, 1)[0].Kimlik);
    int hamle = oyun.Hamle;
    Dogrula(oyun.TaramayiBaslat(), "Tarama başlamadı.");
    Dogrula(oyun.Kartlar.Count(k => k.Acik) == 14 && oyun.Kartlar.Where(k => k.Eslesmis).All(k => !k.Acik), "Tarama eşleşmiş kartları getirdi.");
    Dogrula(oyun.Hamle == hamle && oyun.EslesenCift == 1 && oyun.TaramaKullanildi, "Tarama sayaçları değiştirdi.");
    Dogrula(oyun.KartAc(0) == KartAcmaSonucu.Reddedildi && !oyun.TaramayiBaslat(), "Tarama sırasında giriş kabul edildi.");
    oyun.GecisiTamamla();
    Dogrula(!oyun.TaramayiBaslat() && oyun.Kartlar.All(k => !k.Acik), "Tarama ikinci kez kullanılabildi.");
    Esles(oyun, 1);
    Dogrula(oyun.Hamle == hamle + 1, "Yarım seçim tarama sonrasına sızdı.");
});

Test("Duraklatma süreyi dondurur ve yanlış çift kilidini temizler", () =>
{
    var saat = new DenemeSaati();
    var oyun = new OyunOturumu(12, saat: saat);
    saat.Ilerle(35);
    oyun.KartAc(Cift(oyun, 0)[0].Kimlik);
    oyun.KartAc(Cift(oyun, 1)[0].Kimlik);
    oyun.Duraklat();
    saat.Ilerle(600);
    Dogrula(oyun.Sure == TimeSpan.FromSeconds(35) && !oyun.IslemBekleniyor, "Duraklatma süreyi veya kilidi bozdu.");
    Dogrula(oyun.Kartlar.All(k => !k.Acik) && oyun.KartAc(0) == KartAcmaSonucu.Reddedildi, "Duraklatmada kart etkileşimi var.");
    oyun.DevamEt();
    saat.Ilerle(5);
    Dogrula(oyun.Sure == TimeSpan.FromSeconds(40) && oyun.EtkilesimeAcik, "Devam sırasında süre yanlış.");
});

Test("Tarama sırasında duraklatma ipucunu geri vermez", () =>
{
    var oyun = new OyunOturumu(12);
    oyun.TaramayiBaslat();
    oyun.Duraklat();
    oyun.DevamEt();
    Dogrula(!oyun.Taraniyor && oyun.TaramaKullanildi && !oyun.TaramayiBaslat(), "Tarama hakkı geri geldi.");
    Dogrula(oyun.Kartlar.All(k => !k.Acik), "Tarama kartları açık kaldı.");
});

Test("Eşleşme sırasında duraklatma modülü kaybetmez", () =>
{
    var oyun = new OyunOturumu(12);
    var cift = Cift(oyun);
    oyun.KartAc(cift[0].Kimlik);
    oyun.KartAc(cift[1].Kimlik);
    oyun.Duraklat();
    oyun.DevamEt();
    Dogrula(oyun.EslesenCift == 1 && cift.All(k => k.Eslesmis && !k.Acik), "Kabul edilmiş eşleşme kayboldu.");
});

Test("Sonlandırma ve yeni oturumun birbirinden bağımsızlığı", () =>
{
    var saat = new DenemeSaati();
    var eski = new OyunOturumu(12, saat: saat);
    eski.TaramayiBaslat();
    eski.Sonlandir();
    saat.Ilerle(20);
    eski.DevamEt();
    Dogrula(eski.Sure == TimeSpan.Zero && eski.KartAc(0) == KartAcmaSonucu.Reddedildi, "Kapanan oturum yeniden başladı.");
    var yeni = new OyunOturumu(12);
    eski.GecisiTamamla();
    Dogrula(yeni.EtkilesimeAcik && yeni.Hamle == 0 && !yeni.TaramaKullanildi, "Eski işlem yeni oyunu değiştirdi.");
});

Test("Skor ve süre biçimi", () =>
{
    var hizliSaat = new DenemeSaati();
    var hizli = new OyunOturumu(12, saat: hizliSaat);
    hizliSaat.Ilerle(10);
    Tamamla(hizli);
    var yavasSaat = new DenemeSaati();
    var yavas = new OyunOturumu(12, saat: yavasSaat);
    yavasSaat.Ilerle(70);
    yavas.KartAc(Cift(yavas, 0)[0].Kimlik);
    yavas.KartAc(Cift(yavas, 1)[0].Kimlik);
    yavas.GecisiTamamla();
    Tamamla(yavas);
    Dogrula(hizli.Skor > yavas.Skor, "Hızlı ve az hamleli oyun daha yüksek skor almadı.");
    var uzunSaat = new DenemeSaati();
    var uzun = new OyunOturumu(12, saat: uzunSaat);
    uzunSaat.Ilerle(100000);
    Dogrula(uzun.Skor == 0, "Skor negatif oldu.");
    Dogrula(OyunOturumu.SureMetni(TimeSpan.FromSeconds(125)) == "02:05", "Süre biçimi yanlış.");
});

var depo = new Dictionary<string, string>();
OyunIstatistikServisi Servis() => new(
    anahtar => depo.GetValueOrDefault(anahtar, ""),
    (anahtar, deger) => depo[anahtar] = deger);

Test("Kayıt yeniden okunur, aynı sonuç iki kez sayılmaz", () =>
{
    var oyun = new OyunOturumu(12);
    Tamamla(oyun);
    Servis().Kaydet(oyun.SonucuAl());
    Servis().Kaydet(oyun.SonucuAl());
    var tekrar = Servis().Oku();
    Dogrula(tekrar.ToplamTamamlananOyun == 1 && tekrar.ToplamSkor == oyun.Skor, "Tekrar okuma veya kayıt tekilleştirmesi hatalı.");
    Dogrula(tekrar.Seviyeler["Kolay"].TamamlanmaSayisi == 1, "Seviye kaydı yanlış.");
});

Test("Her seviyenin rekoru ve toplamlar ayrı tutulur", () =>
{
    Servis().Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Orta, TimeSpan.FromSeconds(70), 12, 8, 6000));
    Servis().Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Orta, TimeSpan.FromSeconds(90), 10, 8, 6500));
    Servis().Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Zor, TimeSpan.FromSeconds(100), 15, 10, 8000));
    var kayit = Servis().Oku();
    Dogrula(kayit.ToplamTamamlananOyun == 4 && kayit.Seviyeler.Count == 3, "Toplamlar veya seviyeler eksik.");
    Dogrula(kayit.Seviyeler["Orta"].EnIyiSureTicks == TimeSpan.FromSeconds(70).Ticks &&
            kayit.Seviyeler["Orta"].EnAzHamle == 10 && kayit.Seviyeler["Orta"].EnIyiSkor == 6500, "Bağımsız rekorlar yanlış.");
});

Test("Bozuk kayıt ve depolama hatası", () =>
{
    var bozuk = new OyunIstatistikServisi(_ => "{bozuk", (_, _) => { });
    Dogrula(bozuk.Oku().ToplamTamamlananOyun == 0, "Bozuk kayıt güvenli okunamadı.");
    var hataVeren = new OyunIstatistikServisi(_ => "", (_, _) => throw new IOException("Deneme"));
    bool yakalandi = false;
    try { hataVeren.Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Kolay, TimeSpan.Zero, 6, 6, 6000)); }
    catch (IOException) { yakalandi = true; }
    Dogrula(yakalandi, "Depolama hatası kayboldu.");
});

foreach (int adet in new[] { 12, 16, 20 })
{
    Test($"{adet} kart: bildirim eşikleri, sıra, tek gösterim ve açıkken kart eşleştirme", () =>
    {
        var oyun = new OyunOturumu(adet);
        var saat = new DenemeSaati();
        var akis = new SistemBildirimAkisi(saat);
        akis.DevamEt();
        Dogrula(akis.Metin == "KRİTİK HATA! Hafıza modülleri hasarlı.", "%0 mesajı yok.");
        var gorulen = new List<string> { akis.Metin! };
        saat.IlerleSure(SistemBildirimAkisi.ToplamSure);
        akis.Guncelle();
        Dogrula(akis.Metin is null, "İlk mesaj bitmedi.");
        int[] esikler = [25, 50, 75];
        string[] mesajlar = ["VERİLER KURTARILIYOR...", "SİNİR AĞLARI YENİDEN BAĞLANIYOR.",
                            "SİSTEM KARARLILIĞI SAĞLANIYOR."];
        int sonraki = 0;
        for (int modul = 0; modul < oyun.ToplamCift; modul++)
        {
            Esles(oyun, modul);
            akis.IlerlemeyiBildir(oyun.EslesenCift, oyun.ToplamCift);
            // Her çiftte aynı bilgi yeniden iletilse bile mesaj tekrarlanamaz.
            akis.IlerlemeyiBildir(oyun.EslesenCift, oyun.ToplamCift);
            bool gecildi = sonraki < esikler.Length && oyun.Onarim * 100 >= esikler[sonraki];
            Dogrula(akis.BekleyenSayisi == (gecildi ? 1 : 0), "Eşik erken/geç veya iki kez tetiklendi.");
            if (gecildi)
            {
                Dogrula(akis.Metin == mesajlar[sonraki], "Mesaj veya sırası yanlış.");
                gorulen.Add(akis.Metin!);
                sonraki++;
                saat.IlerleSure(SistemBildirimAkisi.ToplamSure);
                akis.Guncelle();
            }
        }
        Dogrula(gorulen.Count == 4 && gorulen.Distinct().Count() == 4, "Her eşik bir kez görünmedi.");
        Dogrula(akis.Metin is null && oyun.Tamamlandi && oyun.Hamle == oyun.ToplamCift,
            "Bildirimler oyun mantığını etkiledi veya ikinci %100 bildirimi açtı.");
    });
}

Test("Hızlı eşleşmeler bildirimleri sırayla kuyruğa alır", () =>
{
    var saat = new DenemeSaati();
    var akis = new SistemBildirimAkisi(saat);
    akis.DevamEt();
    akis.IlerlemeyiBildir(5, 6); // %83: %25 ve %75'e eşit olmadan tüm eşikleri geçer.
    akis.IlerlemeyiBildir(5, 6);
    Dogrula(akis.BekleyenSayisi == 4, "Mesajlar kayboldu veya tekrar kuyruğa girdi.");
    string[] sira = ["KRİTİK HATA! Hafıza modülleri hasarlı.", "VERİLER KURTARILIYOR...",
                    "SİNİR AĞLARI YENİDEN BAĞLANIYOR.", "SİSTEM KARARLILIĞI SAĞLANIYOR."];
    foreach (string metin in sira)
    {
        Dogrula(akis.Metin == metin, "Kuyruk sırası bozuldu.");
        saat.IlerleSure(SistemBildirimAkisi.ToplamSure);
        akis.Guncelle();
    }
    Dogrula(akis.Metin is null, "Kuyruk boşalmadı.");
});

Test("Bildirim 2 saniye okunur kalır, yumuşak belirir ve söner", () =>
{
    var saat = new DenemeSaati();
    var akis = new SistemBildirimAkisi(saat);
    akis.DevamEt();
    Dogrula(akis.Gorunurluk == 0, "Belirme başlangıcı yanlış.");
    saat.IlerleSure(SistemBildirimAkisi.BelirmeSuresi / 2);
    akis.Guncelle();
    Dogrula(Math.Abs(akis.Gorunurluk - 0.5) < 0.001, "Belirme ara karesi yanlış.");
    saat.IlerleSure(SistemBildirimAkisi.BelirmeSuresi / 2 + SistemBildirimAkisi.BeklemeSuresi);
    akis.Guncelle();
    Dogrula(akis.Metin is not null && akis.Gorunurluk == 1, "2 saniye tam görünür kalmadı.");
    saat.IlerleSure(SistemBildirimAkisi.SonmeSuresi / 2);
    akis.Guncelle();
    Dogrula(Math.Abs(akis.Gorunurluk - 0.5) < 0.001, "Sönme ara karesi yanlış.");
    saat.IlerleSure(SistemBildirimAkisi.SonmeSuresi / 2);
    akis.Guncelle();
    Dogrula(akis.Metin is null, "Sönme bitmedi.");
});

Test("Duraklatma/aradan dönüş bildirim süresini ve sırasını korur", () =>
{
    var saat = new DenemeSaati();
    var akis = new SistemBildirimAkisi(saat);
    akis.DevamEt();
    akis.IlerlemeyiBildir(3, 6);
    saat.IlerleSure(TimeSpan.FromMilliseconds(90));
    akis.Duraklat();
    double onceki = akis.Gorunurluk;
    saat.Ilerle(600);
    akis.Duraklat();
    akis.Guncelle();
    Dogrula(akis.Gorunurluk == onceki && akis.BekleyenSayisi == 3, "Duraklatma süre tüketti.");
    akis.DevamEt();
    akis.DevamEt();
    saat.IlerleSure(SistemBildirimAkisi.ToplamSure - TimeSpan.FromMilliseconds(90));
    akis.Guncelle();
    Dogrula(akis.Metin == "VERİLER KURTARILIYOR..." && akis.Gorunurluk == 0, "Kalan süreden devam etmedi.");
});

Test("Yeniden başlatma/çıkış eski kuyruğu kapatır, yeni oyunun eşiklerini sıfırlar", () =>
{
    var saat = new DenemeSaati();
    var eski = new SistemBildirimAkisi(saat);
    eski.DevamEt();
    eski.IlerlemeyiBildir(5, 6);
    eski.Sonlandir();
    var yeni = new SistemBildirimAkisi(saat);
    yeni.DevamEt();
    saat.Ilerle(100);
    eski.DevamEt();
    eski.IlerlemeyiBildir(6, 6);
    eski.Guncelle();
    Dogrula(eski.Metin is null && eski.BekleyenSayisi == 0, "Eski oturum mesajı geri geldi.");
    Dogrula(yeni.BekleyenSayisi == 1 && yeni.Metin!.StartsWith("KRİTİK HATA!"), "Yeni oyun temiz başlamadı.");
    yeni.IlerlemeyiBildir(2, 6);
    Dogrula(yeni.Metin == "VERİLER KURTARILIYOR...", "Yeni oyunda %25 eşiği sıfırlanmadı.");
});

Test("Sonuç ekranı bekleyen bildirimleri temizler ve %100 mesajının tek sahibidir", () =>
{
    var akis = new SistemBildirimAkisi();
    akis.IlerlemeyiBildir(6, 6);
    Dogrula(akis.BekleyenSayisi == 4, "%100 ayrıca kuyruğa girdi.");
    akis.Sonlandir();
    akis.Sonlandir();
    akis.DevamEt();
    Dogrula(akis.Metin is null && SistemBildirimAkisi.TamamlanmaMesaji ==
        "SİSTEM HAFIZASI BAŞARIYLA ONARILDI!", "Sonuç mesajı/temizliği yanlış.");
});

Test("Tek modal: yeniden başlatmadan vazgeç ve geri tuşu duraklatmaya döner", () =>
{
    var panel = new OyunPanelAkisi();
    panel.Duraklat();
    panel.YenidenBaslatOnayi();
    panel.Duraklat(); // Android arka plan olayı onay durumunu değiştirmemeli.
    Dogrula(panel.Durum == OyunPaneli.YenidenBaslat, "Onay arka plan olayıyla kayboldu.");
    panel.Geri();
    Dogrula(panel.Durum == OyunPaneli.Duraklatildi, "Vazgeç duraklatmaya dönmedi.");
    panel.Geri();
    Dogrula(!panel.Acik, "Duraklatmada geri oyuna dönmedi.");
});

Test("Çıkışta geri tuşu aktif, duraklatılmış ve onay durumlarını geri yükler", () =>
{
    foreach (var onceki in new[] { OyunPaneli.Kapali, OyunPaneli.Duraklatildi, OyunPaneli.YenidenBaslat })
    {
        var panel = new OyunPanelAkisi();
        if (onceki == OyunPaneli.Duraklatildi) panel.Duraklat();
        if (onceki == OyunPaneli.YenidenBaslat) panel.YenidenBaslatOnayi();
        panel.CikisOnayi();
        panel.CikisOnayi(); // Tekrarlanan dokunma önceki durumu ezmemeli.
        panel.Duraklat();
        panel.Geri();
        Dogrula(panel.Durum == onceki, "Çıkıştan geri yanlış duruma döndü.");
        panel.Kapat();
        Dogrula(!panel.Acik, "Devam et modalı kapatmadı.");
    }
});

Test("Modal iptali oyun ilerlemesini, tarama hakkını ve durmuş süreyi korur", () =>
{
    var saat = new DenemeSaati();
    var oyun = new OyunOturumu(12, saat: saat);
    Esles(oyun, 0);
    oyun.TaramayiBaslat();
    oyun.GecisiTamamla();
    saat.Ilerle(12);
    var panel = new OyunPanelAkisi();
    oyun.Duraklat();
    panel.YenidenBaslatOnayi();
    saat.Ilerle(100);
    panel.Geri();
    Dogrula(oyun.Sure.TotalSeconds == 12 && oyun.EslesenCift == 1 && oyun.TaramaKullanildi,
        "Modal iptali ilerlemeyi değiştirdi.");
    Dogrula(!oyun.EtkilesimeAcik && !oyun.TaramayiBaslat(), "Panel açıkken etkileşim kabul edildi.");
    panel.Geri();
    oyun.DevamEt();
    Dogrula(oyun.EtkilesimeAcik && !panel.Acik, "Devam çalışmadı.");
});

Test("Boş kayıtta beş başarı da kilitlidir", () =>
{
    var basarilar = OyunBasarilari.Hesapla(new());
    Dogrula(basarilar.Count == 5 && basarilar.All(b => !b.Kazanildi), "Kanıtsız başarı kazanılmış gösterildi.");
});

Test("Başarılar gerçek seviye ve bir dakikanın altındaki süreyle hesaplanır", () =>
{
    var kayit = new OyunIstatistikleri { ToplamTamamlananOyun = 1 };
    kayit.Seviyeler["Orta"] = new() { TamamlanmaSayisi = 1, EnIyiSureTicks = TimeSpan.FromMinutes(1).Ticks };
    var ilk = OyunBasarilari.Hesapla(kayit);
    Dogrula(ilk.All(b => !b.Kazanildi), "Orta seviye İlk Adım veya tam 60 saniye Hızlı Zihin sayıldı.");
    kayit.Seviyeler["Kolay"] = new() { TamamlanmaSayisi = 1, EnIyiSureTicks = TimeSpan.FromSeconds(59).Ticks };
    kayit.Seviyeler["Zor"] = new() { TamamlanmaSayisi = 1, EnIyiSureTicks = TimeSpan.FromSeconds(80).Ticks };
    var son = OyunBasarilari.Hesapla(kayit);
    Dogrula(son.Take(4).All(b => b.Kazanildi) && !son[4].Kazanildi, "Seviye/süre kanıtları yanlış değerlendirildi.");
});

Test("Eski JSON kayıt genişlemesinde toplamlar ve rekorlar korunur, tarama başarısı varsayılmaz", () =>
{
    string json = """
        {"ToplamTamamlananOyun":3,"ToplamSkor":17000,"SonOyunKimligi":"00000000-0000-0000-0000-000000000001",
         "Seviyeler":{"Kolay":{"TamamlanmaSayisi":2,"EnIyiSureTicks":450000000,"EnAzHamle":6,"EnIyiSkor":5800},
                      "Zor":{"TamamlanmaSayisi":1,"EnIyiSureTicks":900000000,"EnAzHamle":12,"EnIyiSkor":8000}}}
        """;
    var servis = new OyunIstatistikServisi(_ => json, (_, deger) => json = deger);
    var eski = servis.Oku();
    Dogrula(!OyunBasarilari.Hesapla(eski)[4].Kazanildi, "Eski oyunun tarama bilgisi uyduruldu.");
    servis.Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Orta, TimeSpan.FromSeconds(70), 8, 8, 7000));
    var yeni = servis.Oku();
    Dogrula(yeni.ToplamTamamlananOyun == 4 && yeni.ToplamSkor == 24000 &&
        yeni.Seviyeler["Kolay"].EnIyiSureTicks == 450000000 && yeni.Seviyeler["Zor"].EnIyiSkor == 8000 &&
        !yeni.TaramasizOyunTamamlandi, "Geriye uyumlu kayıt mevcut değerleri bozdu.");
});

Test("Mükemmel Onarım yalnızca yeni ve doğrulanmış taramasız sonuçla açılır ve kalıcıdır", () =>
{
    string json = "";
    OyunIstatistikServisi YeniServis() => new(_ => json, (_, deger) => json = deger);
    var taramali = new OyunOturumu(12);
    taramali.TaramayiBaslat();
    taramali.GecisiTamamla();
    Tamamla(taramali);
    Dogrula(taramali.SonucuAl().TaramaKullanildi == true, "Tarama sonucu kayboldu.");
    YeniServis().Kaydet(taramali.SonucuAl());
    Dogrula(!OyunBasarilari.Hesapla(YeniServis().Oku())[4].Kazanildi, "Tarama kullanılmış oyunda başarı açıldı.");
    var taramasiz = new OyunOturumu(16);
    Tamamla(taramasiz);
    YeniServis().Kaydet(taramasiz.SonucuAl());
    YeniServis().Kaydet(taramasiz.SonucuAl());
    Dogrula(OyunBasarilari.Hesapla(YeniServis().Oku())[4].Kazanildi &&
        YeniServis().Oku().ToplamTamamlananOyun == 2, "Başarı kalıcı değil veya aynı sonuç tekrar sayıldı.");
    YeniServis().Kaydet(new(Guid.NewGuid(), OyunSeviyesi.Zor, TimeSpan.FromSeconds(100), 10, 10, 8000, true));
    Dogrula(YeniServis().Oku().TaramasizOyunTamamlandi, "Sonraki taramalı oyun başarıyı geri kilitledi.");
});

Console.WriteLine($"\n{basarili} mantık testi başarıyla tamamlandı.");

sealed class DenemeSaati : TimeProvider
{
    private long _an;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _an;
    public void Ilerle(int saniye) => _an += TimeSpan.FromSeconds(saniye).Ticks;
    public void IlerleSure(TimeSpan sure) => _an += sure.Ticks;
}

