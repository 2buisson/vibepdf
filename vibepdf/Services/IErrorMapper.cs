namespace vibepdf.Services;

public interface IErrorMapper
{
    string MapToUserMessage(Exception ex);
}
