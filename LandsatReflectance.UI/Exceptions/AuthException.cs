namespace LandsatReflectance.UI.Exceptions;

public class AuthException : Exception
{
    public AuthException(string message) : base(message)
    { }
    
    public AuthException(string message, Exception innerException) : base(message, innerException)
    { }

    public const string GenericLoginErrorMessage = "Unexpected Login Fail";
    
    public const string GenericRegistrationErrorMessage = "Unexpected Registration Fail";
}