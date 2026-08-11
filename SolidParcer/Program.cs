using SolidParcer.Interfaces;
using SolidParcer.Models;
using SolidParcer.Services;
using System;

class Program
{
    static void Main(string[] args)
    {
        OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("UniversalParser");
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

        IPageParserService pageParser;
        IPaginationHandler paginationHandler;

        if (url.Contains("heroleague.ru"))
        {
            paginationHandler = new PaginationHandler();
            pageParser = new PageParserService(paginationHandler);
        }
        else if (url.Contains("results.zone"))
        {
            string basePath = ExtractBasePath(url);
            paginationHandler = new ResultsZonePaginationHandler();
            pageParser = new ResultsZonePageParser(paginationHandler, basePath);
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

        // Блок для работы с браузером
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
                // Браузер будет закрыт автоматически при выходе из using
            }
        }

        // Экспорт выполняется после закрытия браузера
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