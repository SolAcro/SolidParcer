using OpenQA.Selenium;

namespace SolidParcer.Interfaces
{
    public interface IWebDriverService
    {
        IWebDriver CreateDriver();
        void Dispose();
    }
}
