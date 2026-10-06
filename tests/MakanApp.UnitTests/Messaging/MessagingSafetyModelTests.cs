using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingSafetyModelTests
{
    [Fact]
    public void PersianSearchNormalizationPreservesOriginalAndNormalizesVariants()
    {
        var message = CreateMessage("  كتاب\tعربي\u200C تست  ");

        Assert.Equal("  كتاب\tعربي\u200C تست  ", message.Text);
        Assert.Equal("کتاب عربی تست", message.SearchText);
    }

    [Fact]
    public void EditedMessageSearchUsesOnlyCurrentText()
    {
        var message = CreateMessage("نسخه قدیمی");

        message.EditText("نسخه جدید", 4000, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("نسخه جدید", message.SearchText);
        Assert.DoesNotContain("قدیمی", message.SearchText);
    }

    [Fact]
    public void DeletedMessageClearsSearchableContent()
    {
        var message = CreateMessage("محتوای محرمانه");

        message.Delete(message.SenderUserId, DateTime.UtcNow.AddMinutes(1));

        Assert.Null(message.Text);
        Assert.Null(message.SearchText);
    }

    [Fact]
    public void UserCannotBlockSelf()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            UserBlock.Create(userId, userId, DateTime.UtcNow));
    }

    [Fact]
    public void UnblockPreservesHistoricalLifecycleAndIsIdempotent()
    {
        var block = UserBlock.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        var endedAtUtc = DateTime.UtcNow.AddMinutes(1);

        block.End(endedAtUtc);
        block.End(endedAtUtc.AddMinutes(1));

        Assert.False(block.IsActive);
        Assert.Equal(UserBlockStatus.Ended, block.Status);
        Assert.Equal(endedAtUtc, block.EndedAtUtc);
    }

    [Fact]
    public void ReportStartsAsSubmittedAllegationWithOnlySelectedMessageSnapshot()
    {
        var messageId = Guid.NewGuid();
        var report = AbuseReport.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            messageId,
            Guid.NewGuid(),
            AbuseReportReason.Harassment,
            "نیازمند بررسی انسانی",
            MessageKind.Text,
            "فقط پیام انتخاب‌شده",
            new byte[8],
            new byte[32],
            DateTime.UtcNow);

        Assert.Equal(AbuseReportStatus.Submitted, report.Status);
        Assert.Equal(messageId, report.MessageId);
        Assert.Equal("فقط پیام انتخاب‌شده", report.ReportedContentSnapshot);
    }

    private static Message CreateMessage(string text) =>
        Message.CreateText(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            text,
            4000,
            DateTime.UtcNow);
}
