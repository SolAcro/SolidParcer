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
    public class ResultsZoneTeamPageParserService : IPageParserService
    {
        public string ExtractEventTitle(IWebDriver driver)
        {
            try
            {
                var h2 = driver.FindElement(By.CssSelector("main h2"));
                string title = h2.Text;
                foreach (var small in h2.FindElements(By.CssSelector("small")))
                    title = title.Replace(small.Text, "").Trim();
                return string.IsNullOrEmpty(title) ? null : title;
            }
            catch { return null; }
        }

        public string ExtractEventDate(IWebDriver driver)
        {
            try
            {
                var js = ((IJavaScriptExecutor)driver).ExecuteScript(
                    "if (window.general && window.general.event) return window.general.event.date_start; return null;");
                if (js != null) return js.ToString();

                string body = driver.FindElement(By.TagName("body")).Text;
                var m = Regex.Match(body, @"\b\d{2}\.\d{2}\.\d{4}\b");
                if (m.Success) return m.Value;
            }
            catch { }
            return null;
        }

        public List<string> GetAvailableFormats(IWebDriver driver)
        {
            var result = new List<string>();
            foreach (var s in driver.FindElements(By.CssSelector("section.rz-report-section h2.rz-public-report-section-title")))
            {
                string text = s.Text.Trim();
                if (!string.IsNullOrEmpty(text)) result.Add(text);
            }
            return result;
        }

        public List<ParticipantRecord> ParseFormat(IWebDriver driver, string format)
            => new List<ParticipantRecord>();

        public List<ParticipantRecord> CollectAllRecords(IWebDriver driver)
        {
            var allRecords = new List<ParticipantRecord>();

            // --- 1. Собираем ссылки на все команды с их местом ---
            var teams = new List<(string Section, string Rank, string TeamName, string Url)>();
            foreach (var section in driver.FindElements(By.CssSelector("section.rz-report-section")))
            {
                var titleEl = section.FindElements(By.CssSelector("h2.rz-public-report-section-title")).FirstOrDefault();
                string sectionTitle = titleEl?.Text.Trim() ?? "Команды";

                foreach (var row in section.FindElements(By.CssSelector("tbody tr")))
                {
                    var rankCell = row.FindElements(By.CssSelector("td.rank")).FirstOrDefault();
                    var link = row.FindElements(By.CssSelector("th.primary a")).FirstOrDefault();
                    if (link == null) continue;

                    string rank = rankCell?.Text.Trim() ?? "";
                    string teamName = link.Text.Trim();
                    string href = link.GetAttribute("href");
                    if (string.IsNullOrEmpty(href)) continue;

                    href = href.Replace("&amp;", "&");
                    if (href.StartsWith("/"))
                    {
                        var uri = new Uri(driver.Url);
                        href = $"{uri.Scheme}://{uri.Host}{href}";
                    }
                    teams.Add((sectionTitle, rank, teamName, href));
                }
            }
            Console.WriteLine($"  Команд найдено: {teams.Count}");

            string originalUrl = driver.Url;

            // --- 2. По каждой команде — своя страница с участниками ---
            foreach (var team in teams)
            {
                Console.WriteLine($"  Команда: {team.TeamName} [{team.Section}] место {team.Rank}");
                try
                {
                    driver.Navigate().GoToUrl(team.Url);
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
                    wait.Until(d => d.FindElements(By.CssSelector("tbody#heats_table tr")).Count > 0);

                    var records = ParseTeamMembers(driver, team.TeamName, team.Section, team.Rank);
                    Console.WriteLine($"    Участников: {records.Count}");
                    allRecords.AddRange(records);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Ошибка: {ex.Message}");
                }
            }

            try { driver.Navigate().GoToUrl(originalUrl); } catch { }

            return allRecords;
        }

        private List<ParticipantRecord> ParseTeamMembers(
            IWebDriver driver, string teamName, string section, string teamRank)
        {
            var records = new List<ParticipantRecord>();
            foreach (var row in driver.FindElements(By.CssSelector("tbody#heats_table tr")))
            {
                try
                {
                    var cells = row.FindElements(By.TagName("td"));
                    if (cells.Count < 8) continue;

                    string number = cells[3].Text.Trim();

                    string gender = "";
                    foreach (var icon in cells[4].FindElements(By.CssSelector("i")))
                    {
                        string cls = icon.GetAttribute("class") ?? "";
                        if (cls.Contains("fa-mars")) { gender = "М"; break; }
                        if (cls.Contains("fa-venus")) { gender = "Ж"; break; }
                    }

                    var strong = cells[5].FindElements(By.CssSelector("a strong")).FirstOrDefault();
                    string fullName = strong?.Text.Trim() ?? cells[5].Text.Trim();

                    string lastName = "", firstName = "";
                    var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        lastName = parts[0];
                        firstName = string.Join(" ", parts.Skip(1));
                    }
                    else if (parts.Length == 1) firstName = parts[0];

                    string rawTime = cells[7].Text.Trim();
                    string time = rawTime, status = "finished";
                    if (rawTime == "DNF" || rawTime == "DNS" || rawTime == "DSQ")
                    {
                        status = rawTime;
                        time = "";
                    }

                    records.Add(new ParticipantRecord
                    {
                        Number = number,
                        FirstName = firstName,
                        LastName = lastName,
                        OfficialTime = time,
                        Status = status,
                        Format = section,         // «Мужские команды» / «Женские команды»
                        Category = teamName,      // команда (уйдёт в Комментарий)
                        Place = teamRank,         // одно место на всю команду
                        Gender = gender
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      Строка пропущена: {ex.Message}");
                }
            }
            return records;
        }

        public Dictionary<string, string> GetRaceNames(IWebDriver driver)
        {
            var result = new Dictionary<string, string>();
            foreach (var s in driver.FindElements(By.CssSelector("section.rz-report-section h2.rz-public-report-section-title")))
            {
                string text = s.Text.Trim();
                if (!string.IsNullOrEmpty(text)) result[text] = text;
            }
            return result;
        }
    }
}