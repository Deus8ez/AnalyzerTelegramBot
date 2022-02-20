//using Telegram.Bot;

//var botClient = new TelegramBotClient("5260470589:AAEFGpWHnKfLEsk0-xRlcUvGaw5Ee3FzZH8");

//var me = await botClient.GetMeAsync();
//Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

string botToken = "5260470589:AAEFGpWHnKfLEsk0-xRlcUvGaw5Ee3FzZH8";
string subscriptionKey = "19ddfc6f83b34a11bf5e0ce6984e072e";
string azureEndpoint = "https://computervisiondarkholme.cognitiveservices.azure.com/";
string imageEndpoint = $"https://api.telegram.org/bot{botToken}/getFile?file_id=";

var botClient = new TelegramBotClient(botToken);
ComputerVisionClient client = Authenticate(azureEndpoint, subscriptionKey);

using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};
botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Type != UpdateType.Message)
        return;
    // Only process text messages
    //if (update.Message!.Type != MessageType.Text)
    //    return;

    var type = update.Message!.Type;

    if (type != MessageType.Photo)
    {
        Console.WriteLine("Please send image!");
        return;
    }

    var chatId = update.Message.Chat.Id;
    //var messageText = update.Message.Text;
    var fileId = update.Message.Photo[3].FileId;
    var obj = await GetURL(fileId);
    var filePath = $"https://api.telegram.org/file/bot5260470589:AAEFGpWHnKfLEsk0-xRlcUvGaw5Ee3FzZH8/{obj.result.file_path}";
    Stopwatch timer = new Stopwatch();
    timer.Start();
    var result = await ReadFileUrl(client, filePath);
    timer.Stop();
    var time = timer.Elapsed;
    var timeSeconds = timer.Elapsed.TotalSeconds;
    var timeMilliseconds = timer.Elapsed.TotalMilliseconds;

    var charCount = result.ToString().Length;
    //Console.WriteLine($"Received a '{messageText}' message in chat {chatId}");
    if (fileId != null)
    {
        Console.WriteLine($"file { fileId }");
        Console.WriteLine($"count { charCount }");
        Console.WriteLine($"elapsed { time }");
        Console.WriteLine($"elapsed seconds { timeSeconds }");
        Console.WriteLine($"elapsed milliseconds { timeMilliseconds }");

    }

    // Echo received message text
    Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: result.ToString(),
        cancellationToken: cancellationToken);
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}


ComputerVisionClient Authenticate(string endpoint, string key)
{
    ComputerVisionClient client =
      new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
      { Endpoint = endpoint };
    return client;
}

async Task<Root> GetURL(string fileId)
{

    HttpClient httpClient = new HttpClient();
    HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, imageEndpoint + fileId);
    var response = await httpClient.SendAsync(httpRequestMessage);
    response.EnsureSuccessStatusCode();
    var obj = JsonSerializer.Deserialize<Root>(await response.Content.ReadAsStringAsync());
    return obj;
}


async Task<StringBuilder> ReadFileUrl(ComputerVisionClient client, string urlFile)
{
    Console.WriteLine("----------------------------------------------------------");
    Console.WriteLine("READ FILE FROM URL");
    Console.WriteLine();

    // Read text from URL
    var textHeaders = await client.ReadAsync(urlFile);
    // After the request, get the operation location (operation ID)
    string operationLocation = textHeaders.OperationLocation;
    Thread.Sleep(2000);
    // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
    // We only need the ID and not the full URL
    const int numberOfCharsInOperationId = 36;
    string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

    // Extract the text
    ReadOperationResult results;
    Console.WriteLine($"Extracting text from URL file {Path.GetFileName(urlFile)}...");
    Console.WriteLine();
    do
    {
        results = await client.GetReadResultAsync(Guid.Parse(operationId));
    }
    while ((results.Status == OperationStatusCodes.Running ||
        results.Status == OperationStatusCodes.NotStarted));
    // Display the found text.
    Console.WriteLine();
    var textUrlFileResults = results.AnalyzeResult.ReadResults;

    StringBuilder textResult = new StringBuilder();
    foreach (ReadResult page in textUrlFileResults)
    {
        foreach (Line line in page.Lines)
        {
            Console.WriteLine(line.Text);
            textResult.Append(line.Text);
            textResult.Append(' ');
        }
    }
    Console.WriteLine();
    return textResult;
}

public class Result
{
    public string file_id { get; set; }
    public string file_unique_id { get; set; }
    public int file_size { get; set; }
    public string file_path { get; set; }
}

public class Root
{
    public bool ok { get; set; }
    public Result result { get; set; }
}