using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SolidParcer.Services
{
    public class TourDeRussiePageParserService : IPageParserService
    {
        private readonly IPaginationHandler _paginationHandler;

        public TourDeRussiePageParserService(IPaginationHandler paginationHandler)
        {
            _paginationHandler = paginationHandler;
        }

        public string ExtractEventTitle(IWebDriver driver)
        {
            try
            {
                var h1 = driver.FindElement(By.CssSelector("h1.text-center"));
                string text = h1.Text.Trim();
                var parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[0].Trim() : null;
            }
            catch { return null; }
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            try
            {
                var h1 = driver.FindElement(By.CssSelector("h1.text-center"));
                string text = h1.Text.Trim();
                var parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    string datePart = parts[parts.Length - 1].Trim().Replace(" г.", "").Trim();
                    return datePart;
                }
                return null;
            }
            catch { return null; }
        }

        public List<string> GetAvailableFormats(IWebDriver driver)
        {
            var formats = new List<string>();
            try
            {
                // Ожидаем появления кнопок дистанций
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
                wait.Until(drv => drv.FindElements(By.CssSelector("div.flex.flex-wrap.gap-2.justify-center.mb-6 button")).Count > 0);

                var buttons = driver.FindElements(By.CssSelector("div.flex.flex-wrap.gap-2.justify-center.mb-6 button"));
                foreach (var btn in buttons)
                {
                    string text = btn.Text.Trim();
                    if (!string.IsNullOrEmpty(text) && (text.Contains("Grand") || text.Contains("Median") || text.Contains("Intro")))
                        formats.Add(text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении форматов: {ex.Message}");
            }
            return formats;
        }

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format)
        {
            var allRecords = new List<ParticipantRecord>();
            try
            {
                // 1. Найти и кликнуть по кнопке дистанции
                var formatButtons = driver.FindElements(By.CssSelector("div.flex.flex-wrap.gap-2.justify-center.mb-6 button"));
                IWebElement targetButton = formatButtons.FirstOrDefault(b => b.Text.Trim().Equals(format, StringComparison.OrdinalIgnoreCase));
                if (targetButton == null)
                {
                    Console.WriteLine($"Кнопка дистанции '{format}' не найдена.");
                    return allRecords;
                }

                // Кликаем, если не активна
                if (!IsButtonActive(targetButton))
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", targetButton);
                    WaitForTable(driver);
                }

                // 2. Получить категории (кнопки фильтра)
                var categoryButtons = driver.FindElements(By.CssSelector("div.flex.flex-wrap.gap-2.justify-center.mb-4 button"));
                var categories = categoryButtons.Select(b => b.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

                if (categories.Count == 0)
                {
                    // Если категорий нет – собираем всё как есть
                    var records = _paginationHandler.CollectRowsWithPagination(driver);
                    foreach (var rec in records)
                    {
                        rec.Format = format;
                        rec.Category = "";
                    }
                    allRecords.AddRange(records);
                }
                else
                {
                    foreach (var cat in categories)
                    {
                        // Заново получаем кнопки, т.к. DOM может обновиться
                        categoryButtons = driver.FindElements(By.CssSelector("div.flex.flex-wrap.gap-2.justify-center.mb-4 button"));
                        IWebElement catButton = categoryButtons.FirstOrDefault(b => b.Text.Trim().Equals(cat, StringComparison.OrdinalIgnoreCase));
                        if (catButton == null) continue;

                        if (!IsButtonActive(catButton))
                        {
                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", catButton);
                            WaitForTable(driver);
                        }

                        var records = _paginationHandler.CollectRowsWithPagination(driver);
                        foreach (var rec in records)
                        {
                            rec.Format = format;
                            rec.Category = cat;
                        }
                        allRecords.AddRange(records);
                        System.Threading.Thread.Sleep(500); // небольшая задержка между категориями
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при парсинге формата {format}: {ex.Message}");
            }
            return allRecords;
        }

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            var formats = GetAvailableFormats(driver);
            Console.WriteLine($"Найдено форматов: {formats.Count}");
            foreach (var format in formats)
            {
                Console.WriteLine($"Парсим формат: {format}");
                var records = ParseFormat(driver, format);
                Console.WriteLine($"  Всего строк в формате {format}: {records.Count}");
                allRecords.AddRange(records);
            }
            return allRecords;
        }

        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            var dict = new Dictionary<string, string>();
            var formats = GetAvailableFormats(driver);
            foreach (var f in formats)
                dict[f] = f;
            return dict;
        }

        private bool IsButtonActive(IWebElement button)
        {
            // Активная кнопка обычно имеет цвет фона (background-color не transparent)
            string bg = button.GetCssValue("background-color");
            return bg != "rgba(0, 0, 0, 0)" && bg != "transparent";
        }

        private void WaitForTable(IWebDriver driver)
        {
            new WebDriverWait(driver, TimeSpan.FromSeconds(15))
                .Until(drv => drv.FindElements(By.CssSelector("table tbody tr")).Count > 0);
        }
    }
}