using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AudioDeck
{
    internal static class SoundFeedback
    {
        private const uint SndAsync = 0x0001;
        private const uint SndNoDefault = 0x0002;
        private const uint SndAlias = 0x00010000;
        private const uint SndFilename = 0x00020000;
        private const uint SndSystem = 0x00200000;

        private const int VolumeTickIntervalMs = 120;

        private static int _lastVolumeTick;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySound(string sound, IntPtr module, uint flags);

        public static void VolumeTick()
        {
            VolumeTick(false);
        }

        public static void VolumeTick(bool force)
        {
            int now = Environment.TickCount;
            if (!force)
            {
                int elapsed = unchecked(now - _lastVolumeTick);
                if (elapsed >= 0 && elapsed < VolumeTickIntervalMs)
                {
                    return;
                }
            }

            _lastVolumeTick = now;

            // A short, quiet Windows UI sound. SND_SYSTEM routes it through the
            // "System Sounds" volume in Volume Mixer, like the Windows volume OSD.
            string media = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string file = Path.Combine(media, "Media", "Windows Information Bar.wav");
            if (File.Exists(file) &&
                PlaySound(file, IntPtr.Zero, SndFilename | SndAsync | SndNoDefault | SndSystem))
            {
                return;
            }

            PlaySound("SystemDefault", IntPtr.Zero,
                SndAlias | SndAsync | SndNoDefault | SndSystem);
        }

        public static void DeviceSwitched()
        {
            // Uses the user's current "Device connect" system sound. If the user
            // disabled it in the sound scheme, Windows stays silent, as it should.
            PlaySound("DeviceConnect", IntPtr.Zero,
                SndAlias | SndAsync | SndNoDefault | SndSystem);
        }
    }
}
