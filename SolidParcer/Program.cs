using SolidParcer.Interfaces;
using SolidParcer.Models;
using SolidParcer.Services;
using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("UniversalParser");
        var genderMap = NameGenderLoader.LoadGenderMap();
        Console.WriteLine("=== Универсальный парсер результатов ===\n");

        var userInteraction = new UserInteractionService();
        var dataProcessing = new DataProcessingService();
        var excelExport = new ExcelExportService(dataProcessing);

        string url = userInteraction.AskUrl();
        if (string.IsNullOrEmpty(url))
        {
            userInteraction.ShowMessage("Ссылка не может быть пустой. Завершение.");
            return;
        }

        IPageParserService pageParser = null;
        IPaginationHandler paginationHandler = null;
        bool useWebDriver = true; // флаг, нужен ли WebDriver

        if (url.Contains("heroleague.ru"))
        {
            paginationHandler = new HeroLeaguePaginationHandlerService();
            pageParser = new HeroLeaguePageParserService(paginationHandler);
        }
        else if (url.Contains("results.zone"))
        {
            string basePath = ExtractBasePath(url);
            paginationHandler = new ResultsZonePaginationHandlerService();
            pageParser = new ResultsZonePageParserService(paginationHandler, basePath);
        }
        else if (url.Contains("tourderussie.ru"))
        {
            var apiParser = new TourDeRussieApiParserService();
            useWebDriver = false;

            // Пробуем извлечь ID из URL
            string eventIdFromUrl = apiParser.ExtractEventIdFromUrl(url);
            if (!string.IsNullOrEmpty(eventIdFromUrl))
            {
                apiParser.SetEventId(eventIdFromUrl);
            }
            else
            {
                // Получаем список событий
                var events = apiParser.GetAvailableEventsAsync().GetAwaiter().GetResult();
                if (events == null || !events.Any())
                {
                    Console.WriteLine("Нет доступных событий.");
                    return;
                }

                var available = events.Where(e => e.is_results_visible && e.status == "completed").ToList();
                if (!available.Any())
                {
                    Console.WriteLine("Нет событий с опубликованными результатами.");
                    return;
                }

                Console.WriteLine("Доступные события:");
                for (int i = 0; i < available.Count; i++)
                {
                    var e = available[i];
                    Console.WriteLine($"{i + 1}. {e.name} - {e.location} ({e.date})");
                }

                Console.Write("Выберите номер события (или введите его ID): ");
                string input = Console.ReadLine().Trim();
                string selectedId = null;
                if (int.TryParse(input, out int idx) && idx >= 1 && idx <= available.Count)
                {
                    selectedId = available[idx - 1].id;
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(input, @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"))
                {
                    selectedId = input;
                }

                if (string.IsNullOrEmpty(selectedId))
                {
                    Console.WriteLine("Неверный выбор.");
                    return;
                }

                apiParser.SetEventId(selectedId);
            }

            pageParser = apiParser;
        }
        else
        {
            Console.WriteLine("Сайт не поддерживается.");
            return;
        }

        string eventTitle = null;
        string eventDate = null;
        List<ParticipantRecord> allRecords = null;
        Dictionary<string, string> raceNames = null;

        if (useWebDriver)
        {
            // Для heroleague и results.zone используем WebDriver
            using (var webDriverService = new WebDriverService())
            {
                using (var driver = webDriverService.CreateDriver())
                {
                    try
                    {
                        driver.Navigate().GoToUrl(url);
                        userInteraction.ShowMessage("Страница загружается...");

                        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(30));
                        if (url.Contains("heroleague.ru"))
                        {
                            wait.Until(drv => drv.FindElements(OpenQA.Selenium.By.CssSelector("input[type='radio'][name='format']")).Count > 0);
                        }
                        else if (url.Contains("results.zone"))
                        {
                            wait.Until(drv => drv.FindElements(OpenQA.Selenium.By.CssSelector("a[href*='/races/']")).Count > 0 ||
                                              drv.FindElements(OpenQA.Selenium.By.CssSelector(".heats_table")).Count > 0);
                        }

                        eventTitle = pageParser.ExtractEventTitle(driver);
                        eventDate = pageParser.ExtractEventDate(driver);

                        userInteraction.ShowMessage($"Извлечено название: {eventTitle ?? "не найдено"}");
                        userInteraction.ShowMessage($"Извлечена дата: {eventDate ?? "не найдена"}");

                        if (string.IsNullOrEmpty(eventTitle))
                            eventTitle = userInteraction.AskTitle("Мероприятие");
                        if (string.IsNullOrEmpty(eventDate))
                            eventDate = userInteraction.AskDate("01.01.2026");

                        allRecords = pageParser.CollectAllRecords(driver);
                        raceNames = pageParser.GetRaceNames(driver);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Критическая ошибка при парсинге: {ex.Message}");
                        Console.WriteLine("Нажмите любую клавишу для выхода...");
                        Console.ReadKey();
                        return;
                    }
                }
            }
        }
        else
        {
            // Для tourderussie.ru используем API-парсер без WebDriver
            var apiParser = pageParser as TourDeRussieApiParserService;
            if (apiParser == null)
            {
                Console.WriteLine("Ошибка: парсер не является TourDeRussieApiParserService.");
                return;
            }

            allRecords = apiParser.CollectAllRecords(null);
            eventTitle = apiParser.ExtractEventTitle(null);
            eventDate = apiParser.ExtractEventDate(null);
            raceNames = apiParser.GetRaceNames(null);
        }

        if (allRecords != null && allRecords.Any())
        {
            try
            {
                excelExport.Export(allRecords, eventTitle, eventDate, raceNames);
                userInteraction.ShowMessage($"\nВсего собрано записей: {allRecords.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при экспорте: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Не удалось собрать данные.");
        }

        userInteraction.ShowMessage("Готово! Нажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    private static string ExtractBasePath(string url)
    {
        var uri = new Uri(url);
        string path = uri.AbsolutePath.Trim('/');
        var parts = path.Split('/');
        return parts.Length > 0 ? parts[0] : "gf-dubna-2026";
    }
}