using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using tbvolscroll.Classes;
using tbvolscroll.Properties;

namespace tbvolscroll
{
    public class AudioHandler
    {
        private static IEnumerable<CoreAudioDevice> audioDevices = null;
        private static CoreAudioController coreAudioController;
        private static int volume = 0;
        private static bool muted = false;
        private static bool audioDisabled = false;

        public AudioHandler()
        {
            coreAudioController = new CoreAudioController();
            coreAudioController.AudioDeviceChanged.Subscribe(OnDeviceChanged);
        }
        public async void OnDeviceChanged(DeviceChangedArgs value)
        {
            await RefreshPlaybackDevices();
            UpdateAudioState();
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(
          IntPtr hWnd,
          int X,
          int Y,
          int nWidth,
          int nHeight,
          bool bRepaint);

        [DllImport("User32.dll")]
        internal static extern bool ClientToScreen(IntPtr hWnd, out Point point);

        [DllImport("User32.dll")]
        internal static extern bool GetClientRect(IntPtr hWnd, out WindowRect lpRect);

        public int Volume
        {
            get => volume;
            set => volume = value;
        }

        public bool Muted
        {
            get => muted;
            set => muted = value;
        }

        public IEnumerable<CoreAudioDevice> AudioDevices
        {
            get => audioDevices;
            set => audioDevices = value;
        }

        public CoreAudioController CoreAudioController
        {
            get => coreAudioController;
            set => coreAudioController = value;
        }
        public bool AudioDisabled { get => audioDisabled; set => audioDisabled = value; }

        public void UpdateAudioState()
        {
            if (CoreAudioController.DefaultPlaybackDevice != null)
            {
                Volume = (int)CoreAudioController.DefaultPlaybackDevice.Volume;
                Muted = CoreAudioController.DefaultPlaybackDevice.IsMuted;
            }
            else
            {
                Volume = 0;
                Muted = true;
                AudioDisabled = true;
            }
        }

        public List<CoreAudioDevice> GetAudioDevicesList()
        {
            List<CoreAudioDevice> audioDevicesList = new List<CoreAudioDevice>();
            audioDevicesList.AddRange(AudioDevices);
            return audioDevicesList;
        }

        public async Task RefreshPlaybackDevices()
        {
            if (coreAudioController == null)
                coreAudioController = new CoreAudioController();
            IEnumerable<CoreAudioDevice> coreAudioDevices = await coreAudioController.GetPlaybackDevicesAsync(DeviceState.Active);
            audioDevices = coreAudioDevices;
        }

        public double GetMasterVolume()
        {
            try
            {
                return coreAudioController != null && coreAudioController.DefaultPlaybackDevice != null ? coreAudioController.DefaultPlaybackDevice.Volume : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        public void SetMasterVolume(int volume)
        {
            try
            {
                if (coreAudioController == null || coreAudioController.DefaultPlaybackDevice == null)
                    return;
                coreAudioController.DefaultPlaybackDevice.Volume = volume;
            }
            catch
            {
            }
        }

        public void SetMasterVolumeMute(bool isMuted = false)
        {
            try
            {
                if (coreAudioController == null || coreAudioController.DefaultPlaybackDevice == null)
                    return;
                coreAudioController.DefaultPlaybackDevice.Mute(isMuted);
            }
            catch
            {
            }
        }

        public async Task OpenSndVol()
        {
            Process sndvolProc = new Process();
            sndvolProc.StartInfo.FileName = "sndvol.exe";
            sndvolProc.Start();

            bool hasWindow = false;
            IntPtr windowHandle = new IntPtr();
            while (!hasWindow)
            {
                Process[] processes = Process.GetProcessesByName("sndvol");

                foreach (Process p in processes)
                {
                    IntPtr tempHandle = p.MainWindowHandle;
                    if (tempHandle.ToInt32() != 0)
                    {
                        windowHandle = tempHandle;
                        hasWindow = true;
                    }
                }
                await Task.Delay(50);
            }

            int sndvolWidth = 1000;
            int sndvolHeight = 500;
            Point position = Cursor.Position;
            Screen screen = Screen.FromPoint(position);
            Point location = new Point();
            Rectangle workingArea = screen.WorkingArea;

            switch (TaskbarHelper.Position)
            {
                case TaskbarPosition.Bottom:
                    location = new Point(workingArea.Right - sndvolWidth, workingArea.Bottom - sndvolHeight);
                    break;
                case TaskbarPosition.Right:
                    location = new Point(workingArea.Right - sndvolWidth, workingArea.Bottom - sndvolHeight);
                    break;
                case TaskbarPosition.Left:
                    location = new Point(workingArea.Left, workingArea.Bottom - sndvolHeight);
                    break;
                case TaskbarPosition.Top:
                    location = new Point(workingArea.Right - sndvolWidth, workingArea.Top);
                    break;
            }
            MoveWindow(windowHandle, location.X, location.Y, sndvolWidth, sndvolHeight, true);

        }

        public async Task DoVolumeChanges(int delta)
        {

            await Task.Run(() =>
            {
                if ((delta < 0 && Globals.AudioHandler.Volume != 0) || (delta > 0 && Globals.AudioHandler.Volume != 100))
                {
                    try
                    {

                        Globals.AudioHandler.UpdateAudioState();
                        int newVolume = Globals.AudioHandler.Volume;

                        if (delta < 0)
                        {
                            if (Globals.InputHandler.IsAltDown || Globals.AudioHandler.Volume <= Settings.Default.PreciseScrollThreshold)
                                newVolume -= 1;
                            else
                                newVolume -= Settings.Default.VolumeStep;
                            if (newVolume <= 0 && Globals.AudioHandler.Muted == false)
                            {
                                newVolume = 0;
                                Globals.AudioHandler.SetMasterVolume(newVolume);
                                Globals.AudioHandler.SetMasterVolumeMute(isMuted: true);
                            }
                            else
                                Globals.AudioHandler.SetMasterVolume(newVolume);
                        }
                        else
                        {
                            if (Globals.InputHandler.IsAltDown || Globals.AudioHandler.Volume < Settings.Default.PreciseScrollThreshold)
                                newVolume += 1;
                            else
                                newVolume += Settings.Default.VolumeStep;
                            if (newVolume > 0 && Globals.AudioHandler.Muted == true)
                                Globals.AudioHandler.SetMasterVolumeMute(isMuted: false);
                            if (newVolume > 100)
                                newVolume = 100;
                            Globals.AudioHandler.SetMasterVolume(newVolume);

                        }
                        Globals.AudioHandler.UpdateAudioState();
                    }
                    catch { }
                }
            });
        }

        public async Task ToggleAudioPlaybackDevice(int delta)
        {
            try
            {
                List<CoreAudioDevice> audioDevicesList = new List<CoreAudioDevice>();
                audioDevicesList.AddRange(Globals.AudioHandler.AudioDevices);
                CoreAudioDevice curDevice = Globals.AudioHandler.CoreAudioController.DefaultPlaybackDevice;
                if (Globals.CurrentAudioDeviceIndex == -1)
                    Globals.CurrentAudioDeviceIndex = audioDevicesList.FindIndex(x => x.Id == curDevice.Id);
                int newDeviceIndex = Globals.CurrentAudioDeviceIndex;

                if (delta < 0)
                {
                    if (Globals.CurrentAudioDeviceIndex > 0)
                        --Globals.CurrentAudioDeviceIndex;
                    else
                        Globals.CurrentAudioDeviceIndex = 0;
                }
                else
                {
                    if (Globals.CurrentAudioDeviceIndex < audioDevicesList.Count - 1)
                        ++Globals.CurrentAudioDeviceIndex;
                    else
                        Globals.CurrentAudioDeviceIndex = audioDevicesList.Count - 1;
                }

                if (newDeviceIndex != Globals.CurrentAudioDeviceIndex)
                {
                    CoreAudioDevice newPlaybackDevice = audioDevicesList[Globals.CurrentAudioDeviceIndex];
                    await newPlaybackDevice.SetAsDefaultAsync();
                }
            }
            catch { }
        }


        public static Rectangle GetWindowClientRectangle(IntPtr handle)
        {
            GetClientRect(handle, out WindowRect lpRect);
            ClientToScreen(handle, out Point point);
            return lpRect.ToRectangleOffset(point);
        }

        public struct WindowRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rectangle ToRectangleOffset(Point p) => Rectangle.FromLTRB(p.X, p.Y, Right + p.X, Bottom + p.Y);
        }
    }
}