using SolidParcer.Models;
using System.Collections.Generic;

namespace SolidParcer.Interfaces
{
    public interface IDataProcessingService
    {
        void AssignGender(List<ParticipantRecord> records);
        void CalculatePlaces(List<ParticipantRecord> records);
        List<ParticipantRecord> RemoveDuplicates(List<ParticipantRecord> records);
    }
}
