using Tesseract;
using FinserveNew.Services;
using System.Text.RegularExpressions;

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

            if (!Directory.Exists(_tessDataPath))
            {
                _logger.LogError($"Tessdata directory not found at: {_tessDataPath}");
            }
            else
            {
                _logger.LogInformation($"Tessdata directory found at: {_tessDataPath}");

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

                    // Extract prices from the OCR text
                    var extractedPrices = ExtractPricesFromText(text);

                    return new OcrResult
                    {
                        Text = text,
                        Confidence = confidence,
                        Success = true,
                        WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                        ExtractedPrices = extractedPrices,
                        CalculatedAmount = CalculateTotalAmount(extractedPrices),
                        Currency = DetectCurrency(text)
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

                    // Extract prices from the OCR text
                    var extractedPrices = ExtractPricesFromText(text);

                    return new OcrResult
                    {
                        Text = text,
                        Confidence = confidence,
                        Success = true,
                        WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                        ExtractedPrices = extractedPrices,
                        CalculatedAmount = CalculateTotalAmount(extractedPrices),
                        Currency = DetectCurrency(text)
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

        //// NEW: Method to process multiple images and calculate total
        //public async Task<MultipleOcrResult> ProcessMultipleImagesAsync(List<byte[]> imageDataList)
        //{
        //    var results = new List<OcrResult>();
        //    var allExtractedPrices = new List<ExtractedPrice>();

        //    _logger.LogInformation($"Processing {imageDataList.Count} images for total calculation");

        //    foreach (var imageData in imageDataList)
        //    {
        //        var result = await ProcessImageAsync(imageData);
        //        results.Add(result);

        //        if (result.Success && result.ExtractedPrices != null)
        //        {
        //            allExtractedPrices.AddRange(result.ExtractedPrices);
        //        }
        //    }

        //    var totalAmount = CalculateMultiDocumentTotal(results);
        //    var combinedCurrency = DetermineCombinedCurrency(results);

        //    return new MultipleOcrResult
        //    {
        //        IndividualResults = results,
        //        TotalAmount = totalAmount,
        //        Currency = combinedCurrency,
        //        ProcessedDocuments = results.Count(r => r.Success),
        //        TotalDocuments = imageDataList.Count,
        //        Success = results.Any(r => r.Success)
        //    };
        //}

        //// NEW: Calculate total from multiple documents
        //private decimal CalculateMultiDocumentTotal(List<OcrResult> results)
        //{
        //    decimal total = 0;
        //    const decimal USD_TO_MYR_RATE = 4.7m; // You can make this configurable

        //    foreach (var result in results.Where(r => r.Success))
        //    {
        //        var amount = result.CalculatedAmount;

        //        // Convert USD to MYR if needed
        //        if (result.Currency == "USD")
        //        {
        //            amount *= USD_TO_MYR_RATE;
        //            _logger.LogInformation($"Converted USD {result.CalculatedAmount} to MYR {amount}");
        //        }

        //        total += amount;
        //        _logger.LogInformation($"Added {result.Currency} {result.CalculatedAmount} (MYR {amount}) to total");
        //    }

        //    _logger.LogInformation($"Multi-document total calculated: MYR {total}");
        //    return total;
        //}

        //// NEW: Determine combined currency for multiple documents
        //private string DetermineCombinedCurrency(List<OcrResult> results)
        //{
        //    var currencies = results.Where(r => r.Success && !string.IsNullOrEmpty(r.Currency))
        //                          .Select(r => r.Currency)
        //                          .ToList();

        //    // If all documents are in MYR or mixed, return MYR as the final currency
        //    // since we convert everything to MYR for the total
        //    return "MYR";
        //}

        private List<ExtractedPrice> ExtractPricesFromText(string text)
        {
            var extractedPrices = new List<ExtractedPrice>();

            try
            {
                // Common price patterns with various formats
                var pricePatterns = new[]
                {
                    // USD patterns
                    @"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",           // $123.45, $1,234.56
                    @"USD\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",          // USD 123.45
                    @"(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*USD",          // 123.45 USD
                    
                    // MYR patterns
                    @"RM\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",           // RM 123.45
                    @"MYR\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",          // MYR 123.45
                    @"(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*MYR",          // 123.45 MYR
                    
                    // Generic patterns for totals, subtotals, amounts
                    @"(?:total|amount|subtotal|sub-total|grand\s*total|final\s*total)[\s:]*\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // Total: 123.45
                    @"(?:total|amount|subtotal|sub-total|grand\s*total|final\s*total)[\s:]*RM\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // Total: RM 123.45
                    
                    // Tax patterns
                    @"(?:tax|vat|gst|sst)[\s:]*\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",    // Tax: 12.34
                    @"(?:tax|vat|gst|sst)[\s:]*RM\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",     // Tax: RM 12.34
                    
                    // Service charge patterns
                    @"(?:service\s*charge|s\/c)[\s:]*\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // Service Charge: 5.00
                    @"(?:service\s*charge|s\/c)[\s:]*RM\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",  // Service Charge: RM 5.00
                    
                    // Line item patterns (less specific, used as fallback)
                    @"\b(\d{1,3}(?:,\d{3})*\.\d{2})\b",                 // 123.45 (standalone numbers with 2 decimals)
                };

                foreach (var pattern in pricePatterns)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    var matches = regex.Matches(text);

                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var priceText = match.Groups[1].Value.Replace(",", ""); // Remove commas

                            if (decimal.TryParse(priceText, out decimal amount))
                            {
                                var extractedPrice = new ExtractedPrice
                                {
                                    Amount = amount,
                                    Currency = DetermineCurrencyFromMatch(match.Value),
                                    Type = DeterminePriceType(match.Value),
                                    OriginalText = match.Value,
                                    Confidence = CalculateMatchConfidence(match.Value)
                                };

                                // Avoid duplicates
                                if (!extractedPrices.Any(p => Math.Abs(p.Amount - amount) < 0.01m && p.Currency == extractedPrice.Currency))
                                {
                                    extractedPrices.Add(extractedPrice);
                                }
                            }
                        }
                    }
                }

                // Sort by confidence and relevance
                extractedPrices = extractedPrices
                    .OrderByDescending(p => p.Confidence)
                    .ThenByDescending(p => p.Amount)
                    .ToList();

                _logger.LogInformation($"Extracted {extractedPrices.Count} prices from OCR text");

                foreach (var price in extractedPrices)
                {
                    _logger.LogInformation($"Extracted: {price.Currency} {price.Amount} ({price.Type}) - {price.OriginalText}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting prices from text");
            }

            return extractedPrices;
        }

        private string DetermineCurrencyFromMatch(string matchText)
        {
            if (matchText.Contains("$") || matchText.ToUpper().Contains("USD"))
                return "USD";
            if (matchText.ToUpper().Contains("RM") || matchText.ToUpper().Contains("MYR"))
                return "MYR";

            // Default to MYR for Malaysian context
            return "MYR";
        }

        private string DeterminePriceType(string matchText)
        {
            var upperMatch = matchText.ToUpper();

            if (upperMatch.Contains("TOTAL") || upperMatch.Contains("GRAND"))
                return "Total";
            if (upperMatch.Contains("SUBTOTAL") || upperMatch.Contains("SUB-TOTAL"))
                return "Subtotal";
            if (upperMatch.Contains("TAX") || upperMatch.Contains("GST") || upperMatch.Contains("SST") || upperMatch.Contains("VAT"))
                return "Tax";
            if (upperMatch.Contains("SERVICE") || upperMatch.Contains("S/C"))
                return "Service Charge";

            return "Amount";
        }

        private int CalculateMatchConfidence(string matchText)
        {
            var confidence = 50; // Base confidence
            var upperMatch = matchText.ToUpper();

            // Higher confidence for explicit keywords
            if (upperMatch.Contains("TOTAL")) confidence += 30;
            if (upperMatch.Contains("AMOUNT")) confidence += 25;
            if (upperMatch.Contains("SUBTOTAL")) confidence += 20;
            if (upperMatch.Contains("TAX")) confidence += 15;
            if (upperMatch.Contains("$") || upperMatch.Contains("RM")) confidence += 10;

            return Math.Min(confidence, 100);
        }

        private decimal CalculateTotalAmount(List<ExtractedPrice> prices)
        {
            if (!prices.Any()) return 0;

            // Try to find the highest confidence "Total" amount first
            var totalPrice = prices.FirstOrDefault(p => p.Type == "Total" && p.Confidence > 70);
            if (totalPrice != null)
            {
                _logger.LogInformation($"Using explicit total: {totalPrice.Currency} {totalPrice.Amount}");
                return totalPrice.Amount;
            }

            // If no explicit total, try to calculate from components
            var subtotal = prices.FirstOrDefault(p => p.Type == "Subtotal")?.Amount ?? 0;
            var tax = prices.Where(p => p.Type == "Tax").Sum(p => p.Amount);
            var serviceCharge = prices.Where(p => p.Type == "Service Charge").Sum(p => p.Amount);

            if (subtotal > 0)
            {
                var calculatedTotal = subtotal + tax + serviceCharge;
                _logger.LogInformation($"Calculated total: {calculatedTotal} (Subtotal: {subtotal} + Tax: {tax} + Service: {serviceCharge})");
                return calculatedTotal;
            }

            // Fallback to the highest amount
            var highestAmount = prices.OrderByDescending(p => p.Amount).FirstOrDefault();
            if (highestAmount != null)
            {
                _logger.LogInformation($"Using highest amount as total: {highestAmount.Currency} {highestAmount.Amount}");
                return highestAmount.Amount;
            }

            return 0;
        }

        private string DetectCurrency(string text)
        {
            var upperText = text.ToUpper();

            // Count currency indicators
            var usdCount = Regex.Matches(upperText, @"\$|USD").Count;
            var myrCount = Regex.Matches(upperText, @"RM|MYR").Count;

            // Return the most frequent currency, default to MYR
            return usdCount > myrCount ? "USD" : "MYR";
        }
    }

    // Add these classes to support price extraction
    public class ExtractedPrice
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "MYR";
        public string Type { get; set; } = "Amount"; // Total, Subtotal, Tax, Service Charge, Amount
        public string OriginalText { get; set; } = "";
        public int Confidence { get; set; } = 0;
    }

    // NEW: Class for multiple OCR results
    public class MultipleOcrResult
    {
        public List<OcrResult> IndividualResults { get; set; } = new List<OcrResult>();
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "MYR";
        public int ProcessedDocuments { get; set; }
        public int TotalDocuments { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

   
}