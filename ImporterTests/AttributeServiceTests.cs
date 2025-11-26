using Importer.Client;
using Importer.Models;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;
using Attribute = Models.Attribute;

namespace ImporterTests;

[TestFixture]
public class AttributeServiceTests
{
    private Guid _projectId;

    private Mock<ILogger<AttributeService>> _loggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private AttributeService _attributeService = null!;

    private Attribute _optionsAttribute = null!;
    private Attribute _stringAttribute = null!;

    private TmsAttribute _tmsOptionsAttribute = null!;
    private TmsAttribute _tmsStringAttribute = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<AttributeService>>();
        _clientAdapterMock = new Mock<IClientAdapter>();
        _attributeService = new AttributeService(_loggerMock.Object, _clientAdapterMock.Object);

        _projectId = Guid.NewGuid();

        _stringAttribute = new Attribute
        {
            Id = Guid.Parse("8e2b4dc4-f6c3-472f-a58f-d57b968bbee6"),
            Name = "TestAttribute",
            IsActive = true,
            IsRequired = false,
            Type = AttributeType.String,
            Options = new List<string>()
        };

        _optionsAttribute = new Attribute
        {
            Id = Guid.Parse("9767ce0e-a214-4ebc-af69-71aa88b0ad0d"),
            Name = "TestAttribute2",
            IsActive = true,
            IsRequired = false,
            Type = AttributeType.Options,
            Options = new List<string> { "Option1", "Option2" }
        };

        _tmsStringAttribute = new TmsAttribute
        {
            Id = Guid.Parse("8e2b4dc4-f6c3-472f-a58f-d57b968bbe10"),
            Name = "TestAttribute",
            IsRequired = false,
            IsEnabled = true,
            Type = "String",
            Options = new List<TmsAttributeOptions>()
        };

        _tmsOptionsAttribute = new TmsAttribute
        {
            Id = Guid.Parse("9767ce0e-a214-4ebc-af69-71aa88b0ad10"),
            Name = "TestAttribute2",
            IsRequired = false,
            IsEnabled = true,
            Type = "Options",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = Guid.NewGuid(), Value = "Option1", IsDefault = true },
                new() { Id = Guid.NewGuid(), Value = "Option2", IsDefault = false }
            }
        };
    }

    [Test]
    public void ImportAttributes_WhenGetProjectAttributesFails_ThrowsException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Failed to get project attributes");

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ThrowsAsync(expectedException);

        // Act
        var actualException = Assert.ThrowsAsync<InvalidOperationException>(
            () => _attributeService.ImportAttributes(_projectId, new List<Attribute> { _stringAttribute, _optionsAttribute }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualException, Is.SameAs(expectedException));
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(It.IsAny<Attribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()), Times.Never);

            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, It.IsAny<TmsAttribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.IsAny<IEnumerable<Guid>>()), Times.Never);
        });
    }

    [Test]
    public void ImportAttributes_WhenImportAttributeFails_ThrowsException()
    {
        // Arrange
        var expectedException = new ApplicationException("Failed to import attribute");

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.ImportAttribute(_stringAttribute))
            .ThrowsAsync(expectedException);

        // Act
        var actualException = Assert.ThrowsAsync<ApplicationException>(
            () => _attributeService.ImportAttributes(_projectId, new List<Attribute> { _stringAttribute, _optionsAttribute }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualException, Is.SameAs(expectedException));
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(_stringAttribute), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(_optionsAttribute), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()), Times.Never);

            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, It.IsAny<TmsAttribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.IsAny<IEnumerable<Guid>>()), Times.Never);
        });
    }

    [Test]
    public void ImportAttributes_WhenUpdateAttributeFails_ThrowsException()
    {
        // Arrange
        var expectedException = new ApplicationException("Failed to update attribute");

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute> { _tmsOptionsAttribute });

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()))
            .ThrowsAsync(expectedException);

        // Act
        var actualException = Assert.ThrowsAsync<ApplicationException>(
            () => _attributeService.ImportAttributes(_projectId, new[] { _optionsAttribute }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualException, Is.SameAs(expectedException));
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);

            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(It.IsAny<Attribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()), Times.Once);

            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, It.IsAny<TmsAttribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.IsAny<IEnumerable<Guid>>()), Times.Never);
        });
    }

    [Test]
    public async Task ImportAttributes_WhenUpdatingExistingOptionsAttribute_AddsMissingOptionsAndReturnsLatestVersion()
    {
        // Arrange
        var existingAttribute = CloneAttribute(_tmsOptionsAttribute);
        existingAttribute.Options = new List<TmsAttributeOptions>
        {
            new() { Id = Guid.NewGuid(), Value = "Option1", IsDefault = true }
        };

        var updatedAttribute = CloneAttribute(existingAttribute);
        updatedAttribute.Options = new List<TmsAttributeOptions>
        {
            existingAttribute.Options[0],
            new() { Id = Guid.NewGuid(), Value = "Option2", IsDefault = false }
        };

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute> { existingAttribute });

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()))
            .ReturnsAsync((TmsAttribute attribute) => attribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributeById(existingAttribute.Id))
            .ReturnsAsync(updatedAttribute);

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, new[] { _optionsAttribute });

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(It.IsAny<Attribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.UpdateAttribute(It.Is<TmsAttribute>(attr =>
                attr.Options.Count == 2 && attr.Options.Any(option => option.Value == "Option2"))), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.Is<IEnumerable<Guid>>(ids =>
                ids.Single() == existingAttribute.Id)), Times.Once);

            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributeById(existingAttribute.Id), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.ContainsKey(_optionsAttribute.Id), Is.True);

            var resultAttribute = result[_optionsAttribute.Id];
            Assert.That(resultAttribute, Is.Not.Null);
            Assert.That(resultAttribute.Id, Is.EqualTo(updatedAttribute.Id));
            Assert.That(resultAttribute.Name, Is.EqualTo(updatedAttribute.Name));
            Assert.That(resultAttribute.Type, Is.EqualTo(updatedAttribute.Type));
            Assert.That(resultAttribute.IsRequired, Is.EqualTo(updatedAttribute.IsRequired));
            Assert.That(resultAttribute.IsEnabled, Is.EqualTo(updatedAttribute.IsEnabled));
            Assert.That(resultAttribute.Options, Is.Not.Null);
            Assert.That(resultAttribute.Options.Count, Is.EqualTo(updatedAttribute.Options.Count));
            for (var i = 0; i < resultAttribute.Options.Count; i++)
            {
                Assert.That(resultAttribute.Options[i].Value, Is.EqualTo(updatedAttribute.Options[i].Value));
                Assert.That(resultAttribute.Options[i].IsDefault, Is.EqualTo(updatedAttribute.Options[i].IsDefault));
            }
        });
    }

    [Test]
    public async Task ImportAttributes_WhenImportingNewAttributes_ReturnsImportedAttributesMap()
    {
        // Arrange
        var importedStringAttribute = CloneAttribute(_tmsStringAttribute);
        var importedOptionsAttribute = CloneAttribute(_tmsOptionsAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.ImportAttribute(_stringAttribute))
            .ReturnsAsync(importedStringAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.ImportAttribute(_optionsAttribute))
            .ReturnsAsync(importedOptionsAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetAttribute(importedStringAttribute.Id))
            .ReturnsAsync(importedStringAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetAttribute(importedOptionsAttribute.Id))
            .ReturnsAsync(importedOptionsAttribute);

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, new List<Attribute> { _stringAttribute, _optionsAttribute });

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(_stringAttribute), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(_optionsAttribute), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetAttribute(importedStringAttribute.Id), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetAttribute(importedOptionsAttribute.Id), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.UpdateAttribute(It.IsAny<TmsAttribute>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(importedStringAttribute.Id) && ids.Contains(importedOptionsAttribute.Id))), Times.Once);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.ContainsKey(_stringAttribute.Id), Is.True);
            Assert.That(result.ContainsKey(_optionsAttribute.Id), Is.True);

            var stringResult = result[_stringAttribute.Id];
            Assert.That(stringResult.Id, Is.EqualTo(importedStringAttribute.Id));
            Assert.That(stringResult.Name, Is.EqualTo(importedStringAttribute.Name));
            Assert.That(stringResult.Type, Is.EqualTo(importedStringAttribute.Type));
            Assert.That(stringResult.IsRequired, Is.EqualTo(importedStringAttribute.IsRequired));
            Assert.That(stringResult.IsEnabled, Is.EqualTo(importedStringAttribute.IsEnabled));
            Assert.That(stringResult.Options, Is.Not.Null);
            Assert.That(stringResult.Options.Count, Is.EqualTo(importedStringAttribute.Options.Count));

            var optionsResult = result[_optionsAttribute.Id];
            Assert.That(optionsResult.Id, Is.EqualTo(importedOptionsAttribute.Id));
            Assert.That(optionsResult.Name, Is.EqualTo(importedOptionsAttribute.Name));
            Assert.That(optionsResult.Type, Is.EqualTo(importedOptionsAttribute.Type));
            Assert.That(optionsResult.IsRequired, Is.EqualTo(importedOptionsAttribute.IsRequired));
            Assert.That(optionsResult.IsEnabled, Is.EqualTo(importedOptionsAttribute.IsEnabled));
            Assert.That(optionsResult.Options, Is.Not.Null);
            Assert.That(optionsResult.Options.Count, Is.EqualTo(importedOptionsAttribute.Options.Count));
            for (var i = 0; i < optionsResult.Options.Count; i++)
            {
                Assert.That(optionsResult.Options[i].Value, Is.EqualTo(importedOptionsAttribute.Options[i].Value));
                Assert.That(optionsResult.Options[i].IsDefault, Is.EqualTo(importedOptionsAttribute.Options[i].IsDefault));
            }
        });
    }

    [Test]
    public async Task ImportAttributes_WhenTypeMismatchRequiresRenaming_RenamesUntilUniqueAndImports()
    {
        // Arrange
        var baseName = _stringAttribute.Name;
        var expectedImportedName = $"{baseName} (2)";

        var conflictingAttribute = CloneAttribute(_tmsStringAttribute, name: baseName, type: "Options");
        var conflictingFirstRename = CloneAttribute(_tmsStringAttribute, name: $"{baseName} (1)", type: "Options");

        var importedAttribute = CloneAttribute(_tmsStringAttribute, id: Guid.NewGuid(), name: expectedImportedName);

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute> { conflictingAttribute, conflictingFirstRename });

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.ImportAttribute(It.Is<Attribute>(attr => attr.Name == expectedImportedName)))
            .ReturnsAsync(importedAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetAttribute(importedAttribute.Id))
            .ReturnsAsync(importedAttribute);

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, new[] { _stringAttribute });

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.ImportAttribute(It.Is<Attribute>(attr =>
                attr.Name == expectedImportedName && attr.Id == _stringAttribute.Id)), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.Is<IEnumerable<Guid>>(ids =>
                ids.Single() == importedAttribute.Id)), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetAttribute(importedAttribute.Id), Times.Once);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(_stringAttribute.Id), Is.True);

            var renamedResult = result[_stringAttribute.Id];
            Assert.That(renamedResult.Id, Is.EqualTo(importedAttribute.Id));
            Assert.That(renamedResult.Name, Is.EqualTo(importedAttribute.Name));
            Assert.That(renamedResult.Type, Is.EqualTo(importedAttribute.Type));
            Assert.That(renamedResult.IsRequired, Is.EqualTo(importedAttribute.IsRequired));
            Assert.That(renamedResult.IsEnabled, Is.EqualTo(importedAttribute.IsEnabled));
        });
    }

    [Test]
    public async Task ImportAttributes_WhenUnusedRequiredAttributesRemain_MarksThemAsOptional()
    {
        // Arrange
        var unusedAttributes = new List<TmsAttribute>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Unused1",
                IsRequired = true,
                Type = "String"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Unused2",
                IsRequired = true,
                Type = "Options"
            }
        };

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(unusedAttributes);

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, Array.Empty<Attribute>());

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, It.IsAny<TmsAttribute>()), Times.Exactly(2));
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.IsAny<IEnumerable<Guid>>()), Times.Never);

            Assert.That(unusedAttributes.All(attr => attr.IsRequired == false), Is.True);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public async Task ImportAttributes_WhenRequiredAttributeIsUsed_DoesNotUpdateProjectAttribute()
    {
        // Arrange
        var requiredAttributeToRemove = CloneAttribute(_tmsStringAttribute, id: Guid.NewGuid(), name: _stringAttribute.Name, type: _stringAttribute.Type.ToString());
        var stillRequiredAttribute = CloneAttribute(_tmsOptionsAttribute, id: Guid.NewGuid(), name: "StillRequired", type: "Options");

        var requiredAttributes = new List<TmsAttribute> { requiredAttributeToRemove, stillRequiredAttribute };

        var importedAttribute = CloneAttribute(_tmsStringAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(requiredAttributes);

        _clientAdapterMock
            .Setup(adapter => adapter.ImportAttribute(_stringAttribute))
            .ReturnsAsync(importedAttribute);

        _clientAdapterMock
            .Setup(adapter => adapter.GetAttribute(importedAttribute.Id))
            .ReturnsAsync(importedAttribute);

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, new[] { _stringAttribute });

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, stillRequiredAttribute), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, requiredAttributeToRemove), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.Is<IEnumerable<Guid>>(ids =>
                ids.Single() == importedAttribute.Id)), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.GetAttribute(importedAttribute.Id), Times.Once);

            Assert.That(requiredAttributes, Does.Not.Contain(requiredAttributeToRemove));
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(_stringAttribute.Id), Is.True);

            var resultAttribute = result[_stringAttribute.Id];
            Assert.That(resultAttribute.Id, Is.EqualTo(importedAttribute.Id));
            Assert.That(resultAttribute.Name, Is.EqualTo(importedAttribute.Name));
            Assert.That(resultAttribute.Type, Is.EqualTo(importedAttribute.Type));
            Assert.That(resultAttribute.IsRequired, Is.EqualTo(importedAttribute.IsRequired));
            Assert.That(resultAttribute.IsEnabled, Is.EqualTo(importedAttribute.IsEnabled));
        });
    }

    [Test]
    public async Task ImportAttributes_WhenNoAttributesProvided_DoesNotCallAddAttributesToProject()
    {
        // Arrange
        _clientAdapterMock
            .Setup(adapter => adapter.GetProjectAttributes())
            .ReturnsAsync(new List<TmsAttribute>());

        _clientAdapterMock
            .Setup(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId))
            .ReturnsAsync(new List<TmsAttribute>());

        // Act
        var result = await _attributeService.ImportAttributes(_projectId, Array.Empty<Attribute>());

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(adapter => adapter.GetProjectAttributes(), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.AddAttributesToProject(_projectId, It.IsAny<IEnumerable<Guid>>()), Times.Never);
            _clientAdapterMock.Verify(adapter => adapter.GetRequiredProjectAttributesByProjectId(_projectId), Times.Once);
            _clientAdapterMock.Verify(adapter => adapter.UpdateProjectAttribute(_projectId, It.IsAny<TmsAttribute>()), Times.Never);
            Assert.That(result, Is.Empty);
        });
    }

    private static TmsAttribute CloneAttribute(
        TmsAttribute source,
        Guid? id = null,
        string? name = null,
        string? type = null)
    {
        return new TmsAttribute
        {
            Id = id ?? source.Id,
            Name = name ?? source.Name,
            IsRequired = source.IsRequired,
            IsEnabled = source.IsEnabled,
            Type = type ?? source.Type,
            Options = source.Options?.Select(option => new TmsAttributeOptions
            {
                Id = option.Id,
                Value = option.Value,
                IsDefault = option.IsDefault
            }).ToList() ?? new List<TmsAttributeOptions>()
        };
    }
}

