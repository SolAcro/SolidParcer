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
    public class TourDeRussieApiParserService : IPageParserService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _eventId;
        private string _eventTitle;
        private string _eventDate;

        public TourDeRussieApiParserService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public string ExtractEventTitle(IWebDriver driver) => _eventTitle;

        public string ExtractEventDate(IWebDriver driver) => _eventDate;

        public List<string> GetAvailableFormats(IWebDriver driver) => new List<string>();

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format) => new List<ParticipantRecord>();

        public async Task<List<ApiEvent>> GetAvailableEventsAsync()
        {
            return await GetEventsAsync();
        }

        public void SetEventId(string eventId)
        {
            _eventId = eventId;
        }

        public string ExtractEventIdFromUrl(string url)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, @"events/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})");
            return match.Success ? match.Groups[1].Value : null;
        }

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            if (string.IsNullOrEmpty(_eventId))
            {
                string url = null;
                try { url = driver?.Url; } catch { }
                if (!string.IsNullOrEmpty(url))
                    _eventId = ExtractEventIdFromUrl(url);
                if (string.IsNullOrEmpty(_eventId))
                    _eventId = GetDefaultEventId().GetAwaiter().GetResult();
            }

            // Загружаем информацию о событии
            if (string.IsNullOrEmpty(_eventTitle) || string.IsNullOrEmpty(_eventDate))
            {
                var events = GetEventsAsync().GetAwaiter().GetResult();
                var evt = events.FirstOrDefault(e => e.id == _eventId);
                if (evt != null)
                {
                    _eventTitle = evt.name;
                    if (DateTime.TryParse(evt.date, out var dt))
                        _eventDate = dt.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                    else
                        _eventDate = "01.01.2026";
                }
                else
                {
                    _eventTitle = "Tour de Russie";
                    _eventDate = "01.01.2026";
                }
            }

            // Получаем дистанции
            var distances = GetDistancesAsync(_eventId).GetAwaiter().GetResult();
            if (distances == null || !distances.Any())
                return new List<ParticipantRecord>();

            var allRecords = new List<ParticipantRecord>();
            foreach (var dist in distances)
            {
                var results = GetResultsAsync(_eventId, dist.id).GetAwaiter().GetResult();
                foreach (var apiResult in results)
                {
                    var record = MapToParticipantRecord(apiResult, dist.name);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }

        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            var distances = GetDistancesAsync(_eventId).GetAwaiter().GetResult();
            return distances?.ToDictionary(d => d.id, d => d.name) ?? new Dictionary<string, string>();
        }

        // ----- Приватные методы -----

        private async Task<string> GetDefaultEventId()
        {
            var events = await GetEventsAsync();
            var evt = events
                .Where(e => e.is_results_visible && e.status == "completed")
                .OrderByDescending(e => e.date)
                .FirstOrDefault();
            return evt?.id;
        }

        private async Task<List<ApiEvent>> GetEventsAsync()
        {
            string url = "https://tourderussie.ru/api/events";
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<ApiEventsResponse>(response, options);
            return data?.events ?? new List<ApiEvent>();
        }

        private async Task<List<ApiDistance>> GetDistancesAsync(string eventId)
        {
            string url = $"https://tourderussie.ru/api/events/{eventId}/distances";
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<ApiDistancesResponse>(response, options);
            return data?.distances ?? new List<ApiDistance>();
        }

        private async Task<List<ApiResult>> GetResultsAsync(string eventId, string distanceId)
        {
            string url = $"https://tourderussie.ru/api/events/{eventId}/results?distance_id={distanceId}";
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<ApiResultsResponse>(response, options);
            return data?.results ?? new List<ApiResult>();
        }

        private ParticipantRecord MapToParticipantRecord(ApiResult api, string distanceName)
        {
            string firstName = api.first_name ?? "";
            if (!string.IsNullOrEmpty(api.patronymic))
                firstName += " " + api.patronymic;

            string officialTime = "";
            if (api.finish_time != null)
            {
                var ft = api.finish_time;
                officialTime = $"{ft.hours:D2}:{ft.minutes:D2}:{ft.seconds:D2}.{ft.milliseconds:D2}";
            }

            string status = "finished";
            if (!string.IsNullOrEmpty(api.result_status))
            {
                status = api.result_status;
            }
            else if (!api.place.HasValue && !string.IsNullOrEmpty(officialTime))
            {
                status = "DNF";
            }
            else if (!api.place.HasValue)
            {
                status = "DNS";
            }

            return new ParticipantRecord
            {
                Number = api.bib_number.ToString(),
                FirstName = firstName,
                LastName = api.last_name ?? "",
                OfficialTime = officialTime,
                Status = status,
                Format = distanceName,
                Gender = "",
                Place = api.place?.ToString() ?? "",
                Category = api.category ?? "",
            };
        }
    }
}