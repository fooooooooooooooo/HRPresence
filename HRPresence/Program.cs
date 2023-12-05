using System.Diagnostics;
using System.Net;
using DiscordRPC;
using HRPresence;
using Tomlyn;

HeartRateReading? reading = null;

DiscordRpcClient? discord = null;
OscService? osc = null;

var lastUpdate = DateTime.MinValue;

var config = new Config();
if (File.Exists("config.toml")) {
  config = Toml.ToModel<Config>(File.OpenText("config.toml").ReadToEnd());
}
else {
  File.WriteAllText("config.toml", Toml.FromModel(config));
}

if (config.UseDiscordRPC) {
  discord = new DiscordRpcClient(config.DiscordRPCId);
  var result = discord.Initialize();

  discord.OnConnectionEstablished += (_, _) => { Console.WriteLine("> Discord RPC connected"); };
  discord.OnConnectionFailed += (_, failed) => { Console.WriteLine($"> Discord RPC failed {failed}"); };
  discord.OnReady += (_, _) => { Console.WriteLine("> Discord RPC ready"); };
  discord.OnClose += (_, _) => { Console.WriteLine("> Discord RPC closed"); };

  Console.WriteLine(!result ? "> Discord RPC failed" : $"> Discord RPC [on] ({config.DiscordRPCId})");
}

if (config.UseOSC) {
  osc = new OscService();
  osc.Initialize(IPAddress.Loopback, config.OSCPort);
  Console.WriteLine($"> OSC [on] ({IPAddress.Loopback}:{config.OSCPort})");
}

var heartRate = new HeartRateService();

heartRate.HeartRateUpdated += heart => {
  reading = heart;

  Console.Write($"{DateTime.Now}  \n{reading.Value.BeatsPerMinute} BPM   ");
  Console.CursorLeft = 0;
  Console.CursorTop -= 1;

  lastUpdate = DateTime.Now;
  File.WriteAllText("rate.txt", $"{reading.Value.BeatsPerMinute}");

  osc?.Update(reading.Value.BeatsPerMinute);
};

Console.WriteLine("> awaiting heart rate");
Console.CursorLeft = 0;

while (true) {
  if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(config.TimeOutInterval)) {
    Debug.WriteLine("Heart rate monitor uninitialized. Starting...");
    while (true) {
      try {
        heartRate.InitiateDefault();
        break;
      }
      catch (Exception e) {
        Debug.WriteLine(
          $"Failure while initiating heart rate service, retrying in {config.RestartDelay} seconds:");
        Debug.WriteLine(e);
        Thread.Sleep((int)(config.RestartDelay * 1000));
      }
    }
  }

  discord?.SetPresence(new RichPresence {
    Details = "0",
    State = $"{reading?.BeatsPerMinute}",
  });

  Thread.Sleep(2000);
}