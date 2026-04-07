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
        if (!licenseService.EnsureLicenseOrExit(configPath, config))
        {
            return;
        }

        Application.Run(new MainForm(configPath, config, licenseService));
    }
}
