# Neural Quest

Neural Quest, hasar görmüş bir yapay zekânın hafıza modüllerini onarmak üzerine kurulu Türkçe bir hafıza kartı oyunudur. Aynı güvenlik modülünü temsil eden iki kartı eşleştirerek çekirdek onarımını tamamlamayı amaçlar. Koyu lacivert arka planı, neon kartları ve holografik panelleri bilim kurgu temasını oluşturur.

## Özellikler

- Güvenlik kalkanı, şifreleme kilidi, yapay zekâ çekirdeği ve diğer siber güvenlik modüllerinden oluşan özgün kart seti.
- Her oyunda karıştırılan kartlar; eşleşen kartların yerinde kalan boş hücreler.
- Süre, hamle, skor, kurtarılan modül ve çekirdek onarım göstergeleri.
- Oyun başına bir kez kullanılabilen **Sistem Taraması** ipucu.
- Duraklatma, yeniden başlatma, çıkış onayı ve Android geri tuşu akışları.
- Dijital çözülme, neon parçacıklar, enerji halkası ve kısa ışık izi.
- Onarım eşikleri geçildiğinde sırayla gösterilen Türkçe sistem bildirimleri.
- Neon HUD bilgi panelleri, başarılar ve cihazda kalıcı tutulan istatistikler.
- Yeni Neural Quest uygulama ikonu ve koyu temayla uyumlu Android sistem çubukları.

Oyun çevrimdışı çalışır. Bir sunucu hesabı veya API anahtarı gerektirmez. İstatistikler cihazda saklanır.

## Zorluk seviyeleri

| Seviye | Kart | Eşleşme | Düzen (sütun × satır) |
|---|---:|---:|---|
| Kolay | 12 | 6 | 3 × 4 |
| Orta | 16 | 8 | 4 × 4 |
| Zor | 20 | 10 | 4 × 5 |

Her çift açma girişimi bir hamledir. Skor, modül başına 1000 puandan hesaplanır; gerekli çift sayısını aşan her hamle 100, her aktif saniye 5 puan düşürür. Skor sıfırın altına inmez. Duraklatılan süre aktif oyun süresine eklenmez.

## Teknolojiler ve yapı

- .NET 10, .NET MAUI, C# ve XAML.
- Android hedefi `net10.0-android`; yapılandırılan minimum Android API seviyesi 21.
- MAUI `GraphicsView` / `IDrawable` ile çizilen HUD ve parçacık efektleri.
- Yerel kayıt için MAUI Preferences; JSON tabanlı istatistik modeli.
- .NET 10 üzerinde çalışan, arayüzden bağımsız konsol mantık testleri.

Temel hedef ve doğrulama platformu Android'dir. Projede Windows, iOS ve Mac Catalyst platform dosyaları da bulunur; bu platformların dağıtımı ayrıca doğrulanmalıdır.

```text
NeuralQuest.slnx
NeuralQuest/
  Models/           # Oyun oturumu, kartlar ve bildirim/panel akışları
  Services/         # Cihaz kayıtları ve istatistikler
  Effects/          # Dijital kart çözülmesi
  Controls/         # Neon HUD bileşenleri
  Platforms/Android/
  Resources/        # Özgün kartlar, menü görselleri, ikon ve fontlar
NeuralQuest.Tests/  # Mantık testleri ve Android arayüz test betikleri
```

## Kurulum ve Android Debug derlemesi

Gerekenler: .NET 10 SDK, MAUI Android iş yükü, uyumlu Android SDK/JDK ve bir Android emülatörü veya USB hata ayıklaması yetkilendirilmiş telefon. Bunlar MAUI destekli Visual Studio üzerinden de kurulabilir.

```powershell
git clone https://github.com/abdulselam-code/NeuralQuest.git
cd NeuralQuest
dotnet workload install maui-android
dotnet build NeuralQuest/NeuralQuest.csproj -f net10.0-android -p:TargetFrameworks=net10.0-android -p:EmbedAssembliesIntoApk=true
```

`TargetFrameworks` parametresi bu komutu Android ile sınırlar; diğer platform iş yüklerinin kurulu olmasını gerektirmez. Visual Studio'da `NeuralQuest.slnx` açılıp Android cihazı seçilerek de çalıştırılabilir.

İmzalı Debug APK, başarılı derlemeden sonra şu konumda oluşur:

```text
NeuralQuest/bin/Debug/net10.0-android/com.companyname.neuralquest-Signed.apk
```

ADB ile kurulumda cihazı açıkça seçin:

```powershell
adb devices -l
adb -s CIHAZ_SERI_NUMARASI install -r --no-incremental NeuralQuest/bin/Debug/net10.0-android/com.companyname.neuralquest-Signed.apk
```

Mevcut kurulumu güncellemek aynı imzayı gerektirir. Kayıtları korumak için uygulamayı kaldırmayın veya verilerini temizlemeyin. İmzalama anahtarları bu depoya eklenmez; başka bir geliştirici makinesinin Debug imzası mevcut telefon kurulumuyla farklı olabilir.

## Testler

Mantık testleri Android emülatörü gerektirmez:

```powershell
dotnet run --project NeuralQuest.Tests/NeuralQuest.Tests.csproj
```

Mevcut 34 test; üç zorluk seviyesini, eşleştirmeyi, sayaçları, taramayı, duraklatmayı, kayıtları/başarıları, bildirim eşiklerini ve oturum temizliğini kapsar.

`android_*.py` betikleri gerçek dokunmalar ve ekran ağaçlarıyla Android arayüzünü denetler. Çalıştırmadan önce ADB yolu ve cihaz hedefi kontrol edilmelidir; mevcut betikler `emulator-5554` için hazırlanmıştır. Python gerekir. Betikler uygulama verilerini temizlemez; tamamlanan test oyunları emülatör istatistiklerine eklenebilir.

`verify_neon_preservation.py`, yerel `output` içindeki geçmiş hash kayıtlarına bağlı bir koruma denetimidir. Bu referans çıktıları Git'e gönderilmez; temiz klondaki bağımsız mantık testlerinin parçası değildir.

## Doğrulama sınırları

Son fiziksel cihaz kontrolleri Samsung Galaxy A32 / Android 13 üzerinde yapıldı. Çok hızlı panel kapat/aç sırasında bazı emülatörlerde daha önce dokunma kaybı gözlendi; fiziksel cihazdaki başarılı kontroller bu durumun tüm Android cihazlarda giderildiği anlamına gelmez. Android 15+ sistem çubuğu davranışı ayrıca cihaz üzerinde doğrulanmalıdır.

## Depo kapsamı

Depo uygulama kaynaklarını, gerekli görselleri ve test kaynaklarını içerir. `bin`, `obj`, `.vs`, `output`, APK/AAB paketleri, ekran görüntüleri, yedekler, kullanıcıya özel IDE ayarları, imzalama dosyaları ve gizli bilgiler Git dışında tutulur. PNG kaynakları ile uygulama içi yerel kayıt mekanizması korunur; cihazdaki oyun verileri depoya taşınmaz.
