namespace SolidParcer.Models;

public class ParticipantRecord
{
    /// <summary>
    /// Номер участника
    /// </summary>
    public string Number { get; set; }
    /// <summary>
    /// Имя
    /// </summary>
    public string FirstName { get; set; }
    /// <summary>
    /// Фамилия
    /// </summary>
    public string LastName { get; set; }
    /// <summary>
    /// Оффициальное время финиша
    /// </summary>
    public string OfficialTime { get; set; }
    /// <summary>
    /// Статус (DNS, DSQ, DNF)
    /// </summary>
    public string Status { get; set; }
    /// <summary>
    /// Формат
    /// </summary>
    public string Format { get; set; }
    /// <summary>
    /// Пол
    /// </summary>
    public string Gender { get; set; }
    /// <summary>
    /// Итоговое место (вычисляется)
    /// </summary>
    public string Place { get; set; }
    /// <summary>
    /// Категория (если есть)
    /// </summary>
    public string Category { get; set; }

}