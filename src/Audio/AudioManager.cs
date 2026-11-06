using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioDeck.Audio
{
    public sealed class AudioDevice
    {
        public string Id;
        public string Name;
        public string Description;
        public int FormFactor;
        public bool IsDefault;
    }

    public sealed class AudioSnapshot
    {
        public List<AudioDevice> Devices = new List<AudioDevice>();
        public string DefaultDeviceId;
        public string DefaultDeviceName;
        public float Volume;
        public bool Muted;
        public string Error;
    }

    public sealed class AudioManager : IDisposable
    {
        private static readonly Guid PkeyDeviceFriendlyName =
            new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
        private static readonly Guid PkeyDeviceInterfaceFriendlyName =
            new Guid("026E516E-B814-414B-83CD-856D6FEF4822");
        private static readonly Guid PkeyAudioEndpointFormFactor =
            new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E");

        private const int StgmRead = 0x00000000;
        private const uint ClsctxAll = 0x17;
        private const ushort VtUi4 = 19;
        private const ushort VtI4 = 3;
        private const ushort VtLpwstr = 31;

        private readonly IMMDeviceEnumerator _enumerator;
        private readonly NotificationClient _notification;
        private IAudioEndpointVolume _volume;
        private string _volumeDeviceId;
        private bool _disposed;

        public event EventHandler Changed;

        public AudioManager()
        {
            Type type = Type.GetTypeFromCLSID(AudioGuids.MMDeviceEnumerator);
            _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type);
            _notification = new NotificationClient(this);
            int hr = _enumerator.RegisterEndpointNotificationCallback(_notification);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public AudioSnapshot GetSnapshot()
        {
            AudioSnapshot snapshot = new AudioSnapshot();
            IMMDeviceCollection collection = null;
            try
            {
                string defaultId = null;
                IMMDevice defaultDevice = null;
                int hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole,
                    out defaultDevice);
                if (hr >= 0 && defaultDevice != null)
                {
                    try
                    {
                        defaultDevice.GetId(out defaultId);
                    }
                    finally
                    {
                        Release(defaultDevice);
                    }
                }

                hr = _enumerator.EnumAudioEndpoints(EDataFlow.eRender, (uint)DeviceState.Active,
                    out collection);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                uint count;
                hr = collection.GetCount(out count);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        hr = collection.Item(i, out device);
                        if (hr < 0 || device == null)
                        {
                            continue;
                        }

                        string id;
                        hr = device.GetId(out id);
                        if (hr < 0 || string.IsNullOrEmpty(id))
                        {
                            continue;
                        }

                        AudioDevice item = new AudioDevice();
                        item.Id = id;
                        item.Name = GetStringProperty(device, PkeyDeviceFriendlyName, 14);
                        if (string.IsNullOrEmpty(item.Name))
                        {
                            item.Name = "音频输出设备";
                        }

                        string deviceDesc = GetStringProperty(device, PkeyDeviceFriendlyName, 2);
                        string interfaceName = GetStringProperty(device, PkeyDeviceInterfaceFriendlyName, 2);
                        string adapterName = ExtractAdapterName(item.Name);
                        if (!string.IsNullOrEmpty(adapterName))
                        {
                            item.Description = adapterName;
                        }
                        else if (!string.IsNullOrEmpty(deviceDesc) &&
                                 !string.Equals(deviceDesc, item.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            item.Description = deviceDesc;
                        }
                        else if (!string.IsNullOrEmpty(interfaceName) &&
                                 !string.Equals(interfaceName, item.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            item.Description = interfaceName;
                        }
                        else
                        {
                            item.Description = "音频输出";
                        }

                        item.FormFactor = GetIntProperty(device, PkeyAudioEndpointFormFactor, 0);
                        item.IsDefault = !string.IsNullOrEmpty(defaultId) &&
                                         string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase);
                        snapshot.Devices.Add(item);
                    }
                    catch
                    {
                        // A device can disappear halfway through enumeration. Keep the rest.
                    }
                    finally
                    {
                        Release(device);
                    }
                }

                snapshot.DefaultDeviceId = defaultId;
                for (int i = 0; i < snapshot.Devices.Count; i++)
                {
                    if (snapshot.Devices[i].IsDefault)
                    {
                        snapshot.DefaultDeviceName = snapshot.Devices[i].Name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(snapshot.DefaultDeviceName))
                {
                    snapshot.DefaultDeviceName = "未选择输出设备";
                }

                if (!string.IsNullOrEmpty(defaultId))
                {
                    IAudioEndpointVolume volume = GetVolumeInterface(defaultId);
                    if (volume != null)
                    {
                        float level;
                        bool muted;
                        if (volume.GetMasterVolumeLevelScalar(out level) >= 0)
                        {
                            snapshot.Volume = Clamp01(level);
                        }

                        if (volume.GetMute(out muted) >= 0)
                        {
                            snapshot.Muted = muted;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                snapshot.Error = ex.Message;
            }
            finally
            {
                Release(collection);
            }

            return snapshot;
        }

        public AudioSnapshot RefreshVolume(AudioSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.DefaultDeviceId))
            {
                return snapshot;
            }

            try
            {
                IAudioEndpointVolume volume = GetVolumeInterface(snapshot.DefaultDeviceId);
                if (volume == null)
                {
                    return snapshot;
                }

                float level;
                bool muted;
                if (volume.GetMasterVolumeLevelScalar(out level) >= 0)
                {
                    snapshot.Volume = Clamp01(level);
                }

                if (volume.GetMute(out muted) >= 0)
                {
                    snapshot.Muted = muted;
                }
            }
            catch
            {
                // The endpoint may have just been removed. The next full refresh will fix it.
            }

            return snapshot;
        }

        public void SetDefaultDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentException("deviceId");
            }

            object comObject = null;
            IPolicyConfig policy = null;
            try
            {
                Type type = Type.GetTypeFromCLSID(AudioGuids.PolicyConfigClient);
                comObject = Activator.CreateInstance(type);
                policy = (IPolicyConfig)comObject;

                int success = 0;
                int lastError = 0;
                for (int role = 0; role <= 2; role++)
                {
                    int hr = policy.SetDefaultEndpoint(deviceId, role);
                    if (hr >= 0)
                    {
                        success++;
                    }
                    else
                    {
                        lastError = hr;
                    }
                }

                if (success == 0)
                {
                    Marshal.ThrowExceptionForHR(lastError);
                }
            }
            finally
            {
                if (policy != null)
                {
                    ReleaseComObject(policy);
                }
                else if (comObject != null)
                {
                    ReleaseComObject(comObject);
                }
            }

            ReleaseVolume();
        }

        public void SetVolume(float level)
        {
            IAudioEndpointVolume volume = _volume;
            if (volume == null)
            {
                throw new InvalidOperationException("没有可用的默认输出设备。");
            }

            Guid context = Guid.Empty;
            int hr = volume.SetMasterVolumeLevelScalar(Clamp01(level), ref context);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public void SetMute(bool muted)
        {
            IAudioEndpointVolume volume = _volume;
            if (volume == null)
            {
                throw new InvalidOperationException("没有可用的默认输出设备。");
            }

            Guid context = Guid.Empty;
            int hr = volume.SetMute(muted, ref context);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        private IAudioEndpointVolume GetVolumeInterface(string deviceId)
        {
            if (_volume != null &&
                string.Equals(_volumeDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return _volume;
            }

            ReleaseVolume();
            IMMDevice device = null;
            try
            {
                int hr = _enumerator.GetDevice(deviceId, out device);
                if (hr < 0 || device == null)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                Guid iid = AudioGuids.AudioEndpointVolume;
                object obj;
                hr = device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out obj);
                if (hr < 0 || obj == null)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                _volume = (IAudioEndpointVolume)obj;
                _volumeDeviceId = deviceId;
                return _volume;
            }
            finally
            {
                Release(device);
            }
        }

        private static string GetStringProperty(IMMDevice device, Guid formatId, int pid)
        {
            PROPVARIANT value = new PROPVARIANT();
            IPropertyStore store = null;
            try
            {
                int hr = device.OpenPropertyStore(StgmRead, out store);
                if (hr < 0 || store == null)
                {
                    return null;
                }

                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = formatId;
                key.pid = pid;
                hr = store.GetValue(ref key, out value);
                if (hr < 0)
                {
                    return null;
                }

                if (value.vt == VtLpwstr && value.pointerValue != IntPtr.Zero)
                {
                    return Marshal.PtrToStringUni(value.pointerValue);
                }

                return null;
            }
            finally
            {
                if (value.vt != 0)
                {
                    Interop.PropVariantClear(ref value);
                }

                Release(store);
            }
        }

        private static string ExtractAdapterName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return null;
            }

            int start = deviceName.IndexOf('(');
            int end = deviceName.LastIndexOf(')');
            if (start >= 0 && end > start + 1)
            {
                return deviceName.Substring(start + 1, end - start - 1).Trim();
            }

            start = deviceName.IndexOf('（');
            end = deviceName.LastIndexOf('）');
            if (start >= 0 && end > start + 1)
            {
                return deviceName.Substring(start + 1, end - start - 1).Trim();
            }

            return null;
        }

        private static int GetIntProperty(IMMDevice device, Guid formatId, int pid)
        {            PROPVARIANT value = new PROPVARIANT();
            IPropertyStore store = null;
            try
            {
                int hr = device.OpenPropertyStore(StgmRead, out store);
                if (hr < 0 || store == null)
                {
                    return 0;
                }

                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = formatId;
                key.pid = pid;
                hr = store.GetValue(ref key, out value);
                if (hr < 0)
                {
                    return 0;
                }

                if (value.vt == VtUi4)
                {
                    return (int)value.uintValue;
                }

                if (value.vt == VtI4)
                {
                    return value.intValue;
                }

                return 0;
            }
            finally
            {
                if (value.vt != 0)
                {
                    Interop.PropVariantClear(ref value);
                }

                Release(store);
            }
        }

        private void ReleaseVolume()
        {
            if (_volume != null)
            {
                ReleaseComObject(_volume);
                _volume = null;
                _volumeDeviceId = null;
            }
        }

        private void OnNotification()
        {
            EventHandler handler = Changed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch
            {
                // Never let an exception escape into the COM notification thread.
            }
        }

        public static string ProbePolicyConfig()
        {
            object obj = null;
            try
            {
                Type type = Type.GetTypeFromCLSID(AudioGuids.PolicyConfigClient);
                obj = Activator.CreateInstance(type);
                IPolicyConfig policy = obj as IPolicyConfig;
                return policy == null ? "FAIL: interface not available" : "OK";
            }
            catch (Exception ex)
            {
                return "FAIL: " + ex.Message;
            }
            finally
            {
                if (obj != null)
                {
                    ReleaseComObject(obj);
                }
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private static void Release(object comObject)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                if (Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            catch
            {
            }
        }

        private static void ReleaseComObject(object comObject)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                if (Marshal.IsComObject(comObject))
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _enumerator.UnregisterEndpointNotificationCallback(_notification);
            }
            catch
            {
            }

            ReleaseVolume();
            ReleaseComObject(_enumerator);
        }

        private sealed class NotificationClient : IMMNotificationClient
        {
            private readonly AudioManager _owner;

            public NotificationClient(AudioManager owner)
            {
                _owner = owner;
            }

            public int OnDeviceStateChanged(string pwstrDeviceId, uint dwNewState)
            {
                _owner.OnNotification();
                return 0;
            }

            public int OnDeviceAdded(string pwstrDeviceId)
            {
                _owner.OnNotification();
                return 0;
            }

            public int OnDeviceRemoved(string pwstrDeviceId)
            {
                _owner.OnNotification();
                return 0;
            }

            public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string pwstrDefaultDeviceId)
            {
                if (flow == EDataFlow.eRender)
                {
                    _owner.OnNotification();
                }

                return 0;
            }

            public int OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key)
            {
                return 0;
            }
        }
    }
}
