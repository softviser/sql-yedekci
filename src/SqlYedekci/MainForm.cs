namespace SqlYedekci;

internal sealed class MainForm : Form
{
    private AppSettings _settings = AppSettings.Load(AppPaths.SettingsFile);
    private readonly Logger _log;

    // Bağlantı
    private readonly ComboBox _server = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 320 };
    private readonly ComboBox _auth = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly TextBox _user = new() { Width = 320 };
    private readonly TextBox _password = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly CheckBox _trustCert = new() { Text = "Sunucu sertifikasına güven (TrustServerCertificate)", AutoSize = true };
    private readonly Label _connStatus = new() { AutoSize = true, MaximumSize = new Size(620, 0) };

    // Yedekleme
    private readonly CheckedListBox _dbs = new() { CheckOnClick = true, Width = 320, Height = 190, IntegralHeight = false };
    private readonly TextBox _dir = new() { Width = 320 };
    private readonly ComboBox _freq = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly NumericUpDown _hours = new() { Minimum = 1, Maximum = 23, Width = 60 };
    private readonly DateTimePicker _time = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 80 };
    private readonly ComboBox _day = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly NumericUpDown _keep = new() { Minimum = 1, Maximum = 999, Width = 80 };
    private readonly CheckBox _compress = new() { Text = "Yedeği zip olarak sıkıştır", AutoSize = true };
    private readonly CheckBox _verify = new() { Text = "Yedekten sonra doğrula (RESTORE VERIFYONLY)", AutoSize = true };
    private readonly Label _taskStatus = new() { AutoSize = true, MaximumSize = new Size(620, 0) };

    // Geri yükleme
    private readonly ListView _backups = new() { View = View.Details, FullRowSelect = true, MultiSelect = false, Dock = DockStyle.Fill, HideSelection = false };
    private readonly TextBox _target = new() { Width = 260 };
    private readonly CheckBox _overwrite = new() { Text = "Varolan veritabanının üzerine yaz", AutoSize = true };
    private string? _restoreFile;

    // Günlük
    private readonly TextBox _logView = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Visible = false };

    public MainForm()
    {
        Text = $"{AppInfo.Name} {AppInfo.Version}";
        Icon = AppInfo.AppIcon;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(720, 560);
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterScreen;
        _log = new Logger(AppPaths.LogDir, line => BeginInvoke(() => _logView.AppendText(line + Environment.NewLine)));

        _auth.Items.AddRange(["Windows kimlik doğrulaması", "SQL Server kimlik doğrulaması"]);
        _freq.Items.AddRange(["Saatlik", "Günlük", "Haftalık"]);
        _day.Items.AddRange(Culture.DayNames);
        _backups.Columns.Add("Dosya", 280);
        _backups.Columns.Add("Veritabanı", 140);
        _backups.Columns.Add("Tarih", 130);
        _backups.Columns.Add("Boyut", 90, HorizontalAlignment.Right);

        _tabs.TabPages.Add(BuildConnectionTab());
        _tabs.TabPages.Add(BuildBackupTab());
        _tabs.TabPages.Add(BuildRestoreTab());
        _tabs.TabPages.Add(BuildLogTab());
        var strip = new StatusStrip();
        strip.Items.AddRange([_status, _progress, new ToolStripStatusLabel(AppInfo.Company) { IsLink = true }]);
        strip.Items[^1].Click += (_, _) => AppInfo.Open(AppInfo.Website);
        Controls.Add(_tabs);
        Controls.Add(strip);

        _auth.SelectedIndexChanged += (_, _) => _user.Enabled = _password.Enabled = _auth.SelectedIndex == 1;
        _freq.SelectedIndexChanged += (_, _) => { _hours.Enabled = _freq.SelectedIndex == 0; _day.Enabled = _freq.SelectedIndex == 2; };
        _backups.SelectedIndexChanged += (_, _) =>
        {
            if (_backups.SelectedItems.Count == 0) return;
            _restoreFile = ((BackupFile)_backups.SelectedItems[0].Tag!).Path;
            _target.Text = ((BackupFile)_backups.SelectedItems[0].Tag!).Database;
        };
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedIndex == 2) RefreshBackups();
            if (_tabs.SelectedIndex == 3) RefreshLog();
        };

        LoadToForm();
        try { AppPaths.EnsureDataDir(); }
        catch (Exception ex) { SetStatus("Ayar klasörü hazırlanamadı: " + ex.Message); }
        UpdateTaskStatus();
    }

    // ---------- Sekmeler ----------

    private TabPage BuildConnectionTab()
    {
        var discover = new Button { Text = "Sunucuları ara", AutoSize = true };
        discover.Click += async (_, _) => await Busy("Sunucular aranıyor (ağ taraması birkaç saniye sürebilir)...", async () =>
        {
            var servers = await Task.Run(SqlEngine.DiscoverServers);
            var current = _server.Text;
            _server.Items.Clear();
            _server.Items.AddRange(servers.ToArray<object>());
            _server.Text = current;
            if (servers.Count > 0) _server.DroppedDown = true;
            SetStatus($"{servers.Count} sunucu bulundu.");
        });

        var test = new Button { Text = "Bağlantıyı test et", AutoSize = true };
        test.Click += async (_, _) => await TestConnection(loadDatabases: true);

        var grid = Grid();
        AddRow(grid, "Sunucu:", Flow(_server, discover));
        AddRow(grid, "Kimlik doğrulama:", _auth);
        AddRow(grid, "Kullanıcı:", _user);
        AddRow(grid, "Parola:", _password);
        AddRow(grid, "", _trustCert);
        AddRow(grid, "", test);
        AddRow(grid, "", _connStatus);
        return Page("Bağlantı", grid);
    }

    private TabPage BuildBackupTab()
    {
        var reload = new Button { Text = "Listeyi yenile", AutoSize = true };
        reload.Click += async (_, _) => await TestConnection(loadDatabases: true);
        var all = new Button { Text = "Tümü", AutoSize = true };
        all.Click += (_, _) => { for (var i = 0; i < _dbs.Items.Count; i++) _dbs.SetItemChecked(i, true); };
        var none = new Button { Text = "Hiçbiri", AutoSize = true };
        none.Click += (_, _) => { for (var i = 0; i < _dbs.Items.Count; i++) _dbs.SetItemChecked(i, false); };
        var dbButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        dbButtons.Controls.AddRange([reload, all, none]);

        var browse = new Button { Text = "Gözat...", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _dir.Text, Description = "Yedeklerin yazılacağı klasör" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _dir.Text = dlg.SelectedPath;
        };

        var save = new Button { Text = "Kaydet ve zamanla", AutoSize = true };
        save.Click += async (_, _) => await SaveAndSchedule();
        var now = new Button { Text = "Şimdi yedekle", AutoSize = true };
        now.Click += async (_, _) => await BackupNow();
        var remove = new Button { Text = "Zamanlanmış görevi kaldır", AutoSize = true };
        remove.Click += (_, _) =>
        {
            try { Scheduler.Remove(); SetStatus("Zamanlanmış görev kaldırıldı."); }
            catch (Exception ex) { Error(ex); }
            UpdateTaskStatus();
        };

        var grid = Grid();
        AddRow(grid, "Veritabanları:", Flow(_dbs, dbButtons));
        AddRow(grid, "Hedef klasör:", Flow(_dir, browse));
        AddRow(grid, "Sıklık:", Flow(_freq, Caption("her"), _hours, Caption("saatte bir")));
        AddRow(grid, "Saat / gün:", Flow(_time, _day));
        AddRow(grid, "Saklanacak yedek:", Flow(_keep, Caption("adet (her veritabanı için en yeni yedekler kalır)")));
        AddRow(grid, "", _compress);
        AddRow(grid, "", _verify);
        AddRow(grid, "", Flow(save, now, remove));
        AddRow(grid, "", _taskStatus);
        return Page("Yedekleme", grid);
    }

    private TabPage BuildRestoreTab()
    {
        var refresh = new Button { Text = "Yenile", AutoSize = true };
        refresh.Click += (_, _) => RefreshBackups();
        var other = new Button { Text = "Başka dosya seç...", AutoSize = true };
        other.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "SQL yedeği (*.bak;*.zip)|*.bak;*.zip", InitialDirectory = _dir.Text };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _backups.SelectedItems.Clear();
            _restoreFile = dlg.FileName;
            _target.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            SetStatus("Seçilen dosya: " + dlg.FileName);
        };
        var restore = new Button { Text = "Geri yükle", AutoSize = true };
        restore.Click += async (_, _) => await RestoreSelected();

        var bottom = Grid();
        bottom.Dock = DockStyle.Bottom;
        AddRow(bottom, "", Flow(refresh, other));
        AddRow(bottom, "Hedef veritabanı:", Flow(_target, _overwrite));
        AddRow(bottom, "", restore);

        var page = new TabPage("Geri yükleme") { Padding = new Padding(10) };
        page.Controls.Add(_backups);
        page.Controls.Add(bottom);
        return page;
    }

    private TabPage BuildLogTab()
    {
        var refresh = new Button { Text = "Yenile", AutoSize = true };
        refresh.Click += (_, _) => RefreshLog();
        var open = new Button { Text = "Log klasörünü aç", AutoSize = true };
        open.Click += (_, _) => { Directory.CreateDirectory(AppPaths.LogDir); AppInfo.Open(AppPaths.LogDir); };
        var buttons = Flow(refresh, open);
        buttons.Dock = DockStyle.Bottom;

        var page = new TabPage("Günlük") { Padding = new Padding(10) };
        page.Controls.Add(_logView);
        page.Controls.Add(buttons);
        return page;
    }

    // ---------- İşlemler ----------

    private async Task<bool> TestConnection(bool loadDatabases)
    {
        var ok = false;
        ReadFromForm();
        var settings = _settings;
        await Busy("Bağlanılıyor...", async () =>
        {
            try
            {
                var engine = new SqlEngine(settings);
                var (info, dbs) = await Task.Run(() => (engine.Test(), loadDatabases ? engine.ListDatabases() : null));
                _connStatus.ForeColor = Color.DarkGreen;
                _connStatus.Text = $"Bağlantı başarılı.\n{info.Version}\nSunucu makinesi: {info.MachineName}"
                    + (info.IsSysAdmin ? "" : "\nUyarı: bu oturum sysadmin değil; yedek/geri yükleme yetkileri eksik olabilir.")
                    + (info.IsLocal ? "" : "\nUyarı: sunucu başka bir makinede. Hedef klasör, SQL Server'ın yazabildiği ve bu bilgisayarın da erişebildiği bir paylaşım (\\\\sunucu\\yol) olmalı.");
                if (dbs != null) FillDatabases(dbs);
                SetStatus("Bağlantı başarılı.");
                ok = true;
            }
            catch (Exception ex)
            {
                _connStatus.ForeColor = Color.DarkRed;
                _connStatus.Text = "Bağlantı başarısız: " + ex.Message;
                SetStatus("Bağlantı başarısız.");
            }
        });
        return ok;
    }

    private void FillDatabases(List<string> databases)
    {
        var selected = new HashSet<string>(_settings.Databases, StringComparer.OrdinalIgnoreCase);
        _dbs.Items.Clear();
        foreach (var db in databases.Union(_settings.Databases, StringComparer.OrdinalIgnoreCase))
            _dbs.Items.Add(db, selected.Contains(db));
    }

    private async Task SaveAndSchedule()
    {
        ReadFromForm();
        if (_settings.Databases.Count == 0) { Warn("En az bir veritabanı seçin."); return; }
        if (string.IsNullOrWhiteSpace(_settings.BackupDir)) { Warn("Hedef klasörü seçin."); return; }
        if (!await TestConnection(loadDatabases: false)) { Warn("Bağlantı doğrulanamadı; görev kurulmadı."); return; }

        var settings = _settings;
        await Busy("Kaydediliyor ve görev kuruluyor...", async () =>
        {
            try
            {
                if (!settings.SqlAuth)
                {
                    var engine = new SqlEngine(settings);
                    var canBackup = await Task.Run(() => engine.SystemCanBackup(settings.Databases));
                    if (canBackup != true && MessageBox.Show(this,
                            "Zamanlanmış görev SYSTEM hesabıyla çalışır ve Windows kimlik doğrulamasında SQL Server'a "
                            + @"NT AUTHORITY\SYSTEM olarak bağlanır. Bu hesabın seçili veritabanlarını yedekleme yetkisi "
                            + (canBackup == false ? "yok." : "kontrol edilemedi.")
                            + "\n\nSYSTEM'e gerekli en az yetki verilsin mi? (her veritabanında db_backupoperator, doğrulama için master'da CREATE DATABASE)",
                            AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        await Task.Run(() => engine.GrantSystemBackup(settings.Databases));
                        _log.Write(@"NT AUTHORITY\SYSTEM için yedekleme yetkileri verildi.");
                    }
                }
                AppPaths.EnsureDataDir();
                settings.Save(AppPaths.SettingsFile);
                Scheduler.Install(settings);
                _log.Write($"Ayarlar kaydedildi, görev kuruldu: {Scheduler.Describe(settings)}; {string.Join(", ", settings.Databases)}");
                SetStatus("Kaydedildi. İlk çalışmayı beklemeden denemek için \"Şimdi yedekle\".");
            }
            catch (Exception ex) { Error(ex); }
        });
        UpdateTaskStatus();
    }

    private async Task BackupNow()
    {
        ReadFromForm();
        if (_settings.Databases.Count == 0) { Warn("En az bir veritabanı seçin."); return; }
        var settings = _settings;
        _tabs.SelectedIndex = 3;
        await Busy("Yedekleniyor...", async () =>
        {
            var failed = await Task.Run(() => Program.RunJob(settings, _log));
            SetStatus(failed == 0 ? "Yedekleme tamamlandı." : $"Yedekleme bitti, {failed} hata var. Günlüğe bakın.");
        });
    }

    private async Task RestoreSelected()
    {
        ReadFromForm();
        var file = _restoreFile;
        var target = _target.Text.Trim();
        if (file == null) { Warn("Listeden bir yedek ya da dosya seçin."); return; }
        if (target == "") { Warn("Hedef veritabanı adını yazın."); return; }
        var overwrite = _overwrite.Checked;
        if (MessageBox.Show(this,
                $"{Path.GetFileName(file)}\n→ {_settings.Server} / {target}\n\n"
                + (overwrite ? "Hedef veritabanı varsa açık bağlantılar kesilip ÜZERİNE YAZILACAK. " : "") + "Devam edilsin mi?",
                AppInfo.Name, MessageBoxButtons.YesNo, overwrite ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var settings = _settings;
        await Busy("Geri yükleniyor...", async () =>
        {
            try
            {
                await Task.Run(() => new SqlEngine(settings).Restore(file, target, overwrite, _log));
                SetStatus($"Geri yükleme tamamlandı: {target}");
                MessageBox.Show(this, $"'{target}' geri yüklendi.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _log.Write($"HATA  Geri yükleme {Path.GetFileName(file)}: {ex.Message}");
                Error(ex);
            }
        });
    }

    private void RefreshBackups()
    {
        _backups.BeginUpdate();
        _backups.Items.Clear();
        try
        {
            foreach (var b in BackupFiles.List(_dir.Text))
                _backups.Items.Add(new ListViewItem([Path.GetFileName(b.Path), b.Database, b.Time.ToString("dd.MM.yyyy HH:mm"), $"{b.Size / 1048576.0:N1} MB"]) { Tag = b });
        }
        catch (Exception ex) { SetStatus("Yedekler listelenemedi: " + ex.Message); }
        _backups.EndUpdate();
    }

    private void RefreshLog()
    {
        try
        {
            var file = _log.CurrentFile;
            _logView.Text = File.Exists(file) ? File.ReadAllText(file) : "(bu ay için log yok)";
            _logView.SelectionStart = _logView.TextLength;
            _logView.ScrollToCaret();
        }
        catch (Exception ex) { _logView.Text = ex.Message; }
    }

    private void UpdateTaskStatus()
    {
        var exists = Scheduler.Exists();
        _taskStatus.ForeColor = exists ? Color.DarkGreen : Color.DimGray;
        _taskStatus.Text = exists
            ? $"Zamanlanmış görev kurulu: {Scheduler.Describe(_settings)} (Görev Zamanlayıcı → \"{AppInfo.TaskName}\")"
            : "Zamanlanmış görev kurulu değil.";
    }

    // ---------- Form <-> ayarlar ----------

    private void LoadToForm()
    {
        var s = _settings;
        _server.Text = s.Server;
        _auth.SelectedIndex = s.SqlAuth ? 1 : 0;
        _user.Text = s.User;
        try { _password.Text = s.Password; } catch { /* başka makinede şifrelenmiş parola çözülemez */ }
        _trustCert.Checked = s.TrustServerCertificate;
        FillDatabases([]);
        _dir.Text = s.BackupDir;
        _freq.SelectedIndex = (int)s.Frequency;
        _hours.Value = Math.Clamp(s.IntervalHours, 1, 23);
        _time.Value = DateTime.Today + (TimeSpan.TryParse(s.Time, out var t) ? t : new TimeSpan(2, 0, 0));
        _day.SelectedIndex = (int)s.Day;
        _keep.Value = Math.Clamp(s.KeepCount, 1, 999);
        _compress.Checked = s.Compress;
        _verify.Checked = s.Verify;
    }

    private void ReadFromForm()
    {
        _settings = new AppSettings
        {
            Server = _server.Text.Trim(),
            SqlAuth = _auth.SelectedIndex == 1,
            User = _user.Text.Trim(),
            Password = _auth.SelectedIndex == 1 ? _password.Text : "",
            TrustServerCertificate = _trustCert.Checked,
            Databases = _dbs.CheckedItems.Cast<string>().ToList(),
            BackupDir = _dir.Text.Trim(),
            Frequency = (Frequency)_freq.SelectedIndex,
            IntervalHours = (int)_hours.Value,
            Time = _time.Value.ToString("HH:mm"),
            Day = (DayOfWeek)_day.SelectedIndex,
            KeepCount = (int)_keep.Value,
            Compress = _compress.Checked,
            Verify = _verify.Checked,
        };
    }

    // ---------- Yardımcılar ----------

    private async Task Busy(string message, Func<Task> action)
    {
        _tabs.Enabled = false;
        UseWaitCursor = true;
        _progress.Visible = true;
        SetStatus(message);
        try { await action(); }
        finally
        {
            _progress.Visible = false;
            UseWaitCursor = false;
            _tabs.Enabled = true;
        }
    }

    private void SetStatus(string text) => _status.Text = text;
    private void Warn(string text) => MessageBox.Show(this, text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void Error(Exception ex)
    {
        SetStatus("Hata: " + ex.Message);
        MessageBox.Show(this, ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static TableLayoutPanel Grid()
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.RowCount++;
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 7, 3, 3) });
        grid.Controls.Add(control);
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(10), AutoScroll = true };
        page.Controls.Add(content);
        return page;
    }
}
