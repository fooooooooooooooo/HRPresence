using Tomlyn.Model;

namespace HRPresence;

public class Config : ITomlMetadataProvider {
  public int MonitorTimeout { get; set; } = 4000;
  public int InitFailureDelay { get; set; } = 4000;
  public string RatePath { get; set; } = "rate.txt";

  // Rpc
  public bool EnableRpc { get; set; } = true;
  public string RpcId { get; set; } = "385821357151223818";
  public string RpcDetailsTemplate { get; set; } = "aaaa";
  public string RpcNaDetailsTemplate { get; set; } = "aaaa";
  public string RpcStateTemplate { get; set; } = "{reading}";
  public string RpcNaStateTemplate { get; set; } = "N/A";
  public int RpcUpdateInterval { get; set; } = 5000;

  // Osc
  public bool EnableOsc { get; set; } = true;
  public int OscPort { get; set; } = 9000;

  // Logging
  public bool EnableLogging { get; set; } = false;
  public int LogInterval { get; set; } = 10000;
  public string LogTemplate { get; set; } = "{timestamp} {reading}";
  public string LogPath { get; set; } = "log.txt";
  public TomlPropertiesMetadata PropertiesMetadata { get; set; } = null!;
}