using AutoMinigameWinForms.Core;

namespace AutoMinigameWinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configPath = ConfigStore.DefaultPath;
        var config = ConfigStore.Load(configPath);

        using var licenseService = new LicenseService();
        if (!AppConstants.DebugBypassLicense && !licenseService.EnsureLicenseOrExit(configPath, config))
        {
            return;
        }

        if (AppConstants.DebugBypassLicense)
        {
            config.LicenseLastMessage = "DEBUG MODE: license bypass aktif.";
            if (string.IsNullOrWhiteSpace(config.LicenseLastVerifiedAt))
            {
                config.LicenseLastVerifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        Application.Run(new MainForm(configPath, config, licenseService));
    }
}
