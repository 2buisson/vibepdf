namespace pdfjunior.Services;

public interface IErrorMapper
{
    string MapToUserMessage(Exception ex);
}
