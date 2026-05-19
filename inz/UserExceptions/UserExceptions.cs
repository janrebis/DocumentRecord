namespace inz.UserExceptions;

public abstract class UserException : Exception
{
    protected UserException(string message) : base(message)
    {
    }

    protected UserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class UserNotFoundException : UserException
{
    public UserNotFoundException(string message) : base(message)
    {
    }
}

public class UserAlreadyExistsException : UserException
{
    public UserAlreadyExistsException(string message) : base(message)
    {
    }
}

public class UserInactiveException : UserException
{
    public UserInactiveException(string message) : base(message)
    {
    }
}

public class OrganizationNotFoundException : UserException
{
    public OrganizationNotFoundException(string message) : base(message)
    {
    }
}

public class RoleNotFoundException : UserException
{
    public RoleNotFoundException(string message) : base(message)
    {
    }
}

public class RoleAlreadyAssignedException : UserException
{
    public RoleAlreadyAssignedException(string message) : base(message)
    {
    }
}

public class InsufficientPermissionsException : UserException
{
    public string RequiredPermission { get; }

    public InsufficientPermissionsException(string message, string requiredPermission)
        : base(message)
    {
        RequiredPermission = requiredPermission;
    }
}
