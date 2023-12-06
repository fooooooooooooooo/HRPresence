using Tomlyn.Model;

namespace HRPresence; 

public class Config : ITomlMetadataProvider {
  public float TimeOutInterval { get; set; } = 4f;
  public float RestartDelay { get; set; } = 4f;
  public bool UseDiscordRpc { get; set; } = true;
  public string DiscordRpcId { get; set; } = "385821357151223818";
  public string DiscordRpcDetails { get; set; } = "aaaa";

  public bool UseOsc { get; set; } = true;
  public int OscPort { get; set; } = 9000;
  public int UpdateDelay { get; set; } = 5000;
  public TomlPropertiesMetadata PropertiesMetadata { get; set; }
}