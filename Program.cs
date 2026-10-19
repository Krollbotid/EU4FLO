using EU4FLO.Localization;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Loc.Initialize();
        Application.Run(new EU4FLO.MainForm());
    }
}