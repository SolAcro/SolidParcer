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

            var genderMap = NameGenderLoader.LoadGenderMap();
            if (genderMap != null)
            {
                string normalizedName = firstName.Trim().ToLowerInvariant();
                if (genderMap.TryGetValue(normalizedName, out string genderFromDict))
                {
                    return genderFromDict == "m" ? "М" : "Ж";
                }
                if (!string.IsNullOrEmpty(firstName) && genderMap.TryGetValue(firstName.ToLowerInvariant(), out string genderFromDictLatn))
                {
                    return genderFromDictLatn == "m" ? "М" : "Ж";
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
            //  (HH:mm:ss.ff)
            if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss\.ff", null, out var ts))
                return ts;
            //  (HH:mm:ss)
            if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss", null, out var ts2))
                return ts2;
            // Общий парсинг
            if (TimeSpan.TryParse(timeStr, out var ts3))
                return ts3;
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
