using System.Buffers.Binary;

namespace MakanApp.Application.Messaging;

public static class MessageSearchCursor
{
    private const byte Version = 1;
    private const int PayloadLength = 25;

    public static string Encode(MessageSearchPosition position)
    {
        Span<byte> payload = stackalloc byte[PayloadLength];
        payload[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(payload[1..9], position.SentAtUtc.Ticks);
        position.MessageId.TryWriteBytes(payload[9..]);
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string value, out MessageSearchPosition? position)
    {
        position = null;
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var payload = Convert.FromBase64String(base64);
            if (payload.Length != PayloadLength || payload[0] != Version)
            {
                return false;
            }

            var ticks = BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(1, 8));
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return false;
            }

            position = new MessageSearchPosition(
                new DateTime(ticks, DateTimeKind.Utc),
                new Guid(payload.AsSpan(9, 16)));
            return position.MessageId != Guid.Empty;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
