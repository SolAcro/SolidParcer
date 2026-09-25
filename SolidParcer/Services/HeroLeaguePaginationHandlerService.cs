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
        // Префиксы классов (хэш после префикса меняется между сборками фронта)
        private const string RowSelector = "div[role='row']";
        private const string CellSelector = "[data-label]";

        public List<ParticipantRecord> CollectRowsWithPagination(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            int currentPage = 1;
            const int maxPages = 50;
            string prevFirstNumber = null;

            while (currentPage <= maxPages)
            {
                if (!driver.Url.Contains("/results/"))
                {
                    Console.WriteLine("  Переход на другую страницу (не results). Завершаем.");
                    break;
                }

                Console.WriteLine($"  Ожидаем загрузки данных... (страница {currentPage})");
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                wait.Until(drv => GetDataRows(drv).Count > 0);

                // Прокрутка для подгрузки всех строк
                int lastCount = 0, stableCount = 0;
                while (stableCount < 3)
                {
                    var rows = GetDataRows(driver);
                    int currentCount = rows.Count;
                    if (currentCount == lastCount) stableCount++;
                    else stableCount = 0;
                    lastCount = currentCount;
                    Console.Write($"    Текущее количество строк: {currentCount}\r");
                    if (currentCount > 0)
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", rows.Last());
                    Thread.Sleep(200);
                }

                var pageRows = GetDataRows(driver);
                if (!pageRows.Any())
                {
                    Console.WriteLine("\n  Строки данных не найдены.");
                    break;
                }

                Console.WriteLine($"\n  Собрано строк на странице {currentPage}: {pageRows.Count}");

                // Проверка — не зациклились ли на одной и той же странице
                string firstNumber = pageRows.First()
                    .FindElements(By.CssSelector(CellSelector)).FirstOrDefault()?.Text?.Trim();
                if (firstNumber != null && firstNumber == prevFirstNumber)
                {
                    Console.WriteLine("  Первая строка не изменилась — вероятно, переход не сработал. Завершаем.");
                    break;
                }
                prevFirstNumber = firstNumber;

                // --- Определяем индексы колонок по data-label первой строки ---
                var firstRowCells = pageRows.First().FindElements(By.CssSelector(CellSelector)).ToList();
                if (firstRowCells.Count < 5)
                {
                    Console.WriteLine($"  В первой строке только {firstRowCells.Count} ячеек — пропускаем.");
                    break;
                }

                int idxNumber = -1, idxFirstName = -1, idxLastName = -1,
                    idxNetTime = -1, idxOfficialTime = -1,
                    idxStatus = -1, idxPlace = -1, idxCategory = -1;

                for (int i = 0; i < firstRowCells.Count; i++)
                {
                    string label = (firstRowCells[i].GetAttribute("data-label") ?? "").ToLowerInvariant();

                    if (label.Contains("номер")) idxNumber = i;
                    else if (label.Contains("имя")) idxFirstName = i;
                    else if (label.Contains("фамилия")) idxLastName = i;
                    else if (label.Contains("время") && label.Contains("чистое")) idxNetTime = i;
                    else if (label.Contains("время") && label.Contains("официальн")) idxOfficialTime = i;
                    else if (label.Contains("статус")) idxStatus = i;
                    else if (label.Contains("место")) idxPlace = i;
                    else if (label.Contains("категория")) idxCategory = i;
                }

                // Если «чистое» не нашли — используем «официальное»
                if (idxNetTime == -1)
                {
                    if (idxOfficialTime != -1)
                    {
                        idxNetTime = idxOfficialTime;
                        Console.WriteLine("  Внимание: колонка «Время чистое» не найдена, используется «Время официальное».");
                    }
                    else
                    {
                        // Последний фолбэк — берём 5-ю колонку (0-based = 4)
                        idxNetTime = firstRowCells.Count > 4 ? 4 : firstRowCells.Count - 1;
                        Console.WriteLine("  Внимание: колонки времени не найдены, берём колонку по индексу.");
                    }
                }

                if (idxNumber == -1) idxNumber = 0;
                if (idxFirstName == -1) idxFirstName = 1;
                if (idxLastName == -1) idxLastName = 2;
                if (idxStatus == -1) idxStatus = 5;
                if (idxCategory == -1) idxCategory = firstRowCells.Count - 1;

                Console.WriteLine($"  Индексы: Номер={idxNumber}, Имя={idxFirstName}, Фамилия={idxLastName}, " +
                                  $"ЧистоеВремя={idxNetTime}, Статус={idxStatus}, Категория={idxCategory}");

                // --- Парсим все строки ---
                foreach (var row in pageRows)
                {
                    var cells = row.FindElements(By.CssSelector(CellSelector)).ToList();
                    int maxIdx = new[] { idxNumber, idxFirstName, idxLastName, idxNetTime, idxStatus, idxCategory }.Max();
                    if (cells.Count <= maxIdx)
                        continue;

                    var record = new ParticipantRecord
                    {
                        Number = cells[idxNumber].Text.Trim(),
                        FirstName = cells[idxFirstName].Text.Trim(),
                        LastName = cells[idxLastName].Text.Trim(),
                        OfficialTime = NormalizeHeroleagueTime(
                            idxNetTime >= 0 && idxNetTime < cells.Count ? cells[idxNetTime].Text.Trim() : ""),
                        Status = NormalizeStatus(
                            idxStatus >= 0 && idxStatus < cells.Count ? cells[idxStatus].Text.Trim() : ""),
                        Category = idxCategory >= 0 && idxCategory < cells.Count ? cells[idxCategory].Text.Trim() : "",
                        Place = idxPlace >= 0 && idxPlace < cells.Count ? cells[idxPlace].Text.Trim() : ""
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

        /// <summary>
        /// Возвращает только строки данных (не заголовок): div[role=row], внутри которых есть [data-label].
        /// </summary>
        private List<IWebElement> GetDataRows(IWebDriver driver)
        {
            return driver.FindElements(By.CssSelector(RowSelector))
                         .Where(r => r.FindElements(By.CssSelector(CellSelector)).Count > 0)
                         .ToList();
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
            if (status.Equals("retired", StringComparison.OrdinalIgnoreCase)) return "DNF";
            if (status.Equals("quarantine", StringComparison.OrdinalIgnoreCase)) return "DSQ";
            return status;
        }

        private bool TryGoToNextPage(IWebDriver driver, int currentPage, out int nextPage)
        {
            nextPage = currentPage + 1;
            int targetPage = nextPage;

            try
            {
                // Запоминаем номер первой строки — это наш маркер смены страницы
                string beforeFirstNumber = GetFirstRowNumber(driver);

                // 1. Ищем прямую ссылку на целевую страницу (номер "2", "3", ...)
                IWebElement nextLink = null;
                var allPageLinks = driver.FindElements(By.CssSelector("ul[class*='_pagination_'] a"));

                foreach (var link in allPageLinks)
                {
                    if (link.Displayed && link.Enabled && link.Text.Trim() == targetPage.ToString())
                    {
                        nextLink = link;
                        break;
                    }
                }

                // 2. Если такой нет — берём «Next» (aria-label='Next' или текст «»)
                if (nextLink == null)
                {
                    nextLink = allPageLinks.FirstOrDefault(el =>
                        el.Displayed && el.Enabled &&
                        (((el.GetAttribute("aria-label") ?? "").ToLowerInvariant().Contains("next")) ||
                         el.Text.Contains("»")));
                }

                if (nextLink == null)
                {
                    Console.WriteLine("  Кнопка перехода на следующую страницу не найдена. Завершаем.");
                    return false;
                }

                Console.WriteLine($"  URL до клика: {driver.Url}");
                Console.WriteLine($"  Активная страница до клика: {GetActivePageText(driver)}");

                // 3. Нативный клик Selenium — isTrusted=true, SPA-обработчики его видят
                try
                {
                    nextLink.Click();
                }
                catch (Exception)
                {
                    // Если клик перехвачен оверлеем — пробуем через JS, БЕЗ preventDefault
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextLink);
                }

                // 4. Ждём смены первой строки (более надёжно, чем класс активной страницы)
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
                try
                {
                    wait.Until(drv =>
                    {
                        string nowFirstNumber = GetFirstRowNumber(drv);
                        return nowFirstNumber != null && nowFirstNumber != beforeFirstNumber;
                    });
                }
                catch (WebDriverTimeoutException)
                {
                    Console.WriteLine($"  URL после клика: {driver.Url}");
                    Console.WriteLine($"  Активная страница после клика: {GetActivePageText(driver)}");
                    Console.WriteLine("  Данные не изменились после клика. Завершаем.");
                    return false;
                }

                Console.WriteLine($"  Успешно перешли на страницу {targetPage}.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ошибка при переходе на следующую страницу: {ex.Message}");
                return false;
            }
        }

        private string GetFirstRowNumber(IWebDriver driver)
        {
            try
            {
                var rows = GetDataRows(driver);
                if (rows.Count == 0) return null;
                var cells = rows.First().FindElements(By.CssSelector(CellSelector));
                return cells.Count > 0 ? cells[0].Text.Trim() : null;
            }
            catch { return null; }
        }

        private string GetActivePageText(IWebDriver driver)
        {
            try
            {
                var active = driver.FindElements(By.CssSelector("li[class*='_pageItemActive_'] a"))
                                   .FirstOrDefault();
                return active?.Text.Trim() ?? "(не найдено)";
            }
            catch { return "(ошибка)"; }
        }
    }
}