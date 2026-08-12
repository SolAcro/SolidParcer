using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SolidParcer.Services
{
    public static class NameGenderLoader
    {
        private static Dictionary<string, string> _genderMap;

        // Загружаем при первом обращении (ленивая инициализация)
        public static Dictionary<string, string> LoadGenderMap(string csvPath = null)
        {
            if (_genderMap != null) return _genderMap;

            _genderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Русские имена
            LoadRussianNames(_genderMap);

            // 2. Чувашские имена
            LoadChuvashNames(_genderMap);

            // 3. Международные имена
            LoadInternationalNames(_genderMap);

            Console.WriteLine($"Загружено всего имён: {_genderMap.Count}");
            return _genderMap;
        }

        private static void LoadInternationalNames(Dictionary<string, string> map)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "international_names.csv");
            if (!File.Exists(path))
            {
                Console.WriteLine($"Файл международных имён не найден: {path}");
                return;
            }

            var lines = File.ReadAllLines(path).Skip(1);
            int loaded = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                string nameLatin = parts[0].Trim();
                string nameCyrillic = parts[1].Trim();
                string gender = parts[2].Trim();

                if (string.IsNullOrEmpty(nameLatin) && string.IsNullOrEmpty(nameCyrillic)) continue;

                string mappedGender = gender == "male" || gender == "M" || gender == "m" ? "М" :
                                      gender == "female" || gender == "F" || gender == "f" ? "Ж" :
                                      gender == "unknown" ? null : null;

                if (mappedGender == null) continue;

                if (!string.IsNullOrEmpty(nameLatin) && !map.ContainsKey(nameLatin))
                {
                    map[nameLatin] = mappedGender;
                    loaded++;
                }

                if (!string.IsNullOrEmpty(nameCyrillic) && !map.ContainsKey(nameCyrillic))
                {
                    map[nameCyrillic] = mappedGender;
                    loaded++;
                }
            }
            Console.WriteLine($"Загружено международных имён: {loaded}");
        }
        private static void LoadChuvashNames(Dictionary<string, string> map)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chuvash_names.jsonl");
            if (!File.Exists(path))
            {
                Console.WriteLine($"Файл чувашских имён не найден: {path}");
                return;
            }

            int loaded = 0;
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    string name = root.GetProperty("name").GetString()?.Trim();
                    string gender = root.GetProperty("gender").GetString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(gender)) continue;

                    // m → М, f → Ж, u → Неизвестно
                    string mappedGender = gender == "m" ? "М" :
                                          gender == "f" ? "Ж" :
                                          gender == "u" ? null : null;

                    if (mappedGender == null) continue;

                    if (!map.ContainsKey(name))
                    {
                        map[name] = mappedGender;
                        loaded++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка парсинга строки чувашских имён: {ex.Message}");
                }
            }
            Console.WriteLine($"Загружено чувашских имён: {loaded}");
        }

        private static void LoadRussianNames(Dictionary<string, string> map)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "russian_names.csv");
            if (!File.Exists(path))
            {
                Console.WriteLine($"Файл русских имён не найден: {path}");
                return;
            }

            var lines = File.ReadAllLines(path).Skip(1);
            int loaded = 0;
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                string name = parts[0].Trim();
                string gender = parts[2].Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(gender)) continue;

                string mappedGender = gender == "m" ? "М" : "Ж";
                string nameLower = name.ToLowerInvariant();

                if (!map.ContainsKey(nameLower))
                {
                    map[nameLower] = mappedGender;
                    loaded++;
                }

                string nameLatn = parts[1].Trim();
                if (!string.IsNullOrEmpty(nameLatn))
                {
                    string latnLower = nameLatn.ToLowerInvariant();
                    if (!map.ContainsKey(latnLower))
                    {
                        map[latnLower] = mappedGender;
                        loaded++;
                    }
                }
            }
            Console.WriteLine($"Загружено русских имён: {loaded}");
        }
    }
}
