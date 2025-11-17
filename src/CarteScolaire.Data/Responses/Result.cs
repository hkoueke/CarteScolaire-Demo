namespace CarteScolaire.Data.Responses;


using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the result of an operation, either a success with a value or a failure with an error.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
public readonly struct Result<T>
{
    private readonly T _value;
    private readonly string? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
    }

    private Result(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        // We must assign '_value'. We use 'default!' to suppress warnings,
        // as it will never be accessed in a failure state.
        _value = default!;
        _error = error;
        _isSuccess = false;
    }

    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value. Throws if the result is a failure.
    /// </summary>
    public T Value => !_isSuccess 
        ? throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {_error}") 
        : _value;

    /// <summary>
    /// Gets the error message. Throws if the result is a success.
    /// </summary>
    public string Error => _isSuccess
        ? throw new InvalidOperationException("Cannot access Error of a successful result.")
        : _error!;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// Executes different actions based on success or failure.
    /// </summary>
    public void Match(Action<T> onSuccess, Action<string> onFailure)
    {
        if (_isSuccess)
            onSuccess(_value);
        else
            onFailure(_error!);
    }

    /// <summary>
    /// Returns a value based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => _isSuccess ? onSuccess(_value) : onFailure(_error!);


    /// <summary>
    /// Maps the success value to a new type.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        _isSuccess ? mapper(_value) : Result<TResult>.Failure(_error!);


    /// <summary>
    /// Flat maps the success value to a new Result.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder) =>
        _isSuccess ? binder(_value) : Result<TResult>.Failure(_error!);


    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess)
            action(_value);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (!_isSuccess)
            action(_error!);
        return this;
    }

    /// <summary>
    /// Returns the value if successful, otherwise returns the default value.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => _isSuccess ? _value : defaultValue;


    /// <summary>
    /// Returns the value if successful, otherwise computes and returns a fallback value.
    /// </summary>
    public T GetValueOrElse(Func<string, T> fallback) => _isSuccess ? _value : fallback(_error!);



    /// <summary>
    /// Tries to get the value, returning true if successful.
    /// </summary>
    /// <remarks>
    /// This is the standard .NET 'Try' pattern.
    /// 'value' will be the success value or default(T) on failure.
    /// 'error' will be the error message or null on success.
    /// </remarks>
    public bool TryGetValue([MaybeNullWhen(false)] out T value, out string? error)
    {
        if (_isSuccess)
        {
            value = _value;
            error = null;
            return true;
        }
        else
        {
            value = default!; // '_value' is already default(T)! but this is clearer
            error = _error!;
            return false;
        }
    }

    /// <summary>
    /// Converts the result to a string representation.
    /// </summary>
    public override string ToString() => _isSuccess ? $"Success({_value})" : $"Failure({_error})";

    /// <summary>
    /// Implicitly converts a value to a successful Result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
}

