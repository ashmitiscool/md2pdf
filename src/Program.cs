using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Md2PdfDesktop
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        internal const uint PW_RENDERFULLCONTENT = 0x00000002;
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        internal static void EnableDarkTitleBar(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                int useDark = 1;
                int hr = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
                if (hr != 0)
                {
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
                }
            }
            catch
            {
                // Ignore on older Windows versions without DWM dark attribute support
            }
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color BgShell = Color.FromArgb(11, 13, 17);
        private static readonly Color BgPanel = Color.FromArgb(18, 22, 31);
        private static readonly Color BgElevated = Color.FromArgb(24, 30, 41);
        private static readonly Color BorderColor = Color.FromArgb(35, 41, 54);
        private static readonly Color AccentHover = Color.FromArgb(36, 52, 78);

        public override Color MenuStripGradientBegin { get { return BgShell; } }
        public override Color MenuStripGradientEnd { get { return BgShell; } }
        public override Color ToolStripDropDownBackground { get { return BgPanel; } }
        public override Color MenuBorder { get { return BorderColor; } }
        public override Color MenuItemBorder { get { return Color.FromArgb(59, 130, 246); } }
        public override Color MenuItemSelected { get { return AccentHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return AccentHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return AccentHover; } }
        public override Color MenuItemPressedGradientBegin { get { return BgElevated; } }
        public override Color MenuItemPressedGradientEnd { get { return BgElevated; } }
        public override Color ImageMarginGradientBegin { get { return BgPanel; } }
        public override Color ImageMarginGradientMiddle { get { return BgPanel; } }
        public override Color ImageMarginGradientEnd { get { return BgPanel; } }
        public override Color SeparatorDark { get { return BorderColor; } }
        public override Color SeparatorLight { get { return BorderColor; } }
        public override Color StatusStripGradientBegin { get { return BgShell; } }
        public override Color StatusStripGradientEnd { get { return BgShell; } }
        public override Color CheckBackground { get { return Color.FromArgb(30, 64, 175); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(37, 99, 235); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(29, 78, 216); } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item != null && e.Item.Enabled)
            {
                e.TextColor = Color.FromArgb(236, 240, 246);
            }
            else
            {
                e.TextColor = Color.FromArgb(107, 114, 128);
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e.Item != null)
            {
                e.ArrowColor = e.Item.Enabled
                    ? Color.FromArgb(156, 163, 175)
                    : Color.FromArgb(90, 96, 110);
            }
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle rect = new Rectangle(e.ImageRectangle.Left - 2, e.ImageRectangle.Top - 2,
                e.ImageRectangle.Width + 4, e.ImageRectangle.Height + 4);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(30, 64, 175)))
            {
                e.Graphics.FillRectangle(bg, rect);
            }
            using (Pen pen = new Pen(Color.FromArgb(147, 197, 253), 2f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Point p1 = new Point(rect.Left + 4, rect.Top + rect.Height / 2);
                Point p2 = new Point(rect.Left + rect.Width / 2 - 1, rect.Bottom - 5);
                Point p3 = new Point(rect.Right - 4, rect.Top + 5);
                e.Graphics.DrawLines(pen, new Point[] { p1, p2, p3 });
            }
        }
    }

    internal sealed class AppSettings
    {
        public int WindowX = -1;
        public int WindowY = -1;
        public int WindowWidth = 1380;
        public int WindowHeight = 880;
        public bool WindowMaximized = false;

        public string Theme = "dark";
        public string Paper = "A4";
        public string Margins = "normal";
        public int FontSize = 15;
        public bool ReconstructLists = true;
        public bool CleanInvisible = true;
        public string ViewMode = "split";

        public string PdfEngine = "python"; // "python" or "webview2"
        public bool OpenPdfAfterExport = true;
        public bool WatchOpenFile = false;
        public bool AlwaysOnTop = false;
        public double ZoomFactor = 1.0;

        public string LastOpenDir = "";
        public string LastExportDir = "";
        public List<string> RecentFiles = new List<string>();
    }

    internal sealed class MainForm : Form
    {
        private readonly string _appDir;
        private readonly string _backendScriptPath;
        private readonly string _settingsPath;
        private readonly string _startupFilePath;
        private readonly string _selfTestOutputPath;
        private readonly bool _isSelfTest;
        private readonly string _captureScreenshotsDir;
        private readonly bool _isCaptureScreenshots;
        private readonly JavaScriptSerializer _json;

        private AppSettings _settings;
        private WebView2 _webView;
        private MenuStrip _menuStrip;
        private StatusStrip _statusStrip;

        private ToolStripStatusLabel _statusFile;
        private ToolStripStatusLabel _statusMetrics;
        private ToolStripStatusLabel _statusMath;
        private ToolStripStatusLabel _statusEngine;
        private ToolStripStatusLabel _statusOpenPdf;
        private ToolStripStatusLabel _statusShowInFolder;

        private ToolStripMenuItem _recentMenu;
        private ToolStripMenuItem _menuWatchFile;
        private ToolStripMenuItem _menuAlwaysOnTop;
        private ToolStripMenuItem _menuOpenPdfAfter;
        private ToolStripMenuItem _menuEnginePython;
        private ToolStripMenuItem _menuEngineWebView;
        private ToolStripMenuItem _menuThemeDark;
        private ToolStripMenuItem _menuThemeLight;
        private ToolStripMenuItem _menuPaperA4;
        private ToolStripMenuItem _menuPaperLetter;
        private ToolStripMenuItem _menuMarginCompact;
        private ToolStripMenuItem _menuMarginNormal;
        private ToolStripMenuItem _menuMarginAcademic;
        private ToolStripMenuItem _menuViewSplit;
        private ToolStripMenuItem _menuViewEditor;
        private ToolStripMenuItem _menuViewPreview;
        private ToolStripMenuItem _menuRevealLastPdf;
        private ToolStripMenuItem _menuOpenLastPdf;

        private FileSystemWatcher _fileWatcher;
        private Timer _watchDebounceTimer;
        private Timer _selfTestTimeoutTimer;

        private bool _uiReady = false;
        private bool _isDirty = false;
        private bool _allowClose = false;
        private bool _isFullScreen = false;
        private FormWindowState _preFullScreenState = FormWindowState.Normal;
        private FormBorderStyle _preFullScreenBorder = FormBorderStyle.Sizable;

        private string _currentFilePath = "";
        private string _currentFileName = "Untitled";
        private string _lastDocTitle = "md2pdf_Document";
        private string _lastExportedPdfPath = "";
        private DateTime _lastSelfSaveUtc = DateTime.MinValue;
        private string _pythonExecutable = null;

        public MainForm(string startupFilePath, string selfTestOutputPath, string captureScreenshotsDir)
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            _backendScriptPath = Path.Combine(_appDir, "desktop_backend.py");
            _settingsPath = Path.Combine(_appDir, "settings.json");
            _startupFilePath = startupFilePath ?? "";
            _selfTestOutputPath = selfTestOutputPath ?? "";
            _isSelfTest = !string.IsNullOrEmpty(_selfTestOutputPath);
            _captureScreenshotsDir = captureScreenshotsDir ?? "";
            _isCaptureScreenshots = !string.IsNullOrEmpty(_captureScreenshotsDir);

            _json = new JavaScriptSerializer();
            _json.MaxJsonLength = int.MaxValue;

            _settings = _isCaptureScreenshots ? new AppSettings() : LoadSettings();
            if (_settings.RecentFiles == null)
            {
                _settings.RecentFiles = new List<string>();
            }

            InitializeWindow();
            BuildMenuStrip();
            BuildStatusStrip();
            InitializeWebViewContainer();
            ApplyPersistedWindowState();
            SyncMenuChecks();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.EnableDarkTitleBar(this.Handle);
        }

        private void InitializeWindow()
        {
            this.Text = "md2pdf";
            this.BackColor = Color.FromArgb(11, 13, 17);
            this.ForeColor = Color.FromArgb(236, 240, 246);
            this.MinimumSize = new Size(880, 560);
            this.KeyPreview = true;

            string iconPath = Path.Combine(_appDir, "app.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            else
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }

            this.FormClosing += MainForm_FormClosing;
            this.Load += MainForm_Load;
        }

        private void ApplyPersistedWindowState()
        {
            if (_isSelfTest)
            {
                this.StartPosition = FormStartPosition.CenterScreen;
                this.Size = new Size(1280, 820);
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                return;
            }

            if (_isCaptureScreenshots)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                int capW = Math.Min(1420, Math.Max(880, wa.Width));
                int capH = Math.Min(880, Math.Max(560, wa.Height));
                this.StartPosition = FormStartPosition.CenterScreen;
                this.WindowState = FormWindowState.Normal;
                this.Size = new Size(capW, capH);
                this.TopMost = true;
                return;
            }

            if (_settings.WindowWidth >= 880 && _settings.WindowHeight >= 560)
            {
                this.Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
            }
            else
            {
                this.Size = new Size(1380, 880);
            }

            if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
            {
                Rectangle desired = new Rectangle(_settings.WindowX, _settings.WindowY, this.Width, this.Height);
                bool visibleOnAnyScreen = false;
                foreach (Screen scr in Screen.AllScreens)
                {
                    if (scr.WorkingArea.IntersectsWith(desired))
                    {
                        visibleOnAnyScreen = true;
                        break;
                    }
                }
                if (visibleOnAnyScreen)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(_settings.WindowX, _settings.WindowY);
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                }
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }

            if (_settings.WindowMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }

            this.TopMost = _settings.AlwaysOnTop;
        }

        private void BuildMenuStrip()
        {
            _menuStrip = new MenuStrip();
            _menuStrip.Renderer = new DarkMenuRenderer();
            _menuStrip.BackColor = Color.FromArgb(11, 13, 17);
            _menuStrip.ForeColor = Color.FromArgb(236, 240, 246);
            _menuStrip.Padding = new Padding(6, 2, 0, 2);

            // File menu
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(MakeItem("&New", Keys.Control | Keys.N, delegate { TriggerUiAction("newDocument", null); CloseFileWatcher(); }));
            fileMenu.DropDownItems.Add(MakeItem("&Open File...", Keys.Control | Keys.O, delegate { OpenFileWithDialog(); }));

            _recentMenu = new ToolStripMenuItem("Open &Recent");
            fileMenu.DropDownItems.Add(_recentMenu);
            RebuildRecentMenu();

            fileMenu.DropDownItems.Add(MakeItem("Load &Sample", Keys.Control | Keys.Shift | Keys.L, async delegate { await LoadSampleAsync(); }));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(MakeItem("&Save Source", Keys.Control | Keys.S, delegate { TriggerUiAction("requestSaveSource", null); }));
            fileMenu.DropDownItems.Add(MakeItem("Save Source &As...", Keys.Control | Keys.Shift | Keys.S, delegate { TriggerUiAction("requestSaveSourceAs", null); }));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(MakeItem("&Export Vector PDF...", Keys.Control | Keys.E, delegate { TriggerUiAction("requestExportPdf", null); }));
            fileMenu.DropDownItems.Add(MakeItem("Export Standalone &HTML...", Keys.Control | Keys.Shift | Keys.H, delegate { TriggerUiAction("requestSaveHtml", null); }));
            fileMenu.DropDownItems.Add(MakeItem("&Batch Convert Files to PDF...", Keys.Control | Keys.B, delegate { RunBatchExportDialog(); }));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(MakeItem("&Print...", Keys.Control | Keys.P, delegate { ShowNativePrint(); }));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(MakeItem("E&xit", Keys.Alt | Keys.F4, delegate { this.Close(); }));

            // Edit menu
            ToolStripMenuItem editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(MakeItem("&Paste Clipboard", Keys.Control | Keys.Shift | Keys.V, delegate { TriggerUiAction("pasteText", null); }));
            editMenu.DropDownItems.Add(MakeItem("&Find and Replace...", Keys.Control | Keys.F, delegate { TriggerUiAction("toggleFind", null); }));
            editMenu.DropDownItems.Add(MakeItem("Copy Rendered &HTML", Keys.None, delegate { TriggerUiAction("copyRenderedHtml", null); }));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(MakeItem("&Clear Editor", Keys.None, delegate { TriggerUiAction("clearEditor", null); }));

            // View menu
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("&View");
            _menuViewSplit = MakeItem("&Split View (Editor + Preview)", Keys.Control | Keys.D1, delegate { SetViewMode("split"); });
            _menuViewEditor = MakeItem("&Editor Only", Keys.Control | Keys.D2, delegate { SetViewMode("editor"); });
            _menuViewPreview = MakeItem("&Preview Only", Keys.Control | Keys.D3, delegate { SetViewMode("preview"); });
            viewMenu.DropDownItems.Add(_menuViewSplit);
            viewMenu.DropDownItems.Add(_menuViewEditor);
            viewMenu.DropDownItems.Add(_menuViewPreview);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());

            _menuWatchFile = MakeItem("&Watch Open File for Changes", Keys.None, delegate { ToggleWatchFile(); });
            _menuAlwaysOnTop = MakeItem("&Always on Top", Keys.None, delegate { ToggleAlwaysOnTop(); });
            viewMenu.DropDownItems.Add(_menuWatchFile);
            viewMenu.DropDownItems.Add(_menuAlwaysOnTop);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(MakeItem("Zoom &In", Keys.Control | Keys.Oemplus, delegate { AdjustZoom(0.1); }));
            viewMenu.DropDownItems.Add(MakeItem("Zoom &Out", Keys.Control | Keys.OemMinus, delegate { AdjustZoom(-0.1); }));
            viewMenu.DropDownItems.Add(MakeItem("&Reset Zoom", Keys.Control | Keys.D0, delegate { SetZoom(1.0); }));
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(MakeItem("&Full Screen", Keys.F11, delegate { ToggleFullScreen(); }));

            // Document menu
            ToolStripMenuItem docMenu = new ToolStripMenuItem("&Document");
            ToolStripMenuItem themeMenu = new ToolStripMenuItem("&Theme");
            _menuThemeDark = MakeItem("Gemini &Dark", Keys.None, delegate { SetDocumentSetting("theme", "dark"); });
            _menuThemeLight = MakeItem("Academic &Light", Keys.None, delegate { SetDocumentSetting("theme", "light"); });
            themeMenu.DropDownItems.Add(_menuThemeDark);
            themeMenu.DropDownItems.Add(_menuThemeLight);
            docMenu.DropDownItems.Add(themeMenu);

            ToolStripMenuItem paperMenu = new ToolStripMenuItem("&Paper Size");
            _menuPaperA4 = MakeItem("A&4 (210 x 297 mm)", Keys.None, delegate { SetDocumentSetting("paper", "A4"); });
            _menuPaperLetter = MakeItem("US &Letter (8.5 x 11 in)", Keys.None, delegate { SetDocumentSetting("paper", "Letter"); });
            paperMenu.DropDownItems.Add(_menuPaperA4);
            paperMenu.DropDownItems.Add(_menuPaperLetter);
            docMenu.DropDownItems.Add(paperMenu);

            ToolStripMenuItem marginMenu = new ToolStripMenuItem("&Margins");
            _menuMarginCompact = MakeItem("&Compact", Keys.None, delegate { SetDocumentSetting("margins", "compact"); });
            _menuMarginNormal = MakeItem("&Normal", Keys.None, delegate { SetDocumentSetting("margins", "normal"); });
            _menuMarginAcademic = MakeItem("&Academic", Keys.None, delegate { SetDocumentSetting("margins", "academic"); });
            marginMenu.DropDownItems.Add(_menuMarginCompact);
            marginMenu.DropDownItems.Add(_menuMarginNormal);
            marginMenu.DropDownItems.Add(_menuMarginAcademic);
            docMenu.DropDownItems.Add(marginMenu);

            docMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem engineMenu = new ToolStripMenuItem("PDF &Engine");
            _menuEnginePython = MakeItem("Python Chromium Engine (pdf_engine.py)", Keys.None, delegate { SetPdfEngine("python"); });
            _menuEngineWebView = MakeItem("Native WebView2 Direct PrintToPdf", Keys.None, delegate { SetPdfEngine("webview2"); });
            engineMenu.DropDownItems.Add(_menuEnginePython);
            engineMenu.DropDownItems.Add(_menuEngineWebView);
            docMenu.DropDownItems.Add(engineMenu);

            _menuOpenPdfAfter = MakeItem("&Open PDF After Export", Keys.None, delegate
            {
                _settings.OpenPdfAfterExport = !_settings.OpenPdfAfterExport;
                SaveSettings();
                SyncMenuChecks();
            });
            docMenu.DropDownItems.Add(_menuOpenPdfAfter);

            // Help menu
            ToolStripMenuItem helpMenu = new ToolStripMenuItem("&Help");
            _menuOpenLastPdf = MakeItem("&Open Last Exported PDF", Keys.None, delegate { OpenLastExportedPdf(); });
            _menuRevealLastPdf = MakeItem("&Reveal Last Exported PDF in Explorer", Keys.None, delegate { RevealLastExportedPdf(); });
            _menuOpenLastPdf.Enabled = false;
            _menuRevealLastPdf.Enabled = false;
            helpMenu.DropDownItems.Add(_menuOpenLastPdf);
            helpMenu.DropDownItems.Add(_menuRevealLastPdf);
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add(MakeItem("Developer &Tools", Keys.F12, delegate
            {
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.OpenDevToolsWindow();
                }
            }));
            helpMenu.DropDownItems.Add(MakeItem("&About md2pdf", Keys.None, delegate { ShowAboutDialog(); }));

            _menuStrip.Items.Add(fileMenu);
            _menuStrip.Items.Add(editMenu);
            _menuStrip.Items.Add(viewMenu);
            _menuStrip.Items.Add(docMenu);
            _menuStrip.Items.Add(helpMenu);

            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);
        }

        private ToolStripMenuItem MakeItem(string text, Keys shortcut, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }
            item.Click += onClick;
            return item;
        }

        private void BuildStatusStrip()
        {
            _statusStrip = new StatusStrip();
            _statusStrip.Renderer = new DarkMenuRenderer();
            _statusStrip.BackColor = Color.FromArgb(11, 13, 17);
            _statusStrip.ForeColor = Color.FromArgb(138, 148, 166);
            _statusStrip.SizingGrip = true;

            _statusFile = new ToolStripStatusLabel("Untitled");
            _statusFile.Spring = true;
            _statusFile.TextAlign = ContentAlignment.MiddleLeft;
            _statusFile.ForeColor = Color.FromArgb(220, 227, 238);

            _statusMetrics = new ToolStripStatusLabel("0 chars · 0 words · 0 lines");
            _statusMetrics.ForeColor = Color.FromArgb(138, 148, 166);
            _statusMetrics.BorderSides = ToolStripStatusLabelBorderSides.Left;
            _statusMetrics.BorderStyle = Border3DStyle.Etched;

            _statusMath = new ToolStripStatusLabel("0 block · 0 inline");
            _statusMath.ForeColor = Color.FromArgb(16, 185, 129);
            _statusMath.BorderSides = ToolStripStatusLabelBorderSides.Left;

            _statusEngine = new ToolStripStatusLabel("Engine: pdf_engine.py");
            _statusEngine.ForeColor = Color.FromArgb(147, 197, 253);
            _statusEngine.BorderSides = ToolStripStatusLabelBorderSides.Left;

            _statusOpenPdf = new ToolStripStatusLabel("Open PDF");
            _statusOpenPdf.IsLink = true;
            _statusOpenPdf.LinkColor = Color.FromArgb(96, 165, 250);
            _statusOpenPdf.Visible = false;
            _statusOpenPdf.Click += delegate { OpenLastExportedPdf(); };

            _statusShowInFolder = new ToolStripStatusLabel("Show in Folder");
            _statusShowInFolder.IsLink = true;
            _statusShowInFolder.LinkColor = Color.FromArgb(96, 165, 250);
            _statusShowInFolder.Visible = false;
            _statusShowInFolder.Click += delegate { RevealLastExportedPdf(); };

            _statusStrip.Items.Add(_statusFile);
            _statusStrip.Items.Add(_statusMetrics);
            _statusStrip.Items.Add(_statusMath);
            _statusStrip.Items.Add(_statusEngine);
            _statusStrip.Items.Add(_statusOpenPdf);
            _statusStrip.Items.Add(_statusShowInFolder);

            this.Controls.Add(_statusStrip);
        }

        private void InitializeWebViewContainer()
        {
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = Color.FromArgb(11, 13, 17);
            this.Controls.Add(_webView);

            // Ensure Z-order docks WebView2 strictly between MenuStrip (top) and StatusStrip (bottom)
            _webView.BringToFront();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (_isSelfTest)
            {
                _selfTestTimeoutTimer = new Timer();
                _selfTestTimeoutTimer.Interval = 45000;
                _selfTestTimeoutTimer.Tick += delegate
                {
                    _selfTestTimeoutTimer.Stop();
                    WriteSelfTestFailure("Self-test timed out after 45 seconds.");
                };
                _selfTestTimeoutTimer.Start();
            }

            try
            {
                string userDataDir = Path.Combine(_appDir, "webview2_data");
                Directory.CreateDirectory(userDataDir);

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir, null);
                await _webView.EnsureCoreWebView2Async(env);

                CoreWebView2 core = _webView.CoreWebView2;
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsZoomControlEnabled = true;

                // Map https://studio.local/ -> app folder so vendor/ & gemini-renderer.js load cleanly
                core.SetVirtualHostNameToFolderMapping(
                    "studio.local",
                    _appDir,
                    CoreWebView2HostResourceAccessKind.Allow
                );

                core.WebMessageReceived += Core_WebMessageReceived;
                if (_settings.ZoomFactor >= 0.5 && _settings.ZoomFactor <= 2.5)
                {
                    _webView.ZoomFactor = _settings.ZoomFactor;
                }

                core.Navigate("https://studio.local/desktop_ui.html");
            }
            catch (Exception ex)
            {
                if (_isSelfTest)
                {
                    WriteSelfTestFailure("WebView2 initialization failed: " + ex.Message);
                    return;
                }
                MessageBox.Show(
                    "Failed to initialize Microsoft Edge WebView2.\n\n" + ex.Message,
                    "md2pdf",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                _allowClose = true;
                this.Close();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                TriggerUiAction("requestSaveSource", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.S))
            {
                TriggerUiAction("requestSaveSourceAs", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.O))
            {
                OpenFileWithDialog();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N))
            {
                TriggerUiAction("newDocument", null);
                CloseFileWatcher();
                return true;
            }
            if (keyData == (Keys.Control | Keys.E))
            {
                TriggerUiAction("requestExportPdf", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.H))
            {
                TriggerUiAction("requestSaveHtml", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.B))
            {
                RunBatchExportDialog();
                return true;
            }
            if (keyData == (Keys.Control | Keys.P))
            {
                ShowNativePrint();
                return true;
            }
            if (keyData == (Keys.Control | Keys.F))
            {
                TriggerUiAction("toggleFind", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.D1))
            {
                SetViewMode("split");
                return true;
            }
            if (keyData == (Keys.Control | Keys.D2))
            {
                SetViewMode("editor");
                return true;
            }
            if (keyData == (Keys.Control | Keys.D3))
            {
                SetViewMode("preview");
                return true;
            }
            if (keyData == Keys.F11)
            {
                ToggleFullScreen();
                return true;
            }
            if (keyData == Keys.F12 && _webView != null && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.OpenDevToolsWindow();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async void Core_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawJson = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(rawJson)) return;

                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(rawJson);
                if (msg == null || !msg.ContainsKey("type")) return;

                string type = Convert.ToString(msg["type"]);
                switch (type)
                {
                    case "ready":
                        await HandleUiReadyAsync();
                        break;
                    case "stateChanged":
                        HandleStateChanged(msg);
                        break;
                    case "openFile":
                        OpenFileWithDialog();
                        break;
                    case "loadSample":
                        await LoadSampleAsync();
                        break;
                    case "saveSource":
                        await HandleSaveSourceAsync(msg);
                        break;
                    case "saveHtml":
                        HandleSaveHtml(msg);
                        break;
                    case "exportPdf":
                        await HandleExportPdfAsync(msg);
                        break;
                    case "batchExport":
                        await RunBatchExportDialogAsync(msg);
                        break;
                    case "print":
                        ShowNativePrint();
                        break;
                    case "openExternal":
                        OpenPathWithShell(GetString(msg, "path"));
                        break;
                    case "revealInExplorer":
                        RevealPathInExplorer(GetString(msg, "path"));
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_isSelfTest)
                {
                    WriteSelfTestFailure("WebMessage handler error: " + ex.Message);
                }
                else
                {
                    TriggerUiAction("showToast", "Action error: " + ex.Message);
                }
            }
        }

        private async Task HandleUiReadyAsync()
        {
            _uiReady = true;
            PushSettingsToUi();

            if (!string.IsNullOrEmpty(_startupFilePath) && File.Exists(_startupFilePath))
            {
                await LoadFileFromDiskAsync(_startupFilePath, true);
            }
            else
            {
                await LoadSampleAsync();
            }

            if (_isSelfTest)
            {
                await RunSelfTestAsync();
            }
            else if (_isCaptureScreenshots)
            {
                await RunCaptureScreenshotsAsync();
            }
        }

        private void HandleStateChanged(Dictionary<string, object> msg)
        {
            _isDirty = GetBool(msg, "dirty", false);
            string fn = GetString(msg, "filename");
            if (!string.IsNullOrEmpty(fn)) _currentFileName = fn;
            string fp = GetString(msg, "filepath");
            if (fp != null) _currentFilePath = fp;
            string title = GetString(msg, "title");
            if (!string.IsNullOrEmpty(title)) _lastDocTitle = title;

            int chars = GetInt(msg, "charCount", 0);
            int words = GetInt(msg, "wordCount", 0);
            int lines = GetInt(msg, "lineCount", 0);
            int blockEq = GetInt(msg, "blockEq", 0);
            int inlineEq = GetInt(msg, "inlineEq", 0);
            int errors = GetInt(msg, "errors", 0);

            if (msg.ContainsKey("settings"))
            {
                Dictionary<string, object> s = msg["settings"] as Dictionary<string, object>;
                if (s != null)
                {
                    string th = GetString(s, "theme");
                    string pa = GetString(s, "paper");
                    string ma = GetString(s, "margins");
                    string vm = GetString(s, "viewMode");
                    if (!string.IsNullOrEmpty(th)) _settings.Theme = th;
                    if (!string.IsNullOrEmpty(pa)) _settings.Paper = pa;
                    if (!string.IsNullOrEmpty(ma)) _settings.Margins = ma;
                    if (!string.IsNullOrEmpty(vm)) _settings.ViewMode = vm;
                    _settings.FontSize = GetInt(s, "fontSize", _settings.FontSize);
                    _settings.ReconstructLists = GetBool(s, "reconstructLists", _settings.ReconstructLists);
                    _settings.CleanInvisible = GetBool(s, "cleanInvisible", _settings.CleanInvisible);
                    SyncMenuChecks();
                }
            }

            UpdateWindowAndStatusText(chars, words, lines, blockEq, inlineEq, errors);
        }

        private void UpdateWindowAndStatusText(int chars, int words, int lines, int blockEq, int inlineEq, int errors)
        {
            string dirtyPrefix = _isDirty ? "* " : "";
            string displayName = string.IsNullOrEmpty(_currentFileName) ? "Untitled" : _currentFileName;
            this.Text = string.Format("{0}{1} - md2pdf", dirtyPrefix, displayName);

            string watchTag = (_fileWatcher != null && _settings.WatchOpenFile) ? " [Watching]" : "";
            string pathText = !string.IsNullOrEmpty(_currentFilePath) ? _currentFilePath : displayName;
            _statusFile.Text = dirtyPrefix + pathText + watchTag;

            _statusMetrics.Text = string.Format("{0:N0} chars · {1:N0} words · {2:N0} lines", chars, words, lines);
            if (errors > 0)
            {
                _statusMath.ForeColor = Color.FromArgb(245, 158, 11);
                _statusMath.Text = string.Format("{0} block · {1} inline ({2} warn)", blockEq, inlineEq, errors);
            }
            else
            {
                _statusMath.ForeColor = Color.FromArgb(16, 185, 129);
                _statusMath.Text = string.Format("{0} block · {1} inline", blockEq, inlineEq);
            }

            _statusEngine.Text = _settings.PdfEngine == "webview2"
                ? "Engine: WebView2 Native"
                : "Engine: pdf_engine.py";
        }

        private async void OpenFileWithDialog()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Open Markdown or Gemini Text File";
                dlg.Filter = "Markdown & Text Files (*.md;*.txt;*.markdown;*.tex)|*.md;*.txt;*.markdown;*.text;*.tex|All Files (*.*)|*.*";
                if (!string.IsNullOrEmpty(_settings.LastOpenDir) && Directory.Exists(_settings.LastOpenDir))
                {
                    dlg.InitialDirectory = _settings.LastOpenDir;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.LastOpenDir = Path.GetDirectoryName(dlg.FileName);
                    SaveSettings();
                    await LoadFileFromDiskAsync(dlg.FileName, true);
                }
            }
        }

        private async Task LoadFileFromDiskAsync(string path, bool addToRecent)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                TriggerUiAction("showToast", "File not found: " + path);
                return;
            }

            string content = null;
            try
            {
                string resJson = await RunPythonBackendAsync(new string[] { "read", "--file", path });
                if (!string.IsNullOrEmpty(resJson))
                {
                    Dictionary<string, object> data = _json.Deserialize<Dictionary<string, object>>(resJson);
                    if (data != null && GetBool(data, "ok", false))
                    {
                        content = GetString(data, "content");
                    }
                }
            }
            catch
            {
                // Fallback below
            }

            if (content == null)
            {
                content = ReadTextAutoCSharp(path);
            }

            _currentFilePath = Path.GetFullPath(path);
            _currentFileName = Path.GetFileName(_currentFilePath);

            if (addToRecent)
            {
                AddRecentFile(_currentFilePath);
            }

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["filename"] = _currentFileName;
            payload["filepath"] = _currentFilePath;
            payload["content"] = content;
            payload["markClean"] = true;
            payload["toast"] = "Loaded " + _currentFileName;
            await CallUiMethodAsync("loadDocument", payload);

            SetupFileWatcherIfEnabled();
        }

        private async Task LoadSampleAsync()
        {
            try
            {
                string resJson = await RunPythonBackendAsync(new string[] { "sample" });
                if (!string.IsNullOrEmpty(resJson))
                {
                    Dictionary<string, object> data = _json.Deserialize<Dictionary<string, object>>(resJson);
                    if (data != null && GetBool(data, "ok", false))
                    {
                        _currentFilePath = "";
                        _currentFileName = GetString(data, "filename") ?? "text_865.txt";
                        CloseFileWatcher();

                        Dictionary<string, object> payload = new Dictionary<string, object>();
                        payload["filename"] = _currentFileName;
                        payload["filepath"] = "";
                        payload["content"] = GetString(data, "content");
                        payload["markClean"] = true;
                        await CallUiMethodAsync("loadDocument", payload);
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to built-in JS sample
            }

            _currentFilePath = "";
            _currentFileName = "sample_preview.txt";
            CloseFileWatcher();
            TriggerUiAction("loadFallbackSample", null);
        }

        private async Task HandleSaveSourceAsync(Dictionary<string, object> msg)
        {
            string content = GetString(msg, "content") ?? "";
            bool saveAs = GetBool(msg, "saveAs", false);
            string suggested = GetString(msg, "suggestedName");
            if (string.IsNullOrEmpty(suggested))
            {
                suggested = _lastDocTitle + ".md";
            }

            string targetPath = _currentFilePath;
            if (saveAs || string.IsNullOrEmpty(targetPath))
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = "Save Markdown / Text Source";
                    dlg.Filter = "Markdown (*.md)|*.md|Text File (*.txt)|*.txt|All Files (*.*)|*.*";
                    dlg.FileName = suggested;
                    if (!string.IsNullOrEmpty(_settings.LastOpenDir) && Directory.Exists(_settings.LastOpenDir))
                    {
                        dlg.InitialDirectory = _settings.LastOpenDir;
                    }
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    targetPath = dlg.FileName;
                    _settings.LastOpenDir = Path.GetDirectoryName(targetPath);
                    SaveSettings();
                }
            }

            _lastSelfSaveUtc = DateTime.UtcNow;
            File.WriteAllText(targetPath, content, new UTF8Encoding(false));
            _currentFilePath = Path.GetFullPath(targetPath);
            _currentFileName = Path.GetFileName(_currentFilePath);
            AddRecentFile(_currentFilePath);
            SetupFileWatcherIfEnabled();

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["filename"] = _currentFileName;
            payload["filepath"] = _currentFilePath;
            payload["toast"] = "Saved " + _currentFileName;
            await CallUiMethodAsync("markSaved", payload);
        }

        private void HandleSaveHtml(Dictionary<string, object> msg)
        {
            string html = GetString(msg, "html");
            string suggested = GetString(msg, "suggestedName") ?? (_lastDocTitle + ".html");
            if (string.IsNullOrEmpty(html)) return;

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export Standalone HTML";
                dlg.Filter = "HTML Document (*.html)|*.html";
                dlg.FileName = suggested;
                if (!string.IsNullOrEmpty(_settings.LastExportDir) && Directory.Exists(_settings.LastExportDir))
                {
                    dlg.InitialDirectory = _settings.LastExportDir;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.LastExportDir = Path.GetDirectoryName(dlg.FileName);
                    SaveSettings();
                    File.WriteAllText(dlg.FileName, html, new UTF8Encoding(false));
                    TriggerUiAction("showToast", "Saved " + Path.GetFileName(dlg.FileName));
                }
            }
        }

        private async Task HandleExportPdfAsync(Dictionary<string, object> msg)
        {
            string standaloneHtml = GetString(msg, "standaloneHtml");
            string suggested = SanitizePdfFilename(GetString(msg, "suggestedName"));
            if (string.IsNullOrEmpty(standaloneHtml))
            {
                TriggerUiAction("showToast", "Nothing to export yet");
                return;
            }

            string targetPdf = null;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export Vector PDF";
                dlg.Filter = "PDF Document (*.pdf)|*.pdf";
                dlg.FileName = suggested;
                if (!string.IsNullOrEmpty(_settings.LastExportDir) && Directory.Exists(_settings.LastExportDir))
                {
                    dlg.InitialDirectory = _settings.LastExportDir;
                }
                else if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_currentFilePath);
                }

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                targetPdf = dlg.FileName;
                _settings.LastExportDir = Path.GetDirectoryName(targetPdf);
                SaveSettings();
            }

            await ExportPdfToPathAsync(standaloneHtml, targetPdf, _settings.PdfEngine, true);
        }

        private async Task<bool> ExportPdfToPathAsync(string standaloneHtml, string targetPdf, string preferredEngine, bool allowFallback)
        {
            TriggerUiAction("setExportBusy", true);
            bool success = false;
            string usedEngine = preferredEngine;
            string errorMessage = "";

            try
            {
                if (preferredEngine == "python")
                {
                    bool fallbackNeeded = false;
                    try
                    {
                        await ExportViaPythonEngineAsync(standaloneHtml, targetPdf);
                        success = File.Exists(targetPdf) && new FileInfo(targetPdf).Length > 0;
                    }
                    catch (Exception pyEx)
                    {
                        errorMessage = pyEx.Message;
                        fallbackNeeded = allowFallback;
                    }
                    if (!success && fallbackNeeded)
                    {
                        usedEngine = "webview2 (fallback)";
                        success = await ExportViaWebView2Async(targetPdf);
                    }
                }
                else
                {
                    success = await ExportViaWebView2Async(targetPdf);
                }

                if (success)
                {
                    _lastExportedPdfPath = targetPdf;
                    _statusOpenPdf.Visible = true;
                    _statusShowInFolder.Visible = true;
                    _menuOpenLastPdf.Enabled = true;
                    _menuRevealLastPdf.Enabled = true;

                    long sizeBytes = new FileInfo(targetPdf).Length;
                    TriggerUiAction(
                        "showToast",
                        string.Format("Exported {0} ({1:N1} KB via {2})", Path.GetFileName(targetPdf), sizeBytes / 1024.0, usedEngine)
                    );

                    if (!_isSelfTest && _settings.OpenPdfAfterExport)
                    {
                        OpenPathWithShell(targetPdf);
                    }
                }
                else
                {
                    string msg = string.IsNullOrEmpty(errorMessage) ? "PDF export failed." : errorMessage;
                    if (!_isSelfTest)
                    {
                        MessageBox.Show(msg, "Export PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                TriggerUiAction("setExportBusy", false);
            }

            return success;
        }

        private async Task ExportViaPythonEngineAsync(string standaloneHtml, string targetPdf)
        {
            string tempHtml = Path.Combine(Path.GetTempPath(), "md2pdf_desktop_" + Guid.NewGuid().ToString("N") + ".html");
            try
            {
                File.WriteAllText(tempHtml, standaloneHtml, new UTF8Encoding(false));
                string resJson = await RunPythonBackendAsync(new string[] {
                    "export-pdf",
                    "--html-file", tempHtml,
                    "--output", targetPdf
                });
                Dictionary<string, object> res = _json.Deserialize<Dictionary<string, object>>(resJson);
                if (res == null || !GetBool(res, "ok", false))
                {
                    string err = res != null ? GetString(res, "error") : "Empty response from desktop_backend.py";
                    throw new InvalidOperationException(err);
                }
            }
            finally
            {
                try { if (File.Exists(tempHtml)) File.Delete(tempHtml); } catch { }
            }
        }

        private async Task<bool> ExportViaWebView2Async(string targetPdf)
        {
            if (_webView == null || _webView.CoreWebView2 == null) return false;

            CoreWebView2PrintSettings ps = _webView.CoreWebView2.Environment.CreatePrintSettings();
            ps.ShouldPrintBackgrounds = true;
            ps.ShouldPrintHeaderAndFooter = false;
            ps.ShouldPrintSelectionOnly = false;
            ps.Orientation = CoreWebView2PrintOrientation.Portrait;

            if (_settings.Paper == "Letter")
            {
                ps.PageWidth = 8.5;
                ps.PageHeight = 11.0;
            }
            else
            {
                ps.PageWidth = 8.27;
                ps.PageHeight = 11.69;
            }

            if (_settings.Margins == "compact")
            {
                ps.MarginTop = 0.47;
                ps.MarginBottom = 0.47;
                ps.MarginLeft = 0.55;
                ps.MarginRight = 0.55;
            }
            else if (_settings.Margins == "academic")
            {
                ps.MarginTop = 0.87;
                ps.MarginBottom = 0.87;
                ps.MarginLeft = 0.94;
                ps.MarginRight = 0.94;
            }
            else
            {
                ps.MarginTop = 0.71;
                ps.MarginBottom = 0.71;
                ps.MarginLeft = 0.79;
                ps.MarginRight = 0.79;
            }

            string dir = Path.GetDirectoryName(targetPdf);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return await _webView.CoreWebView2.PrintToPdfAsync(targetPdf, ps);
        }

        private async void RunBatchExportDialog()
        {
            await RunBatchExportDialogAsync(null);
        }

        private async Task RunBatchExportDialogAsync(Dictionary<string, object> msg)
        {
            string[] selectedFiles = null;
            using (OpenFileDialog openDlg = new OpenFileDialog())
            {
                openDlg.Title = "Select .txt / .md Files for Batch PDF Export";
                openDlg.Filter = "Markdown & Text Files (*.md;*.txt;*.markdown;*.tex)|*.md;*.txt;*.markdown;*.text;*.tex|All Files (*.*)|*.*";
                openDlg.Multiselect = true;
                if (!string.IsNullOrEmpty(_settings.LastOpenDir) && Directory.Exists(_settings.LastOpenDir))
                {
                    openDlg.InitialDirectory = _settings.LastOpenDir;
                }
                if (openDlg.ShowDialog(this) != DialogResult.OK || openDlg.FileNames.Length == 0)
                {
                    return;
                }
                selectedFiles = openDlg.FileNames;
                _settings.LastOpenDir = Path.GetDirectoryName(selectedFiles[0]);
            }

            string outputDir = null;
            using (FolderBrowserDialog folderDlg = new FolderBrowserDialog())
            {
                folderDlg.Description = "Choose destination folder for batch exported PDFs";
                folderDlg.SelectedPath = !string.IsNullOrEmpty(_settings.LastExportDir) && Directory.Exists(_settings.LastExportDir)
                    ? _settings.LastExportDir
                    : _settings.LastOpenDir;
                if (folderDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                outputDir = folderDlg.SelectedPath;
                _settings.LastExportDir = outputDir;
                SaveSettings();
            }

            TriggerUiAction("setExportBusy", true);
            TriggerUiAction("showToast", string.Format("Batch converting {0} file(s)...", selectedFiles.Length));

            string tempCfg = Path.Combine(Path.GetTempPath(), "md2pdf_batch_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Dictionary<string, object> cfg = new Dictionary<string, object>();
                cfg["files"] = selectedFiles;
                cfg["outputDir"] = outputDir;
                cfg["theme"] = _settings.Theme;
                cfg["paper"] = _settings.Paper;
                cfg["margins"] = _settings.Margins;
                cfg["fontSize"] = _settings.FontSize;
                File.WriteAllText(tempCfg, _json.Serialize(cfg), new UTF8Encoding(false));

                string resJson = await RunPythonBackendAsync(new string[] { "batch-export", "--config", tempCfg });
                Dictionary<string, object> res = _json.Deserialize<Dictionary<string, object>>(resJson);
                int okCount = res != null ? GetInt(res, "succeeded", 0) : 0;
                int total = res != null ? GetInt(res, "total", selectedFiles.Length) : selectedFiles.Length;

                TriggerUiAction("showToast", string.Format("Batch export complete: {0}/{1} PDFs saved", okCount, total));
                if (okCount > 0)
                {
                    OpenPathWithShell(outputDir);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Batch export failed:\n\n" + ex.Message, "Batch PDF Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (File.Exists(tempCfg)) File.Delete(tempCfg); } catch { }
                TriggerUiAction("setExportBusy", false);
            }
        }

        private void ShowNativePrint()
        {
            if (_webView != null && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
        }

        private void SetViewMode(string mode)
        {
            _settings.ViewMode = mode;
            SaveSettings();
            PushSettingsToUi();
            SyncMenuChecks();
        }

        private void SetDocumentSetting(string key, string value)
        {
            if (key == "theme") _settings.Theme = value;
            else if (key == "paper") _settings.Paper = value;
            else if (key == "margins") _settings.Margins = value;
            SaveSettings();
            PushSettingsToUi();
            SyncMenuChecks();
        }

        private void SetPdfEngine(string engine)
        {
            _settings.PdfEngine = engine;
            SaveSettings();
            SyncMenuChecks();
            _statusEngine.Text = engine == "webview2"
                ? "Engine: WebView2 Native"
                : "Engine: pdf_engine.py";
            TriggerUiAction("showToast", "PDF engine set to " + _statusEngine.Text.Replace("Engine: ", ""));
        }

        private void ToggleWatchFile()
        {
            _settings.WatchOpenFile = !_settings.WatchOpenFile;
            SaveSettings();
            SyncMenuChecks();
            SetupFileWatcherIfEnabled();
        }

        private void ToggleAlwaysOnTop()
        {
            _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
            this.TopMost = _settings.AlwaysOnTop;
            SaveSettings();
            SyncMenuChecks();
        }

        private void AdjustZoom(double delta)
        {
            SetZoom(_settings.ZoomFactor + delta);
        }

        private void SetZoom(double factor)
        {
            double clamped = Math.Max(0.6, Math.Min(2.0, Math.Round(factor, 2)));
            _settings.ZoomFactor = clamped;
            if (_webView != null)
            {
                _webView.ZoomFactor = clamped;
            }
            SaveSettings();
        }

        private void ToggleFullScreen()
        {
            if (!_isFullScreen)
            {
                _preFullScreenState = this.WindowState;
                _preFullScreenBorder = this.FormBorderStyle;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                _isFullScreen = true;
            }
            else
            {
                this.FormBorderStyle = _preFullScreenBorder;
                this.WindowState = _preFullScreenState;
                _isFullScreen = false;
            }
        }

        private void SyncMenuChecks()
        {
            _menuThemeDark.Checked = _settings.Theme == "dark";
            _menuThemeLight.Checked = _settings.Theme == "light";
            _menuPaperA4.Checked = _settings.Paper == "A4";
            _menuPaperLetter.Checked = _settings.Paper == "Letter";
            _menuMarginCompact.Checked = _settings.Margins == "compact";
            _menuMarginNormal.Checked = _settings.Margins == "normal";
            _menuMarginAcademic.Checked = _settings.Margins == "academic";
            _menuViewSplit.Checked = _settings.ViewMode == "split";
            _menuViewEditor.Checked = _settings.ViewMode == "editor";
            _menuViewPreview.Checked = _settings.ViewMode == "preview";
            _menuEnginePython.Checked = _settings.PdfEngine != "webview2";
            _menuEngineWebView.Checked = _settings.PdfEngine == "webview2";
            _menuOpenPdfAfter.Checked = _settings.OpenPdfAfterExport;
            _menuWatchFile.Checked = _settings.WatchOpenFile;
            _menuAlwaysOnTop.Checked = _settings.AlwaysOnTop;
        }

        private void PushSettingsToUi()
        {
            if (!_uiReady) return;
            Dictionary<string, object> s = new Dictionary<string, object>();
            s["theme"] = _settings.Theme;
            s["paper"] = _settings.Paper;
            s["margins"] = _settings.Margins;
            s["fontSize"] = _settings.FontSize;
            s["reconstructLists"] = _settings.ReconstructLists;
            s["cleanInvisible"] = _settings.CleanInvisible;
            s["viewMode"] = _settings.ViewMode;
            CallUiMethodFireAndForget("applySettings", s);
        }

        private void TriggerUiAction(string actionName, object extra)
        {
            if (!_uiReady || _webView == null || _webView.CoreWebView2 == null) return;
            string script = extra == null
                ? string.Format("window.DesktopHostApp && window.DesktopHostApp.triggerAction({0});", _json.Serialize(actionName))
                : string.Format("window.DesktopHostApp && window.DesktopHostApp.triggerAction({0}, {1});", _json.Serialize(actionName), _json.Serialize(extra));
            _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void CallUiMethodFireAndForget(string methodName, object arg)
        {
            if (!_uiReady || _webView == null || _webView.CoreWebView2 == null) return;
            string script = string.Format(
                "window.DesktopHostApp && window.DesktopHostApp.{0}({1});",
                methodName,
                _json.Serialize(arg)
            );
            _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async Task<string> CallUiMethodAsync(string methodName, object arg)
        {
            if (_webView == null || _webView.CoreWebView2 == null) return null;
            string script = arg == null
                ? string.Format("window.DesktopHostApp && window.DesktopHostApp.{0}();", methodName)
                : string.Format("window.DesktopHostApp && window.DesktopHostApp.{0}({1});", methodName, _json.Serialize(arg));
            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void SetupFileWatcherIfEnabled()
        {
            CloseFileWatcher();
            if (!_settings.WatchOpenFile || string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
            {
                return;
            }

            string dir = Path.GetDirectoryName(_currentFilePath);
            string file = Path.GetFileName(_currentFilePath);

            _watchDebounceTimer = new Timer();
            _watchDebounceTimer.Interval = 350;
            _watchDebounceTimer.Tick += async delegate
            {
                _watchDebounceTimer.Stop();
                if ((DateTime.UtcNow - _lastSelfSaveUtc).TotalSeconds < 1.5) return;
                if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
                {
                    await LoadFileFromDiskAsync(_currentFilePath, false);
                }
            };

            _fileWatcher = new FileSystemWatcher(dir, file);
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
            _fileWatcher.Changed += delegate
            {
                if (this.IsDisposed) return;
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (_watchDebounceTimer != null)
                    {
                        _watchDebounceTimer.Stop();
                        _watchDebounceTimer.Start();
                    }
                });
            };
            _fileWatcher.EnableRaisingEvents = true;
        }

        private void CloseFileWatcher()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
            if (_watchDebounceTimer != null)
            {
                _watchDebounceTimer.Stop();
                _watchDebounceTimer.Dispose();
                _watchDebounceTimer = null;
            }
        }

        private void AddRecentFile(string path)
        {
            string full = Path.GetFullPath(path);
            _settings.RecentFiles.RemoveAll(delegate (string item)
            {
                return string.Equals(item, full, StringComparison.OrdinalIgnoreCase);
            });
            _settings.RecentFiles.Insert(0, full);
            if (_settings.RecentFiles.Count > 10)
            {
                _settings.RecentFiles.RemoveRange(10, _settings.RecentFiles.Count - 10);
            }
            SaveSettings();
            RebuildRecentMenu();
        }

        private void RebuildRecentMenu()
        {
            _recentMenu.DropDownItems.Clear();
            if (_settings.RecentFiles == null || _settings.RecentFiles.Count == 0)
            {
                ToolStripMenuItem emptyItem = new ToolStripMenuItem("(No recent files)");
                emptyItem.Enabled = false;
                _recentMenu.DropDownItems.Add(emptyItem);
                return;
            }

            foreach (string path in _settings.RecentFiles)
            {
                string captured = path;
                ToolStripMenuItem item = new ToolStripMenuItem(captured);
                item.Click += async delegate
                {
                    await LoadFileFromDiskAsync(captured, true);
                };
                _recentMenu.DropDownItems.Add(item);
            }

            _recentMenu.DropDownItems.Add(new ToolStripSeparator());
            _recentMenu.DropDownItems.Add(MakeItem("Clear Recent List", Keys.None, delegate
            {
                _settings.RecentFiles.Clear();
                SaveSettings();
                RebuildRecentMenu();
            }));
        }

        private string FindPythonExecutable()
        {
            if (!string.IsNullOrEmpty(_pythonExecutable)) return _pythonExecutable;

            string[] candidates = new string[]
            {
                "python",
                @"C:\Python314\python.exe",
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python314\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe")
            };

            foreach (string cand in candidates)
            {
                try
                {
                    if (cand != "python" && !File.Exists(cand)) continue;
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = cand;
                    psi.Arguments = "--version";
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit(3000);
                        if (p.ExitCode == 0)
                        {
                            _pythonExecutable = cand;
                            return cand;
                        }
                    }
                }
                catch
                {
                    // Try next candidate
                }
            }
            throw new FileNotFoundException("Python 3 executable was not found in PATH or standard installation paths.");
        }

        private Task<string> RunPythonBackendAsync(string[] args)
        {
            return Task.Run(delegate
            {
                string py = FindPythonExecutable();
                StringBuilder argLine = new StringBuilder();
                argLine.Append(QuoteArg(_backendScriptPath));
                foreach (string a in args)
                {
                    argLine.Append(' ').Append(QuoteArg(a));
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = py;
                psi.Arguments = argLine.ToString();
                psi.WorkingDirectory = _appDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process proc = Process.Start(psi))
                {
                    Task<string> outTask = proc.StandardOutput.ReadToEndAsync();
                    Task<string> errTask = proc.StandardError.ReadToEndAsync();
                    proc.WaitForExit();
                    string stdout = outTask.Result.Trim();
                    string stderr = errTask.Result.Trim();

                    if (proc.ExitCode != 0 && string.IsNullOrEmpty(stdout))
                    {
                        throw new InvalidOperationException(string.IsNullOrEmpty(stderr) ? "Backend exited with code " + proc.ExitCode : stderr);
                    }
                    return stdout;
                }
            });
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        private static string ReadTextAutoCSharp(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
            }
            if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
            }
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
            }
            return Encoding.UTF8.GetString(raw);
        }

        private static string SanitizePdfFilename(string name)
        {
            string baseName = string.IsNullOrEmpty(name) ? "md2pdf_Document" : Path.GetFileNameWithoutExtension(name);
            StringBuilder sb = new StringBuilder();
            foreach (char ch in baseName)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('_');
                }
            }
            string clean = sb.ToString().Trim('.', '_');
            if (string.IsNullOrEmpty(clean)) clean = "md2pdf_Document";
            if (clean.Length > 80) clean = clean.Substring(0, 80);
            return clean + ".pdf";
        }

        private void OpenLastExportedPdf()
        {
            if (!string.IsNullOrEmpty(_lastExportedPdfPath) && File.Exists(_lastExportedPdfPath))
            {
                OpenPathWithShell(_lastExportedPdfPath);
            }
        }

        private void RevealLastExportedPdf()
        {
            if (!string.IsNullOrEmpty(_lastExportedPdfPath) && File.Exists(_lastExportedPdfPath))
            {
                RevealPathInExplorer(_lastExportedPdfPath);
            }
        }

        private static void OpenPathWithShell(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // Ignore shell launch errors
            }
        }

        private static void RevealPathInExplorer(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Process.Start("explorer.exe", string.Format("/select,\"{0}\"", path));
            }
            catch
            {
                // Ignore explorer launch errors
            }
        }

        private void ShowAboutDialog()
        {
            string engineInfo = _settings.PdfEngine == "webview2"
                ? "Native WebView2 PrintToPdf"
                : "Python Chromium Engine (pdf_engine.py)";

            MessageBox.Show(
                "md2pdf (Windows Desktop Edition)\n\n" +
                "Converts Markdown, LaTeX, Mermaid diagrams, and Gemini exports into crisp vector PDFs.\n\n" +
                "Renderer: gemini-renderer.js\n" +
                "Bundled Libraries: vendor/\n" +
                "Active PDF Engine: " + engineInfo,
                "About md2pdf",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isSelfTest && !_isCaptureScreenshots && _isDirty && !_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult res = MessageBox.Show(
                    "You have unsaved changes in the editor.\n\nDiscard changes and exit?",
                    "Unsaved Changes - md2pdf",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2
                );
                if (res == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            CloseFileWatcher();
            if (!_isSelfTest && !_isCaptureScreenshots)
            {
                SaveWindowBounds();
                SaveSettings();
            }
        }

        private void SaveWindowBounds()
        {
            if (_isFullScreen) return;
            _settings.WindowMaximized = this.WindowState == FormWindowState.Maximized;
            Rectangle bounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
            _settings.WindowX = bounds.X;
            _settings.WindowY = bounds.Y;
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string raw = File.ReadAllText(_settingsPath, Encoding.UTF8);
                    AppSettings loaded = _json.Deserialize<AppSettings>(raw);
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // Fallback to defaults
            }
            return new AppSettings();
        }

        private void SaveSettings()
        {
            if (_isSelfTest || _isCaptureScreenshots) return;
            try
            {
                File.WriteAllText(_settingsPath, _json.Serialize(_settings), new UTF8Encoding(false));
            }
            catch
            {
                // Non-fatal
            }
        }

        private Rectangle GetVisibleWindowBounds()
        {
            try
            {
                NativeMethods.RECT rect;
                int hr = NativeMethods.DwmGetWindowAttribute(
                    this.Handle,
                    NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out rect,
                    Marshal.SizeOf(typeof(NativeMethods.RECT))
                );
                if (hr == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top)
                {
                    return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                }
            }
            catch
            {
                // Fallback below
            }
            return this.Bounds;
        }

        private async Task CaptureWindowPngAsync(string filePath)
        {
            this.Activate();
            this.BringToFront();
            this.Refresh();
            Application.DoEvents();
            await Task.Delay(200);

            Rectangle winBounds = GetVisibleWindowBounds();
            if (winBounds.Width <= 0 || winBounds.Height <= 0)
            {
                throw new InvalidOperationException("Invalid window bounds for screenshot capture.");
            }

            Image wvImg = null;
            Rectangle wvScreen = Rectangle.Empty;
            if (_webView != null && _webView.CoreWebView2 != null)
            {
                wvScreen = _webView.RectangleToScreen(_webView.ClientRectangle);
                using (MemoryStream ms = new MemoryStream())
                {
                    await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                    ms.Position = 0;
                    wvImg = Image.FromStream(ms);
                }
            }

            try
            {
                bool capturedFromScreen = false;
                using (Bitmap bmp = new Bitmap(winBounds.Width, winBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        try
                        {
                            g.CopyFromScreen(winBounds.Location, Point.Empty, winBounds.Size);
                            capturedFromScreen = true;
                        }
                        catch
                        {
                            capturedFromScreen = false;
                        }

                        if (capturedFromScreen)
                        {
                            if (wvImg != null)
                            {
                                g.DrawImage(wvImg, new Rectangle(wvScreen.Left - winBounds.Left, wvScreen.Top - winBounds.Top, wvScreen.Width, wvScreen.Height));
                            }
                        }
                    }
                    if (capturedFromScreen)
                    {
                        bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        return;
                    }
                }

                // Fallback when desktop screen DC is unavailable (non-interactive / locked session)
                Rectangle fullBounds = this.Bounds;
                using (Bitmap fullBmp = new Bitmap(fullBounds.Width, fullBounds.Height))
                {
                    bool printed = false;
                    using (Graphics fg = Graphics.FromImage(fullBmp))
                    {
                        fg.Clear(Color.FromArgb(11, 13, 17));
                        IntPtr hdc = fg.GetHdc();
                        try
                        {
                            printed = NativeMethods.PrintWindow(this.Handle, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                            if (!printed)
                            {
                                printed = NativeMethods.PrintWindow(this.Handle, hdc, 0);
                            }
                        }
                        finally
                        {
                            fg.ReleaseHdc(hdc);
                        }
                    }

                    if (!printed)
                    {
                        this.DrawToBitmap(fullBmp, new Rectangle(0, 0, fullBounds.Width, fullBounds.Height));
                    }

                    if (wvImg != null)
                    {
                        using (Graphics fg = Graphics.FromImage(fullBmp))
                        {
                            fg.DrawImage(wvImg, new Rectangle(wvScreen.Left - fullBounds.Left, wvScreen.Top - fullBounds.Top, wvScreen.Width, wvScreen.Height));
                        }
                    }

                    int cropX = Math.Max(0, winBounds.Left - fullBounds.Left);
                    int cropY = Math.Max(0, winBounds.Top - fullBounds.Top);
                    int cropW = Math.Min(winBounds.Width, fullBounds.Width - cropX);
                    int cropH = Math.Min(winBounds.Height, fullBounds.Height - cropY);
                    if (cropW <= 0 || cropH <= 0)
                    {
                        fullBmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    else
                    {
                        using (Bitmap cropped = fullBmp.Clone(new Rectangle(cropX, cropY, cropW, cropH), fullBmp.PixelFormat))
                        {
                            cropped.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
            }
            finally
            {
                if (wvImg != null)
                {
                    wvImg.Dispose();
                }
            }
        }

        private async Task RunCaptureScreenshotsAsync()
        {
            try
            {
                string outDir = Path.GetFullPath(_captureScreenshotsDir);
                Directory.CreateDirectory(outDir);

                this.Activate();
                this.BringToFront();
                await Task.Delay(800);

                // 1. Capture Gemini Dark split view
                await CaptureWindowPngAsync(Path.Combine(outDir, "app-dark.png"));

                // 2. Switch to Academic Light, scroll to display math + Mermaid diagram, open Document Outline, and capture
                SetDocumentSetting("theme", "light");
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        "(function(){" +
                        "var ps = document.getElementById('previewScroll'); if (ps) ps.scrollTop = ps.scrollHeight - ps.clientHeight;" +
                        "var si = document.getElementById('sourceInput'); if (si) si.scrollTop = si.scrollHeight - si.clientHeight;" +
                        "document.getElementById('outlineMenu').classList.add('visible');" +
                        "})();"
                    );
                }
                await Task.Delay(500);
                await CaptureWindowPngAsync(Path.Combine(outDir, "app-light.png"));

                // 3. Clean up UI state and exit
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('outlineMenu').classList.remove('visible');");
                }
                SetDocumentSetting("theme", "dark");

                _allowClose = true;
                Program.ExitCodeOverride = 0;
                Application.Exit();
            }
            catch (Exception ex)
            {
                try
                {
                    string outDir = Path.GetFullPath(_captureScreenshotsDir);
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, "capture_error.txt"), ex.ToString(), new UTF8Encoding(false));
                }
                catch
                {
                }
                _allowClose = true;
                Program.ExitCodeOverride = 1;
                Application.Exit();
            }
        }

        private async Task RunSelfTestAsync()
        {
            try
            {
                // Give KaTeX and fonts a brief moment to settle after loadDocument
                await Task.Delay(500);

                string snapshotRaw = await CallUiMethodAsync("getSelfTestSnapshot", null);
                // ExecuteScriptAsync returns JSON-encoded result
                Dictionary<string, object> snapshot = _json.Deserialize<Dictionary<string, object>>(snapshotRaw);
                if (snapshot == null || !GetBool(snapshot, "ready", false))
                {
                    WriteSelfTestFailure("DesktopHostApp.getSelfTestSnapshot() reported not ready.");
                    return;
                }

                string standaloneHtml = GetString(snapshot, "standaloneHtml");
                int blockEq = GetInt(snapshot, "blockEquations", 0);
                int inlineEq = GetInt(snapshot, "inlineEquations", 0);
                int errorCount = GetInt(snapshot, "errorCount", 0);

                if (string.IsNullOrEmpty(standaloneHtml) || (blockEq + inlineEq) == 0)
                {
                    WriteSelfTestFailure("Renderer produced empty HTML or zero equations.");
                    return;
                }

                string outPdfPrimary = Path.GetFullPath(_selfTestOutputPath);
                string outDir = Path.GetDirectoryName(outPdfPrimary);
                string outPdfWebView = Path.Combine(
                    outDir,
                    Path.GetFileNameWithoutExtension(outPdfPrimary) + "_webview2.pdf"
                );

                Stopwatch swPy = Stopwatch.StartNew();
                await ExportViaPythonEngineAsync(standaloneHtml, outPdfPrimary);
                swPy.Stop();

                Stopwatch swWv = Stopwatch.StartNew();
                bool wvOk = await ExportViaWebView2Async(outPdfWebView);
                swWv.Stop();

                if (!File.Exists(outPdfPrimary) || new FileInfo(outPdfPrimary).Length < 1000)
                {
                    WriteSelfTestFailure("Python shared pdf_engine.py output missing or too small.");
                    return;
                }
                if (!wvOk || !File.Exists(outPdfWebView) || new FileInfo(outPdfWebView).Length < 1000)
                {
                    WriteSelfTestFailure("WebView2 native PrintToPdfAsync output missing or too small.");
                    return;
                }

                Dictionary<string, object> report = new Dictionary<string, object>();
                report["ok"] = true;
                report["title"] = GetString(snapshot, "title");
                report["charCount"] = GetInt(snapshot, "charCount", 0);
                report["blockEquations"] = blockEq;
                report["inlineEquations"] = inlineEq;
                report["errorCount"] = errorCount;
                report["pythonPdfPath"] = outPdfPrimary;
                report["pythonPdfBytes"] = new FileInfo(outPdfPrimary).Length;
                report["pythonElapsedMs"] = swPy.ElapsedMilliseconds;
                report["webview2PdfPath"] = outPdfWebView;
                report["webview2PdfBytes"] = new FileInfo(outPdfWebView).Length;
                report["webview2ElapsedMs"] = swWv.ElapsedMilliseconds;

                string reportPath = Path.Combine(outDir, "selftest_report.json");
                File.WriteAllText(reportPath, _json.Serialize(report), new UTF8Encoding(false));

                if (_selfTestTimeoutTimer != null) _selfTestTimeoutTimer.Stop();
                _allowClose = true;
                Program.ExitCodeOverride = 0;
                Application.Exit();
            }
            catch (Exception ex)
            {
                WriteSelfTestFailure("Self-test exception: " + ex.Message);
            }
        }

        private void WriteSelfTestFailure(string error)
        {
            try
            {
                string outDir = Path.GetDirectoryName(Path.GetFullPath(_selfTestOutputPath));
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                Dictionary<string, object> report = new Dictionary<string, object>();
                report["ok"] = false;
                report["error"] = error;
                File.WriteAllText(Path.Combine(outDir, "selftest_report.json"), _json.Serialize(report), new UTF8Encoding(false));
            }
            catch
            {
                // Ignore
            }
            if (_selfTestTimeoutTimer != null) _selfTestTimeoutTimer.Stop();
            _allowClose = true;
            Program.ExitCodeOverride = 1;
            Application.Exit();
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return null;
            return Convert.ToString(dict[key]);
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            try { return Convert.ToBoolean(dict[key]); } catch { return fallback; }
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            try { return Convert.ToInt32(dict[key]); } catch { return fallback; }
        }
    }

    internal static class Program
    {
        internal static int ExitCodeOverride = 0;

        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string startupFile = "";
            string selfTestOut = "";
            string captureDir = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--self-test", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    selfTestOut = args[i + 1];
                    i++;
                }
                else if (string.Equals(args[i], "--capture-screenshots", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    captureDir = args[i + 1];
                    i++;
                }
                else if (string.IsNullOrEmpty(startupFile) && !args[i].StartsWith("--"))
                {
                    startupFile = args[i];
                }
            }

            using (MainForm form = new MainForm(startupFile, selfTestOut, captureDir))
            {
                Application.Run(form);
            }

            return ExitCodeOverride;
        }
    }
}
