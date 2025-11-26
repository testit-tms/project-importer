using Importer.Client;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests;

[TestFixture]
public class SectionServiceTests
{
    private Mock<ILogger<SectionService>> _loggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private SectionService _sectionService = null!;

    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _rootSectionId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<SectionService>>();
        _clientAdapterMock = new Mock<IClientAdapter>();
        _sectionService = new SectionService(_loggerMock.Object, _clientAdapterMock.Object);

        _clientAdapterMock
            .Setup(adapter => adapter.GetRootSectionId(_projectId))
            .ReturnsAsync(_rootSectionId);
    }

    [Test]
    public async Task ImportSections_WhenNoSections_ReturnsEmptyMap()
    {
        // Arrange
        var sections = Array.Empty<Section>();

        // Act
        var result = await _sectionService.ImportSections(_projectId, sections);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _clientAdapterMock.Verify(adapter => adapter.GetRootSectionId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Section>()), Times.Never);
            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public async Task ImportSections_WithSingleSection_ReturnsCorrectMapping()
    {
        // Arrange
        var section = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Single"
        };
        var newSectionId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, section))
            .ReturnsAsync(newSectionId);

        // Act
        var result = await _sectionService.ImportSections(_projectId, new[] { section });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[section.Id], Is.EqualTo(newSectionId));

            _clientAdapterMock.Verify(adapter => adapter.GetRootSectionId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, _rootSectionId, section), Times.Once);

            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public async Task ImportSections_WhenMultipleSections_ImportsAllSections()
    {
        // Arrange
        var firstSection = new Section { Id = Guid.NewGuid(), Name = "Section 1" };
        var secondSection = new Section { Id = Guid.NewGuid(), Name = "Section 2" };

        var newSection1Id = Guid.NewGuid();
        var newSection2Id = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, firstSection))
            .ReturnsAsync(newSection1Id);
        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, secondSection))
            .ReturnsAsync(newSection2Id);

        // Act
        var result = await _sectionService.ImportSections(_projectId, new[] { firstSection, secondSection });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[firstSection.Id], Is.EqualTo(newSection1Id));
            Assert.That(result[secondSection.Id], Is.EqualTo(newSection2Id));

            _clientAdapterMock.Verify(adapter => adapter.GetRootSectionId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, _rootSectionId, firstSection),
                Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, _rootSectionId, secondSection),
                Times.Once);

            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public async Task ImportSections_WithNestedSections_ReturnsCorrectMapping()
    {
        // Arrange
        var childSection = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Child",
            Sections = new List<Section>()
        };

        var parentSection = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Parent",
            Sections = new List<Section> { childSection }
        };


        var newParentSectionId = Guid.NewGuid();
        var newChildSectionId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, parentSection))
            .ReturnsAsync(newParentSectionId);

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, newParentSectionId, childSection))
            .ReturnsAsync(newChildSectionId);

        // Act
        var result = await _sectionService.ImportSections(_projectId, new[] { parentSection });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[parentSection.Id], Is.EqualTo(newParentSectionId));
            Assert.That(result[childSection.Id], Is.EqualTo(newChildSectionId));

            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, _rootSectionId, parentSection),
                Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, newParentSectionId, childSection),
                Times.Once);
        });
    }

    [Test]
    public async Task ImportSections_WithNestedSections_LogsAndCallsExpectedApiSequence()
    {
        // Arrange
        var childSection = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Child",
            Sections = new List<Section>()
        };

        var parentSection = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Parent",
            Sections = new List<Section> { childSection }
        };

        var capturedParentIds = new List<Guid>();
        var capturedSections = new List<Section>();
        var createdParentId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, parentSection))
            .Callback<Guid, Guid, Section>((_, parentId, section) =>
            {
                capturedParentIds.Add(parentId);
                capturedSections.Add(section);
            })
            .ReturnsAsync(createdParentId);

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, createdParentId, childSection))
            .Callback<Guid, Guid, Section>((_, parentId, section) =>
            {
                capturedParentIds.Add(parentId);
                capturedSections.Add(section);
            })
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sectionService.ImportSections(_projectId, new[] { parentSection });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(capturedSections, Is.EquivalentTo(new[] { parentSection, childSection }));
            Assert.That(capturedParentIds, Has.Count.EqualTo(2));
            Assert.That(capturedParentIds[0], Is.EqualTo(_rootSectionId));
            Assert.That(capturedParentIds[1], Is.EqualTo(createdParentId));

            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, It.IsAny<Guid>(), It.IsAny<Section>()),
                Times.Exactly(2));
            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public void ImportSections_WhenGetRootSectionIdFails_PropagatesException()
    {
        // Arrange
        var sections = new[]
        {
            new Section
            {
                Id = Guid.NewGuid(),
                Name = "Any"
            }
        };

        var expectedException = new InvalidOperationException("Failed to resolve root section");

        _clientAdapterMock
            .Setup(adapter => adapter.GetRootSectionId(_projectId))
            .ThrowsAsync(expectedException);

        // Act
        var actualException = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sectionService.ImportSections(_projectId, sections));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualException, Is.SameAs(expectedException));
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Section>()), Times.Never);
            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
        });
    }

    [Test]
    public void ImportSections_WhenNestedImportFails_StopsProcessingAndPropagatesException()
    {
        // Arrange
        var parentSection = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Parent",
            Sections = new List<Section>()
        };

        var failingChild = new Section
        {
            Id = Guid.NewGuid(),
            Name = "Failing child"
        };

        parentSection.Sections.Add(failingChild);

        var createdParentId = Guid.NewGuid();
        var expectedException = new ApplicationException("Import failed");

        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, _rootSectionId, parentSection))
            .ReturnsAsync(createdParentId);
        _clientAdapterMock
            .Setup(adapter => adapter.ImportSection(_projectId, createdParentId, failingChild))
            .ThrowsAsync(expectedException);

        // Act
        var actualException = Assert.ThrowsAsync<ApplicationException>(
            () => _sectionService.ImportSections(_projectId, new[] { parentSection }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualException, Is.SameAs(expectedException));
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, createdParentId, failingChild),
                Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportSection(_projectId, It.IsAny<Guid>(), It.IsAny<Section>()),
                Times.AtLeastOnce);

            _loggerMock.VerifyLogging("Importing sections", LogLevel.Information, Times.Once());
            _loggerMock.VerifyLogging("Imported section", LogLevel.Debug, Times.Never());
        });
    }

    [Test]
    public void ImportSections_WhenSectionsIsNull_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<NullReferenceException>(() => 
            _sectionService.ImportSections(_projectId, null!));
    }
}

