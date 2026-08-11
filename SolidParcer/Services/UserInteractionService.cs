using SolidParcer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolidParcer.Services
{
    public class UserInteractionService : IUserInteractionService
    {
        public string AskUrl()
        {
            Console.Write("Введите полную ссылку на страницу с результатами: ");
            return Console.ReadLine()?.Trim();
        }

        public string AskTitle(string defaultTitle)
        {
            Console.Write($"Не удалось определить название. Введите его вручную (или нажмите Enter для '{defaultTitle}'): ");
            string input = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(input) ? defaultTitle : input;
        }

        public string AskDate(string defaultDate)
        {
            Console.Write($"Не удалось определить дату. Введите её в формате ДД.ММ.ГГГГ (или нажмите Enter для '{defaultDate}'): ");
            string input = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(input) ? defaultDate : input;
        }

        public void ShowMessage(string message) => Console.WriteLine(message);
    }
}
