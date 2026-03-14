namespace FinanceTracker.Application.Common;

public class ApplicationExceptionBase(string message) : Exception(message);
public sealed class NotFoundException(string message) : ApplicationExceptionBase(message);
public sealed class ConflictException(string message) : ApplicationExceptionBase(message);
public sealed class ValidationException(string message) : ApplicationExceptionBase(message);
