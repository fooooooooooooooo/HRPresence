using System.Net;
using System.Text;
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
} else {
  File.WriteAllText("config.toml", Toml.FromModel(config));
}

if (config.EnableRpc) {
  discord = new DiscordRpcClient(config.RpcId);
  var result = discord.Initialize();

  discord.OnConnectionEstablished += (_, _) => { Console.WriteLine("> Discord RPC connected"); };
  discord.OnConnectionFailed += (_, failed) => { Console.WriteLine($"> Discord RPC failed {failed}"); };
  discord.OnReady += (_, _) => { Console.WriteLine("> Discord RPC ready"); };
  discord.OnClose += (_, _) => { Console.WriteLine("> Discord RPC closed"); };

  Console.WriteLine(!result ? "> Discord RPC failed" : $"> Discord RPC [on] ({config.RpcId})");
}

if (config.EnableOsc) {
  osc = new OscService(IPAddress.Loopback, config.OscPort);
  Console.WriteLine($"> OSC [on] ({IPAddress.Loopback}:{config.OscPort})");
}

var monitor = new HeartRateService();

monitor.HeartRateUpdated += hrReading => {
  reading = hrReading;

  Console.Write($"{DateTime.Now}  \n{reading.Value.BeatsPerMinute} BPM   ");
  Console.CursorLeft = 0;
  Console.CursorTop -= 1;

  lastUpdate = DateTime.Now;
  File.WriteAllText(config.RatePath, reading.Value.BeatsPerMinute.ToString());

  osc?.Update(reading.Value.BeatsPerMinute);
};

Console.WriteLine("> awaiting heart rate");
Console.CursorLeft = 0;

if (config.EnableLogging) {
  Console.WriteLine("> logging enabled");
  Console.CursorLeft = 0;
}

var lastLogWrite = DateTime.MinValue;

while (true) {
  if (DateTime.Now - lastUpdate > TimeSpan.FromMilliseconds(config.MonitorTimeout)) {
    Console.WriteLine("Heart rate monitor uninitialized. Starting...");

    while (true) {
      try {
        monitor.InitiateDefault();
        break;
      } catch (Exception e) {
        Console.WriteLine(
          $"Failure while initiating heart rate service, retrying in {config.InitFailureDelay}ms:");
        Console.WriteLine(e);
        Thread.Sleep(config.InitFailureDelay);
      }
    }
  }

  if (reading == null) {
    Thread.Sleep(100);
    Console.WriteLine("No reading");
  }

  if (config.EnableRpc) {
    StringTemplate details;
    StringTemplate state;

    if (reading is not null) {
      details = new StringTemplate(config.RpcDetailsTemplate);
      state = new StringTemplate(config.RpcStateTemplate)
        .Add("reading", reading.Value.BeatsPerMinute.ToString());
    } else {
      details = new StringTemplate(config.RpcNaDetailsTemplate);
      state = new StringTemplate(config.RpcNaStateTemplate);
    }

    discord?.SetPresence(new RichPresence {
      Details = details.ToString(),
      State = state.ToString(),
    });
  }

  if (config.EnableLogging && DateTime.Now - lastLogWrite > TimeSpan.FromMilliseconds(config.LogInterval)) {
    WriteLog(reading?.BeatsPerMinute ?? 0);

    lastLogWrite = DateTime.Now;
  }

  Thread.Sleep(config.RpcUpdateInterval);
}

void WriteLog(int bpm) {
  var template = new StringTemplate(config.LogTemplate)
    .Add("timestamp", DateTime.Now.ToString("s"))
    .Add("reading", bpm.ToString());

  File.AppendAllText(config.LogPath, $"{template}\n");
}