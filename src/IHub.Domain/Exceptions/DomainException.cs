namespace IHub.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message)
{
}

public sealed class InvalidActivityException(string message) : DomainException(message)
{
}

public sealed class DuplicateActivityConflictException(string activityId) : DomainException($"Activity '{activityId}' already exists with different payload data.")
{
}

public sealed class ActivityIdentityConstraintException : DomainException
{
    public ActivityIdentityConstraintException() : base("An activity with this source system and external activity ID already exists.")
    {
    }
}

public sealed class InvalidStatementWindowException : DomainException
{
    public InvalidStatementWindowException() : base("The statement start must be earlier than the end.")
    {
    }
}
