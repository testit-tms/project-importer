using Microsoft.Extensions.Logging;

namespace ImporterTests
{
    public static class TestHelper
    {
        public static void VerifyLogging<T>(
            this Mock<ILogger<T>> loggerMock, 
            string expectedMessage, 
            LogLevel expectedLevel, 
            Times? times = null)
        {
            loggerMock.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times ?? Times.AtLeastOnce());
        }

        public static void VerifyLoggingCalls<T>(
            this Mock<ILogger<T>> loggerMock, 
            LogLevel expectedLevel, 
            int minimumCalls)
        {
            loggerMock.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeast(minimumCalls));
        }
    }
}
