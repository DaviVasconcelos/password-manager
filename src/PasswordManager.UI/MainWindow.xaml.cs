using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PasswordManager.UI.Localization;
using PasswordManager.UI.Views;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace PasswordManager.UI
{
    /// <summary>
    /// Janela principal da aplicação, responsável apenas pela navegação
    /// entre as páginas (desbloqueio e cofre).
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Aplicar tema pendente (definido por App.AplicarTema antes da janela existir).
            try
            {
                if (Content is FrameworkElement raizInicial)
                    raizInicial.RequestedTheme = App.ObterTemaPendente();
            }
            catch
            {
            }

            Title = App.Services.GetRequiredService<ILocalizationService>().GetString("MainWindow.Title");
            ContentFrame.Navigate(typeof(UnlockPage));

            ConfigurarJanelaAdaptativa();
            AtualizarBarraTitulo();
            if (Content is FrameworkElement raiz)
                raiz.ActualThemeChanged += (_, _) => AtualizarBarraTitulo();
        }

        /// <summary>
        /// Dimensiona a janela com base na área de trabalho do monitor (85% da
        /// largura/altura, teto 1100×720) e impõe Min 640×480 para caber em 720p.
        /// Centraliza na tela.
        /// </summary>
        private void ConfigurarJanelaAdaptativa()
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                if (appWindow is null)
                    return;
                appWindow.SetIcon("Assets\\logo-password-manager.ico");

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                var workArea = displayArea.WorkArea;

                int alvoW = System.Math.Min(1100, (int)(workArea.Width * 0.85));
                int alvoH = System.Math.Min(720, (int)(workArea.Height * 0.85));
                // Garantir mínimos para 720p (640×480 cabe com folga em 1280×720).
                alvoW = System.Math.Max(alvoW, 640);
                alvoH = System.Math.Max(alvoH, 480);
                // Não exceder a área de trabalho.
                alvoW = System.Math.Min(alvoW, workArea.Width);
                alvoH = System.Math.Min(alvoH, workArea.Height);

                appWindow.Resize(new SizeInt32(alvoW, alvoH));

                int posX = workArea.X + (workArea.Width - alvoW) / 2;
                int posY = workArea.Y + (workArea.Height - alvoH) / 2;
                appWindow.Move(new PointInt32(posX, posY));

                // Impor tamanho mínimo via Win32 WM_GETMINMAXINFO.
                WindowMinSizeHelper.SetMinSize(hwnd, 640, 480);

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
            }
            catch
            {
                // Falha silenciosa: manter tamanho padrão do SO.
            }
        }

        /// <summary>
        /// Pinta a barra de título com os tokens do design system
        /// (sem isso, ela segue o tema do sistema e destoa do app).
        /// Usa cores fixas por tema em vez de ResourceLookup via App.Current
        /// (que ignora o RequestedTheme da janela e retorna sempre o tema do app).
        /// </summary>
        private void AtualizarBarraTitulo()
        {
            var escuro = (Content as FrameworkElement)?.ActualTheme != ElementTheme.Light;
            // Cores idênticas aos tokens PMBackgroundBrush / PMTextPrimaryBrush.
            var fundo = escuro ? Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Color.FromArgb(255, 0xF3, 0xF3, 0xF3);
            var texto = escuro ? Color.FromArgb(255, 0xFF, 0xFF, 0xFF) : Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
            var hover = escuro ? Color.FromArgb(255, 0x2D, 0x2D, 0x2D) : Color.FromArgb(255, 0xE0, 0xE0, 0xE0);

            var barra = AppWindow.TitleBar;
            barra.BackgroundColor = fundo;
            barra.ForegroundColor = texto;
            barra.InactiveBackgroundColor = fundo;
            barra.InactiveForegroundColor = texto;
            barra.ButtonBackgroundColor = fundo;
            barra.ButtonForegroundColor = texto;
            barra.ButtonInactiveBackgroundColor = fundo;
            barra.ButtonInactiveForegroundColor = texto;
            barra.ButtonHoverBackgroundColor = hover;
        }

        private static class WindowMinSizeHelper
        {
            private const int WM_GETMINMAXINFO = 0x0024;
            private static readonly Dictionary<nint, (int w, int h)> MinSizes = new();
            private static readonly SUBCLASSPROC Proc = SubclassProc;

            [DllImport("Comctl32.dll", SetLastError = true)]
            private static extern bool SetWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass, nuint dwRefData);

            private delegate nint SUBCLASSPROC(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

            [DllImport("Comctl32.dll")]
            private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

            [DllImport("User32.dll")]
            private static extern uint GetDpiForWindow(nint hWnd);

            [StructLayout(LayoutKind.Sequential)]
            private struct POINT { public int x; public int y; }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINMAXINFO
            {
                public POINT ptReserved;
                public POINT ptMaxSize;
                public POINT ptMaxPosition;
                public POINT ptMinTrackSize;
                public POINT ptMaxTrackSize;
            }

            public static void SetMinSize(nint hwnd, int minW, int minH)
            {
                // Converter de effective pixels para físicos via DPI.
                try
                {
                    uint dpi = GetDpiForWindow(hwnd);
                    if (dpi != 0)
                    {
                        minW = (int)(minW * dpi / 96.0);
                        minH = (int)(minH * dpi / 96.0);
                    }
                }
                catch { }

                MinSizes[hwnd] = (minW, minH);
                SetWindowSubclass(hwnd, Proc, 0, 0);
            }

            private static nint SubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
            {
                if (uMsg == WM_GETMINMAXINFO && MinSizes.TryGetValue(hWnd, out var min))
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize.x = System.Math.Max(mmi.ptMinTrackSize.x, min.w);
                    mmi.ptMinTrackSize.y = System.Math.Max(mmi.ptMinTrackSize.y, min.h);
                    Marshal.StructureToPtr(mmi, lParam, false);
                    return nint.Zero;
                }

                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
            }
        }
    }
}