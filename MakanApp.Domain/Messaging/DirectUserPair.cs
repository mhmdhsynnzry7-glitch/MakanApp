namespace MakanApp.Domain.Messaging;

public readonly record struct DirectUserPair(Guid LowerUserId, Guid HigherUserId)
{
    public static DirectUserPair Create(Guid firstUserId, Guid secondUserId)
    {
        if (firstUserId == Guid.Empty || secondUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه هر دو کاربر الزامی است.");
        }

        if (firstUserId == secondUserId)
        {
            throw new ArgumentException("گفتگوی مستقیم با خود کاربر مجاز نیست.");
        }

        return string.CompareOrdinal(firstUserId.ToString("N"), secondUserId.ToString("N")) < 0
            ? new DirectUserPair(firstUserId, secondUserId)
            : new DirectUserPair(secondUserId, firstUserId);
    }

    public Guid Other(Guid userId) =>
        userId == LowerUserId
            ? HigherUserId
            : userId == HigherUserId
                ? LowerUserId
                : throw new InvalidOperationException("کاربر عضو این زوج مستقیم نیست.");
}
