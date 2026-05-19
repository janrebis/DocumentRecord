namespace inz.UserExceptions;

public class AuthenticationException : UserException
{
    public AuthenticationException(string message) : base(message)
    {
    }
}

public class InvalidCredentialsException : AuthenticationException
{
    public InvalidCredentialsException(string message) : base(message)
    {
    }
}

public class EmailAlreadyRegisteredException : AuthenticationException
{
    public EmailAlreadyRegisteredException(string message) : base(message)
    {
    }
}
