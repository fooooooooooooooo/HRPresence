using System.Net;
using System.Net.Sockets;
using OscCore;

namespace HRPresence;

public class OscService {
  private readonly UdpClient _udp;

  public OscService(IPAddress ip, int port) {
    _udp = new UdpClient();
    _udp.Connect(ip, port);
  }

  public void Update(int heartRate) {
    // Maps the heart rate from [0;255] to [-1;+1]
    var floatHr = heartRate * 0.0078125f - 1.0f;
    var data = new (string, object)[] {
      ("HR", heartRate),
      ("onesHR", heartRate % 10),
      ("tensHR", heartRate / 10 % 10),
      ("hundredsHR", heartRate / 100 % 10),
      ("floatHR", floatHr)
    };

    try {
      foreach (var (path, value) in data) {
        var bytes = new OscMessage($"/avatar/parameters/{path}", value).ToByteArray();
        _udp.Send(bytes, bytes.Length);
      }
    } catch (Exception e) {
      Console.WriteLine("Failed to send OSC message:");
      Console.WriteLine(e);
    }
  }
}