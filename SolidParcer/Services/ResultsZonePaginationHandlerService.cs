using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SolidParcer.Services
{
    public class ResultsZonePaginationHandlerService : IPaginationHandler
    {
        public List<ParticipantRecord> CollectRowsWithPagination(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();
            int currentPage = 1;
            const int maxPages = 50;

            while (currentPage <= maxPages)
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(drv => drv.FindElements(By.CssSelector("table tbody tr")).Count > 0);

                var rows = driver.FindElements(By.CssSelector("table tbody tr"));
                Console.WriteLine($"  Страница {currentPage}: собрано строк {rows.Count}");

                foreach (var row in rows)
                {
                    var cells = row.FindElements(By.TagName("td")).ToList();
                    if (cells.Count < 5) continue;

                    var record = new ParticipantRecord
                    {
                        Number = cells[0].Text.Trim(),
                        FirstName = cells[1].Text.Trim(),
                        LastName = cells[2].Text.Trim(),
                        OfficialTime = cells[3].Text.Trim(),
                        Status = cells[4].Text.Trim(),
                        Gender = cells.Count > 5 ? cells[5].Text.Trim() : "",
                        Category = cells.Count > 6 ? cells[6].Text.Trim() : ""
                    };
                    allRecords.Add(record);
                }

                var nextButton = driver.FindElements(By.CssSelector("a.next, li.next a, button[aria-label='Next'], a[rel='next']"))
                                       .FirstOrDefault(el => el.Enabled && el.Displayed);

                if (nextButton == null)
                {
                    Console.WriteLine("  Достигнут конец списка.");
                    break;
                }

                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextButton);
                Thread.Sleep(2000);
                currentPage++;
            }

            return allRecords;
        }
    }
}