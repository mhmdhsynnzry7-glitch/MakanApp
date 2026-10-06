using System.Buffers.Binary;

namespace MakanApp.Application.Messaging;

public static class MessagingChangeCursor
{
    private const byte CurrentVersion = 1;
    private const int CursorByteLength = 25;

    public static string Encode(Guid conversationId, long changeSequence)
    {
        if (conversationId == Guid.Empty || changeSequence < 0)
        {
            throw new ArgumentException("اطلاعات نشانگر تغییر معتبر نیست.");
        }

        Span<byte> bytes = stackalloc byte[CursorByteLength];
        bytes[0] = CurrentVersion;
        conversationId.TryWriteBytes(bytes[1..17]);
        BinaryPrimitives.WriteInt64BigEndian(bytes[17..], changeSequence);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string value, Guid expectedConversationId, out long changeSequence)
    {
        changeSequence = 0;
        if (string.IsNullOrWhiteSpace(value) || expectedConversationId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            var bytes = Convert.FromBase64String(normalized);
            if (bytes.Length != CursorByteLength || bytes[0] != CurrentVersion)
            {
                return false;
            }

            var conversationId = new Guid(bytes.AsSpan(1, 16));
            var sequence = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(17, 8));
            if (conversationId != expectedConversationId || sequence < 0)
            {
                return false;
            }

            changeSequence = sequence;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
