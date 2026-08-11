using OpenQA.Selenium;
using SolidParcer.Models;
using System.Collections.Generic;

namespace SolidParcer.Interfaces
{
    public interface IPageParserService
    {
        string ExtractEventTitle(IWebDriver driver);
        string ExtractEventDate(IWebDriver driver);
        List<string> GetAvailableFormats(IWebDriver driver);
        List<ParticipantRecord> ParseFormat(IWebDriver driver, string format);
        List<ParticipantRecord> CollectAllRecords(IWebDriver driver);
        Dictionary<string, string> GetRaceNames(IWebDriver driver);
    }
}
