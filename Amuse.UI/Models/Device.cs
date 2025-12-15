using Amuse.UI.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Amuse.UI.Models
{
    public class Device : INotifyPropertyChanged
    {
        public Device(DeviceType deviceType, ExecutionProvider provider ,string name, int deviceId, float memory, float memoryShared, string adapterId, string driverVersion)
        {
            DeviceType = deviceType;
            Provider = provider;
            Name = name;
            Memory = memory;
            MemoryShared = memoryShared;
            DeviceId = deviceId;
            AdapterId = adapterId;
            DriverVersion = driverVersion;
        }

        public DeviceType DeviceType { get;}
        public ExecutionProvider Provider { get; set; }

        public string Name { get; }
        public float Memory { get; }
        public float MemoryShared { get; }
        public int DeviceId { get; }
        public string AdapterId { get; }
        public bool IsDefault { get; set; }
        public int MemoryGB => (int)Math.Round((Memory / 1024f), 0);
        public int MemorySharedGB => (int)Math.Round((MemoryShared / 1024f), 0);
        public string DriverVersion { get;  }

        public int Usage{ get; set; }
        public float MemoryUsage { get; set; }
        public float MemoryRemaining => MemoryGB - MemoryUsage;
        public float ProcessMemoryUsage { get; set; }

        public float ProgressValue => MemoryUsage;
        public float ProgressSubValue => MemoryUsage - ProcessMemoryUsage;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }

    public enum DeviceType
    {
        CPU = 0,
        GPU = 1,
        NPU = 2,
        Other = 100
    }
}
