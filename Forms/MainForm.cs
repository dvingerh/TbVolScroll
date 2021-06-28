using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Collections.Generic;
using tbvolscroll.Properties;
using tbvolscroll.Forms;
using System.Media;

namespace tbvolscroll
{
    public partial class MainForm : Form
    {


        #region DLLImports 
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion

        #region Variables
        public bool isReady = false;
        private int curDeviceIndex = -1;
        public InputHandler inputHandler;
        public AudioHandler audioHandler;
        private int volumeBarAutoHideTimeout = 1000;
        private bool isDisplayingVolume = false;
        #endregion

        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= 0x80;

                return Params;
            }
        }

        private void SetPositionTopmost()
        {
            Invoke((MethodInvoker)delegate
            {
                SetWindowPos(Handle.ToInt32(), -1, Left, Top, Width, Height, 16u);
            });
        }

        private void ShowInactiveTopmost()
        {
            Invoke((MethodInvoker)delegate
            {
                ShowWindow(Handle, 4);
            });
        }

        public MainForm(bool noTray = false, bool attemptedAdmin = false)
        {
            InitializeComponent();
            bool hasAdmin = IsAdministrator();

            TitleLabelMenuItem.Text = $"{Application.ProductName} v{Application.ProductVersion}" + (hasAdmin ? " (Administrator)" : "");

            if (noTray)
                TrayNotifyIcon.Visible = false;

            if (!attemptedAdmin && !hasAdmin && Settings.Default.AutoRetryAdmin)
            {
                Process proc = new Process();
                proc.StartInfo.FileName = Application.ExecutablePath;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.StartInfo.Arguments = "admin";
                proc.Start();
                ExitMenuItem.PerformClick();
            }
        }

        private bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public void DoVolumeChanges(int delta)
        {
            try
            {
                Invoke((MethodInvoker)delegate
                {
                    audioHandler.UpdateAudioState();
                    if (delta < 0)
                    {
                        if (inputHandler.isAltDown || audioHandler.Volume <= Settings.Default.PreciseScrollThreshold)
                            audioHandler.SetMasterVolume(audioHandler.Volume - 1);
                        else
                            audioHandler.SetMasterVolume(audioHandler.Volume - Settings.Default.VolumeStep);
                    }
                    else
                    {
                        if (inputHandler.isAltDown || audioHandler.Volume < Settings.Default.PreciseScrollThreshold)
                            audioHandler.SetMasterVolume(audioHandler.Volume + 1);
                        else
                            audioHandler.SetMasterVolume(audioHandler.Volume + Settings.Default.VolumeStep);
                    }

                    audioHandler.UpdateAudioState();
                    if (audioHandler.Volume == 0 && audioHandler.Muted == false)
                        audioHandler.SetMasterVolumeMute(isMuted: true);
                    else if (audioHandler.Volume > 0 && audioHandler.Muted == true)
                        audioHandler.SetMasterVolumeMute(isMuted: false);

                    audioHandler.UpdateAudioState();
                    VolumeTextLabel.Text = $"{audioHandler.Volume}% ";
                    TrayNotifyIcon.Text = $"{Application.ProductName} - {audioHandler.Volume}%";
                    Refresh();

                    Width = (int)CalculateBarSize(VolumeTextLabel.Text).Width + audioHandler.Volume + Settings.Default.BarWidthPadding;
                    Opacity = Settings.Default.BarOpacity;
                    if (Settings.Default.UseBarGradient)
                        VolumeTextLabel.BackColor = CalculateColor(audioHandler.Volume);
                    else
                        VolumeTextLabel.BackColor = Settings.Default.BarColor;

                    DoAppearanceUpdate();
                });
            }
            catch { }
        }

        public void SetVolumeBarPosition()
        {
            Point cursorPosition = Cursor.Position;
            switch (TaskbarHelper.Position)
            {
                case TaskbarPosition.Bottom:
                    Left = cursorPosition.X - Width / 2;
                    Top = cursorPosition.Y - Height - 5;
                    break;
                case TaskbarPosition.Left:
                    Left = cursorPosition.X + 25;
                    Top = cursorPosition.Y - 5;
                    break;
                case TaskbarPosition.Top:
                    Left = cursorPosition.X - Width / 2;
                    Top = cursorPosition.Y + 25;
                    break;
                case TaskbarPosition.Right:
                    Left = cursorPosition.X - Width - 25;
                    Top = cursorPosition.Y - 5;
                    break;
                case TaskbarPosition.Unknown:
                    Left = cursorPosition.X - Width / 2;
                    Top = cursorPosition.Y - Height - 5;
                    break;
                default:
                    Left = cursorPosition.X - Width / 2;
                    Top = cursorPosition.Y - Height - 5;
                    break;

            }
        }

        private Color CalculateColor(double percentage)
        {
            double num = ((percentage > 50.0) ? (1.0 - 2.0 * (percentage - 50.0) / 100.0) : 1.0) * 255.0;
            double num2 = ((percentage > 50.0) ? 1.0 : (2.0 * percentage / 100.0)) * 255.0;
            double num3 = 0.0;
            return Color.FromArgb((int)num, (int)num2, (int)num3);
        }


        private async void AutoHideVolume()
        {
            isDisplayingVolume = true;
            volumeBarAutoHideTimeout = Settings.Default.AutoHideTimeOut;
            ShowInactiveTopmost();

            while (volumeBarAutoHideTimeout >= 10)
            {
                await Task.Delay(10);
                volumeBarAutoHideTimeout -= 10;
            }

            if (!inputHandler.isScrolling)
            {
                Invoke((MethodInvoker)delegate
                {
                    Hide();
                    volumeBarAutoHideTimeout = Settings.Default.AutoHideTimeOut;
                    isDisplayingVolume = false;
                });
            }
            else
                AutoHideVolume();
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            if (inputHandler != null)
                inputHandler.inputEvents.Dispose();
            TrayNotifyIcon.Dispose();
            Environment.Exit(0);
        }

        private void RestartAppNormal(object sender, EventArgs e)
        {
            if (inputHandler != null)
                inputHandler.inputEvents.Dispose();
            TrayNotifyIcon.Dispose();
            Process proc = new Process();
            proc.StartInfo.FileName = Application.ExecutablePath;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "runas";
            proc.StartInfo.Arguments = "restart";
            proc.Start();
            ExitMenuItem.PerformClick();
        }

        private void ShowTrayMenuOnClick(object sender, EventArgs e)
        {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(TrayNotifyIcon, null);
        }

        private void OpenConfigureDialog(object sender, EventArgs e)
        {
            new ConfigurationForm(this).ShowDialog();
        }

        private void RestartAppAsAdministrator(object sender, EventArgs e)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = Application.ExecutablePath;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "runas";
            proc.StartInfo.Arguments = "restart";
            proc.Start();
            ExitMenuItem.PerformClick();
        }

        private void DrawVolumeBarBorder(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawRectangle(new Pen(Color.Black, 2), DisplayRectangle);
        }

        private void OpenStartupFolder(object sender, EventArgs e)
        {
            Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        }


        private void VolumeMixerMenuItemClick(object sender, EventArgs e)
        {
            audioHandler.OpenSndVol();
        }

        private void AudioDevicesMenuItemClick(object sender, EventArgs e)
        {
            new AudioPlaybackDevicesForm(this).ShowDialog();
            audioHandler.UpdateAudioState();
            SetTrayIcon();
        }

        private void DoAppearanceUpdate()
        {
            audioHandler.UpdateAudioState();
            TrayNotifyIcon.Text = $"{Application.ProductName} - {audioHandler.Volume}%";

            SetTrayIcon();
            SetVolumeBarPosition();
            SetPositionTopmost();
            if (isDisplayingVolume)
                return;
            AutoHideVolume();
        }

        public void SetTrayIcon()
        {
            if (audioHandler.AudioDisabled)
            {
                TrayNotifyIcon.Text = "Audio disabled";
                TrayNotifyIcon.Icon = Resources.voldisabled;
            }
            else if (audioHandler.Muted)
            {
                TrayNotifyIcon.Text = "System is muted";
                TrayNotifyIcon.Icon = Resources.volmute;
            }
            else
            {
                if (audioHandler.Volume >= 90)
                    TrayNotifyIcon.Icon = Resources.vol100;
                if (audioHandler.Volume >= 80 && audioHandler.Volume < 90)
                    TrayNotifyIcon.Icon = Resources.vol90;
                if (audioHandler.Volume >= 70 && audioHandler.Volume < 80)
                    TrayNotifyIcon.Icon = Resources.vol80;
                if (audioHandler.Volume >= 60 && audioHandler.Volume < 70)
                    TrayNotifyIcon.Icon = Resources.vol70;
                if (audioHandler.Volume >= 50 && audioHandler.Volume < 60)
                    TrayNotifyIcon.Icon = Resources.vol60;
                if (audioHandler.Volume >= 40 && audioHandler.Volume < 50)
                    TrayNotifyIcon.Icon = Resources.vol50;
                if (audioHandler.Volume >= 30 && audioHandler.Volume < 40)
                    TrayNotifyIcon.Icon = Resources.vol40;
                if (audioHandler.Volume >= 20 && audioHandler.Volume < 30)
                    TrayNotifyIcon.Icon = Resources.vol30;
                if (audioHandler.Volume >= 10 && audioHandler.Volume < 20)
                    TrayNotifyIcon.Icon = Resources.vol20;
                if (audioHandler.Volume > 0 && audioHandler.Volume < 10)
                    TrayNotifyIcon.Icon = Resources.vol10;
            }
            Icon = TrayNotifyIcon.Icon;
        }

        public void SetMuteStatus(int delta)
        {
            bool isMuted = delta < 0;
            audioHandler.SetMasterVolumeMute(isMuted);
            if (isMuted)
            {
                VolumeTextLabel.Text = "System Muted";
                TrayNotifyIcon.Text = "System is muted";
            }
            else
            {
                VolumeTextLabel.Text = "System Unmuted";
                TrayNotifyIcon.Text = $"Volume: {audioHandler.Volume}%";
            }
            Width = Settings.Default.BarWidthPadding + (int)CalculateBarSize(VolumeTextLabel.Text).Width + 10;
            VolumeTextLabel.BackColor = Color.SkyBlue;
            DoAppearanceUpdate();
        }

        public async Task ToggleAudioPlaybackDevice(int delta)
        {
            try
            {
                List<CoreAudioDevice> audioDevicesList = new List<CoreAudioDevice>();
                audioDevicesList.AddRange(audioHandler.AudioDevices);
                CoreAudioDevice curDevice = audioHandler.CoreAudioController.DefaultPlaybackDevice;
                if (curDeviceIndex == -1)
                    curDeviceIndex = audioDevicesList.FindIndex(x => x.Id == curDevice.Id);
                int newDeviceIndex = curDeviceIndex;

                if (delta < 0)
                {
                    if (curDeviceIndex > 0)
                        --curDeviceIndex;
                    else
                        curDeviceIndex = 0;
                }
                else
                {
                    if (curDeviceIndex < audioDevicesList.Count - 1)
                        ++curDeviceIndex;
                    else
                        curDeviceIndex = audioDevicesList.Count - 1;
                }

                if (newDeviceIndex != curDeviceIndex)
                {
                    CoreAudioDevice newPlaybackDevice = audioDevicesList[curDeviceIndex];
                    await newPlaybackDevice.SetAsDefaultAsync();
                }
                VolumeTextLabel.Text = $"({curDeviceIndex + 1}/{audioDevicesList.Count}) {audioHandler.CoreAudioController.DefaultPlaybackDevice.Name}";
                Width = Settings.Default.BarWidthPadding + (int)CalculateBarSize(VolumeTextLabel.Text).Width + 10;
                VolumeTextLabel.BackColor = Color.SkyBlue;
                DoAppearanceUpdate();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public SizeF CalculateBarSize(string text)
        {
            return VolumeTextLabel.CreateGraphics().MeasureString(text, VolumeTextLabel.Font);
        }

        private void PlaySystemSoundTrayMenu(object sender, MouseEventArgs e)
        {
            if (TitleLabelMenuItem.ContentRectangle.Contains(e.Location))
                SystemSounds.Exclamation.Play();
        }

        private void ShowVolumeSliderPopupForm(object sender, MouseEventArgs e)
        {
            if (isReady)
            {
                if (e.Button == MouseButtons.Middle)
                {
                    inputHandler.popupShowing = true;
                    TrayContextMenu.Hide();
                    new VolumeSliderPopupForm(this).ShowDialog();
                    inputHandler.popupShowing = false;
                }
            }
        }

        private async void LoadProgramConfiguration(object sender, EventArgs e)
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                Settings.Default.Save();
            }
            inputHandler = new InputHandler(this);
            audioHandler = new AudioHandler(this);
            await audioHandler.RefreshPlaybackDevices();
            audioHandler.UpdateAudioState();
            SetTrayIcon();

            volumeBarAutoHideTimeout = Settings.Default.AutoHideTimeOut;
            VolumeTextLabel.Font = Settings.Default.FontStyle;
            VolumeTextLabel.Text = $"{audioHandler.Volume}%";
            TrayNotifyIcon.Text = $"{Application.ProductName} - {audioHandler.Volume}%";

            SizeF newMinSizes = CalculateBarSize("0%");
            MinimumSize = new Size(Settings.Default.BarWidthPadding + (int)newMinSizes.Width, Settings.Default.BarHeightPadding + (int)newMinSizes.Height);
            Width = MinimumSize.Width;
            Height = MinimumSize.Height;

            SystemVolumeMixerMenuItem.Enabled = true;
            AudioPlaybackDevicesMenuItem.Enabled = true;
            VolumeSliderPopupMenuItem.Enabled = true;
            MoreMenuItem.Enabled = true;
            isReady = true;
            Hide();
        }

        private void VolumeSliderPopupMenuItemClick(object sender, EventArgs e)
        {
            inputHandler.popupShowing = true;
            new VolumeSliderPopupForm(this).ShowDialog();
            inputHandler.popupShowing = false;
        }

        private void CheckForUpdatesMenuItem_Click(object sender, EventArgs e)
        {
            new UpdateForm().ShowDialog();
        }

        private void CheckAudioHealth(object sender, EventArgs e)
        {
            Console.WriteLine(audioHandler.AudioDisabled);
            if (audioHandler.CoreAudioController.DefaultPlaybackDevice != null)
            {
                if (audioHandler.AudioDisabled == true)
                {
                    RestartAppAsAdministrator(null, null); // For now, just restart app. Properly reinitialize global hooks some other time
                }
            }
            else
            {
                audioHandler.AudioDisabled = true;
                AudioPlaybackDevicesMenuItem.Enabled = false;
                VolumeSliderPopupMenuItem.Enabled = false;
                if (inputHandler.IsSubscribed)
                    inputHandler.UnsubscribeMouse();
            }
            SetTrayIcon();
        }
    }
}