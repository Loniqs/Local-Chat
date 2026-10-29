using System.Net.Sockets;
using System.Text;

internal class Program
{
  private static readonly object ConsoleLock = new();
  static async Task Main()
  {
    Console.Write("Введите IP собеседника: ");
    string? targetIp = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(targetIp))
    {
      targetIp = "127.0.0.1";
    }

    UdpClient receiveClient;
    int receivePort;
    int sendPort;

    while (true)
    {
      Console.Write("Введите порт получения: ");
      receivePort = int.Parse(Console.ReadLine());

      try
      {
        receiveClient = new UdpClient(receivePort);
        break;
      }
      catch (SocketException ex)
      {
        Console.WriteLine($"Порт {receivePort} уже занят другой программой");
      }
    }

    while (true)
    {
      Console.Write("Введите порт отправки: ");
      sendPort = int.Parse(Console.ReadLine());

      if (sendPort == receivePort)
      {
        Console.WriteLine("Порт отправки не может совпадать с портом получения");
        continue;
      }

      break;
    }

    Console.Clear();

    using CancellationTokenSource cts = new();

    UdpClient sendClient = new();

    Task? receiveTask = ReceiveLoop(receiveClient, cts.Token);

    while (true)
    {
      string? command = Console.ReadLine();

      if (command == "exit") break;
      if (string.IsNullOrEmpty(command)) continue;

      byte[] bytes = Encoding.UTF8.GetBytes(command);
      try
      {
        await sendClient.SendAsync(bytes, bytes.Length, targetIp, sendPort);
        lock (ConsoleLock)
        {
          Console.SetCursorPosition(0, Console.CursorTop - 1);

          ClearCurrentConsoleLine();

          string time = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
          Console.WriteLine($"[{time}] u: {command}");
        }
      }
      catch (OperationCanceledException) { break; }
      catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); }
    }

    cts.Cancel();
    try
    {
      await receiveTask;
    }
    catch (OperationCanceledException) { }
  }

  static async Task ReceiveLoop(UdpClient client, CancellationToken ct)
  {
    try
    {
      while (!ct.IsCancellationRequested)
      {
        UdpReceiveResult receivedData = await client.ReceiveAsync(ct);
        string message = Encoding.UTF8.GetString(receivedData.Buffer);

        lock (ConsoleLock)
        {
          string time = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
          Console.WriteLine($"[{time}] username: {message}");
        }
      }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
      Console.WriteLine($"\nОшибка: {ex.Message}");
    }
  }

  private static void ClearCurrentConsoleLine()
  {
    int currentLineCursor = Console.CursorTop;
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', Console.WindowWidth - 1));
    Console.SetCursorPosition(0, currentLineCursor);
  }
}
