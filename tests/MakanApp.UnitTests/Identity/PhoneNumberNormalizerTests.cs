using MakanApp.Application.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("0912 123 4567", "+989121234567")]
    [InlineData("+98 (912) 123-4567", "+989121234567")]
    [InlineData("۰۰۹۸۹۱۲۱۲۳۴۵۶۷", "+989121234567")]
    [InlineData("989121234567", "+989121234567")]
    public void TryNormalizeAcceptsSupportedMobileFormats(string input, string expected)
    {
        var succeeded = PhoneNumberNormalizer.TryNormalize(input, out var normalized);

        Assert.True(succeeded);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0912")]
    [InlineData("phone")]
    [InlineData("9121234567")]
    public void TryNormalizeRejectsInvalidValues(string input)
    {
        Assert.False(PhoneNumberNormalizer.TryNormalize(input, out _));
    }
}
