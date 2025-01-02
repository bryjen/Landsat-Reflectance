namespace LandsatReflectance.UI.Exceptions;

public class RecoverableException<T> : Exception
{
    public T? ExceptionData { get; init; }
    public bool HasData => ExceptionData is not null;
    
    public RecoverableException(string message) : base(message)
    { }

    public RecoverableException(string message, Exception innerException) : base(message, innerException)
    { }
}