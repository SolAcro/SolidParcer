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

            if (string.IsNullOrEmpty(csvPath))
                csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "russian_names.csv");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"Файл датасета не найден: {csvPath}. Будет использована встроенная эвристика.");
                return null;
            }

            _genderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(csvPath);

            // Пропускаем заголовок (первая строка)
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                string name = parts[0].Trim();   // name_cyrl
                string gender = parts[2].Trim(); // gender (m/f)
                if (string.IsNullOrEmpty(name)) continue;

                // Сохраняем в двух вариантах: оригинальный регистр и в нижнем
                string nameLower = name.ToLowerInvariant();
                if (!_genderMap.ContainsKey(nameLower))
                {
                    _genderMap[nameLower] = gender;
                }
                // Если есть транслитерация (name_latn), можно добавить и её
                string nameLatn = parts[1].Trim();
                if (!string.IsNullOrEmpty(nameLatn))
                {
                    string latnLower = nameLatn.ToLowerInvariant();
                    if (!_genderMap.ContainsKey(latnLower))
                        _genderMap[latnLower] = gender;
                }
            }
            Console.WriteLine($"Загружено имён из датасета: {_genderMap.Count}");
            return _genderMap;
        }
    }
}