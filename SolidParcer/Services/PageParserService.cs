using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolidParcer.Services
{
    public class PageParserService : IPageParserService
    {
        private readonly IPaginationHandler _paginationHandler;

        public PageParserService(IPaginationHandler paginationHandler)
        {
            _paginationHandler = paginationHandler;
        }

        public string ExtractEventTitle(IWebDriver driver)
        {
            try
            {
                return driver.FindElement(By.CssSelector("h1._mainTitle_y5cus_77")).Text.Trim();
            }
            catch { return null; }
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            try
            {
                var metaItems = driver.FindElements(By.CssSelector("div._metaItem_y5cus_94"));
                if (metaItems.Count >= 2)
                    return metaItems[1].Text.Trim();
                return null;
            }
            catch { return null; }
        }

        public List<string> GetAvailableFormats(IWebDriver driver)
        {
            var radios = driver.FindElements(By.CssSelector("input[type='radio'][name='format']"));
            var formats = new List<string>();
            foreach (var radio in radios)
            {
                string id = radio.GetAttribute("id");
                if (!string.IsNullOrEmpty(id))
                    formats.Add(id.Replace("_filter", ""));
            }
            return formats;
        }

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format)
        {
            // Выбор формата
            var radio = driver.FindElement(By.CssSelector($"input[type='radio'][id='{format}_filter']"));
            if (!radio.Selected)
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", radio);

            // Ждём появления строк
            new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(drv => drv.FindElements(By.CssSelector("div._tr_1boqa_9")).Count > 0);

            var records = _paginationHandler.CollectRowsWithPagination(driver);
            foreach (var rec in records)
                rec.Format = format;
            return records;
        }

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            var formats = GetAvailableFormats(driver);
            Console.WriteLine($"Найдено форматов: {formats.Count}");
            foreach (var format in formats)
            {
                Console.WriteLine($"\nПарсим формат: {format}");
                var records = ParseFormat(driver, format);
                Console.WriteLine($"  Всего строк в формате {format}: {records.Count}");
                allRecords.AddRange(records);
            }
            return allRecords;
        }
        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            return new Dictionary<string, string>();
        }
    }
}
