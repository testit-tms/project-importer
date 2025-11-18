using Importer.Client;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests;

[TestFixture]
public class ProjectServiceTests
{
    private Mock<ILogger<ProjectService>> _loggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private ProjectService _projectService = null!;
    private const string ProjectName = "Test Project";

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<ProjectService>>();
        _clientAdapterMock = new Mock<IClientAdapter>();
        _projectService = new ProjectService(_loggerMock.Object, _clientAdapterMock.Object);
    }

    [Test]
    public async Task ImportProject_WhenProjectExists_ReturnsExistingId()
    {
        // Arrange
        var existingProjectId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.GetProject(ProjectName))
            .ReturnsAsync(existingProjectId);

        // Act
        var result = await _projectService.ImportProject(ProjectName);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(existingProjectId));
            _clientAdapterMock.Verify(adapter => adapter.GetProject(ProjectName), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.CreateProject(It.IsAny<string>()), Times.Never);
            _loggerMock.VerifyLogging("Importing project", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public async Task ImportProject_WhenProjectNotExists_CreatesNewProject()
    {
        // Arrange
        var createdProjectId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.GetProject(ProjectName))
            .ReturnsAsync(Guid.Empty);
        _clientAdapterMock
            .Setup(adapter => adapter.CreateProject(ProjectName))
            .ReturnsAsync(createdProjectId);

        // Act
        var result = await _projectService.ImportProject(ProjectName);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(createdProjectId));
            _clientAdapterMock.Verify(adapter => adapter.GetProject(ProjectName), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.CreateProject(ProjectName), Times.Once);
            _loggerMock.VerifyLogging("Importing project", LogLevel.Information, Times.Once());
        });
    }
}

