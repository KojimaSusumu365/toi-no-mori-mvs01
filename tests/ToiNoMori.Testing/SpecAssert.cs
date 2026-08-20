namespace ToiNoMori.Testing;

public static class SpecAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new TestFailureException(message);
        }
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestFailureException($"{message} Expected: {expected}; actual: {actual}.");
        }
    }

    public static void NotNull(object? value, string message) => True(value is not null, message);

    public static TException Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new TestFailureException(
                $"{message} Expected {typeof(TException).Name}, but got {exception.GetType().Name}.",
                exception);
        }

        throw new TestFailureException($"{message} Expected {typeof(TException).Name}, but no exception was thrown.");
    }
}

public sealed class TestFailureException : Exception
{
    public TestFailureException(string message)
        : base(message)
    {
    }

    public TestFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
