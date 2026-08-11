using OpenQA.Selenium;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace SolidParcer.Services
{
    public class ResultsZonePageParserService : IPageParserService
    {
        private readonly IPaginationHandler _paginationHandler;
        private readonly string _basePath;
        private readonly HttpClient _httpClient = new HttpClient();

        public ResultsZonePageParserService(IPaginationHandler paginationHandler, string basePath)
        {
            _paginationHandler = paginationHandler;
            _basePath = basePath;
        }

        public string ExtractEventTitle(IWebDriver driver)
        {
            try
            {
                var breadcrumbItems = driver.FindElements(By.CssSelector("ol.breadcrumb li"));
                if (breadcrumbItems.Count >= 2)
                {
                    return breadcrumbItems[1].Text?.Trim();
                }

                string title = driver.Title;
                if (!string.IsNullOrEmpty(title))
                {
                    var parts = title.Split(new[] { "—" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        string eventPart = parts[1].Trim();
                        eventPart = eventPart.Replace(" :: Результаты", "").Trim();
                        return eventPart;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            try
            {
                var h2 = driver.FindElement(By.CssSelector("h2"));
                var small = h2.FindElement(By.CssSelector("small.text-muted"));
                if (small != null)
                {
                    string dateText = small.Text.Trim();
                    if (!string.IsNullOrEmpty(dateText))
                        return dateText;
                }
            }
            catch { }

            try
            {
                var scriptResult = ((IJavaScriptExecutor)driver).ExecuteScript("return window.general.event.date_start;");
                if (scriptResult != null)
                {
                    string date = scriptResult.ToString().Trim();
                    if (!string.IsNullOrEmpty(date))
                        return date;
                }
            }
            catch { }

            return null;
        }

        public List<string> GetAvailableFormats(IWebDriver driver)
        {
            var result = new List<string>();

            string currentUrl = driver.Url;
            var match = Regex.Match(currentUrl, @"/races/(\d+)/results");
            if (match.Success)
            {
                result.Add(match.Groups[1].Value);
                return result;
            }

            var links = driver.FindElements(By.CssSelector("a[href*='/races/']"));
            foreach (var link in links)
            {
                string href = link.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var parts = href.Split('/');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i] == "races" && int.TryParse(parts[i + 1], out _))
                    {
                        string raceId = parts[i + 1];
                        if (!result.Contains(raceId))
                            result.Add(raceId);
                        break;
                    }
                }
            }
            return result;
        }

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format)
        {
            return DownloadAndParseXlsx(format).GetAwaiter().GetResult();
        }

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            var raceIds = GetAvailableFormats(driver);

            Console.WriteLine($"Найдено заездов: {raceIds.Count}");

            foreach (var raceId in raceIds)
            {
                Console.WriteLine($"Парсим заезд ID: {raceId}");
                var records = ParseFormat(driver, raceId);
                foreach (var rec in records)
                {
                    rec.Format = raceId;
                    allRecords.Add(rec);
                }
                Console.WriteLine($"  Загружено записей: {records.Count}");
            }

            return allRecords;
        }

        private async Task<List<ParticipantRecord>> DownloadAndParseXlsx(string raceId)
        {
            var records = new List<ParticipantRecord>();
            string url = $"https://results.zone/{_basePath}/races/{raceId}/results.xlsx";

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(url);
                using (var stream = new MemoryStream(fileBytes))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    int maxCol = worksheet.Dimension?.Columns ?? 30;
                    int maxRow = Math.Min(worksheet.Dimension?.Rows ?? 50, 30);
                    int headerRow = -1;

                    for (int r = 1; r <= maxRow; r++)
                    {
                        bool hasNumber = false, hasLastName = false, hasFirstName = false;
                        for (int c = 1; c <= maxCol; c++)
                        {
                            string val = worksheet.Cells[r, c].Text?.Trim() ?? "";
                            if (val.IndexOf("Номер", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Bib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("№", StringComparison.OrdinalIgnoreCase) >= 0)
                                hasNumber = true;
                            if (val.IndexOf("Фамилия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Last Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Surname", StringComparison.OrdinalIgnoreCase) >= 0)
                                hasLastName = true;
                            if (val.IndexOf("Имя", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("First Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Given Name", StringComparison.OrdinalIgnoreCase) >= 0)
                                hasFirstName = true;
                        }
                        if (hasNumber && hasLastName && hasFirstName)
                        {
                            headerRow = r;
                            break;
                        }
                    }

                    if (headerRow == -1)
                    {
                        for (int r = 1; r <= maxRow; r++)
                        {
                            bool hasPlace = false, hasNumber = false;
                            for (int c = 1; c <= maxCol; c++)
                            {
                                string val = worksheet.Cells[r, c].Text?.Trim() ?? "";
                                if (val.IndexOf("Место", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("Place", StringComparison.OrdinalIgnoreCase) >= 0)
                                    hasPlace = true;
                                if (val.IndexOf("Номер", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("Bib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    val.IndexOf("№", StringComparison.OrdinalIgnoreCase) >= 0)
                                    hasNumber = true;
                            }
                            if (hasPlace && hasNumber)
                            {
                                headerRow = r;
                                break;
                            }
                        }
                    }

                    if (headerRow == -1)
                    {
                        Console.WriteLine($"  Не удалось найти строку заголовка для заезда {raceId}, используем fallback (строка 5)");
                        headerRow = 5;
                    }

                    Console.Write($"  Заголовки (строка {headerRow}): ");
                    for (int c = 1; c <= Math.Min(maxCol, 22); c++)
                    {
                        string val = worksheet.Cells[headerRow, c].Text?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(val))
                            Console.Write($"[{c}]{val} ");
                    }
                    Console.WriteLine();

                    int colNumber = -1, colFirstName = -1, colLastName = -1, colOfficialTime = -1,
                        colStatus = -1, colGender = -1, colCategory = -1;

                    for (int col = 1; col <= maxCol; col++)
                    {
                        string header = worksheet.Cells[headerRow, col].Text?.Trim() ?? "";
                        if (header.IndexOf("Номер", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            header.IndexOf("Bib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            header.IndexOf("№", StringComparison.OrdinalIgnoreCase) >= 0)
                            colNumber = col;
                        else if (header.IndexOf("Имя", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("First Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Given Name", StringComparison.OrdinalIgnoreCase) >= 0)
                            colFirstName = col;
                        else if (header.IndexOf("Фамилия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Last Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Surname", StringComparison.OrdinalIgnoreCase) >= 0)
                            colLastName = col;
                        else if (header.IndexOf("Результат", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0)
                            colOfficialTime = col;
                        else if (header.IndexOf("Статус", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0)
                            colStatus = col;
                        else if (header.IndexOf("Пол", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Gender", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Sex", StringComparison.OrdinalIgnoreCase) >= 0)
                            colGender = col;
                        else if (header.IndexOf("Категория", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Category", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 header.IndexOf("Class", StringComparison.OrdinalIgnoreCase) >= 0)
                            colCategory = col;
                    }

                    if (colNumber == -1) colNumber = 4;
                    if (colFirstName == -1) colFirstName = 8;
                    if (colLastName == -1) colLastName = 7;
                    if (colOfficialTime == -1) colOfficialTime = 16;
                    if (colStatus == -1) colStatus = 5;
                    if (colGender == -1) colGender = 10;
                    if (colCategory == -1) colCategory = 13;

                    Console.WriteLine($"  Индексы: Номер={colNumber}, Имя={colFirstName}, Фамилия={colLastName}, " +
                                      $"Результат={colOfficialTime}, Статус={colStatus}, Пол={colGender}, Категория={colCategory}");

                    int dataRow = headerRow + 1;
                    int rowCount = 0;
                    while (true)
                    {
                        string numberCell = worksheet.Cells[dataRow, colNumber].Text?.Trim() ?? "";
                        string lastNameCell = worksheet.Cells[dataRow, colLastName].Text?.Trim() ?? "";
                        string firstNameCell = worksheet.Cells[dataRow, colFirstName].Text?.Trim() ?? "";

                        if (string.IsNullOrEmpty(numberCell) && string.IsNullOrEmpty(lastNameCell) && string.IsNullOrEmpty(firstNameCell))
                        {
                            if (dataRow > worksheet.Dimension?.Rows + 5) break;
                            dataRow++;
                            continue;
                        }
                        if (string.IsNullOrEmpty(numberCell))
                        {
                            dataRow++;
                            continue;
                        }

                        string rawTime = worksheet.Cells[dataRow, colOfficialTime].Text?.Trim() ?? "";
                        string rawStatus = worksheet.Cells[dataRow, colStatus].Text?.Trim() ?? "";

                        var record = new ParticipantRecord
                        {
                            Number = numberCell,
                            FirstName = firstNameCell,
                            LastName = lastNameCell,
                            OfficialTime = NormalizeTime(rawTime),
                            Status = NormalizeStatus(rawStatus),
                            Gender = worksheet.Cells[dataRow, colGender].Text?.Trim() ?? "",
                            Category = worksheet.Cells[dataRow, colCategory].Text?.Trim() ?? ""
                        };
                        records.Add(record);
                        rowCount++;
                        dataRow++;
                    }
                    Console.WriteLine($"  Загружено записей: {rowCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при скачивании/парсинге XLSX для заезда {raceId}: {ex.Message}");
            }
            return records;
        }
        private string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "finished";
            if (status.Equals("Q", StringComparison.OrdinalIgnoreCase)) return "finished";
            if (status.Equals("DNS", StringComparison.OrdinalIgnoreCase)) return "DNS";
            if (status.Equals("DNF", StringComparison.OrdinalIgnoreCase)) return "DNF";
            if (status.Equals("DSQ", StringComparison.OrdinalIgnoreCase)) return "DSQ";
            return status;
        }

        private string NormalizeTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return "";
            string cleaned = Regex.Replace(time, @"[^0-9:.]", "");
            cleaned = cleaned.Replace('.', ':');

            var parts = cleaned.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4)
            {
                return $"{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}:{parts[2].PadLeft(2, '0')}.{parts[3].PadLeft(2, '0')}";
            }
            else if (parts.Length == 3)
            {
                return $"{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}:{parts[2].PadLeft(2, '0')}.00";
            }
            else if (parts.Length == 2)
            {
                return $"00:{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}.00";
            }
            else
            {
                return cleaned;
            }
        }

        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var cards = driver.FindElements(By.CssSelector("article.card .card-title a"));
                foreach (var a in cards)
                {
                    string href = a.GetAttribute("href");
                    if (string.IsNullOrEmpty(href)) continue;
                    var match = Regex.Match(href, @"/races/(\d+)/results");
                    if (match.Success)
                    {
                        string id = match.Groups[1].Value;
                        string name = a.Text.Trim();
                        if (!result.ContainsKey(id))
                            result.Add(id, name);
                    }
                }
            }
            catch { /* ignore */ }
            return result;
        }
    }
}