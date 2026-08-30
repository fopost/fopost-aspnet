namespace FoPost.AspNetCore.Tests;

/// <summary>Sets a process environment variable for the life of the scope, then puts it back.</summary>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    public EnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _original = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public static EnvironmentVariableScope Cleared(string name) => new(name, null);

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
}
