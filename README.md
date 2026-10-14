# SQL Yedekçi

SQL Server veritabanlarını zamanlanmış olarak yedekleyen, yedekleri sıkıştıran, eski yedekleri temizleyen ve tek tıkla geri yükleyen küçük bir Windows uygulaması.

Geliştirici: [Softviser Yazılım](https://softviser.com.tr) — MIT lisansı ile açık kaynak.

## Neler yapıyor

- **Sunucu bulma:** Bu bilgisayardaki SQL Server örneklerini ve ağda SQL Browser ile yayın yapan sunucuları listeler. Listede olmayan sunucu elle yazılabilir.
- **Bağlantı testi ve doğrulama:** Windows ya da SQL Server kimlik doğrulamasıyla bağlanır; sürümü, oturumun sysadmin olup olmadığını ve sunucunun bu makinede olup olmadığını gösterir.
- **Tekli / çoklu veritabanı seçimi:** Sunucudaki çevrimiçi kullanıcı veritabanlarından istediklerinizi işaretlersiniz (Tümü / Hiçbiri).
- **Zamanlanmış görev:** Saatlik (her N saatte), günlük ya da haftalık. Görev Windows Görev Zamanlayıcı'ya `SYSTEM` hesabıyla kurulur; kimse oturum açmamışken de çalışır.
- **Yedek döndürme:** Her veritabanı için saklanacak yedek sayısı belirlenir; yeni yedek alındıkça en eskiler silinir. Her zaman en az bir yedek kalır. Uygulama yalnızca kendi oluşturduğu dosyalara (`<veritabanı>_YYYYMMDD_HHMMSS.zip/.bak`) dokunur.
- **Sıkıştırma ve doğrulama:** Yedek `CHECKSUM` ile alınır, isteğe bağlı `RESTORE VERIFYONLY` ile doğrulanır ve zip'lenir (4 GB üzeri desteklenir).
- **Geri yükleme:** Yedek listesinden ya da başka bir `.bak`/`.zip` dosyasından, aynı ya da yeni adla geri yükler. Zip otomatik açılır, veri/log dosyaları sunucunun varsayılan klasörlerine taşınır; üzerine yazmada açık bağlantılar kesilir.
- **Günlük:** Her işlem aylık log dosyasına yazılır ve uygulama içinden görülebilir.

## Kurulum

[Releases](https://github.com/softviser/sql-yedekci/releases) sayfasında iki seçenek var:

- **`SqlYedekci-x.y.z-setup-win-x64.exe`** — normal kurulum (önerilen). Program Files'a kurar, Başlat menüsüne ekler. Kaldırıldığında zamanlanmış görev de silinir.
- **`SqlYedekci-x.y.z-portable-win-x64.zip`** — kurulum gerektirmez. Zamanlanmış görev exe'nin bulunduğu yolu kullandığından, görevi kurduktan sonra exe'yi taşımayın.

Windows 10/11 ya da Windows Server 2016+ (64 bit) gerekir. .NET kurulu olmasına gerek yoktur. Uygulama yönetici yetkisiyle açılır (görev kurmak ve ayarları korumak için).

## Kullanım

1. **Bağlantı** sekmesinde *Sunucuları ara*'ya basın ya da sunucu adını yazın (`.`, `SUNUCU`, `SUNUCU\SQLEXPRESS`). Kimlik doğrulamayı seçip *Bağlantıyı test et*'e basın.
2. **Yedekleme** sekmesinde veritabanlarını işaretleyin, hedef klasörü, sıklığı, saati ve saklanacak yedek sayısını seçin.
3. *Kaydet ve zamanla* bağlantıyı tekrar doğrular, ayarları kaydeder ve görevi kurar. Ardından *Şimdi yedekle* ile hemen bir deneme yapın.
4. **Geri yükleme** sekmesinde yedeği seçin, hedef veritabanı adını yazın ve *Geri yükle*'ye basın.

## Yetkiler — dikkat

- **Yedek dosyasını SQL Server servisi yazar**, bu uygulama değil. Hedef klasöre SQL Server servis hesabının (örn. `NT Service\MSSQLSERVER`) yazma izni olmalıdır. Sunucu başka bir makinedeyse hedef klasör, SQL Server'ın yazabildiği ve bu bilgisayarın da erişebildiği bir paylaşım (`\\sunucu\yedek`) olmalıdır. En kolayı uygulamayı SQL Server makinesine kurmaktır.
- Görev `SYSTEM` hesabıyla çalışır. Windows kimlik doğrulaması seçiliyse SQL Server'a `NT AUTHORITY\SYSTEM` olarak bağlanır ve yeni SQL Server sürümlerinde bu hesabın varsayılan olarak yedek yetkisi yoktur. *Kaydet ve zamanla* bunu kontrol eder; eksikse en az yetkiyi (her veritabanında `db_backupoperator`, doğrulama için master'da `CREATE DATABASE`) vermeyi önerir.
- SQL Server kimlik doğrulamasında parola, `%ProgramData%\Softviser\SqlYedekci\settings.json` içinde Windows DPAPI ile şifrelenir. Klasöre yalnızca Administrators ve SYSTEM erişebilir.

## Dosyalar

| Ne | Nerede |
|----|--------|
| Ayarlar | `%ProgramData%\Softviser\SqlYedekci\settings.json` |
| Loglar | `%ProgramData%\Softviser\SqlYedekci\logs\sql-yedekci_YYYYMM.log` |
| Zamanlanmış görev | Görev Zamanlayıcı → `SQL Yedekci` (`SqlYedekci.exe --run`) |

`SqlYedekci.exe --run` kayıtlı ayarlarla arayüz açmadan bir kez yedek alır; hata olursa çıkış kodu sıfırdan büyüktür.

## Dijital imza

Dağıtılan dosyalar Softviser Yazılım adına oluşturulmuş self-signed bir sertifika ile imzalanır. Ticari bir sertifika otoritesinden alınmadığı için Windows ilk çalıştırmada "Bilinmeyen yayımcı" ya da SmartScreen uyarısı gösterebilir.

İmzanın Softviser'a ait olduğunu bilgisayarınızda tanıtmak isterseniz, sertifika parmak izini sürüm notlarındakiyle karşılaştırdıktan sonra yönetici olarak açtığınız PowerShell'de:

```powershell
powershell -ExecutionPolicy Bypass -File .\trust-cert.ps1 -CerPath .\softviser-codesign.cer
```

## Derleme

.NET 8 SDK ve (kurulum dosyası için) [Inno Setup 6](https://jrsoftware.org/isinfo.php) gerekir.

```powershell
.\build.ps1                                   # imzasız: taşınabilir zip + kurulum
$env:CODESIGN_PFX_PASSWORD = '...'
.\build.ps1 -Sign -PfxPath certs\softviser-codesign.pfx
dotnet run --project tests\SqlYedekci.Tests   # SQL Server gerektirmeyen testler
```

## 2.x'ten geçiş

2.x sürümleri tek dosyalık bir PowerShell scriptiydi ([v2.1.4](https://github.com/softviser/sql-yedekci/tree/v2.1.4)). 3.x ayarları arayüzden yapılır; eski `config.json` okunmaz. Ayarları yeni uygulamada yeniden girip *Kaydet ve zamanla*'ya basın; görev adı aynı olduğundan eski görevin yerine geçer. Eski yedek dosyaları aynı adlandırmayı kullandığından yeni uygulamada listelenir ve döndürmeye dahil edilir.

## Sürüm geçmişi

[CHANGELOG.md](CHANGELOG.md)
