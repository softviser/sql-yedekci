# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) esas alınmıştır, sürüm numaraları [Semantic Versioning](https://semver.org/lang/tr/) kurallarına uyar.

## [3.0.1] - 2026-10-15

PowerShell scripti yerini arayüzlü bir Windows uygulamasına bıraktı.

### Eklendi
- Arayüz: Bağlantı, Yedekleme, Geri yükleme ve Günlük sekmeleri.
- Bu makinedeki ve ağdaki (SQL Browser) SQL Server örneklerini arama.
- Windows ya da SQL Server kimlik doğrulamasıyla bağlantı testi; sürüm, sysadmin yetkisi ve sunucunun yerel olup olmadığı gösteriliyor.
- Listeden tekli / çoklu veritabanı seçimi.
- Saatlik, günlük ve haftalık zamanlama; görev uygulama içinden kuruluyor ve kaldırılıyor.
- Görev kurulurken `NT AUTHORITY\SYSTEM` hesabının yedek yetkisi kontrol ediliyor, eksikse en az yetki verilebiliyor.
- Yedekten sonra `RESTORE VERIFYONLY` ile doğrulama (isteğe bağlı).
- Geri yükleme: `.bak` ya da `.zip`'ten aynı ya da yeni adla; dosyalar sunucunun varsayılan klasörlerine taşınıyor, üzerine yazmada açık bağlantılar kesiliyor.
- SQL parolası DPAPI ile şifreli saklanıyor; ayar klasörüne yalnızca Administrators ve SYSTEM erişebiliyor.
- Softviser Yazılım self-signed sertifikasıyla imzalı kurulum dosyası ve taşınabilir zip.

### Değişti
- **Kırıcı değişiklik:** Eski yedekleri silme gün sayısına göre değil, veritabanı başına saklanacak yedek sayısına göre yapılıyor.
- **Kırıcı değişiklik:** Ayarlar `config.json` yerine `%ProgramData%\Softviser\SqlYedekci\settings.json` dosyasında; loglar aynı klasörün `logs` alt klasöründe.

## [2.1.4] - 2026-10-14

### Düzeltildi
- Adında `/`, `:` gibi dosya adında kullanılamayan karakterler bulunan veritabanlarının yedek dosyası oluşturulamıyordu; bu karakterler artık `_` ile değiştiriliyor.
- Sıkıştırma sırasında hata olursa (disk dolu vb.) yarım kalan `.zip` dosyası siliniyor, `.bak` dosyası korunuyor. Önceden bozuk zip kalıyor ve `.bak` siliniyordu.

## [2.1.3] - 2026-08-27

### Düzeltildi
- 2 GB üzerindeki yedekler sıkıştırılamıyordu. `Compress-Archive` yerine Zip64 destekleyen .NET `ZipArchive` kullanılıyor.

## [2.1.2] - 2026-07-09

### Düzeltildi
- Büyük veritabanlarında yedekleme 30 saniye sonra zaman aşımına uğruyordu. SQL komut zaman aşımı kaldırıldı.

## [2.1.1] - 2026-06-02

### Değişti
- Temizlik, her veritabanının en yeni yedeğini yaşına bakmadan korur. Yedekleme uzun süre başarısız olduğunda tüm yedeklerin silinmesi engellendi.

## [2.1.0] - 2026-04-15

### Eklendi
- `-Install` / `-Uninstall` parametreleriyle Windows Görev Zamanlayıcı'da günlük görev kurma ve kaldırma. `-At` ile saat seçilebilir.

## [2.0.0] - 2026-02-03

### Değişti
- **Kırıcı değişiklik:** Ayarlar script içindeki değişkenlerden `config.json` dosyasına taşındı. `-Config` ile farklı dosya verilebilir. Eski kurulumlarda ayarları `config.example.json` örneğine göre aktarın.
- `BACKUP DATABASE` komutu parametreli çalıştırılıyor (veritabanı adı ve dosya yolu SQL metnine eklenmiyor).

### Eklendi
- `Databases` listesi boş bırakılırsa çevrimiçi tüm kullanıcı veritabanları yedekleniyor.
- Birden fazla veritabanı tek çalıştırmada yedekleniyor; biri başarısız olsa da diğerlerine devam ediliyor.
- `Compress` ayarı ile sıkıştırma kapatılabiliyor.

## [1.2.0] - 2025-11-18

### Eklendi
- Her çalışma aylık log dosyasına (`sql-yedekci_YYYYMM.log`) yazılıyor.
- Hata olduğunda çıkış kodu `1`; Görev Zamanlayıcı geçmişinde başarısız çalışmalar görülebiliyor.
- Yedeklerde `CHECKSUM` doğrulaması.

## [1.1.0] - 2025-09-22

### Eklendi
- `RetentionDays` gününden eski yedeklerin otomatik silinmesi. Yalnızca bu aracın oluşturduğu dosya adı biçimine uyan dosyalar silinir.

## [1.0.0] - 2025-08-05

- İlk sürüm: tek bir veritabanının tam yedeğini alıp zip olarak sıkıştıran script.

[3.0.1]: https://github.com/softviser/sql-yedekci/releases/tag/v3.0.1
[2.1.4]: https://github.com/softviser/sql-yedekci/releases/tag/v2.1.4
