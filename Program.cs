using System.Net;
using System.Net.Sockets;
using System.Text;

internal class Program
{
  private static readonly object ConsoleLock = new();
  private static IPEndPoint? serverEP = null;
  private static string userName = "username";
  private const int ServerPort = 8010;

  static async Task Main()
  {
    Console.Write("Введите свой ник: ");
    userName = Console.ReadLine() ?? "username";

    using UdpClient client = new();
    client.EnableBroadcast = true;

    Console.WriteLine("Поиск сервера в локальной сети..");

    byte[] discoverMsg = Encoding.UTF8.GetBytes("DISCOVER");
    await client.SendAsync(discoverMsg, discoverMsg.Length, new IPEndPoint(IPAddress.Broadcast, ServerPort));

    Task<UdpReceiveResult>? receiveTask = client.ReceiveAsync();
    Task? timeoutTask = Task.Delay(1500);
    Task? completedTask = await Task.WhenAny(receiveTask, timeoutTask);

    if (completedTask == receiveTask)
    {
      UdpReceiveResult result = await receiveTask;
      serverEP = result.RemoteEndPoint;
      Console.WriteLine($"Сервер найден: {serverEP}");
    }
    else
    {
      serverEP = new IPEndPoint(IPAddress.Loopback, ServerPort);
      Console.WriteLine("Широковещательный поиск не удался");
      Console.WriteLine($"Пробуем подключиться к локальному серверу: {serverEP}");
    }

    using CancellationTokenSource cts = new();
    Task receiveLoopTask = ReceiveLoop(client, cts.Token);

    Console.Clear();
    Console.WriteLine($"=== С подключением {userName} ===");

    string joinMsg = $"JOIN:{userName}";
    byte[] joinBytes = Encoding.UTF8.GetBytes(joinMsg);
    await client.SendAsync(joinBytes, joinBytes.Length, serverEP);

    while (true)
    {
      Console.Write("> ");
      string? message = Console.ReadLine();

      if (message == "exit") break;
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

        if (message == "SERVER_HERE" || message == "DISCOVER" || message.StartsWith("JOIN:")) continue;

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
      Console.Write(new string(' ', Console.WindowWidth - 1));
      Console.SetCursorPosition(0, targetLine);
      Console.WriteLine($"           {message}");
    }
  }

  private static void PrintReceivedMessage(string message)
  {
    lock (ConsoleLock)
    {
      Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
      Console.WriteLine(message);
      Console.Write("> ");
    }
  }
}
