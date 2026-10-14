namespace Vineeeeea.Domain.Errors;

public abstract class VineeeeeaException(string message) : Exception(message);

public sealed class ValidationException(string message) : VineeeeeaException(message);

public sealed class BusinessRuleException(string message) : VineeeeeaException(message);

public sealed class NotFoundException(string message) : VineeeeeaException(message);

public sealed class ExternalServiceException(string message) : VineeeeeaException(message);
