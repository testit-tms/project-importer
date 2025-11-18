using Importer.Client;
using Importer.Models;
using Importer.Services;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;
using Attribute = Models.Attribute;

namespace ImporterTests
{
    [TestFixture]
    public class ImportServiceTests
    {
        private Mock<ILogger<ImportService>> _loggerMock = null!;
        private Mock<IParserService> _parserServiceMock = null!;
        private Mock<IClientAdapter> _clientAdapterMock = null!;
        private Mock<IAttributeService> _attributeServiceMock = null!;
        private Mock<ISectionService> _sectionServiceMock = null!;
        private Mock<ISharedStepService> _sharedStepServiceMock = null!;
        private Mock<ITestCaseService> _testCaseServiceMock = null!;
        private Mock<IProjectService> _projectServiceMock = null!;
        private ImportService _importService = null!;

        // Test data
        private static readonly Guid ProjectId = Guid.Parse("8e2b4dc4-f6c3-472f-a58f-d57b968bbee7");
        private static readonly string ProjectName = "Test Project";
        private Root _mainJsonResult = null!;
        private Dictionary<Guid, Guid> _sectionsMap = null!;
        private Dictionary<Guid, TmsAttribute> _attributesMap = null!;
        private Dictionary<Guid, Guid> _sharedStepsMap = null!;
        private List<string> _notImportedTestCases = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ImportService>>();
            _parserServiceMock = new Mock<IParserService>();
            _clientAdapterMock = new Mock<IClientAdapter>();
            _attributeServiceMock = new Mock<IAttributeService>();
            _sectionServiceMock = new Mock<ISectionService>();
            _sharedStepServiceMock = new Mock<ISharedStepService>();
            _testCaseServiceMock = new Mock<ITestCaseService>();
            _projectServiceMock = new Mock<IProjectService>();

            InitializeTestData();

            _importService = new ImportService(
                _loggerMock.Object,
                _parserServiceMock.Object,
                _clientAdapterMock.Object,
                _attributeServiceMock.Object,
                _sectionServiceMock.Object,
                _sharedStepServiceMock.Object,
                _testCaseServiceMock.Object,
                _projectServiceMock.Object
            );
        }

        private void InitializeTestData()
        {
            var sectionId = Guid.NewGuid();
            var attributeId = Guid.NewGuid();
            var sharedStepId = Guid.NewGuid();
            var testCaseId = Guid.NewGuid();

            _mainJsonResult = new Root
            {
                ProjectName = ProjectName,
                Sections = new List<Section> { new() { Id = sectionId } },
                Attributes = new List<Attribute> { new() { Id = attributeId } },
                SharedSteps = new List<Guid> { sharedStepId },
                TestCases = new List<Guid> { testCaseId }
            };

            _sectionsMap = new Dictionary<Guid, Guid>
            {
                { sectionId, Guid.NewGuid() }
            };

            _attributesMap = new Dictionary<Guid, TmsAttribute>
            {
                { attributeId, new TmsAttribute { Id = Guid.NewGuid() } }
            };

            _sharedStepsMap = new Dictionary<Guid, Guid>
            {
                { sharedStepId, Guid.NewGuid() }
            };

            _notImportedTestCases = new List<string> { "Failed Test Case 1", "Failed Test Case 2" };
        }

        [Test]
        public void ImportProject_WhenGetMainFileFails_ThrowsException()
        {
            // Arrange
            var expectedException = new Exception("Failed to get main file");
            _parserServiceMock.Setup(x => x.GetMainFile()).ThrowsAsync(expectedException);

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _importService.ImportProject());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo(expectedException.Message), "The exception message must match");

                _projectServiceMock.Verify(x => x.ImportProject(It.IsAny<string>()), Times.Never);

                _sectionServiceMock.Verify(x => x.ImportSections(It.IsAny<Guid>(), It.IsAny<IEnumerable<Section>>()), Times.Never);

                _attributeServiceMock.Verify(x => x.ImportAttributes(It.IsAny<Guid>(), It.IsAny<IEnumerable<Attribute>>()), Times.Never);

                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>()), Times.Never);

                _testCaseServiceMock.Verify(x => x.ImportTestCases(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>(), It.IsAny<Dictionary<Guid, Guid>>()), Times.Never);
            });
        }

        [Test]
        public void ImportProject_WhenImportProjectFails_ThrowsException()
        {
            // Arrange
            var expectedException = new Exception("Failed to import project");
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ThrowsAsync(expectedException);

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _importService.ImportProject());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo(expectedException.Message), "The exception message must match");

                _parserServiceMock.Verify(x => x.GetMainFile(), Times.Once);
                _projectServiceMock.Verify(x => x.ImportProject(ProjectName), Times.Once);

                // Checking that no subsequent services were called
                _sectionServiceMock.Verify(x => x.ImportSections(It.IsAny<Guid>(), It.IsAny<IEnumerable<Section>>()), Times.Never);

                _attributeServiceMock.Verify(x => x.ImportAttributes(It.IsAny<Guid>(), It.IsAny<IEnumerable<Attribute>>()), Times.Never);

                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>()), Times.Never);

                _testCaseServiceMock.Verify(x => x.ImportTestCases(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>(), It.IsAny<Dictionary<Guid, Guid>>()), Times.Never);
            });
          }

        [Test]
        public void ImportProject_WhenImportSectionsFails_ThrowsException()
        {
            // Arrange
            var expectedException = new Exception("Failed to import sections");
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ReturnsAsync(ProjectId);
            _sectionServiceMock.Setup(x => x.ImportSections(ProjectId, _mainJsonResult.Sections)).ThrowsAsync(expectedException);

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _importService.ImportProject());
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo(expectedException.Message), "The exception message must match");

                _parserServiceMock.Verify(x => x.GetMainFile(), Times.Once);
                _projectServiceMock.Verify(x => x.ImportProject(ProjectName), Times.Once);
                _sectionServiceMock.Verify(x => x.ImportSections(ProjectId, _mainJsonResult.Sections), Times.Once);

                // Checking that no subsequent services were called
                _attributeServiceMock.Verify(x => x.ImportAttributes(It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<Attribute>>()), Times.Never);

                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>()), Times.Never);

                _testCaseServiceMock.Verify(x => x.ImportTestCases(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>(), It.IsAny<Dictionary<Guid, Guid>>()), Times.Never);
            });
        }

        [Test]
        public void ImportProject_WhenImportAttributeFails_ThrowsException()
        {
            // Arrange
            var expectedException = new Exception("Failed to import attributes");
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ReturnsAsync(ProjectId);
            _sectionServiceMock.Setup(x => x.ImportSections(ProjectId, _mainJsonResult.Sections)).ReturnsAsync(_sectionsMap);
            _attributeServiceMock.Setup(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes)).ThrowsAsync(expectedException);

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _importService.ImportProject());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo(expectedException.Message), "Сообщение исключения должно совпадать");

                _parserServiceMock.Verify(x => x.GetMainFile(), Times.Once);
                _projectServiceMock.Verify(x => x.ImportProject(ProjectName), Times.Once);
                _sectionServiceMock.Verify(x => x.ImportSections(ProjectId, _mainJsonResult.Sections), Times.Once);
                _attributeServiceMock.Verify(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes), Times.Once);

                // Checking that no subsequent services were called
                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>()), Times.Never);

                _testCaseServiceMock.Verify(x => x.ImportTestCases(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>(), It.IsAny<Dictionary<Guid, Guid>>()), Times.Never);
            });
        }

        [Test]
        public void ImportProject_WhenImportSharedStepFails_ThrowsException()
        {
            // Arrange
            var expectedException = new Exception("Failed to import shared steps");
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ReturnsAsync(ProjectId);
            _sectionServiceMock.Setup(x => x.ImportSections(ProjectId, _mainJsonResult.Sections)).ReturnsAsync(_sectionsMap);
            _attributeServiceMock.Setup(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes)).ReturnsAsync(_attributesMap);
            _sharedStepServiceMock.Setup(x => x.ImportSharedSteps(ProjectId, _mainJsonResult.SharedSteps, _sectionsMap, _attributesMap))
                .ThrowsAsync(expectedException);

            // Act & Asser
            var exception = Assert.ThrowsAsync<Exception>(async () => await _importService.ImportProject());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo(expectedException.Message));

                _parserServiceMock.Verify(x => x.GetMainFile(), Times.Once);
                _projectServiceMock.Verify(x => x.ImportProject(ProjectName), Times.Once);
                _sectionServiceMock.Verify(x => x.ImportSections(ProjectId, _mainJsonResult.Sections), Times.Once);
                _attributeServiceMock.Verify(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes), Times.Once);
                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(ProjectId, _mainJsonResult.SharedSteps, _sectionsMap, _attributesMap), Times.Once);

                // Checking that no subsequent services were called
                _testCaseServiceMock.Verify(x => x.ImportTestCases(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Dictionary<Guid, Guid>>(), It.IsAny<Dictionary<Guid, TmsAttribute>>(), It.IsAny<Dictionary<Guid, Guid>>()), Times.Never, "Сервис тест-кейсов не должен вызываться при ошибке общих шагов");

            });
        }

        [Test]
        public async Task ImportProject_WhenTestCaseServiceReturnsFailedCases_LogsFailedCases()
        {
            // Arrange
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ReturnsAsync(ProjectId);
            _sectionServiceMock.Setup(x => x.ImportSections(ProjectId, _mainJsonResult.Sections)).ReturnsAsync(_sectionsMap);
            _attributeServiceMock.Setup(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes)).ReturnsAsync(_attributesMap);
            _sharedStepServiceMock.Setup(x => x.ImportSharedSteps(ProjectId, _mainJsonResult.SharedSteps, _sectionsMap, _attributesMap))
                .ReturnsAsync(_sharedStepsMap);
            _testCaseServiceMock.Setup(x => x.ImportTestCases(ProjectId, _mainJsonResult.TestCases, _sectionsMap, _attributesMap, _sharedStepsMap))
                .ReturnsAsync(_notImportedTestCases);

            // Act
            await _importService.ImportProject();

            // Assert
            Assert.Multiple(() =>
            {
                _loggerMock.VerifyLogging("Not imported test cases:", LogLevel.Error);
                _loggerMock.VerifyLogging(_notImportedTestCases[0], LogLevel.Information);
                _loggerMock.VerifyLogging(_notImportedTestCases[1], LogLevel.Information);
                _loggerMock.VerifyLogging("Project imported", LogLevel.Information);
            });
        }

        [Test]
        public async Task ImportProject_WhenSuccessful_ImportsAllComponents()
        {
            // Arrange
            _parserServiceMock.Setup(x => x.GetMainFile()).ReturnsAsync(_mainJsonResult);
            _projectServiceMock.Setup(x => x.ImportProject(ProjectName)).ReturnsAsync(ProjectId);
            _sectionServiceMock.Setup(x => x.ImportSections(ProjectId, _mainJsonResult.Sections)).ReturnsAsync(_sectionsMap);
            _attributeServiceMock.Setup(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes)).ReturnsAsync(_attributesMap);
            _sharedStepServiceMock.Setup(x => x.ImportSharedSteps(ProjectId, _mainJsonResult.SharedSteps, _sectionsMap, _attributesMap))
                .ReturnsAsync(_sharedStepsMap);
            _testCaseServiceMock.Setup(x => x.ImportTestCases(ProjectId, _mainJsonResult.TestCases, _sectionsMap, _attributesMap, _sharedStepsMap))
                .ReturnsAsync(new List<string>());

            // Act
            await _importService.ImportProject();

            // Assert
            Assert.Multiple(() =>
            {
                _parserServiceMock.Verify(x => x.GetMainFile(), Times.Once);

                _projectServiceMock.Verify(x => x.ImportProject(ProjectName), Times.Once);

                _sectionServiceMock.Verify(x => x.ImportSections(ProjectId, _mainJsonResult.Sections), Times.Once);

                _attributeServiceMock.Verify(x => x.ImportAttributes(ProjectId, _mainJsonResult.Attributes), Times.Once);

                _sharedStepServiceMock.Verify(x => x.ImportSharedSteps(ProjectId, _mainJsonResult.SharedSteps,
                    _sectionsMap, _attributesMap), Times.Once);

                _testCaseServiceMock.Verify(x => x.ImportTestCases(ProjectId, _mainJsonResult.TestCases,
                    _sectionsMap, _attributesMap, _sharedStepsMap), Times.Once);

                _loggerMock.VerifyLogging("Importing project", LogLevel.Information);

                _loggerMock.VerifyLogging("Project imported", LogLevel.Information);
            });
        }
    }
}
