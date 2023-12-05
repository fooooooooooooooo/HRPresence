using Tomlyn.Model;

public class Config : ITomlMetadataProvider {
  public float TimeOutInterval { get; set; } = 4f;
  public float RestartDelay { get; set; } = 4f;
  public bool UseDiscordRPC { get; set; } = true;
  public string DiscordRPCId { get; set; } = "385821357151223818";
  public bool UseOSC { get; set; } = true;
  public int OSCPort { get; set; } = 9000;
  public TomlPropertiesMetadata PropertiesMetadata { get; set; }
}