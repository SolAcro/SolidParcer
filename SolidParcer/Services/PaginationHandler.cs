using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SolidParcer.Services
{
    public class PaginationHandler : IPaginationHandler
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

                // Прокрутка для подгрузки всех строк (динамическое ожидание)
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
                Console.WriteLine($"  Собрано строк на странице {currentPage}: {pageRows.Count}");

                // Парсим строки в записи
                foreach (var row in pageRows)
                {
                    var cells = row.FindElements(By.CssSelector("div._td_1boqa_77")).ToList();
                    if (cells.Count < 7) continue;

                    var record = new ParticipantRecord
                    {
                        Number = cells[0].Text.Trim(),
                        FirstName = cells[1].Text.Trim(),
                        LastName = cells[2].Text.Trim(),
                        OfficialTime = NormalizeHeroleagueTime(cells[3].Text.Trim()),
                        Status = cells[6].Text.Trim()
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

        private bool TryGoToNextPage(IWebDriver driver, int currentPage, out int nextPage)
        {
            nextPage = currentPage + 1;
            int targetPage = nextPage; // !!! Копируем значение для использования в лямбде

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

            // Поиск кнопки с номером targetPage
            var nextPageButton = allPageItems.FirstOrDefault(el => el.Text.Trim() == targetPage.ToString());
            if (nextPageButton != null && nextPageButton.Enabled && nextPageButton.Displayed)
            {
                Console.WriteLine($"  Найдена кнопка перехода на страницу {targetPage}. Кликаем.");
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPageButton);
                new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(drv => drv.FindElements(By.CssSelector("div._tr_1boqa_9")).Count > 0);
                return true;
            }

            // Запасной вариант – кнопка "Вперед"
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

        private string NormalizeHeroleagueTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return "";
            // Убираем всё, кроме цифр и двоеточий
            string cleaned = Regex.Replace(time, @"[^0-9:]", "");
            var parts = cleaned.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3)
            {
                // часы:минуты:секунды
                return $"{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}:{parts[2].PadLeft(2, '0')}.00";
            }
            else if (parts.Length == 2)
            {
                // минуты:секунды
                return $"00:{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}.00";
            }
            else if (parts.Length == 1)
            {
                // только секунды
                return $"00:00:{parts[0].PadLeft(2, '0')}.00";
            }
            else
            {
                return cleaned; // fallback
            }
        }
    }
}
