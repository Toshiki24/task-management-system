namespace TaskManagementSystem.Api.Dtos.Common;

public record ErrorResponse(string Message);

public record ValidationErrorItem(string Field, string Message);

public record ValidationErrorResponse(string Message, IEnumerable<ValidationErrorItem> Errors);
