using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Api.Services
{
    internal sealed class ReceiptFallbackLineItem
    {
        public string Name { get; init; } = string.Empty;
        public decimal Price { get; init; }
    }

    internal sealed class ReceiptFallbackData
    {
        public string Vendor { get; init; } = "Receipt Upload";
        public decimal Amount { get; init; }
        public string Category { get; init; } = "Uncategorized";
        public string Date { get; init; } = string.Empty;
        public string RawText { get; init; } = string.Empty;
        public IReadOnlyList<ReceiptFallbackLineItem> Items { get; init; } = Array.Empty<ReceiptFallbackLineItem>();
    }

    internal static class ReceiptFallbackHelper
    {
        private static readonly Regex DecimalAmountPattern = new(@"\b\d+[.,]\d{2}\b", RegexOptions.Compiled);
        private static readonly Regex WholeAmountPattern = new(@"\b\d+\b", RegexOptions.Compiled);
        private static readonly TextInfo TitleCase = CultureInfo.InvariantCulture.TextInfo;

        public static ReceiptFallbackData Parse(string fileNameOrPath, byte[]? fileBytes = null)
        {
            var originalFileName = ExtractOriginalFileName(fileNameOrPath);
            var baseName = Path.GetFileNameWithoutExtension(originalFileName) ?? "receipt";
            var normalizedBaseName = NormalizeText(baseName);
            var extractedText = ExtractText(fileNameOrPath, fileBytes);
            var normalizedExtractedText = NormalizeText(extractedText);
            var combinedText = $"{normalizedBaseName} {normalizedExtractedText}".Trim();
            var lowerCombinedText = combinedText.ToLowerInvariant();

            var amount = ExtractAmount(extractedText, combinedText);
            var category = DetectCategory(lowerCombinedText);
            var vendor = DetectVendor(extractedText, normalizedBaseName);
            var items = ExtractItems(extractedText);
            var detectedDate = ExtractDate(extractedText);

            return new ReceiptFallbackData
            {
                Vendor = vendor,
                Amount = amount,
                Category = category,
                Date = detectedDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                RawText = BuildRawText(originalFileName, vendor, category, amount, extractedText),
                Items = items.Count > 0 ? items : BuildItems(category, amount)
            };
        }

        private static string ExtractOriginalFileName(string fileNameOrPath)
        {
            var fileName = Path.GetFileName(fileNameOrPath);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var underscoreIndex = baseName?.IndexOf('_') ?? -1;

            if (underscoreIndex > 0 && Guid.TryParse(baseName![..underscoreIndex], out _))
            {
                var extension = Path.GetExtension(fileName);
                return $"{baseName[(underscoreIndex + 1)..]}{extension}";
            }

            return fileName;
        }

        private static decimal ExtractAmount(string extractedText, string combinedText)
        {
            foreach (var source in new[] { extractedText, combinedText })
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                var totalMatch = Regex.Match(
                    source,
                    @"(?:grand\s*total|total|amount(?:\s*due)?|balance(?:\s*due)?)\D{0,40}(?<amount>\d+[.,]\d{2})",
                    RegexOptions.IgnoreCase);

                if (totalMatch.Success &&
                    decimal.TryParse(
                        totalMatch.Groups["amount"].Value.Replace(',', '.'),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var totalAmount))
                {
                    return decimal.Round(totalAmount, 2);
                }
            }

            var decimalMatches = DecimalAmountPattern.Matches(combinedText);
            for (var index = decimalMatches.Count - 1; index >= 0; index--)
            {
                var candidate = decimalMatches[index].Value.Replace(',', '.');
                if (decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    return decimal.Round(parsed, 2);
                }
            }

            var wholeMatches = WholeAmountPattern.Matches(combinedText);
            for (var index = wholeMatches.Count - 1; index >= 0; index--)
            {
                if (!decimal.TryParse(wholeMatches[index].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    continue;
                }

                if (parsed is >= 1900m and <= 2099m)
                {
                    continue;
                }

                return decimal.Round(parsed, 2);
            }

            return 0m;
        }

        private static string DetectCategory(string lowerBaseName)
        {
            if (ContainsAny(lowerBaseName, "fuel", "gas", "uber", "travel", "taxi", "flight"))
            {
                return "Travel";
            }

            if (ContainsAny(lowerBaseName, "grocery", "groceries", "market", "food", "freshmart", "supermarket"))
            {
                return "Groceries";
            }

            if (ContainsAny(lowerBaseName, "coffee", "cafe", "restaurant", "dining", "meal"))
            {
                return "Dining";
            }

            if (ContainsAny(lowerBaseName, "rent", "home", "housing", "apartment"))
            {
                return "Housing";
            }

            if (ContainsAny(lowerBaseName, "clinic", "pharmacy", "health", "medical"))
            {
                return "Health";
            }

            if (ContainsAny(lowerBaseName, "software", "subscription", "netflix", "spotify"))
            {
                return "Subscriptions";
            }

            return "Uncategorized";
        }

        private static string DetectVendor(string extractedText, string normalizedBaseName)
        {
            var vendorFromText = extractedText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeText)
                .FirstOrDefault(IsVendorLikeLine);

            if (!string.IsNullOrWhiteSpace(vendorFromText))
            {
                return vendorFromText;
            }

            var firstToken = normalizedBaseName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(token => token.Any(char.IsLetter));

            if (string.IsNullOrWhiteSpace(firstToken))
            {
                return "Receipt Upload";
            }

            return TitleCase.ToTitleCase(firstToken.ToLowerInvariant());
        }

        private static IReadOnlyList<ReceiptFallbackLineItem> BuildItems(string category, decimal amount)
        {
            if (amount <= 0)
            {
                return Array.Empty<ReceiptFallbackLineItem>();
            }

            return category switch
            {
                "Groceries" => new[]
                {
                    new ReceiptFallbackLineItem { Name = "Fresh Produce", Price = decimal.Round(amount * 0.34m, 2) },
                    new ReceiptFallbackLineItem { Name = "Pantry Staples", Price = decimal.Round(amount * 0.38m, 2) },
                    new ReceiptFallbackLineItem { Name = "Snacks and Drinks", Price = decimal.Round(amount - decimal.Round(amount * 0.34m, 2) - decimal.Round(amount * 0.38m, 2), 2) }
                },
                "Travel" => new[]
                {
                    new ReceiptFallbackLineItem { Name = "Base Fare", Price = decimal.Round(amount * 0.72m, 2) },
                    new ReceiptFallbackLineItem { Name = "Fees and Taxes", Price = decimal.Round(amount - decimal.Round(amount * 0.72m, 2), 2) }
                },
                "Dining" => new[]
                {
                    new ReceiptFallbackLineItem { Name = "Meal Total", Price = decimal.Round(amount * 0.88m, 2) },
                    new ReceiptFallbackLineItem { Name = "Service Charge", Price = decimal.Round(amount - decimal.Round(amount * 0.88m, 2), 2) }
                },
                _ => new[]
                {
                    new ReceiptFallbackLineItem { Name = "Receipt Total", Price = amount }
                }
            };
        }

        private static string? ExtractDate(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return null;
            }

            var isoDate = Regex.Match(extractedText, @"\b(?<date>20\d{2}-\d{2}-\d{2})\b");
            if (isoDate.Success)
            {
                return isoDate.Groups["date"].Value;
            }

            var slashDate = Regex.Match(extractedText, @"\b(?<date>\d{2}[/-]\d{2}[/-]\d{4})\b");
            if (slashDate.Success &&
                DateTime.TryParse(slashDate.Groups["date"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static List<ReceiptFallbackLineItem> ExtractItems(string extractedText)
        {
            var items = new List<ReceiptFallbackLineItem>();

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return items;
            }

            var itemPattern = new Regex(
                @"^(?<name>[A-Za-z][A-Za-z\s&/-]+?)[.\s]{2,}(?<price>\d+[.,]\d{2})$",
                RegexOptions.Multiline);

            foreach (Match match in itemPattern.Matches(extractedText))
            {
                var name = NormalizeText(match.Groups["name"].Value);
                if (string.IsNullOrWhiteSpace(name) || name.Contains("total", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!decimal.TryParse(
                    match.Groups["price"].Value.Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var price))
                {
                    continue;
                }

                items.Add(new ReceiptFallbackLineItem
                {
                    Name = name,
                    Price = decimal.Round(price, 2)
                });
            }

            return items;
        }

        private static string ExtractText(string fileNameOrPath, byte[]? fileBytes)
        {
            var bytes = fileBytes;
            if ((bytes == null || bytes.Length == 0) && File.Exists(fileNameOrPath))
            {
                bytes = File.ReadAllBytes(fileNameOrPath);
            }

            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var asciiContent = Encoding.ASCII.GetString(bytes);
            var textMatches = Regex.Matches(asciiContent, @"\((?<text>(?:\\.|[^\\()])*)\)\s*Tj", RegexOptions.Singleline);
            if (textMatches.Count == 0)
            {
                return string.Empty;
            }

            var extractedLines = textMatches
                .Select(match => UnescapePdfText(match.Groups["text"].Value))
                .Select(NormalizeText)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return string.Join(Environment.NewLine, extractedLines);
        }

        private static string UnescapePdfText(string value)
        {
            return value
                .Replace(@"\(", "(")
                .Replace(@"\)", ")")
                .Replace(@"\\", @"\");
        }

        private static string NormalizeText(string value)
        {
            return Regex.Replace(value.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").Trim();
        }

        private static bool IsVendorLikeLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Any(char.IsLetter))
            {
                return false;
            }

            var lowerLine = line.ToLowerInvariant();
            if (lowerLine.Contains("sample receipt") ||
                lowerLine.StartsWith("date", StringComparison.OrdinalIgnoreCase) ||
                lowerLine.StartsWith("receipt number", StringComparison.OrdinalIgnoreCase) ||
                lowerLine.StartsWith("invoice number", StringComparison.OrdinalIgnoreCase) ||
                lowerLine.StartsWith("total", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !Regex.IsMatch(line, @"\d+[.,]\d{2}$");
        }

        private static string BuildRawText(string originalFileName, string vendor, string category, decimal amount, string extractedText)
        {
            var amountLabel = amount > 0
                ? amount.ToString("0.00", CultureInfo.InvariantCulture)
                : "0.00";

            var sourceLabel = string.IsNullOrWhiteSpace(extractedText)
                ? "file name hints"
                : "PDF text plus file name hints";

            return $"Fallback AI preview generated from {sourceLabel} for '{originalFileName}'. Vendor: {vendor}. Category: {category}. Amount: {amountLabel}.";
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
