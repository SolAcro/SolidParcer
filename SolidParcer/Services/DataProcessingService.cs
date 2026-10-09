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
    public class DataProcessingService : IDataProcessingService
    {
        public void AssignGender(List<ParticipantRecord> records)
        {
            foreach (var rec in records)
            {
                string detected = DetectGender(rec.FirstName, rec.LastName); 

                if (!string.IsNullOrEmpty(detected))
                {
                    rec.Gender = detected;
                }
                else
                {
                    rec.Gender = "";
                }
            }
        }

        private string DetectGenderByHeuristics(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName)) return "Неизвестно";
            var maleNames = new HashSet<string> {
                "Илья", "Никита", "Саша", "Женя", "Валя", "Слава", "Дима", "Миша",
                "Ваня", "Даня", "Сеня", "Толя", "Костя", "Паша", "Гриша", "Петя",
                "Федя", "Витя", "Данила"
            };
            if (maleNames.Contains(firstName)) return "М";
            if (firstName.EndsWith("а") || firstName.EndsWith("я")) return "Ж";
            return null;
        }

        private string DetectGender(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                return null;

            // Исключения для имен, которые неправильно определены в датасетах
            var femaleExceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Катя"
            };
            if (femaleExceptions.Contains(firstName))
                return "Ж";

            string normalizedName = firstName.Trim().ToLowerInvariant();
            var genderMap = NameGenderLoader.LoadGenderMap();
            if (genderMap != null)
            {
                if (genderMap.TryGetValue(normalizedName, out string genderFromDict))
                {
                    return genderFromDict;
                }
            }

            return DetectGenderByHeuristics(firstName);
        }

        public void CalculatePlaces(List<ParticipantRecord> records)
        {
            var finished = records
                .Where(r => r.Status == "finished" && !string.IsNullOrEmpty(r.OfficialTime))
                .Select(r => new { Record = r, Time = TryParseTime(r.OfficialTime) })
                .Where(x => x.Time.HasValue)
                .OrderBy(x => x.Time.Value)
                .ToList();

            int place = 1;
            foreach (var item in finished)
            {
                item.Record.Place = place.ToString();
                place++;
            }
        }

        private TimeSpan? TryParseTime(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return null;

            string cleaned = timeStr.Trim();
            var parts = cleaned.Split(':');

            // HH:MM:SS.ms  (основной формат после NormalizeTime)
            if (parts.Length == 3)
            {
                string secPart = parts[2];
                string msPart = "0";

                if (secPart.Contains('.'))
                {
                    var s = secPart.Split('.');
                    secPart = s[0];
                    msPart = s[1];
                }

                if (int.TryParse(parts[0], out int h) &&
                    int.TryParse(parts[1], out int m) &&
                    int.TryParse(secPart, out int s2) &&
                    int.TryParse(msPart.PadRight(3, '0').Substring(0, 3), out int ms))
                {
                    return new TimeSpan(0, h, m, s2, ms);
                }
            }

            // MM:SS  (фолбэк, на всякий случай)
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int mm) &&
                int.TryParse(parts[1], out int ss))
            {
                return new TimeSpan(0, mm, ss);
            }

            // Общий фолбэк
            if (TimeSpan.TryParse(cleaned, out var ts))
                return ts;

            return null;
        }

        public List<ParticipantRecord> RemoveDuplicates(List<ParticipantRecord> records)
        {
            return records
                .GroupBy(r => $"{r.Number}|{r.FirstName}|{r.LastName}")
                .Select(g => g.First())
                .ToList();
        }
    }
}
