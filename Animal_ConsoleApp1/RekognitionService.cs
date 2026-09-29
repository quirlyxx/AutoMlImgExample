using System.Text;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;

////
namespace Animal_ConsoleApp1
{
    internal class RekognitionService
    {
        private readonly AmazonRekognitionClient _client;

        public RekognitionService(string awsAccessKey, string awsSecretKey)
        {
            _client = new AmazonRekognitionClient(awsAccessKey, awsSecretKey, Amazon.RegionEndpoint.EUCentral1);
        }
        private static Image Img(byte[] bytes) => new Image { Bytes = new MemoryStream(bytes) };
        public async Task<string> AnalyzeImageAsync(byte[] bytes)
        {
            var r = await _client.DetectLabelsAsync(new DetectLabelsRequest
            {
                Image = Img(bytes),
                MaxLabels = 10,
                MinConfidence = 75F,
                Features = new List<string> { "GENERAL_LABELS", "IMAGE_PROPERTIES" }
            });

            var sb = new StringBuilder("Знайдені об'єкти:\n");
            if (r.Labels.Count == 0) sb.AppendLine("Нічого не знайдено");
            foreach (var l in r.Labels)
                sb.AppendLine($"• {l.Name} — {l.Confidence:F1}%");

            sb.AppendLine("\nХарактеристики фото:");
            var q = r.ImageProperties?.Quality;
            if (q != null)
            {
                sb.AppendLine($"• Яскравість: {q.Brightness:F0}/100");
                sb.AppendLine($"• Різкість: {q.Sharpness:F0}/100");
            }
            if (r.ImageProperties?.DominantColors?.Count > 0)
            {
                var colors = r.ImageProperties.DominantColors.Take(3).Select(c => c.SimplifiedColor);
                sb.AppendLine("• Основні кольори: " + string.Join(", ", colors));
            }
            return sb.ToString();
        }

        public async Task<string> DetectTextAsync(byte[] bytes)
        {
            var r = await _client.DetectTextAsync(new DetectTextRequest { Image = Img(bytes) });

            var lines = r.TextDetections.Where(t => t.Type == TextTypes.LINE).ToList();
            if (lines.Count == 0)
                return "Текст не знайдено.";

            var sb = new StringBuilder("Знайдений текст:\n");
            foreach (var t in lines)
                sb.AppendLine($"• {t.DetectedText} ({t.Confidence:F0}%)");
            return sb.ToString();
        }

        public async Task<string> ModerateAsync(byte[] bytes)
        {
            var r = await _client.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
            {
                Image = Img(bytes),
                MinConfidence = 50F
            });

            if (r.ModerationLabels.Count == 0)
                return "Неприйнятного контенту не виявлено.";

            var sb = new StringBuilder("Виявлено неприйнятний контент:\n");
            foreach (var m in r.ModerationLabels)
                sb.AppendLine($"• {m.Name} — {m.Confidence:F1}%");
            return sb.ToString();
        }

        public async Task<string> CompareFacesAsync(byte[] first, byte[] second)
        {
            var r = await _client.CompareFacesAsync(new CompareFacesRequest
            {
                SourceImage = Img(first),
                TargetImage = Img(second),
                SimilarityThreshold = 0F
            });

            var similarity = r.FaceMatches.Count > 0 ? r.FaceMatches.Max(m => m.Similarity) : 0f;
            var verdict = similarity >= 90 ? "Схоже, це одна й та сама людина"
                        : similarity >= 70 ? "Є певна схожість"
                        : "Ймовірно, це різні люди";

            return $"Схожість облич: {similarity:F1}%\n{verdict}";
        }
    }
}
