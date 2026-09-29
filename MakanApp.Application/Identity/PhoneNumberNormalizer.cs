using System.Text;

namespace MakanApp.Application.Identity;

public static class PhoneNumberNormalizer
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            builder.Append(ToAsciiDigit(character));
        }

        var candidate = builder.ToString();
        if (candidate.StartsWith("00", StringComparison.Ordinal))
        {
            candidate = $"+{candidate[2..]}";
        }
        else if (candidate.Length == 11 && candidate.StartsWith("09", StringComparison.Ordinal))
        {
            candidate = $"+98{candidate[1..]}";
        }
        else if (candidate.Length == 12 && candidate.StartsWith("989", StringComparison.Ordinal))
        {
            candidate = $"+{candidate}";
        }

        if (!candidate.StartsWith('+'))
        {
            return false;
        }

        var digits = candidate.AsSpan(1);
        if (digits.Length is < 8 or > 15)
        {
            return false;
        }

        foreach (var digit in digits)
        {
            if (digit is < '0' or > '9')
            {
                return false;
            }
        }

        normalized = candidate;
        return true;
    }

    private static char ToAsciiDigit(char value) => value switch
    {
        '\u06F0' or '\u0660' => '0',
        '\u06F1' or '\u0661' => '1',
        '\u06F2' or '\u0662' => '2',
        '\u06F3' or '\u0663' => '3',
        '\u06F4' or '\u0664' => '4',
        '\u06F5' or '\u0665' => '5',
        '\u06F6' or '\u0666' => '6',
        '\u06F7' or '\u0667' => '7',
        '\u06F8' or '\u0668' => '8',
        '\u06F9' or '\u0669' => '9',
        _ => value
    };
}
