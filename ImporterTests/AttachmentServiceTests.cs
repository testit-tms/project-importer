using Importer.Client;
using Importer.Services;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests;

[TestFixture]
public class AttachmentServiceTests
{
    private static readonly Guid WorkItemId = Guid.NewGuid();

    private Mock<ILogger<AttachmentService>> _loggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private Mock<IParserService> _parserServiceMock = null!;
    private AttachmentService _attachmentService = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<AttachmentService>>();
        _clientAdapterMock = new Mock<IClientAdapter>(MockBehavior.Strict);
        _parserServiceMock = new Mock<IParserService>(MockBehavior.Strict);

        _attachmentService = new AttachmentService(
            _loggerMock.Object,
            _clientAdapterMock.Object,
            _parserServiceMock.Object);
    }

    [Test]
    public async Task GetAttachments_WhenNoAttachments_ReturnsEmptyDictionary()
    {
        // Arrange
        var attachments = Array.Empty<string>();

        // Act
        var result = await _attachmentService.GetAttachments(WorkItemId, attachments);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);

            _parserServiceMock.Verify(service => service.GetAttachment(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);

            _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
            _loggerMock.VerifyLogging("Complete GetAttachments with 0 attachments", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public async Task GetAttachments_WhenSingleAttachmentSucceeds_ReturnsDictionaryWithOneEntry()
    {
        // Arrange
        var attachmentName = $"{Guid.NewGuid():N}.txt";
        var attachmentId = Guid.NewGuid();
        FileStream? stream = null;

        try
        {
            stream = CreateTestFile(attachmentName);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachmentName))
                .ReturnsAsync(stream);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachmentName, stream))
                .ReturnsAsync(attachmentId);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachmentName });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result.ContainsKey(attachmentName), Is.True);
                Assert.That(result[attachmentName], Is.EqualTo(attachmentId));

                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachmentName), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachmentName, stream), Times.Once);

                _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Complete GetAttachments with 1 attachments", LogLevel.Information, Times.Once());
            });
        }
        finally
        {
            stream?.Dispose();
            try
            {
                if (File.Exists(attachmentName))
                    File.Delete(attachmentName);
            }
            catch { }
        }
    }

    [Test]
    public async Task GetAttachments_WhenMultipleAttachmentsSucceed_ReturnsDictionaryWithAllEntries()
    {
        // Arrange
        var attachment1Name = $"{Guid.NewGuid():N}.txt";
        var attachment2Name = $"{Guid.NewGuid():N}.txt";
        var attachment3Name = $"{Guid.NewGuid():N}.txt";
        
        var attachment1Id = Guid.NewGuid();
        var attachment2Id = Guid.NewGuid();
        var attachment3Id = Guid.NewGuid();

        FileStream? stream1 = null;
        FileStream? stream2 = null;
        FileStream? stream3 = null;

        try
        {
            stream1 = CreateTestFile(attachment1Name);
            stream2 = CreateTestFile(attachment2Name);
            stream3 = CreateTestFile(attachment3Name);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment1Name))
                .ReturnsAsync(stream1);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment2Name))
                .ReturnsAsync(stream2);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment3Name))
                .ReturnsAsync(stream3);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment1Name, stream1))
                .ReturnsAsync(attachment1Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment2Name, stream2))
                .ReturnsAsync(attachment2Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment3Name, stream3))
                .ReturnsAsync(attachment3Id);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachment1Name, attachment2Name, attachment3Name });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(3));

                Assert.That(result.ContainsKey(attachment1Name), Is.True);
                Assert.That(result.ContainsKey(attachment2Name), Is.True);
                Assert.That(result.ContainsKey(attachment3Name), Is.True);

                Assert.That(result[attachment1Name], Is.EqualTo(attachment1Id));
                Assert.That(result[attachment2Name], Is.EqualTo(attachment2Id));
                Assert.That(result[attachment3Name], Is.EqualTo(attachment3Id));

                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment1Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment2Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment3Name), Times.Once);

                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment1Name, stream1), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment2Name, stream2), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment3Name, stream3), Times.Once);

                _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Complete GetAttachments with 3 attachments", LogLevel.Information, Times.Once());
            });
        }
        finally
        {
            stream1?.Dispose();
            stream2?.Dispose();
            stream3?.Dispose();
            try
            {
                if (File.Exists(attachment1Name)) File.Delete(attachment1Name);
                if (File.Exists(attachment2Name)) File.Delete(attachment2Name);
                if (File.Exists(attachment3Name)) File.Delete(attachment3Name);
            }
            catch { }
        }
    }

    [Test]
    public async Task GetAttachments_WhenGetAttachmentThrowsException_SkipsAttachmentAndContinues()
    {
        // Arrange
        var attachment1Name = "test1.txt";
        var attachment2Name = "test2.txt";
        var attachment3Name = "test3.txt";
        
        var attachment2Id = Guid.NewGuid();
        var attachment3Id = Guid.NewGuid();

        FileStream? stream2 = null;
        FileStream? stream3 = null;

        var exception = new FileNotFoundException("File not found", attachment1Name);

        try
        {
            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment1Name))
                .ThrowsAsync(exception);

            stream2 = CreateTestFile(attachment2Name);
            stream3 = CreateTestFile(attachment3Name);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment2Name))
                .ReturnsAsync(stream2);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment3Name))
                .ReturnsAsync(stream3);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment2Name, stream2))
                .ReturnsAsync(attachment2Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment3Name, stream3))
                .ReturnsAsync(attachment3Id);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachment1Name, attachment2Name, attachment3Name });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(2));

                Assert.That(result.ContainsKey(attachment1Name), Is.False);
                Assert.That(result.ContainsKey(attachment2Name), Is.True);
                Assert.That(result.ContainsKey(attachment3Name), Is.True);

                Assert.That(result[attachment2Name], Is.EqualTo(attachment2Id));
                Assert.That(result[attachment3Name], Is.EqualTo(attachment3Id));

                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment1Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment2Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment3Name), Times.Once);

                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment1Name, It.IsAny<Stream>()), Times.Never);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment2Name, stream2), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment3Name, stream3), Times.Once);

                _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Complete GetAttachments with 2 attachments", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Failed to upload attachment, skip", LogLevel.Warning, Times.Once());
                _loggerMock.VerifyLogging(attachment1Name, LogLevel.Warning, Times.Once());
            });
        }
        finally
        {
            stream2?.Dispose();
            stream3?.Dispose();
            try
            {
                if (File.Exists(attachment2Name)) File.Delete(attachment2Name);
                if (File.Exists(attachment3Name)) File.Delete(attachment3Name);
            }
            catch { }
        }
    }

    [Test]
    public async Task GetAttachments_WhenUploadAttachmentThrowsException_SkipsAttachmentAndContinues()
    {
        // Arrange
        var attachment1Name = "test1.txt";
        var attachment2Name = "test2.txt";
        var attachment3Name = "test3.txt";
        
        var attachment2Id = Guid.NewGuid();
        var attachment3Id = Guid.NewGuid();

        FileStream? stream1 = null;
        FileStream? stream2 = null;
        FileStream? stream3 = null;

        var exception = new UnauthorizedAccessException("Access denied");

        try
        {
            stream1 = CreateTestFile(attachment1Name);
            stream2 = CreateTestFile(attachment2Name);
            stream3 = CreateTestFile(attachment3Name);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment1Name))
                .ReturnsAsync(stream1);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment2Name))
                .ReturnsAsync(stream2);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment3Name))
                .ReturnsAsync(stream3);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment1Name, stream1))
                .ThrowsAsync(exception);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment2Name, stream2))
                .ReturnsAsync(attachment2Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment3Name, stream3))
                .ReturnsAsync(attachment3Id);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachment1Name, attachment2Name, attachment3Name });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(2));

                Assert.That(result.ContainsKey(attachment1Name), Is.False);
                Assert.That(result.ContainsKey(attachment2Name), Is.True);
                Assert.That(result.ContainsKey(attachment3Name), Is.True);

                Assert.That(result[attachment2Name], Is.EqualTo(attachment2Id));
                Assert.That(result[attachment3Name], Is.EqualTo(attachment3Id));

                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment1Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment2Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment3Name), Times.Once);

                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment1Name, stream1), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment2Name, stream2), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment3Name, stream3), Times.Once);

                _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Complete GetAttachments with 2 attachments", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Failed to upload attachment, skip", LogLevel.Warning, Times.Once());
                _loggerMock.VerifyLogging(attachment1Name, LogLevel.Warning, Times.Once());
            });
        }
        finally
        {
            stream1?.Dispose();
            stream2?.Dispose();
            stream3?.Dispose();
            try
            {
                if (File.Exists(attachment1Name)) File.Delete(attachment1Name);
                if (File.Exists(attachment2Name)) File.Delete(attachment2Name);
                if (File.Exists(attachment3Name)) File.Delete(attachment3Name);
            }
            catch { }
        }
    }

    [Test]
    public async Task GetAttachments_WhenAllAttachmentsFail_ReturnsEmptyDictionary()
    {
        // Arrange
        var attachment1Name = "test1.txt";
        var attachment2Name = "test2.txt";

        var exception1 = new FileNotFoundException("File not found", attachment1Name);
        var exception2 = new UnauthorizedAccessException("Access denied");

        _parserServiceMock
            .Setup(service => service.GetAttachment(WorkItemId, attachment1Name))
            .ThrowsAsync(exception1);

        _parserServiceMock
            .Setup(service => service.GetAttachment(WorkItemId, attachment2Name))
            .ThrowsAsync(exception2);

        // Act
        var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachment1Name, attachment2Name });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);

            _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment1Name), Times.Once);
            _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment2Name), Times.Once);

            _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);

            _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
            _loggerMock.VerifyLogging("Complete GetAttachments with 0 attachments", LogLevel.Information, Times.Once());
            _loggerMock.VerifyLoggingCalls(LogLevel.Warning, 2);
            _loggerMock.VerifyLogging(attachment1Name, LogLevel.Warning, Times.Once());
            _loggerMock.VerifyLogging(attachment2Name, LogLevel.Warning, Times.Once());
        });
    }

    [Test]
    public async Task GetAttachments_WhenStreamNameContainsFullPath_UsesPathGetFileNameForUpload()
    {
        // Arrange
        var attachmentName = $"{Guid.NewGuid():N}.txt";
        var attachmentId = Guid.NewGuid();

        FileStream? stream = null;
        string? dir = null;
        string? fullPath = null;

        try
        {
            dir = "att_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(dir);
            fullPath = Path.Combine(dir, attachmentName);
            stream = CreateTestFile(fullPath);
            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachmentName))
                .ReturnsAsync(stream);

            var expectedFileName = attachmentName;
            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(expectedFileName, It.IsAny<Stream>()))
                .ReturnsAsync(attachmentId);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId, new[] { attachmentName });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[attachmentName], Is.EqualTo(attachmentId));

                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(expectedFileName, It.IsAny<Stream>()), Times.Once);
            });
        }
        finally
        {
            stream?.Dispose();
            try
            {
                if (fullPath != null && File.Exists(fullPath))
                    File.Delete(fullPath);
                if (dir != null && Directory.Exists(dir))
                    Directory.Delete(dir);
            }
            catch { }
        }
    }

    [Test]
    public async Task GetAttachments_WhenMixedSuccessAndFailures_ReturnsOnlySuccessfulAttachments()
    {
        // Arrange
        var attachment1Name = "test1.txt";
        var attachment2Name = "test2.txt";
        var attachment3Name = "test3.txt";
        var attachment4Name = "test4.txt";
        
        var attachment1Id = Guid.NewGuid();
        var attachment3Id = Guid.NewGuid();

        FileStream? stream1 = null;
        FileStream? stream3 = null;
        FileStream? stream4 = null;

        var getAttachmentException = new FileNotFoundException("File not found", attachment2Name);
        var uploadException = new UnauthorizedAccessException("Access denied");

        try
        {
            stream1 = CreateTestFile(attachment1Name);
            stream3 = CreateTestFile(attachment3Name);
            stream4 = CreateTestFile(attachment4Name);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment1Name))
                .ReturnsAsync(stream1);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment2Name))
                .ThrowsAsync(getAttachmentException);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment3Name))
                .ReturnsAsync(stream3);

            _parserServiceMock
                .Setup(service => service.GetAttachment(WorkItemId, attachment4Name))
                .ReturnsAsync(stream4);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment1Name, stream1))
                .ReturnsAsync(attachment1Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment3Name, stream3))
                .ReturnsAsync(attachment3Id);

            _clientAdapterMock
                .Setup(adapter => adapter.UploadAttachment(attachment4Name, stream4))
                .ThrowsAsync(uploadException);

            // Act
            var result = await _attachmentService.GetAttachments(WorkItemId,
                new[] { attachment1Name, attachment2Name, attachment3Name, attachment4Name });

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(2));
                Assert.That(result.ContainsKey(attachment1Name), Is.True);
                Assert.That(result.ContainsKey(attachment2Name), Is.False);
                Assert.That(result.ContainsKey(attachment3Name), Is.True);
                Assert.That(result.ContainsKey(attachment4Name), Is.False);
                Assert.That(result[attachment1Name], Is.EqualTo(attachment1Id));
                Assert.That(result[attachment3Name], Is.EqualTo(attachment3Id));

                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment1Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment2Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment3Name), Times.Once);
                _parserServiceMock.Verify(service => service.GetAttachment(WorkItemId, attachment4Name), Times.Once);

                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment1Name, stream1), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment2Name, It.IsAny<Stream>()), Times.Never);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment3Name, stream3), Times.Once);
                _clientAdapterMock.Verify(adapter => adapter.UploadAttachment(attachment4Name, stream4), Times.Once);

                _loggerMock.VerifyLogging("Importing attachments for work item", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLogging("Complete GetAttachments with 2 attachments", LogLevel.Information, Times.Once());
                _loggerMock.VerifyLoggingCalls(LogLevel.Warning, 2);
                _loggerMock.VerifyLogging(attachment2Name, LogLevel.Warning, Times.Once());
                _loggerMock.VerifyLogging(attachment4Name, LogLevel.Warning, Times.Once());
            });
        }
        finally
        {
            stream1?.Dispose();
            stream3?.Dispose();
            stream4?.Dispose();
            try
            {
                if (File.Exists(attachment1Name)) File.Delete(attachment1Name);
                if (File.Exists(attachment3Name)) File.Delete(attachment3Name);
                if (File.Exists(attachment4Name)) File.Delete(attachment4Name);
            }
            catch { }
        }

        }
        private static FileStream CreateTestFile(string fileName)
        {
            using (File.Create(fileName)) { }
            return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    
}

