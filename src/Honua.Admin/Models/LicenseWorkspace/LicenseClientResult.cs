using System;

namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Discriminated outcome of a license-client call. Avoids surfacing raw
/// exceptions to the workspace state — the state machine inspects
/// <see cref="IsSuccess"/> and reads either <see cref="Value"/> or
/// <see cref="Error"/>.
/// </summary>
public sealed class LicenseClientResult<T> where T : class
{
    private LicenseClientResult(T? value, LicenseClientError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public LicenseClientError? Error { get; }

    public bool IsSuccess => Error is null;

    public static LicenseClientResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LicenseClientResult<T>(value, null);
    }

    public static LicenseClientResult<T> Failure(LicenseClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LicenseClientResult<T>(null, error);
    }
}
