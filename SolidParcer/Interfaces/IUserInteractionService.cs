namespace SolidParcer.Interfaces
{
    public interface IUserInteractionService
    {
        string AskUrl();
        string AskTitle(string defaultTitle);
        string AskDate(string defaultDate);
        void ShowMessage(string message);
    }
}
