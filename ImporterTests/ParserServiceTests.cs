using System.Text.Json;
using Importer.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImporterTests
{
    [TestFixture]
    public class ParserServiceTests : IDisposable
    {
        private Mock<ILogger<ParserService>> _loggerMock;
        private Mock<IConfiguration> _configurationMock;

        private ParserService _parserService = null!;
        private string _testPath = null!;
        private static readonly Guid WorkItemId = Guid.NewGuid();
        private const string TestFileName = "test.txt";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ParserService>>();
            _configurationMock = new Mock<IConfiguration>();

            _testPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(_testPath, WorkItemId.ToString()));
            Directory.CreateDirectory(_testPath);

            _configurationMock.Setup(c => c["resultPath"]).Returns(_testPath);

            _parserService = new ParserService(_loggerMock.Object, _configurationMock.Object);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WhenResultPathNotSet_ThrowArgumentException()
        {
            // Arrange
            _configurationMock.Setup(c => c["resultPath"]).Returns((string)null!);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new ParserService(_loggerMock.Object, _configurationMock.Object));

            Assert.That(exception!.Message, Is.EqualTo("resultPath is not set"), "The exception message must match");
        }

        [Test]
        public void Constructor_WhenResultPathWithEmpty_ThrowArgumentException()
        {
            // Arrange
            _configurationMock.Setup(c => c["resultPath"]).Returns(string.Empty);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new ParserService(_loggerMock.Object, _configurationMock.Object));

            Assert.That(exception!.Message, Is.EqualTo("resultPath is not set"), "The exception message must match");
        }

        [Test]
        public void Constructor_WithForwardSlashesOnWindows_ShouldReplaceWithBackslashes()
        {
            // Arrange
            var pathWithForwardSlashes = "C:/TestData/Export";
            _configurationMock.Setup(c => c["resultPath"]).Returns(pathWithForwardSlashes);

            // Act & Assert
            if (Path.DirectorySeparatorChar == '\\')
            {
                var service = new ParserService(_loggerMock.Object, _configurationMock.Object);
                Assert.That(service, Is.Not.Null);
            }
            else
            {
                Assert.Pass("Test skipped: not running on Windows");
            }
        }

        [Test]
        public void Constructor_WithBackslashesOnUnix_ThrowArgumentException()
        {
            // Arrange
            var pathWithBackslashes = "C:\\TestData\\Export";
            _configurationMock.Setup(c => c["resultPath"]).Returns(pathWithBackslashes);

            // Act & Assert
            if (Path.DirectorySeparatorChar == '/')
            {
                var exception = Assert.Throws<ArgumentException>(() =>
                    new ParserService(_loggerMock.Object, _configurationMock.Object));

                Assert.That(exception!.Message, Is.EqualTo("resultPath separators on your OS should be /"),
                    "The exception message must match");
            }
            else
            {
                Assert.Pass("Test skipped: not running on Unix");
            }
        }

        #endregion

        #region GetMainFile Tests

        [Test]
        public void GetMainFile_WhenFileNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetMainFile());

            Assert.That(exception!.Message, Is.EqualTo("Main json file not found"));
        }

        [Test]
        public async Task GetMainFile_WhenFileIsEmpty_ThrowJsonException()
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(_testPath, Constants.MainJson), string.Empty);

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetMainFile());
        }

        [Test]
        public void GetMainFile_WhenFileContainsInvalidJson_ThrowJsonException()
        {
            // Arrange
            var mainJsonPath = Path.Combine(_testPath, Constants.MainJson);
            File.WriteAllText(mainJsonPath, "{ invalid json }");

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetMainFile());
        }

        [Test]
        public async Task GetMainFile_WhenFileContainsNullJson_ThrowApplicationException()
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(_testPath, Constants.MainJson), "null");

            // Act & Assert
            var exception = Assert.ThrowsAsync<ApplicationException>(async () => await _parserService.GetMainFile());
            Assert.That(exception!.Message, Is.EqualTo("Main json file is empty"));
        }

        [Test]
        public async Task GetMainFile_WhenFileContainsValidJson_ReturnRoot()
        {
            // Arrange
            var expectedRoot = new Root
            {
                ProjectName = "Test Project",
                Attributes = new List<Models.Attribute>(),
                Sections = new List<Section>(),
                SharedSteps = new List<Guid>(),
                TestCases = new List<Guid>()
            };
            var validJson = JsonSerializer.Serialize(expectedRoot);
            File.WriteAllText(Path.Combine(_testPath, Constants.MainJson), validJson);

            // Act
            var result = await _parserService.GetMainFile();

            // Assert: Сравниваем объекты через JSON сериализацию
            var expectedJson = JsonSerializer.Serialize(expectedRoot);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        [Test]
        public async Task GetMainFile_WhenFileContainsCompleteData_ReturnRootWithData()
        {
            // Arrange
            var testCaseGuid = Guid.NewGuid();
            var sharedStepGuid = Guid.NewGuid();
            var attributeId = Guid.NewGuid();
            var sectionId = Guid.NewGuid();

            var expectedRoot = new Root
            {
                ProjectName = "Complete Test Project",
                Attributes = new List<Models.Attribute>
                {
                    new Models.Attribute
                    {
                        Id = attributeId,
                        Name = "Priority",
                        Type = AttributeType.Options,
                        IsActive = true,
                        IsRequired = false,
                        Options = new List<string>()
                    }
                },
                Sections = new List<Section>
                {
                    new Section
                    {
                        Id = sectionId,
                        Name = "Test Section",
                        PreconditionSteps = new List<Step>(),
                        PostconditionSteps = new List<Step>(),
                        Sections = new List<Section>()
                    }
                },
                SharedSteps = new List<Guid> { sharedStepGuid },
                TestCases = new List<Guid> { testCaseGuid }
            };

            var validJson = JsonSerializer.Serialize(expectedRoot);
            File.WriteAllText(Path.Combine(_testPath, Constants.MainJson), validJson);

            // Act
            var result = await _parserService.GetMainFile();

            // Assert: Сравниваем объекты через JSON сериализацию
            var expectedJson = JsonSerializer.Serialize(expectedRoot);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        #endregion

        #region GetSharedStep Tests

        [Test]
        public void GetSharedStep_WhenFileNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetSharedStep(WorkItemId));

            Assert.That(exception!.Message, Is.EqualTo("Shared step file not found"));
        }

        [Test]
        public void GetSharedStep_WhenDirectoryNotExists_ThrowApplicationException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetSharedStep(Guid.NewGuid()));

            Assert.That(exception!.Message, Is.EqualTo("Shared step file not found"));
        }

        [Test]
        public void GetSharedStep_WhenFileContainsNullJson_ThrowApplicationException()
        {
            // Arrange
            var sharedStepPath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.SharedStep);
            File.WriteAllText(sharedStepPath, "null");

            // Act & Assert
            var exception = Assert.ThrowsAsync<ApplicationException>(async () =>
                await _parserService.GetSharedStep(WorkItemId));

            Assert.That(exception!.Message, Is.EqualTo("Shared step file is empty"));
        }

        [Test]
        public void GetSharedStep_WhenFileIsEmpty_ThrowJsonException()
        {
            // Arrange
            var sharedStepPath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.SharedStep);
            File.WriteAllText(sharedStepPath, string.Empty);

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetSharedStep(WorkItemId));
        }

        [Test]
        public async Task GetSharedStep_WhenInvalidJson_ThrowsJsonException()
        {
            // Arrange
            var sharedStepPath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.SharedStep);
            await File.WriteAllTextAsync(sharedStepPath, "{invalid json}");

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetSharedStep(WorkItemId));
        }

        [Test]
        public async Task GetSharedStep_WhenFileContainsValidJson_ReturnSharedStep()
        {
            // Arrange
            var expectedSharedStep = new SharedStep
            {
                Id = WorkItemId,
                Name = "Test Shared Step",
                Description = "Test Description",
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                SectionId = Guid.NewGuid(),
                Steps = new List<Step>(),
                Attributes = new List<CaseAttribute>(),
                Links = new List<Link>(),
                Tags = new List<string>(),
                Attachments = new List<string>()
            };

            var validJson = JsonSerializer.Serialize(expectedSharedStep);
            var sharedStepPath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.SharedStep);
            File.WriteAllText(sharedStepPath, validJson);

            // Act
            var result = await _parserService.GetSharedStep(WorkItemId);

            // Assert
            var expectedJson = JsonSerializer.Serialize(expectedSharedStep);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        [Test]
        public async Task GetSharedStep_WhenFileContainsCompleteData_ReturnSharedStepWithData()
        {
            // Arrange
            var expectedSharedStep = new SharedStep
            {
                Id = WorkItemId,
                Name = "Complete Shared Step",
                Description = "Complete Description",
                State = StateType.Ready,
                Priority = PriorityType.High,
                SectionId = Guid.NewGuid(),
                Steps = new List<Step>
                {
                    new Step
                    {
                        Action = "Click button",
                        Expected = "Button clicked",
                        TestData = "test data",
                        ActionAttachments = new List<string>(),
                        ExpectedAttachments = new List<string>(),
                        TestDataAttachments = new List<string>()
                    }
                },
                Attributes = new List<CaseAttribute>
                {
                    new CaseAttribute { Id = Guid.NewGuid(), Value = "Test Value" }
                },
                Links = new List<Link>
                {
                    new Link { Title = "Test Link", Url = "http://test.com", Type = LinkType.Related }
                },
                Tags = new List<string> { "tag1", "tag2" },
                Attachments = new List<string> { "attachment1.png" }
            };

            var validJson = JsonSerializer.Serialize(expectedSharedStep);
            var sharedStepPath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.SharedStep);
            File.WriteAllText(sharedStepPath, validJson);

            // Act
            var result = await _parserService.GetSharedStep(WorkItemId);

            // Assert
            var expectedJson = JsonSerializer.Serialize(expectedSharedStep);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        #endregion

        #region GetTestCase Tests

        [Test]
        public void GetTestCase_WhenFileNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetTestCase(WorkItemId));

            Assert.That(exception!.Message, Is.EqualTo("Test case file not found"));
        }

        [Test]
        public void GetTestCase_WhenDirectoryNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetTestCase(Guid.NewGuid()));

            Assert.That(exception!.Message, Is.EqualTo("Test case file not found"));
        }

        [Test]
        public void GetTestCase_WhenFileContainsNullJson_ThrowApplicationException()
        {
            // Arrange
            var testCasePath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.TestCase);
            File.WriteAllText(testCasePath, "null");

            // Act & Assert
            var exception = Assert.ThrowsAsync<ApplicationException>(async () =>
                await _parserService.GetTestCase(WorkItemId));

            Assert.That(exception!.Message, Is.EqualTo("Test case file is empty"));
        }

        [Test]
        public void GetTestCase_WhenFileIsEmpty_ThrowJsonException()
        {
            // Arrange
            var testCasePath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.TestCase);
            File.WriteAllText(testCasePath, string.Empty);

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetTestCase(WorkItemId));
        }

        [Test]
        public void GetTestCase_WhenInvalidJson_ThrowsJsonException()
        {
            // Arrange
            var testCasePath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.TestCase);
            File.WriteAllText(testCasePath, "{invalid json}");

            // Act & Assert
            Assert.ThrowsAsync<JsonException>(async () => await _parserService.GetTestCase(WorkItemId));
        }

        [Test]
        public async Task GetTestCase_WhenFileContainsValidJson_ReturnTestCase()
        {
            // Arrange
            var expectedTestCase = new TestCase
            {
                Id = WorkItemId,
                Name = "Test Case",
                Description = "Test Description",
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                Duration = 100,
                SectionId = Guid.NewGuid(),
                Steps = new List<Step>(),
                PreconditionSteps = new List<Step>(),
                PostconditionSteps = new List<Step>(),
                Attributes = new List<CaseAttribute>(),
                Links = new List<Link>(),
                Tags = new List<string>(),
                Attachments = new List<string>(),
                Iterations = new List<Iteration>()
            };

            var validJson = JsonSerializer.Serialize(expectedTestCase);
            var testCasePath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.TestCase);
            File.WriteAllText(testCasePath, validJson);

            // Act
            var result = await _parserService.GetTestCase(WorkItemId);

            // Assert
            var expectedJson = JsonSerializer.Serialize(expectedTestCase);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        [Test]
        public async Task GetTestCase_WhenFileContainsCompleteData_ReturnTestCaseWithData()
        {
            // Arrange
            var expectedTestCase = new TestCase
            {
                Id = WorkItemId,
                Name = "Complete Test Case",
                Description = "Complete Description",
                State = StateType.Ready,
                Priority = PriorityType.High,
                Duration = 250,
                SectionId = Guid.NewGuid(),
                Steps = new List<Step>
                {
                    new Step
                    {
                        Action = "Click button",
                        Expected = "Button clicked",
                        TestData = "test data",
                        ActionAttachments = new List<string>(),
                        ExpectedAttachments = new List<string>(),
                        TestDataAttachments = new List<string>()
                    }
                },
                PreconditionSteps = new List<Step>
                {
                    new Step
                    {
                        Action = "Login",
                        Expected = "Logged in",
                        TestData = "credentials",
                        ActionAttachments = new List<string>(),
                        ExpectedAttachments = new List<string>(),
                        TestDataAttachments = new List<string>()
                    }
                },
                PostconditionSteps = new List<Step>
                {
                    new Step
                    {
                        Action = "Logout",
                        Expected = "Logged out",
                        TestData = string.Empty,
                        ActionAttachments = new List<string>(),
                        ExpectedAttachments = new List<string>(),
                        TestDataAttachments = new List<string>()
                    }
                },
                Attributes = new List<CaseAttribute>
                {
                    new CaseAttribute { Id = Guid.NewGuid(), Value = "Test Value" }
                },
                Links = new List<Link>
                {
                    new Link { Title = "Test Link", Url = "http://test.com", Type = LinkType.Related, Description = "Link description" }
                },
                Tags = new List<string> { "tag1", "tag2", "tag3" },
                Attachments = new List<string> { "attachment1.png", "attachment2.pdf" },
                Iterations = new List<Iteration>
                {
                    new Iteration
                    {
                        Parameters = new List<Parameter>
                        {
                            new Parameter { Name = "param1", Value = "value1" },
                            new Parameter { Name = "param2", Value = "value2" }
                        }
                    }
                }
            };

            var validJson = JsonSerializer.Serialize(expectedTestCase);
            var testCasePath = Path.Combine(_testPath, WorkItemId.ToString(), Constants.TestCase);
            File.WriteAllText(testCasePath, validJson);

            // Act
            var result = await _parserService.GetTestCase(WorkItemId);

            // Assert
            var expectedJson = JsonSerializer.Serialize(expectedTestCase);
            var actualJson = JsonSerializer.Serialize(result);
            Assert.That(actualJson, Is.EqualTo(expectedJson));
        }

        #endregion

        #region GetAttachment Tests

        [Test]
        public void GetAttachment_WhenFileNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetAttachment(WorkItemId, "nonexistent.txt"));

            Assert.That(exception!.Message, Is.EqualTo("Attachment file not found"));
        }

        [Test]
        public void GetAttachment_WhenDirectoryNotExists_ThrowFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _parserService.GetAttachment(Guid.NewGuid(), TestFileName));

            Assert.That(exception!.Message, Is.EqualTo("Attachment file not found"));
        }

        [Test]
        public async Task GetAttachment_WhenFileExists_ReturnFileStream()
        {
            // Arrange
            var filePath = Path.Combine(_testPath, WorkItemId.ToString(), TestFileName);
            var fileContent = "test content";
            await File.WriteAllTextAsync(filePath, fileContent);

            // Act
            using var result = await _parserService.GetAttachment(WorkItemId, TestFileName);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.CanRead, Is.True);
            Assert.That(result.Name, Does.EndWith(TestFileName));
            Assert.That(result.Length, Is.GreaterThan(0));

            using var reader = new StreamReader(result);
            var content = await reader.ReadToEndAsync();
            Assert.That(content, Is.EqualTo(fileContent));
        }

        [Test]
        public async Task GetAttachment_WithDifferentFileNames_ReturnsCorrectFiles()
        {
            // Arrange
            var fileName1 = "image.png";
            var filePath1 = Path.Combine(_testPath, WorkItemId.ToString(), fileName1);
            await File.WriteAllTextAsync(filePath1, "PNG content");

            var fileName2 = "document.pdf";
            var filePath2 = Path.Combine(_testPath, WorkItemId.ToString(), fileName2);
            await File.WriteAllTextAsync(filePath2, "PDF content");

            // Act & Assert
            using var result1 = await _parserService.GetAttachment(WorkItemId, fileName1);
            using var reader1 = new StreamReader(result1);
            var content1 = await reader1.ReadToEndAsync();
            Assert.That(content1, Is.EqualTo("PNG content"));

            using var result2 = await _parserService.GetAttachment(WorkItemId, fileName2);
            using var reader2 = new StreamReader(result2);
            var content2 = await reader2.ReadToEndAsync();
            Assert.That(content2, Is.EqualTo("PDF content"));
        }

        #endregion

        public void Dispose()
        {
            // Clean folder
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }
    }
}
