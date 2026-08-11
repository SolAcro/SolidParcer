using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SolidParcer.Interfaces;
using System;

namespace SolidParcer.Services
{
    public class WebDriverService : IWebDriverService, IDisposable
    {
        private IWebDriver _driver;
        private ChromeDriverService _service;

        public IWebDriver CreateDriver()
        {
            string driverPath = AppDomain.CurrentDomain.BaseDirectory;
            _service = ChromeDriverService.CreateDefaultService(driverPath);
            _service.SuppressInitialDiagnosticInformation = true;

            var options = new ChromeOptions();
            // options.AddArgument("--headless");

            _driver = new ChromeDriver(_service, options);
            return _driver;
        }

        public void Dispose()
        {
            if (_driver != null)
            {
                _driver.Quit();
                _driver.Dispose();
                _driver = null;
            }
            if (_service != null)
            {
                _service.Dispose();
                _service = null;
            }
        }
    }
}