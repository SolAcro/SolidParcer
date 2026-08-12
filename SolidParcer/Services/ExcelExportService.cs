using OfficeOpenXml;
using SolidParcer.Interfaces;
using SolidParcer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolidParcer.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly string _templatePath;
        private readonly IDataProcessingService _dataProcessing;

        public ExcelExportService(IDataProcessingService dataProcessing)
        {
            _dataProcessing = dataProcessing;
            _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "template.xlsx");
        }

        public void Export(List<ParticipantRecord> allRecords, string eventTitle, string eventDate, Dictionary<string, string> raceNames)
        {
            _dataProcessing.AssignGender(allRecords);
            _dataProcessing.CalculatePlaces(allRecords);
            var uniqueRecords = _dataProcessing.RemoveDuplicates(allRecords);

            string safeTitle = string.Join("_", eventTitle.Split(Path.GetInvalidFileNameChars()));
            string safeDate = string.Join("_", eventDate.Split(Path.GetInvalidFileNameChars()));
            string rootFolder = $"Results_{safeTitle}_{safeDate}";

            int counter = 1;
            string originalFolder = rootFolder;
            while (Directory.Exists(rootFolder))
                rootFolder = $"{originalFolder}_{counter++}";

            Directory.CreateDirectory(rootFolder);
            string mixedFolder = Path.Combine(rootFolder, "Смешанные");
            string genderFolder = Path.Combine(rootFolder, "По полу");
            string categoryFolder = Path.Combine(rootFolder, "По категориям");
            Directory.CreateDirectory(mixedFolder);
            Directory.CreateDirectory(genderFolder);

            var formatGroups = uniqueRecords.GroupBy(r => r.Format);

            foreach (var group in formatGroups)
            {
                string format = group.Key;
                var records = group.ToList();

                string raceName = raceNames != null && raceNames.ContainsKey(format) ? raceNames[format] : format;
                string safeRaceName = string.Join("_", raceName.Split(Path.GetInvalidFileNameChars()));

                string mixedPath = Path.Combine(mixedFolder, $"{safeRaceName}.xlsx");
                WriteExcelFile(records, mixedPath, $"{eventTitle} - {raceName}", eventDate, includeGender: true);

                var genderGroups = records.GroupBy(r => r.Gender).Where(g => g.Key != "Неизвестно" && !string.IsNullOrEmpty(g.Key));
                foreach (var gGroup in genderGroups)
                {
                    string gender = gGroup.Key;
                    var genderRecords = gGroup.ToList();
                    string genderPath = Path.Combine(genderFolder, $"{safeRaceName}-{gender}.xlsx");
                    WriteExcelFile(genderRecords, genderPath, $"{eventTitle} - {raceName} - {gender}", eventDate, includeGender: true);
                }

                var hasCategories = records.Any(r => !string.IsNullOrEmpty(r.Category));
                if (hasCategories)
                {
                    if (!Directory.Exists(categoryFolder))
                        Directory.CreateDirectory(categoryFolder);

                    var categoryGroups = records.GroupBy(r => r.Category).Where(g => !string.IsNullOrEmpty(g.Key));
                    foreach (var catGroup in categoryGroups)
                    {
                        string category = catGroup.Key;
                        var categoryRecords = catGroup.ToList();
                        string safeCategory = string.Join("_", category.Split(Path.GetInvalidFileNameChars()));
                        string categoryPath = Path.Combine(categoryFolder, $"{safeRaceName}-{safeCategory}.xlsx");
                        WriteExcelFile(categoryRecords, categoryPath, $"{eventTitle} - {raceName} - {category}", eventDate, includeGender: true);
                    }
                }
            }

            Console.WriteLine($"Все файлы сохранены в папке: {rootFolder}");
        }

        private void WriteExcelFile(List<ParticipantRecord> records, string filePath, string title, string date, bool includeGender)
        {

            if (!File.Exists(_templatePath))
            {
                Console.WriteLine($"Шаблон {_templatePath} не найден. Используется встроенное форматирование.");
                WriteExcelFileFallback(records, filePath, title, date, includeGender);
                return;
            }

            File.Copy(_templatePath, filePath, true);
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                worksheet.Cells[1, 1].Value = title;
                worksheet.Cells[2, 1].Value = date;

                int startRow = 7;
                int lastRow = worksheet.Dimension?.Rows ?? startRow;
                if (lastRow >= startRow)
                    worksheet.DeleteRow(startRow, lastRow - startRow + 1);

                int row = startRow;
                foreach (var rec in records)
                {
                    string statusDisplay = rec.Status switch
                    {
                        "finished" or "Q" => "",
                        "notstarted" => "DNS",
                        "disqualified" => "DSQ",
                        "notfinished" or "running" or "retired" or "quarantine" => "DNF",
                        _ => rec.Status
                    };

                    string time = rec.OfficialTime;
                    if (rec.Status != "finished")
                        time = ""; 

                    worksheet.Cells[row, 1].Value = null;
                    worksheet.Cells[row, 2].Value = int.TryParse(rec.Number, out int num) ? num : rec.Number;
                    worksheet.Cells[row, 3].Value = rec.LastName;
                    worksheet.Cells[row, 4].Value = rec.FirstName;
                    worksheet.Cells[row, 5].Value = includeGender ? rec.Gender : "";
                    for (int col = 6; col <= 22; col++)
                        worksheet.Cells[row, col].Value = "";
                    worksheet.Cells[row, 17].Value = time;   // Результат
                    worksheet.Cells[row, 20].Value = statusDisplay; // Статус
                    row++;
                }

                package.Save();
                Console.WriteLine($"Создан файл: {filePath}");
            }
        }

        private void WriteExcelFileFallback(List<ParticipantRecord> records, string filePath, string title, string date, bool includeGender)
        {
            // TODO: Реализация fallback
        }
    }
}
