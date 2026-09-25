using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SolidParcer.Services
{
    public class HeroLeaguePageParserService : IPageParserService
    {
        private readonly IPaginationHandler _paginationHandler;

        public HeroLeaguePageParserService(IPaginationHandler paginationHandler)
        {
            _paginationHandler = paginationHandler;
        }

        public string ExtractEventTitle(IWebDriver driver)
        {
            try
            {
                // class="_mainTitle_1oe5i_77" — берём по префиксу класса
                var h1 = driver.FindElements(By.CssSelector("h1[class*='_mainTitle_']")).FirstOrDefault()
                      ?? driver.FindElements(By.CssSelector("h1")).FirstOrDefault();
                if (h1 != null)
                {
                    string text = h1.Text.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }
            catch { }
            return null;
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            try
            {
                // Ищем во всех _metaItem_ блоки, берём тот, который содержит дату
                var metaItems = driver.FindElements(By.CssSelector("div[class*='_metaItem_']"));
                foreach (var item in metaItems)
                {
                    string text = item.Text.Trim();
                    var m = Regex.Match(text, @"\b\d{2}\.\d{2}\.\d{4}\b");
                    if (m.Success)
                        return m.Value;
                }

                // Фолбэк — ищем дату во всём тексте страницы
                string body = driver.FindElement(By.TagName("body")).Text;
                var match = Regex.Match(body, @"\b\d{2}\.\d{2}\.\d{4}\b");
                if (match.Success)
                    return match.Value;
            }
            catch { }
            return null;
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
            var radio = driver.FindElement(By.CssSelector($"input[type='radio'][id='{format}_filter']"));
            if (!radio.Selected)
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", radio);

            // Ждём появления ячеек с data-label (это строки таблицы результатов)
            new WebDriverWait(driver, TimeSpan.FromSeconds(30))
                .Until(drv => drv.FindElements(By.CssSelector("[data-label]")).Count > 0);

            // Небольшая пауза, чтобы таблица перерисовалась под выбранный формат
            System.Threading.Thread.Sleep(500);

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