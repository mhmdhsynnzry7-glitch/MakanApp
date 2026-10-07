using System.Security.Cryptography;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;

namespace MakanApp.Infrastructure.Assessment;

public sealed class SecureExamQuestionOrderRandomizer : IExamQuestionOrderRandomizer
{
    public IReadOnlyList<QuestionVersion> Randomize(IReadOnlyCollection<QuestionVersion> questions)
    {
        var result = questions.OrderBy(question => question.Order).ToArray();
        for (var index = result.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (result[index], result[swapIndex]) = (result[swapIndex], result[index]);
        }

        return result;
    }
}
