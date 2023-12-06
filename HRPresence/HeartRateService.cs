using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace HRPresence;

public enum ContactSensorStatus {
  NotSupported,
  NotSupported2,
  NoContact,
  Contact
}

[Flags]
public enum HeartRateFlags {
  None = 0,
  IsShort = 1,
  HasEnergyExpended = 1 << 3,
  HasRrInterval = 1 << 4,
}

public struct HeartRateReading {
  public HeartRateFlags Flags { get; init; }
  public ContactSensorStatus Status { get; init; }
  public int BeatsPerMinute { get; init; }
  public int? EnergyExpended { get; set; }
  public int[] RrIntervals { get; set; }
}

internal interface IHeartRateService : IDisposable {
  bool _disposed { get; }

  event HeartRateService.HeartRateUpdateEventHandler HeartRateUpdated;
  void InitiateDefault();
  void Cleanup();
}

internal static class MemoryStreamExtensions {
  public static ushort ReadUInt16(this MemoryStream s)
    => (ushort)(s.ReadByte() | (s.ReadByte() << 8));
}

internal class HeartRateService : IHeartRateService {
  // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml
  private const int HeartRateMeasurementCharacteristicId = 0x2A37;

  private static readonly Guid HeartRateMeasurementCharacteristicUuid =
    BluetoothUuidHelper.FromShortId(HeartRateMeasurementCharacteristicId);

  private GattDeviceService? _service;
  private byte[]? _buffer;
  private readonly object _disposeSync = new();

  public bool _disposed { get; private set; }
  public event HeartRateUpdateEventHandler? HeartRateUpdated;

  public delegate void HeartRateUpdateEventHandler(HeartRateReading reading);

  public void InitiateDefault() {
    var heartRateSelector = GattDeviceService
      .GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate);

    var devices = DeviceInformation
      .FindAllAsync(heartRateSelector)
      .GetAwaiter()
      .GetResult();

    var device = devices[0];

    if (device == null) {
      throw new ArgumentNullException(
        nameof(device),
        "Unable to locate heart rate device.");
    }

    GattDeviceService service;

    lock (_disposeSync) {
      ObjectDisposedException.ThrowIf(_disposed, GetType().Name);

      Cleanup();

      service = GattDeviceService.FromIdAsync(device.Id)
        .GetAwaiter()
        .GetResult();

      _service = service;
    }

    if (service == null) {
      throw new ArgumentOutOfRangeException(
        $"Unable to get service to {device.Name} ({device.Id}). Is the device inuse by another program? The Bluetooth adaptor may need to be turned off and on again.");
    }

    var heartRate = service
      .GetCharacteristicsForUuidAsync(HeartRateMeasurementCharacteristicUuid)
      .GetAwaiter()
      .GetResult()
      .Characteristics[0];

    if (heartRate == null) {
      throw new ArgumentOutOfRangeException(
        $"Unable to locate heart rate measurement on device {device.Name} ({device.Id}).");
    }

    var status = heartRate
      .WriteClientCharacteristicConfigurationDescriptorAsync(
        GattClientCharacteristicConfigurationDescriptorValue.Notify)
      .GetAwaiter()
      .GetResult();

    heartRate.ValueChanged += HeartRate_ValueChanged;

    Console.WriteLine($"Started {status}");
  }

  private void HeartRate_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
    var buffer = args.CharacteristicValue;
    if (buffer.Length == 0)
      return;

    var byteBuffer = Interlocked.Exchange(ref _buffer, null)
                     ?? new byte[buffer.Length];

    if (byteBuffer.Length != buffer.Length) {
      byteBuffer = new byte[buffer.Length];
    }

    try {
      using var reader = DataReader.FromBuffer(buffer);
      reader.ReadBytes(byteBuffer);

      var readingValue = ReadBuffer(byteBuffer, (int)buffer.Length);

      if (readingValue == null) {
        Console.WriteLine($"Buffer was too small. Got {buffer.Length}.");
        return;
      }

      var reading = readingValue.Value;
      Debug.WriteLine($"Read {reading.Flags:X} {reading.Status} {reading.BeatsPerMinute}");

      HeartRateUpdated?.Invoke(reading);
    }
    finally {
      Volatile.Write(ref _buffer, byteBuffer);
    }
  }

  private static HeartRateReading? ReadBuffer(byte[] buffer, int length) {
    if (length == 0)
      return null;

    var ms = new MemoryStream(buffer, 0, length);
    var flags = (HeartRateFlags)ms.ReadByte();
    var isShort = flags.HasFlag(HeartRateFlags.IsShort);
    var contactSensor = (ContactSensorStatus)(((int)flags >> 1) & 3);
    var hasEnergyExpended = flags.HasFlag(HeartRateFlags.HasEnergyExpended);
    var hasRrInterval = flags.HasFlag(HeartRateFlags.HasRrInterval);
    var minLength = isShort ? 3 : 2;

    if (buffer.Length < minLength)
      return null;

    var reading = new HeartRateReading {
      Flags = flags,
      Status = contactSensor,
      BeatsPerMinute = isShort ? ms.ReadUInt16() : ms.ReadByte()
    };

    if (hasEnergyExpended)
      reading.EnergyExpended = ms.ReadUInt16();

    if (!hasRrInterval) return reading;

    var rrValueCount = (buffer.Length - ms.Position) / sizeof(ushort);
    var rrValues = new int[rrValueCount];
    for (var i = 0; i < rrValueCount; ++i) {
      rrValues[i] = ms.ReadUInt16();
    }

    reading.RrIntervals = rrValues;

    return reading;
  }

  public void Cleanup() {
    var service = Interlocked.Exchange(ref _service, null);
    service?.Dispose();
  }

  public void Dispose() {
    lock (_disposeSync) {
      _disposed = true;
      Cleanup();
    }
  }
}