using System.Net;
using System.Net.Sockets;
using System.Text;

internal class Program
{
  private static readonly object ConsoleLock = new();
  private static IPEndPoint? serverEP = null;
  private static string userName = "username";

  static async Task Main()
  {
    Console.Write("Введите свой ник: ");
    userName = Console.ReadLine() ?? "username";

    while (true)
    {
      Console.Write("Введите IP-адрес сервера: ");
      string? ipInput = Console.ReadLine();

      if (string.IsNullOrWhiteSpace(ipInput))
      {
        ipInput = "127.0.0.1";
      }

      if (IPAddress.TryParse(ipInput, out IPAddress? ipAddress))
      {
        serverEP = new IPEndPoint(ipAddress, 8010);
        break;
      }
      else
      {
        Console.WriteLine("Ошибка: неверный формат IP-адреса");
      }
    }

    using UdpClient client = new(0);

    using CancellationTokenSource cts = new();

    Console.Clear();

    Task receiveLoopTask = ReceiveLoop(client, cts.Token);

    string joinMsg = $"JOIN:{userName}";
    byte[] joinBytes = Encoding.UTF8.GetBytes(joinMsg);
    await client.SendAsync(joinBytes, joinBytes.Length, serverEP);

    while (true)
    {
      Console.Write("> ");
      string? message = Console.ReadLine();

      if (message == "exit")
      {
        string leaveMsg = $"LEAVE:{userName}";
        byte[] leaveBytes = Encoding.UTF8.GetBytes(leaveMsg);
        await client.SendAsync(leaveBytes, leaveBytes.Length, serverEP);
        break;
      }
      if (string.IsNullOrEmpty(message)) continue;

      string formattedMessage = $"{userName}: {message}";
      byte[] bytes = Encoding.UTF8.GetBytes(formattedMessage);

      try
      {
        await client.SendAsync(bytes, bytes.Length, serverEP);
        PrintSentMessage($"U: {message}");
      }
      catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); }
    }

    cts.Cancel();
    try { await receiveLoopTask; } catch (OperationCanceledException) { }
  }

  static async Task ReceiveLoop(UdpClient client, CancellationToken ct)
  {
    try
    {
      while (!ct.IsCancellationRequested)
      {
        UdpReceiveResult receivedData = await client.ReceiveAsync(ct);
        string message = Encoding.UTF8.GetString(receivedData.Buffer);

        if (message.StartsWith("JOIN:") || message.StartsWith("LEAVE:")) continue;

        PrintReceivedMessage(message);
      }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Console.WriteLine($"Ошибка приема: {ex.Message}"); }
  }

  private static void ClearCurrentConsoleLine()
  {
    int currentLineCursor = Console.CursorTop;
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', Console.WindowWidth - 1));
    Console.SetCursorPosition(0, currentLineCursor);
  }

  private static void PrintSentMessage(string message)
  {
    lock (ConsoleLock)
    {
      int targetLine = Math.Max(0, Console.CursorTop - 1);
      Console.SetCursorPosition(0, targetLine);
      int width = Math.Max(1, Console.WindowWidth - 1);
      Console.Write(new string(' ', width));
      Console.SetCursorPosition(0, targetLine);
      Console.WriteLine($"           {message}");
    }
  }

  private static void PrintReceivedMessage(string message)
  {
    lock (ConsoleLock)
    {
      int width = Math.Max(1, Console.WindowWidth - 1);
      Console.Write("\r" + new string(' ', width) + "\r");
      Console.WriteLine(message);
      Console.Write("> ");
    }
  }
}
