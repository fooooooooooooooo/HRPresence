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

if (config.UseDiscordRpc) {
  discord = new DiscordRpcClient(config.DiscordRpcId);
  var result = discord.Initialize();

  discord.OnConnectionEstablished += (_, _) => { Console.WriteLine("> Discord RPC connected"); };
  discord.OnConnectionFailed += (_, failed) => { Console.WriteLine($"> Discord RPC failed {failed}"); };
  discord.OnReady += (_, _) => { Console.WriteLine("> Discord RPC ready"); };
  discord.OnClose += (_, _) => { Console.WriteLine("> Discord RPC closed"); };

  Console.WriteLine(!result ? "> Discord RPC failed" : $"> Discord RPC [on] ({config.DiscordRpcId})");
}

if (config.UseOsc) {
  osc = new OscService(IPAddress.Loopback, config.OscPort);
  Console.WriteLine($"> OSC [on] ({IPAddress.Loopback}:{config.OscPort})");
}

var heartRate = new HeartRateService();

heartRate.HeartRateUpdated += hrReading => {
  reading = hrReading;

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
    Console.WriteLine("Heart rate monitor uninitialized. Starting...");

    while (true) {
      try {
        heartRate.InitiateDefault();
        break;
      } catch (Exception e) {
        Console.WriteLine(
          $"Failure while initiating heart rate service, retrying in {config.RestartDelay} seconds:");
        Console.WriteLine(e);
        Thread.Sleep((int)(config.RestartDelay * 1000));
      }
    }
  }

  if (reading == null) {
    Thread.Sleep(100);
    Console.WriteLine("No reading");
  }

  discord?.SetPresence(new RichPresence {
    Details = config.DiscordRpcDetails,
    State = $"{reading?.BeatsPerMinute}",
  });

  Thread.Sleep(config.UpdateDelay);
}