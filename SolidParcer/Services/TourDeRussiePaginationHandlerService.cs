using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SolidParcer.Services
{
    public class TourDeRussiePaginationHandlerService : IPaginationHandler
    {
        public List<ParticipantRecord> CollectRowsWithPagination(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            int currentPage = 1;
            const int maxPages = 50;

            while (currentPage <= maxPages)
            {
                try
                {
                    // Ожидаем появления хотя бы одной строки в таблице
                    new WebDriverWait(driver, TimeSpan.FromSeconds(15))
                        .Until(drv => drv.FindElements(By.CssSelector("table tbody tr")).Count > 0);

                    var rows = driver.FindElements(By.CssSelector("table tbody tr"));
                    Console.WriteLine($"  Страница {currentPage}: собрано строк {rows.Count}");

                    foreach (var row in rows)
                    {
                        var cells = row.FindElements(By.TagName("td")).ToList();
                        if (cells.Count < 4) continue;

                        string placeText = cells[0].Text.Trim();
                        string number = cells[1].Text.Trim();
                        string fullName = cells[2].Text.Trim();
                        string time = cells[3].Text.Trim();
                        string team = cells.Count > 4 ? cells[4].Text.Trim() : "";

                        // Разбор ФИО
                        string firstName = "", lastName = "";
                        var nameParts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (nameParts.Length >= 2)
                        {
                            lastName = nameParts[0];
                            firstName = string.Join(" ", nameParts.Skip(1));
                        }
                        else if (nameParts.Length == 1)
                        {
                            firstName = nameParts[0];
                        }

                        // Статус
                        string status = "finished";
                        int placeNum;
                        if (!int.TryParse(placeText, out placeNum))
                        {
                            status = placeText; // может быть DNF, DNS, DSQ
                            placeText = "";
                        }

                        var record = new ParticipantRecord
                        {
                            Number = number,
                            FirstName = firstName,
                            LastName = lastName,
                            OfficialTime = time,
                            Status = status,
                            Place = placeText
                        };
                        allRecords.Add(record);
                    }

                    // Поиск кнопки "След. →"
                    var nextButton = driver.FindElements(By.CssSelector("div.flex.items-center.gap-2.mt-6.text-sm.justify-center button"))
                                           .FirstOrDefault(el => el.Text.Trim() == "След. →" && el.Enabled && el.Displayed);

                    if (nextButton == null)
                    {
                        Console.WriteLine("  Достигнут конец списка.");
                        break;
                    }

                    // Проверка, не отключена ли кнопка (disabled или класс disabled)
                    string disabled = nextButton.GetAttribute("disabled");
                    string className = nextButton.GetAttribute("class");
                    if (disabled != null || (className != null && className.Contains("disabled")))
                    {
                        Console.WriteLine("  Кнопка 'След.' недоступна.");
                        break;
                    }

                    // Клик и ожидание обновления таблицы (появление новых строк или изменение содержимого)
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextButton);

                    // Ожидаем, что количество строк изменится или появится новая страница
                    new WebDriverWait(driver, TimeSpan.FromSeconds(15))
                        .Until(drv => drv.FindElements(By.CssSelector("table tbody tr")).Count > 0);

                    currentPage++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при сборе страницы {currentPage}: {ex.Message}");
                    break;
                }
            }
            return allRecords;
        }
    }
}