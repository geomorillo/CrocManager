// CrocManager - Interfaz gráfica para croc
// Autor: Manuel Jhobanny Morillo Ordoñez
// © 2026 - Todos los derechos reservados

using WebDesktop.Core;
using CrocManager.Services;

namespace CrocManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("CrocManager", 950, 720);
        var croc = new CrocService();

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            // Pasar la ventana a CrocService para push de progreso en tiempo real
            croc.SetWindow(window);

            // Menú
            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            // Handlers C# llamables desde JS
            window.Externo.RegisterHandler("sendFiles", croc.SendFiles);
            window.Externo.RegisterHandler("sendText", croc.SendText);
            window.Externo.RegisterHandler("receive", croc.Receive);
            window.Externo.RegisterHandler("checkInstall", croc.CheckInstall);
            window.Externo.RegisterHandler("cancelTransfer", croc.CancelTransfer);
            window.Externo.RegisterHandler("getHistory", croc.GetHistory);
            window.Externo.RegisterHandler("clearHistory", croc.ClearHistory);

            window.SetAssetFolder("wwwroot");
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }
}
