# SQL Yedekçi

SQL Server veritabanlarını zamanlanmış olarak yedekleyen, yedekleri sıkıştıran ve eski yedekleri temizleyen tek dosyalık bir PowerShell aracı.

Kurulum gerektirmez, ek modül ya da program istemez. Windows'ta hazır gelen PowerShell 5.1 ve .NET ile çalışır; zamanlama için Windows Görev Zamanlayıcı kullanılır.

Geliştirici: [Softviser Yazılım](https://softviser.com.tr) — MIT lisansı ile açık kaynak.

## Neler yapıyor

- Seçilen veritabanlarının ya da (liste boşsa) tüm kullanıcı veritabanlarının tam yedeğini alır (`BACKUP DATABASE ... WITH CHECKSUM`).
- Her yedeği `.zip` olarak sıkıştırır ve `.bak` dosyasını siler. 4 GB üzerindeki yedekler de desteklenir (Zip64).
- Belirtilen günden eski yedekleri siler. Her veritabanının **en yeni yedeği yaşına bakılmadan korunur**; yedekleme haftalarca başarısız olsa bile elinizde son yedek kalır.
- Yalnızca kendi oluşturduğu dosyalara (`<veritabanı>_YYYYMMDD_HHMMSS.zip/.bak`) dokunur.
- Her çalışmayı aylık log dosyasına yazar. Bir veritabanı başarısız olursa diğerlerine devam eder, sonunda çıkış kodu `1` döner.
- Tek komutla günlük zamanlanmış görev kurar.

## Kurulum

1. [Releases](https://github.com/softviser/sql-yedekci/releases) sayfasından son sürümün zip'ini indirip SQL Server'ın çalıştığı makinede bir klasöre açın (örn. `C:\Araclar\SqlYedekci`).
2. `config.example.json` dosyasını `config.json` olarak kopyalayıp düzenleyin:

```json
{
    "ConnectionString": "Server=localhost;Integrated Security=True",
    "Databases": [],
    "BackupDir": "D:\\SqlYedek",
    "RetentionDays": 14,
    "Compress": true
}
```

| Ayar | Açıklama |
|------|----------|
| `ConnectionString` | SQL Server bağlantı cümlesi. Adlandırılmış örnek için `Server=localhost\\SQLEXPRESS`. |
| `Databases` | Yedeklenecek veritabanları, örn. `["Satis", "Stok"]`. Boş bırakılırsa çevrimiçi tüm kullanıcı veritabanları yedeklenir. |
| `BackupDir` | Yedeklerin ve log dosyasının yazılacağı klasör. |
| `RetentionDays` | Bu günden eski yedekler silinir. `0` temizliği kapatır. Varsayılan `14`. |
| `Compress` | `false` olursa `.bak` dosyası olduğu gibi bırakılır. Varsayılan `true`. |

3. Bir kez elle deneyin:

```powershell
powershell -ExecutionPolicy Bypass -File .\sql-yedekci.ps1
```

4. Yönetici olarak açtığınız PowerShell'de günlük görevi kurun:

```powershell
powershell -ExecutionPolicy Bypass -File .\sql-yedekci.ps1 -Install -At 02:00
```

Görev her gün belirtilen saatte `SYSTEM` hesabıyla çalışır. Kaldırmak için `-Uninstall`. Farklı bir ayar dosyası için `-Config <yol>` eklenebilir; birden fazla sunucu için kopya klasörler kullanın.

## Yetkiler — dikkat

- **Yedek dosyasını SQL Server servisi yazar**, bu araç değil. `BackupDir` klasörüne SQL Server servis hesabının (örn. `NT Service\MSSQLSERVER`) yazma izni olmalıdır. Uzak bir sunucu yedekleniyorsa `BackupDir` o sunucunun gördüğü bir yol olmalıdır (yerel yol ya da UNC paylaşımı), sıkıştırma için aracın da aynı yola erişebilmesi gerekir. En kolayı aracı SQL Server makinesinde çalıştırmaktır.
- Görev `SYSTEM` hesabıyla çalıştığından `Integrated Security=True` kullanılıyorsa `NT AUTHORITY\SYSTEM` girişinin SQL Server'da yedek alma yetkisi olmalıdır (yeni SQL Server sürümlerinde varsayılan olarak yoktur). İki seçenek:
  - Her veritabanında `NT AUTHORITY\SYSTEM` kullanıcısına `db_backupoperator` rolü verin, ya da
  - Bağlantı cümlesinde yalnızca yedek yetkisi olan bir SQL kullanıcısı kullanın (`User Id=...;Password=...`). Bu durumda `config.json` dosyasının okuma iznini yöneticilerle sınırlayın.

## Geri yükleme

Zip'i açıp çıkan `.bak` dosyasını SSMS'te **Restore Database** ile ya da:

```sql
RESTORE DATABASE [Satis] FROM DISK = N'D:\SqlYedek\Satis_20261014_020000.bak' WITH REPLACE
```

Yedeklerinizi düzenli aralıklarla geri yükleyerek deneyin; denenmemiş yedek yedek sayılmaz.

## Testler

SQL Server gerektirmeyen kısımlar (dosya adı, sıkıştırma, temizlik) için:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\test.ps1
```

## Sürüm geçmişi

[CHANGELOG.md](CHANGELOG.md)
