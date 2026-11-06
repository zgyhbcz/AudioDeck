using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AudioDeck.Audio;

namespace AudioDeck
{
    internal static class Diagnostics
    {
        public static void Write(string path)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("AudioDeck diagnostics");
            text.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine("OS: " + Environment.OSVersion.VersionString);
            text.AppendLine("Clr: " + Environment.Version);
            text.AppendLine("PolicyConfig: " + AudioManager.ProbePolicyConfig());
            text.AppendLine();

            try
            {
                using (AudioManager manager = new AudioManager())
                {
                    AudioSnapshot snapshot = manager.GetSnapshot();
                    text.AppendLine("Default: " + snapshot.DefaultDeviceName);
                    text.AppendLine("DefaultId: " + (snapshot.DefaultDeviceId ?? "(none)"));
                    text.AppendLine("Volume: " + Math.Round(snapshot.Volume * 100f) + "%");
                    text.AppendLine("Muted: " + snapshot.Muted);
                    text.AppendLine("Error: " + (snapshot.Error ?? "(none)"));
                    text.AppendLine("Devices: " + snapshot.Devices.Count);
                    for (int i = 0; i < snapshot.Devices.Count; i++)
                    {
                        AudioDevice device = snapshot.Devices[i];
                        text.AppendLine(string.Format("[{0}]{1} {2} | {3} | form={4} | {5}",
                            i,
                            device.IsDefault ? " *" : "  ",
                            device.Name,
                            device.Description,
                            device.FormFactor,
                            device.Id));
                    }
                }
            }
            catch (Exception ex)
            {
                text.AppendLine("FATAL: " + ex);
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
        }
    }
}
