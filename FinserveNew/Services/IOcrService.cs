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
        public string Text { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int WordCount { get; set; }
    }
}