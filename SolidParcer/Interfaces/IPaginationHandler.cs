using OpenQA.Selenium;
using SolidParcer.Models;
using System.Collections.Generic;

namespace SolidParcer.Interfaces
{
    public interface IPaginationHandler
    {
        List<ParticipantRecord> CollectRowsWithPagination(IWebDriver driver);
    }
}
