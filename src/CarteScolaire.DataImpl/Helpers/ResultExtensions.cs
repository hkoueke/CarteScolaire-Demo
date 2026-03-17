namespace CarteScolaire.DataImpl.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using Data.Responses;

#pragma warning disable CA1031
/// <summary>
/// Extension methods for working with Result types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a nullable value to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string errorMessage) where T : class
        => value ?? Result<T>.Failure(errorMessage);


    /// <summary>
    /// Converts a nullable value type to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string errorMessage) where T : struct
        => value ?? Result<T>.Failure(errorMessage);


    // Shared error-mapping logic extracted once
    private static Result<T> Catch<T>(Exception ex, Func<Exception, string>? errorMapper) =>
        Result<T>.Failure(errorMapper?.Invoke(ex) ?? ex.Message);

    /// <summary>
    /// Executes an operation and wraps it in a Result, catching exceptions.
    /// </summary>
    public static Result<T> Try<T>(Func<T> operation, Func<Exception, string>? errorMapper = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            return Catch<T>(ex, errorMapper);
        }
    }

    /// <summary>
    /// Executes an asynchronous operation and wraps it in a Result, catching exceptions.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> operation, Func<Exception, string>? errorMapper = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Catch<T>(ex, errorMapper);
        }
    }

    /// <summary>
    /// Combines multiple results into a single result containing a collection.
    /// Fails on the first failure found.
    /// </summary>
    public static Result<IEnumerable<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        // We must materialize the list to check for failures and then select values.
        List<Result<T>> resultsList = results.ToList();

        // Using FirstOrDefault with a struct is dangerous, as default(Result<T>)
        // would report IsFailure = true, leading to a bug.
        // A simple loop is the safest and most correct way to find the first failure.
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (Result<T> result in resultsList)
        {
            if (result.IsFailure)
            {
                // Fail-fast on the first error.
                return Result<IEnumerable<T>>.Failure(result.Error);
            }
        }

        // If we get here, all results were successful.
        return Result.Success(resultsList.Select(r => r.Value));
    }

    /// <summary>
    /// Ensures a condition is met, otherwise returns a failure.
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value)
            ? result
            : Result<T>.Failure(errorMessage);
    }
}