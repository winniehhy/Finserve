using FinserveNew.Models;

namespace FinserveNew.Services
{
    public interface IOcrService
    {
        Task<OcrResult> ProcessImageAsync(byte[] imageData);
        Task<OcrResult> ProcessImageAsync(string imagePath);
    }

    public class OcrResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = "";
        public float Confidence { get; set; }
        public int WordCount { get; set; }
        public string? ErrorMessage { get; set; }

        // New properties for price extraction
        public List<ExtractedPrice> ExtractedPrices { get; set; } = new List<ExtractedPrice>();
        public decimal CalculatedAmount { get; set; }
        public string Currency { get; set; } = "MYR";
    }
}