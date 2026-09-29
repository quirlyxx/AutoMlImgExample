using Animal_ConsoleApp1;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


var rekognition = new RekognitionService("AKIAUFJLN4BIWRQNVUU7", "e7OXvrzcMqsRW6/DLQNaxf48Y4OAMzL7nWr9iPj/");
var bot = new TelegramBotClient("8729906810:AAEenjAviZhaxgmftaXSCFzOebM1Q7VqX8E");
var me = await bot.GetMe();

const string BtnLabels = "🖼 Аналіз зображення";
const string BtnText = "📝 Розпізнавання тексту";
const string BtnModeration = "🛡 Модерація";
const string BtnFaces = "👥 Порівняння облич";

var menu = new ReplyKeyboardMarkup(new[]
{
    new KeyboardButton[] { BtnLabels, BtnText },
    new KeyboardButton[] { BtnModeration, BtnFaces }
})
{ ResizeKeyboard = true };

var prompts = new Dictionary<string, string>
{
    [BtnLabels] = "Режим: аналіз зображення.\nНадішліть фото",
    [BtnText] = "Режим: розпізнавання тексту.\nНадішліть фото з текстом",
    [BtnModeration] = "Режим: модерація.\nНадішліть фото для перевірки",
    [BtnFaces] = "Режим: порівняння облич.\nНадішліть ПЕРШЕ фото з обличчям"
};

var modes = new ConcurrentDictionary<long, string>();
var firstFaces = new ConcurrentDictionary<long, byte[]>();

bot.OnMessage += OnMessage;

Console.WriteLine($"Бот @{me.Username} запущено. Enter — зупинити.");
Console.ReadLine();

async Task OnMessage(Message msg, UpdateType type)
{
    var chatId = msg.Chat.Id;
    try
    {
        if (msg.Text != null)
        {
            if (msg.Text == "/start")
            {
                modes.TryRemove(chatId, out _);
                firstFaces.TryRemove(chatId, out _);
                await Send(chatId, "Привіт! Я бот на базі Amazon Rekognition.\nОберіть режим роботи");
            }
            else if (prompts.TryGetValue(msg.Text, out var prompt))
            {
                modes[chatId] = msg.Text;
                firstFaces.TryRemove(chatId, out _);
                await Send(chatId, prompt);
            }
            else
            {
                await Send(chatId, "Оберіть режим у меню");
            }
            return;
        }

        if (msg.Photo == null)
        {
            await Send(chatId, "Будь ласка, надішліть фото");
            return;
        }
        if (!modes.TryGetValue(chatId, out var mode))
        {
            await Send(chatId, "Спочатку оберіть режим у меню");
            return;
        }

        await Send(chatId, "Обробляю фото...");
        var bytes = await Download(msg.Photo[^1]);
        string result;

        if (mode == BtnLabels)
            result = await rekognition.AnalyzeImageAsync(bytes);
        else if (mode == BtnText)
            result = await rekognition.DetectTextAsync(bytes);
        else if (mode == BtnModeration)
            result = await rekognition.ModerateAsync(bytes);
        else
        {
            if (firstFaces.TryRemove(chatId, out var first))
                result = await rekognition.CompareFacesAsync(first, bytes);
            else
            {
                firstFaces[chatId] = bytes;
                result = "Перше фото отримано. Тепер надішліть ДРУГЕ фото.";
            }
        }

        await Send(chatId, result);
    }
    catch (Amazon.Rekognition.Model.InvalidParameterException)
    {
        firstFaces.TryRemove(chatId, out _);
        await Send(chatId, "Не вдалося обробити фото (можливо, на ньому немає обличчя). Спробуйте інше.");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        await Send(chatId, "Сталася помилка. Спробуйте ще раз.");
    }
}

async Task<byte[]> Download(PhotoSize photo)
{
    var file = await bot.GetFile(photo.FileId);
    using var ms = new MemoryStream();
    await bot.DownloadFile(file.FilePath!, ms);
    return ms.ToArray();
}

async Task Send(long chatId, string text) =>
    await bot.SendMessage(chatId, text, replyMarkup: menu);