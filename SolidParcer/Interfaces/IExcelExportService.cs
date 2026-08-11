using SolidParcer.Models;
using System.Collections.Generic;

namespace SolidParcer.Interfaces
{
    public interface IExcelExportService
    {
        void Export(List<ParticipantRecord> allRecords, string eventTitle, string eventDate, Dictionary<string, string> raceNames);
    }
}
