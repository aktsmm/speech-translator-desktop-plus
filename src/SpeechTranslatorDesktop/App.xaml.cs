using Velopack;

namespace SpeechTranslatorDesktop;

public partial class App : Application
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run(new MainWindow());
    }
}
