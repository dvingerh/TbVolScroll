using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using TbVolScrollNet5.Classes;

namespace TbVolScrollNet5.Forms
{
    public partial class MainForm : Form
    {

        #region DLLImports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(HandleRef hWnd, [In, Out] ref RECT rect);

        private static void ShowInactiveTopmost(Form frm)
        {
            frm.Invoke((MethodInvoker)delegate
            {
                ShowWindow(frm.Handle, 4);
                SetWindowPos(frm.Handle.ToInt32(), -1, frm.Left, frm.Top, frm.Width, frm.Height, 16u);
            });
        }
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        public static bool IsTaskbarHidden()
        {
            return CheckTaskbarVisibility(null);
        }

        public static bool CheckTaskbarVisibility(Screen screen)
        {
            if (screen == null)
            {
                screen = Screen.PrimaryScreen;
            }
            RECT rect = new RECT();
            GetWindowRect(new HandleRef(null, GetForegroundWindow()), ref rect);
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top).Contains(screen.Bounds);
        }

        public RECT TaskbarRect;
        public InputHandler inputHandler;
        public bool IsDisplayingVolume = false;

        #endregion

        public MainForm(bool noTray = false)
        {
            InitializeComponent();
            if (IsAdministrator())
                TitleLabelMenuItem.Text = $"{Assembly.GetEntryAssembly().GetName().Name} v{tbvolscroll.Properties.Settings.Default.AppVersion} (Admin)";
            else
                TitleLabelMenuItem.Text = $"{Assembly.GetEntryAssembly().GetName().Name} v{tbvolscroll.Properties.Settings.Default.AppVersion}";

            if (noTray)
                TrayNotifyIcon.Visible = false;
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public void DoVolumeChanges(int delta)
        {
            try
            {
                Invoke((MethodInvoker)delegate
                {
                    int CurrentVolume = (int)Math.Round(VolumeHandler.GetMasterVolume());
                    if (CursorInTaskbar() && !IsTaskbarHidden())
                    {
                        inputHandler.TimeOutHelper = 10;
                        if (delta < 0)
                        {
                            if (inputHandler.IsAltDown)
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume - 1);
                            }
                            else if (CurrentVolume <= tbvolscroll.Properties.Settings.Default.PreciseScrollThreshold)
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume - 1);
                            }
                            else
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume - tbvolscroll.Properties.Settings.Default.VolumeStep);
                            }
                        }
                        else
                        {
                            if (inputHandler.IsAltDown)
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume + 1);
                            }
                            else if (CurrentVolume < tbvolscroll.Properties.Settings.Default.PreciseScrollThreshold)
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume + 1);
                            }
                            else
                            {
                                VolumeHandler.SetMasterVolume(CurrentVolume + tbvolscroll.Properties.Settings.Default.VolumeStep);
                            }
                        }

                        CurrentVolume = (int)Math.Round(VolumeHandler.GetMasterVolume());
                        VolumeTextLabel.Text = $"{CurrentVolume}%";
                        TrayNotifyIcon.Text = $"{Assembly.GetEntryAssembly().GetName().Name} - {CurrentVolume}%";


                        Point CursorPosition = Cursor.Position;
                        Width = CurrentVolume + tbvolscroll.Properties.Settings.Default.BarWidth;
                        Left = CursorPosition.X - Width / 2;
                        Top = CursorPosition.Y - Height - 5;

                        VolumeTextLabel.Top = 1;
                        VolumeTextLabel.Left = 1;
                        VolumeTextLabel.Height = Height - 2;
                        VolumeTextLabel.Width = Width - 2;

                        Opacity = tbvolscroll.Properties.Settings.Default.BarOpacity;
                        if (tbvolscroll.Properties.Settings.Default.UseBarGradient)
                        {
                            VolumeTextLabel.BackColor = CalculateColor(CurrentVolume);

                        }
                        else
                        {
                            VolumeTextLabel.BackColor = tbvolscroll.Properties.Settings.Default.BarColor;
                        }
                        if (!IsDisplayingVolume)
                        {
                            IsDisplayingVolume = true;
                            Application.DoEvents();
                            AutoHideVolume();
                        }
                    }
                    else
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            Hide();
                        });
                        IsDisplayingVolume = false;
                    }
                });
            }
            catch { }
        }

        private static Color CalculateColor(double percentage)
        {
            double num = ((percentage > 50.0) ? (1.0 - 2.0 * (percentage - 50.0) / 100.0) : 1.0) * 255.0;
            double num2 = ((percentage > 50.0) ? 1.0 : (2.0 * percentage / 100.0)) * 255.0;
            double num3 = 0.0;
            return Color.FromArgb((int)num, (int)num2, (int)num3);
        }

        async Task PutTaskDelay()
        {
            await Task.Delay(100);
        }



        private async void AutoHideVolume()
        {
            ShowInactiveTopmost(this);

            while (inputHandler.TimeOutHelper != 0)
            {
                await PutTaskDelay();
                inputHandler.TimeOutHelper--;
            }

            Invoke((MethodInvoker)delegate
            {
                Hide();
                WindowState = FormWindowState.Minimized;
            });
            IsDisplayingVolume = false;
        }

        public bool CursorInTaskbar()
        {
            Point position = Cursor.Position;
            if (position.Y >= TaskbarRect.Top && position.Y <= TaskbarRect.Bottom)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            TrayNotifyIcon.Dispose();
            Environment.Exit(0);
        }

        private void RestartAppNormal(object sender, EventArgs e)
        {
            Application.Restart();
        }

        private void SetupProgramVars(object sender, EventArgs e)
        {
            VolumeTextLabel.Font = tbvolscroll.Properties.Settings.Default.FontStyle;
            int CurrentVolume = (int)Math.Round(VolumeHandler.GetMasterVolume());
            VolumeTextLabel.Text = $"{CurrentVolume}%";
            TrayNotifyIcon.Text = $"{Assembly.GetEntryAssembly().GetName().Name} - {CurrentVolume}%";

            MaximumSize = new Size(100 + tbvolscroll.Properties.Settings.Default.BarWidth, tbvolscroll.Properties.Settings.Default.BarHeight);
            MinimumSize = new Size(tbvolscroll.Properties.Settings.Default.BarWidth, tbvolscroll.Properties.Settings.Default.BarHeight);
            Width = 100 + tbvolscroll.Properties.Settings.Default.BarWidth;
            Height = tbvolscroll.Properties.Settings.Default.BarHeight;
            Hide();
            IntPtr hwnd = FindWindow("Shell_traywnd", "");
            GetWindowRect(hwnd, out TaskbarRect);
            inputHandler = new InputHandler(this);

        }

        private void ShowTrayMenuOnClick(object sender, EventArgs e)
        {
            System.Reflection.MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            mi.Invoke(TrayNotifyIcon, null);
        }

        private void OpenSetVolumeStepDialog(object sender, EventArgs e)
        {
            new SetVolumeStepForm().ShowDialog();
        }

        private void OpenSetPreciseScrollThreshold(object sender, EventArgs e)
        {
            new SetPreciseThresholdForm().ShowDialog();
        }

        private void TsmSetVolumeBarDimensions_Click(object sender, EventArgs e)
        {
            new SetAppearanceForm(this).ShowDialog();

        }

        private void RestartAppAsAdministrator(object sender, EventArgs e)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = Application.ExecutablePath;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "runas";
            proc.Start();
            Environment.Exit(0);
        }
    }
}
