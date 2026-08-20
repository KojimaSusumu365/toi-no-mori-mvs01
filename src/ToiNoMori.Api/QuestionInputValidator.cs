namespace ToiNoMori.Api;

public sealed record ValidatedQuestionContent(string Title, string Body, IReadOnlyList<string> Tags);

public static class QuestionInputValidator
{
    public static bool TryValidate(
        QuestionContentRequest request,
        out ValidatedQuestionContent? content,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        var title = request.Title?.Trim() ?? string.Empty;
        var body = request.Body?.Trim() ?? string.Empty;
        var tags = (request.Tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (title.Length is < 1 or > 120)
        {
            errors["title"] = ["Title must contain between 1 and 120 characters."];
        }

        if (body.Length is < 1 or > 5000)
        {
            errors["body"] = ["Body must contain between 1 and 5000 characters."];
        }

        if (tags.Length > 5)
        {
            errors["tags"] = ["No more than five tags are allowed."];
        }
        else if (tags.Any(tag => tag.Length > 30))
        {
            errors["tags"] = ["Each tag must contain no more than 30 characters."];
        }

        if (errors.Count > 0)
        {
            content = null;
            return false;
        }

        content = new(title, body, tags);
        return true;
    }
}
