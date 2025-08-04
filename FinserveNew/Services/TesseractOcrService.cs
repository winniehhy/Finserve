using Tesseract;
using FinserveNew.Services;

namespace FinserveNew.Services
{
    public class TesseractOcrService : IOcrService
    {
        private readonly string _tessDataPath;
        private readonly ILogger<TesseractOcrService> _logger;

        public TesseractOcrService(ILogger<TesseractOcrService> logger)
        {
            _logger = logger;
            _tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");

            _logger.LogInformation($"Tesseract data path: {_tessDataPath}");
            _logger.LogInformation($"Directory exists: {Directory.Exists(_tessDataPath)}");

            // Fix this logic error:
            if (!Directory.Exists(_tessDataPath))
            {
                _logger.LogError($"Tessdata directory not found at: {_tessDataPath}");
            }
            else
            {
                // CHANGE THIS: Remove the error log and add success log
                _logger.LogInformation($"Tessdata directory found at: {_tessDataPath}");

                // Check if eng.traineddata exists
                var engDataFile = Path.Combine(_tessDataPath, "eng.traineddata");
                if (File.Exists(engDataFile))
                {
                    _logger.LogInformation("eng.traineddata file found - OCR service ready");
                }
                else
                {
                    _logger.LogError("eng.traineddata file not found in tessdata directory");
                }
            }
        }

        public async Task<OcrResult> ProcessImageAsync(byte[] imageData)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_tessDataPath))
                    {
                        throw new DirectoryNotFoundException($"Tessdata directory not found at: {_tessDataPath}");
                    }

                    using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                    using var img = Pix.LoadFromMemory(imageData);
                    using var page = engine.Process(img);

                    var text = page.GetText();
                    var confidence = page.GetMeanConfidence();

                    return new OcrResult
                    {
                        Text = text,
                        Confidence = confidence,
                        Success = true,
                        WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OCR processing failed for image data");
                    return new OcrResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }

        public async Task<OcrResult> ProcessImageAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_tessDataPath))
                    {
                        throw new DirectoryNotFoundException($"Tessdata directory not found at: {_tessDataPath}");
                    }

                    using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                    using var img = Pix.LoadFromFile(imagePath);
                    using var page = engine.Process(img);

                    var text = page.GetText();
                    var confidence = page.GetMeanConfidence();

                    return new OcrResult
                    {
                        Text = text,
                        Confidence = confidence,
                        Success = true,
                        WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OCR processing failed for image path: {ImagePath}", imagePath);
                    return new OcrResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }
    }
}