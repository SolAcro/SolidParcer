using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using OpenQA.Selenium;
using SolidParcer.Interfaces;
using SolidParcer.Models;

namespace SolidParcer.Services
{
    public class RacetimeApiParserService : IPageParserService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _raceStageGuid;
        private string _eventTitle;
        private string _eventDate;
        private List<RaceGroup> _groups;

        private const string ApiBaseUrl = "https://services-results.limetime.io/results/get";
        private const string ApiKey = "NFJADAJCNDJK_CDJCDSN_DDJNCDJ";
        private const string ApiKeyHeader = "LIMETIME-API-KEY";

        public RacetimeApiParserService()
        {
            _httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        // ===== Интерфейс IPageParserService =====

        public string ExtractEventTitle(IWebDriver driver)
        {
            LoadMetadataFromPage(driver);
            return _eventTitle;
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            LoadMetadataFromPage(driver);
            return _eventDate;
        }

        public List<string> GetAvailableFormats(IWebDriver driver)
        {
            LoadMetadataFromPage(driver);
            return _groups?.Select(g => g.DisplayName).ToList() ?? new List<string>();
        }

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format)
        {
            LoadMetadataFromPage(driver);
            var group = _groups?.FirstOrDefault(g => g.DisplayName == format);
            if (group == null) return new List<ParticipantRecord>();

            var records = new List<ParticipantRecord>();
            foreach (var category in group.Categories)
            {
                var catRecords = FetchResultsAsync(group.Guid, category.Guid, group.DisplayName, category.DisplayName)
                                 .GetAwaiter().GetResult();
                records.AddRange(catRecords);
            }
            return records;
        }

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            LoadMetadataFromPage(driver);
            if (_groups == null) return new List<ParticipantRecord>();

            var allRecords = new List<ParticipantRecord>();
            foreach (var group in _groups)
            {
                Console.WriteLine($"  Группа: {group.DisplayName}");
                foreach (var category in group.Categories)
                {
                    Console.WriteLine($"    Категория: {category.DisplayName}");
                    var records = FetchResultsAsync(group.Guid, category.Guid,
                                                     group.DisplayName, category.DisplayName)
                                  .GetAwaiter().GetResult();
                    allRecords.AddRange(records);
                    Console.WriteLine($"      Записей: {records.Count}");
                }
            }
            return allRecords;
        }

        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            LoadMetadataFromPage(driver);
            return _groups?.ToDictionary(g => g.DisplayName, g => g.DisplayName)
                   ?? new Dictionary<string, string>();
        }

        // ===== Извлечение метаданных из страницы =====

        private void LoadMetadataFromPage(IWebDriver driver)
        {
            if (_groups != null) return; // уже загружено

            var js = (IJavaScriptExecutor)driver;

            // 1. GUID этапа
            _raceStageGuid = js.ExecuteScript("return window.RACE_STAGE_GUID;")?.ToString();

            // 2. Название и дата события
            _eventTitle = js.ExecuteScript("return document.querySelector('h1.display-5')?.textContent?.trim();")?.ToString();
            if (string.IsNullOrEmpty(_eventTitle))
                _eventTitle = js.ExecuteScript("return document.title;")?.ToString();

            // Дата из блока 1076 (lead)
            var leadText = js.ExecuteScript(
                "return document.querySelector('.header9_content-right p')?.textContent?.trim();")?.ToString();
            if (!string.IsNullOrEmpty(leadText))
            {
                var match = System.Text.RegularExpressions.Regex.Match(leadText,
                    @"(\d{2}\s+\w+\s+\d{4})");
                if (match.Success) _eventDate = match.Value;
            }
            if (string.IsNullOrEmpty(_eventDate))
                _eventDate = DateTime.Now.ToString("dd.MM.yyyy");

            // 3. Группы и категории
            var groupsJson = js.ExecuteScript("return JSON.stringify(window.GROUPS_AND_CATEGORIES);")?.ToString();
            if (!string.IsNullOrEmpty(groupsJson))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _groups = JsonSerializer.Deserialize<List<RaceGroup>>(groupsJson, options);
            }

            Console.WriteLine($"  Этап GUID: {_raceStageGuid}");
            Console.WriteLine($"  Название: {_eventTitle}");
            Console.WriteLine($"  Дата: {_eventDate}");
            Console.WriteLine($"  Групп: {_groups?.Count ?? 0}");
        }

        // ===== Запрос к API =====

        private async Task<List<ParticipantRecord>> FetchResultsAsync(string groupGuid, string categoryGuid,
                                                               string groupName, string categoryName)
        {
            var records = new List<ParticipantRecord>();
            string url = $"{ApiBaseUrl}/{_raceStageGuid}/{groupGuid}/{categoryGuid}";

            try
            {
                var json = await _httpClient.GetStringAsync(url);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                List<LimeTimeResult> list = null;
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        list = JsonSerializer.Deserialize<List<LimeTimeResult>>(json, options);
                    }
                    else if (root.ValueKind == JsonValueKind.Object &&
                             root.TryGetProperty("data", out var dataProp) &&
                             dataProp.ValueKind == JsonValueKind.Array)
                    {
                        list = JsonSerializer.Deserialize<List<LimeTimeResult>>(dataProp.GetRawText(), options);
                    }
                }

                if (list == null || list.Count == 0)
                    return records;

                foreach (var r in list)
                {
                    string firstName = r.Account?.FirstName ?? "";
                    string lastName = r.Account?.LastName ?? "";

                    string time = NormalizeTime(r.ResultTime);

                    // Статус по флагам API
                    string status;
                    if (r.IsDisqualified) status = "DSQ";
                    else if (r.IsFinished) status = "finished";
                    else if (r.IsOnStart) status = "DNF";
                    else status = "DNS";

                    records.Add(new ParticipantRecord
                    {
                        Number = r.GetNumberString(),
                        FirstName = firstName,
                        LastName = lastName,
                        OfficialTime = time,
                        Status = status,
                        Format = groupName,
                        Category = categoryName,
                        Place = r.Position?.ToString() ?? "",
                        Gender = ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Ошибка запроса {url}: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// Приводит время к формату HH:MM:SS.ms, совместимому с внешним TimeSpanConverterHelper.
        /// "39:33"      → "00:39:33.00"
        /// "1:05:06"    → "01:05:06.00"
        /// "1:05:06.45" → "01:05:06.45"
        /// </summary>
        private static string NormalizeTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            string cleaned = raw.Trim();
            var parts = cleaned.Split(':');

            if (parts.Length == 2) // MM:SS
            {
                return $"00:{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}.00";
            }
            if (parts.Length == 3) // H:MM:SS  или  H:MM:SS.ff
            {
                string hours = parts[0].PadLeft(2, '0');
                string minutes = parts[1].PadLeft(2, '0');
                string seconds = parts[2];
                string ms = "00";

                if (seconds.Contains('.'))
                {
                    var secParts = seconds.Split('.');
                    seconds = secParts[0].PadLeft(2, '0');
                    // приводим к двум знакам: "4" → "40", "45" → "45", "456" → "45"
                    string rawMs = secParts[1];
                    if (rawMs.Length == 1) rawMs = rawMs.PadRight(2, '0');
                    else if (rawMs.Length > 2) rawMs = rawMs.Substring(0, 2);
                    ms = rawMs;
                }
                else
                {
                    seconds = seconds.PadLeft(2, '0');
                }

                return $"{hours}:{minutes}:{seconds}.{ms}";
            }

            // Если нестандартный формат — отдаём как есть, пусть внешний хелпер сам решает
            return cleaned;
        }

        // ===== DTO для JSON-ответа API =====

        private class LimeTimeResultsResponse
        {
            public List<LimeTimeResult> Results { get; set; }
        }

        private class LimeTimeResult
        {
            public JsonElement Number { get; set; }
            public int? Position { get; set; }
            public string ResultTime { get; set; }
            public string ResultMilliseconds { get; set; }
            public bool IsDisqualified { get; set; }
            public bool IsFinished { get; set; }
            public bool IsOnStart { get; set; }
            public LimeTimeAccount Account { get; set; }
            public string Club { get; set; }
            public string City { get; set; }
            // public int? Age { get; set; }   ← удалить или закомментировать

            public string GetNumberString()
            {
                if (Number.ValueKind == JsonValueKind.Number) return Number.GetRawText();
                if (Number.ValueKind == JsonValueKind.String) return Number.GetString() ?? "";
                return "";
            }
        }

        private class LimeTimeAccount
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string City { get; set; }
            // public int? Age { get; set; }   ← удалить или закомментировать
        }

        // ===== DTO для GROUPS_AND_CATEGORIES =====

        private class RaceGroup
        {
            public string Guid { get; set; }
            public string DisplayName { get; set; }
            public List<RaceCategory> Categories { get; set; }
        }

        private class RaceCategory
        {
            public string Guid { get; set; }
            public string DisplayName { get; set; }
            public bool IsTeam { get; set; }
        }
    }
}