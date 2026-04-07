using AutoMinigameWinForms.Core;
using AutoMinigameWinForms.Models;

namespace AutoMinigameWinForms;

public sealed class LicenseForm : Form
{
    private readonly TextBox _licenseKeyText = new();
    private readonly Label _statusLabel = new() { AutoSize = true };
    private readonly Label _ownerValueLabel = new() { AutoSize = true };
    private readonly Label _expiryValueLabel = new() { AutoSize = true };
    private readonly Label _licenseStatusValueLabel = new() { AutoSize = true };
    private readonly Button _loginButton = new();
    private readonly Button _cancelButton = new();

    private readonly AppConfig _config;
    private readonly LicenseService _licenseService;
    private bool _isActivating;

    public LicenseForm(AppConfig config, LicenseService licenseService)
    {
        _config = config;
        _licenseService = licenseService;

        Text = "Aktivasi License";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(20, 20, 20);
        ForeColor = Color.White;

        BuildUi();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "License",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
        };
        root.Controls.Add(title);

        root.Controls.Add(MakeField("License Key", _licenseKeyText, _config.LicenseKey));
        root.Controls.Add(BuildInfoCard());

        _statusLabel.ForeColor = Color.Gainsboro;
        _statusLabel.Margin = new Padding(0, 8, 0, 10);
        _statusLabel.Text = string.IsNullOrWhiteSpace(_config.LicenseKey)
            ? "Belum ada license."
            : "License tersimpan siap dicek.";
        root.Controls.Add(_statusLabel);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = BackColor,
        };

        _loginButton.Text = "Login";
        _loginButton.Width = 96;
        _loginButton.Height = 32;
        _loginButton.FlatStyle = FlatStyle.Flat;
        _loginButton.FlatAppearance.BorderSize = 0;
        _loginButton.BackColor = Color.FromArgb(36, 138, 61);
        _loginButton.ForeColor = Color.White;
        _loginButton.Click += async (_, _) => await ActivateAsync();

        _cancelButton.Text = "Batal";
        _cancelButton.Width = 84;
        _cancelButton.Height = 32;
        _cancelButton.FlatStyle = FlatStyle.Flat;
        _cancelButton.BackColor = Color.FromArgb(58, 58, 66);
        _cancelButton.ForeColor = Color.White;
        _cancelButton.FlatAppearance.BorderSize = 0;
        _cancelButton.Click += (_, _) => Close();

        buttonRow.Controls.Add(_loginButton);
        buttonRow.Controls.Add(_cancelButton);
        root.Controls.Add(buttonRow);

        RefreshSavedLicenseInfo();
        AcceptButton = _loginButton;
        CancelButton = _cancelButton;

        _licenseKeyText.Focus();
        _licenseKeyText.Select(_licenseKeyText.TextLength, 0);
        _licenseKeyText.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await ActivateAsync();
            }
        };
    }

    private Control MakeField(string labelText, TextBox textBox, string value)
    {
        textBox.Text = value;
        textBox.BackColor = Color.FromArgb(36, 36, 36);
        textBox.ForeColor = Color.White;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Dock = DockStyle.Top;
        textBox.Height = 32;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = BackColor,
        };

        panel.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            Margin = new Padding(0, 0, 0, 3),
        });
        panel.Controls.Add(textBox);
        return panel;
    }

    private Control BuildInfoCard()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12),
            Margin = new Padding(0, 2, 0, 0),
            BackColor = Color.FromArgb(28, 28, 28),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        StyleInfoLabel(_ownerValueLabel);
        StyleInfoLabel(_licenseStatusValueLabel);
        StyleInfoLabel(_expiryValueLabel);

        panel.Controls.Add(MakeInfoTitle("Akun"), 0, 0);
        panel.Controls.Add(_ownerValueLabel, 1, 0);
        panel.Controls.Add(MakeInfoTitle("Status"), 0, 1);
        panel.Controls.Add(_licenseStatusValueLabel, 1, 1);
        panel.Controls.Add(MakeInfoTitle("Expired"), 0, 2);
        panel.Controls.Add(_expiryValueLabel, 1, 2);

        return panel;
    }

    private static Label MakeInfoTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(156, 163, 175),
            Margin = new Padding(0, 0, 12, 6),
        };
    }

    private static void StyleInfoLabel(Label label)
    {
        label.ForeColor = Color.White;
        label.Margin = new Padding(0, 0, 0, 6);
    }

    private void RefreshSavedLicenseInfo()
    {
        _ownerValueLabel.Text = string.IsNullOrWhiteSpace(_config.LicenseOwner) ? "-" : _config.LicenseOwner;
        _expiryValueLabel.Text = FormatDisplayDate(_config.LicenseExpiresAt);

        if (string.IsNullOrWhiteSpace(_config.LicenseKey))
        {
            _licenseStatusValueLabel.Text = "Tidak aktif";
            _licenseStatusValueLabel.ForeColor = Color.FromArgb(255, 120, 120);
            return;
        }

        if (IsExpired(_config.LicenseExpiresAt))
        {
            _licenseStatusValueLabel.Text = "Expired";
            _licenseStatusValueLabel.ForeColor = Color.FromArgb(255, 170, 70);
            return;
        }

        _licenseStatusValueLabel.Text = "Aktif";
        _licenseStatusValueLabel.ForeColor = Color.FromArgb(110, 220, 140);
    }

    private void SetBusyState(bool busy)
    {
        _isActivating = busy;
        _loginButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        _licenseKeyText.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private async Task ActivateAsync()
    {
        if (_isActivating)
        {
            return;
        }

        var licenseKey = _licenseKeyText.Text.Trim();
        _licenseKeyText.Text = licenseKey;

        SetBusyState(true);
        _statusLabel.Text = "Memverifikasi license...";
        _statusLabel.ForeColor = Color.Gainsboro;
        _licenseStatusValueLabel.Text = "Checking...";
        _licenseStatusValueLabel.ForeColor = Color.Gainsboro;

        var (ok, message, response, failureKind, machineId) = await _licenseService.ValidateAsync(licenseKey);
        if (!ok)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = failureKind is LicenseValidationFailureKind.InvalidLicense or LicenseValidationFailureKind.Input
                ? Color.FromArgb(255, 120, 120)
                : Color.FromArgb(255, 190, 90);

            if (failureKind is LicenseValidationFailureKind.InvalidLicense or LicenseValidationFailureKind.Input)
            {
                _ownerValueLabel.Text = "-";
                _expiryValueLabel.Text = "-";
                _licenseStatusValueLabel.Text = "Tidak valid";
                _licenseStatusValueLabel.ForeColor = Color.FromArgb(255, 120, 120);
            }
            else
            {
                RefreshSavedLicenseInfo();
                _licenseStatusValueLabel.Text = "Gagal cek";
                _licenseStatusValueLabel.ForeColor = Color.FromArgb(255, 190, 90);
            }

            SetBusyState(false);
            return;
        }

        _licenseService.ApplyValidationResult(_config, licenseKey, response, machineId, message);
        RefreshSavedLicenseInfo();
        _licenseStatusValueLabel.Text = IsExpired(_config.LicenseExpiresAt) ? "Expired" : "Aktif";
        _licenseStatusValueLabel.ForeColor = IsExpired(_config.LicenseExpiresAt)
            ? Color.FromArgb(255, 170, 70)
            : Color.FromArgb(110, 220, 140);

        _statusLabel.Text = "License berhasil diverifikasi.";
        _statusLabel.ForeColor = Color.FromArgb(110, 220, 140);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool IsExpired(string expiresAt)
    {
        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTime.TryParse(expiresAt, out var expiresDate))
        {
            return false;
        }

        return expiresDate < DateTime.Now;
    }

    private static string FormatDisplayDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (DateTimeOffset.TryParse(value, out var dto))
        {
            return dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (DateTime.TryParse(value, out var dt))
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return value;
    }
}
