using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SolidParcer.Services
{
    public class HeroLeaguePaginationHandlerService : IPaginationHandler
    {
        public List<ParticipantRecord> CollectRowsWithPagination(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            int currentPage = 1;
            const int maxPages = 50;

            while (currentPage <= maxPages)
            {
                if (!driver.Url.Contains("/results/"))
                {
                    Console.WriteLine("  Переход на другую страницу (не results). Завершаем.");
                    break;
                }

                Console.WriteLine($"  Ожидаем загрузки данных... (страница {currentPage})");
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(drv => drv.FindElements(By.CssSelector("div._tr_1boqa_9")).Count > 0);

                // Прокрутка для подгрузки всех строк
                int lastCount = 0, stableCount = 0;
                while (stableCount < 3)
                {
                    var rows = driver.FindElements(By.CssSelector("div._tr_1boqa_9"));
                    int currentCount = rows.Count;
                    if (currentCount == lastCount) stableCount++;
                    else stableCount = 0;
                    lastCount = currentCount;
                    Console.Write($"    Текущее количество строк: {currentCount}\r");
                    if (currentCount > 0)
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", rows.Last());
                    Thread.Sleep(200);
                }

                var pageRows = driver.FindElements(By.CssSelector("div._tr_1boqa_9"));
                if (!pageRows.Any()) break;
                Console.WriteLine($"  Собрано строк на странице {currentPage}: {pageRows.Count}");

                // --- Определяем индексы колонок по data-label первой строки ---
                var firstRowCells = pageRows.First().FindElements(By.CssSelector("div._td_1boqa_77")).ToList();
                if (firstRowCells.Count < 5) break;

                int idxNumber = -1, idxFirstName = -1, idxLastName = -1, idxOfficialTime = -1, idxStatus = -1, idxCategory = -1;
                for (int i = 0; i < firstRowCells.Count; i++)
                {
                    string label = firstRowCells[i].GetAttribute("data-label") ?? "";
                    if (label.Contains("Номер")) idxNumber = i;
                    else if (label.Contains("Имя")) idxFirstName = i;
                    else if (label.Contains("Фамилия")) idxLastName = i;
                    else if (label.Contains("Время официальное")) idxOfficialTime = i;
                    else if (label.Contains("Статус")) idxStatus = i;
                    else if (label.Contains("Категория")) idxCategory = i;
                }

                // Fallback, если data-label не сработал (страховка)
                if (idxNumber == -1) idxNumber = 0;
                if (idxFirstName == -1) idxFirstName = 1;
                if (idxLastName == -1) idxLastName = 2;
                if (idxOfficialTime == -1) idxOfficialTime = (firstRowCells.Count == 8) ? 3 : 4;
                if (idxStatus == -1) idxStatus = (firstRowCells.Count == 8) ? 5 : 6;
                if (idxCategory == -1) idxCategory = (firstRowCells.Count == 8) ? 7 : 3;

                // --- Парсим все строки ---
                foreach (var row in pageRows)
                {
                    var cells = row.FindElements(By.CssSelector("div._td_1boqa_77")).ToList();
                    int maxIdx = new[] { idxNumber, idxFirstName, idxLastName, idxOfficialTime, idxStatus, idxCategory }.Max();
                    if (cells.Count <= maxIdx)
                        continue;

                    var record = new ParticipantRecord
                    {
                        Number = cells[idxNumber].Text.Trim(),
                        FirstName = cells[idxFirstName].Text.Trim(),
                        LastName = cells[idxLastName].Text.Trim(),
                        OfficialTime = NormalizeHeroleagueTime(idxOfficialTime >= 0 && idxOfficialTime < cells.Count ? cells[idxOfficialTime].Text.Trim() : ""),
                        Status = NormalizeStatus(idxStatus >= 0 && idxStatus < cells.Count ? cells[idxStatus].Text.Trim() : ""),
                        Category = idxCategory >= 0 && idxCategory < cells.Count ? cells[idxCategory].Text.Trim() : ""
                    };
                    allRecords.Add(record);
                }

                // --- Обработка пагинации ---
                if (!TryGoToNextPage(driver, currentPage, out int newPage))
                    break;
                currentPage = newPage;
            }

            return allRecords;
        }

        private string NormalizeHeroleagueTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return "";
            string cleaned = Regex.Replace(time, @"[^0-9:]", "");
            var parts = cleaned.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            if (parts.Length == 3)
                return $"{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}:{parts[2].PadLeft(2, '0')}.00";
            else if (parts.Length == 2)
                return $"00:{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}.00";
            else if (parts.Length == 1)
                return $"00:00:{parts[0].PadLeft(2, '0')}.00";
            else
                return cleaned;
        }

        private string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "finished";
            if (status.Equals("finished", StringComparison.OrdinalIgnoreCase)) return "finished";
            if (status.Equals("disqualified", StringComparison.OrdinalIgnoreCase)) return "DSQ";
            if (status.Equals("dns", StringComparison.OrdinalIgnoreCase)) return "DNS";
            if (status.Equals("dnf", StringComparison.OrdinalIgnoreCase)) return "DNF";
            return status;
        }

        private bool TryGoToNextPage(IWebDriver driver, int currentPage, out int nextPage)
        {
            nextPage = currentPage + 1;
            int targetPage = nextPage;

            var paginationContainers = driver.FindElements(By.CssSelector("[class*='pagination' i], [class*='Pagination' i]"));
            if (paginationContainers.Count == 0)
            {
                Console.WriteLine("  Контейнер пагинации не найден. Завершаем.");
                return false;
            }

            var container = paginationContainers.First();
            var allPageItems = container.FindElements(By.XPath(".//a | .//button"))
                                        .Where(el => int.TryParse(el.Text.Trim(), out _))
                                        .ToList();

            if (allPageItems.Count == 0)
            {
                allPageItems = driver.FindElements(By.XPath("//li[contains(@class,'page') or contains(@class,'Page')]//a | //button[contains(@class,'page') or contains(@class,'Page')]"))
                                     .Where(el => int.TryParse(el.Text.Trim(), out _))
                                     .ToList();
            }

            if (allPageItems.Count == 0)
            {
                Console.WriteLine("  Не найдены кнопки с номерами страниц. Завершаем.");
                return false;
            }

            var nextPageButton = allPageItems.FirstOrDefault(el => el.Text.Trim() == targetPage.ToString());
            if (nextPageButton != null && nextPageButton.Enabled && nextPageButton.Displayed)
            {
                Console.WriteLine($"  Найдена кнопка перехода на страницу {targetPage}. Кликаем.");
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPageButton);
                new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(drv => drv.FindElements(By.CssSelector("div._tr_1boqa_9")).Count > 0);
                return true;
            }

            var nextButton = container.FindElements(By.XPath(
                ".//a[contains(text(),'Вперед') or contains(text(),'Next') or contains(text(),'→') or contains(@aria-label,'Next')] | " +
                ".//button[contains(@aria-label,'Next')] | " +
                ".//a[contains(@rel,'next')]"))
                .FirstOrDefault(el => el.Enabled && el.Displayed);

            if (nextButton == null)
            {
                nextButton = container.FindElements(By.CssSelector("[class*='next' i]:not([class*='disabled']):not([class*='hidden']) a, [class*='next' i]:not([class*='disabled']):not([class*='hidden']) button"))
                                      .FirstOrDefault(el => el.Enabled && el.Displayed);
            }

            if (nextButton == null)
            {
                Console.WriteLine("  Не найдена кнопка для перехода на следующую страницу. Завершаем.");
                return false;
            }

            string btnText = nextButton.Text.Trim().ToLower();
            if (btnText.Contains("prev") || btnText.Contains("назад") || btnText.Contains("предыдущая"))
            {
                Console.WriteLine("  Найдена кнопка 'Назад', а не 'Вперед'. Завершаем.");
                return false;
            }

            Console.WriteLine("  Найдена кнопка 'Вперед'. Кликаем.");
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextButton);
            new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(drv => drv.FindElements(By.CssSelector("div._tr_1boqa_9")).Count > 0);
            return true;
        }
    }
}