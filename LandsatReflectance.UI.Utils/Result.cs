namespace LandsatReflectance.UI.Utils;

public record Result<TOk, TError>
{
    private bool _isOk = false;

    internal TOk OkValue = default!;
    internal TError ErrorValue = default!;
    
    private Result()
    { }


    public bool IsOk => _isOk;
    public bool IsError => !_isOk;

    public static Result<TOk, TError> FromOk(TOk okValue) =>
        new()
        {
            _isOk = true,
            OkValue = okValue
        };
    
    public static Result<TOk, TError> FromError(TError errorValue) =>
        new()
        {
            _isOk = false,
            ErrorValue = errorValue
        };
}

public static class ResultExtensions
{
    public static T Match<T, TOk, TError>(
        this Result<TOk, TError> result, 
        Func<TOk, T> mapOk, 
        Func<TError, T> mapError) 
        =>
        result.IsOk ? mapOk(result.OkValue) : mapError(result.ErrorValue);

    public static void MatchUnit<TOk, TError>(
        this Result<TOk, TError> result,
        Action<TOk> okCallback,
        Action<TError> errorCallback)
    {
        if (result.IsOk)
        {
            okCallback(result.OkValue);
        }
        else
        {
            errorCallback(result.ErrorValue);
        }
    }
    
    public static Result<TNewOk, TError> Bind<TOk, TError, TNewOk>(
        this Result<TOk, TError> result,
        Func<TOk, Result<TNewOk, TError>> binder)
    {
        return result.Match(
            binder,
            Result<TNewOk, TError>.FromError);
    }
}