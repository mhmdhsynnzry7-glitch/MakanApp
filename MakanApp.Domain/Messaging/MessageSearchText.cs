using System.Text;

namespace MakanApp.Domain.Messaging;

public static class MessageSearchText
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || character == '\u200C')
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character switch
            {
                '\u064A' or '\u0649' => '\u06CC',
                '\u0643' => '\u06A9',
                _ => character
            });
        }

        return result.Length == 0 ? null : result.ToString();
    }
}
