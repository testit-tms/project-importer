using Importer.Client;
using Importer.Models;
using Importer.Services;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests;

[TestFixture]
public class SharedStepServiceTests
{
    private static readonly Guid ProjectId = Guid.Parse("8e2b4dc4-f6c3-472f-a58f-d57b968bbee7");
    private static readonly Guid SourceSectionId = Guid.Parse("9f3c5ed5-d7d4-483f-b69f-e68c079cffe8");
    private static readonly Guid TargetSectionId = Guid.Parse("7d1a4ec6-e8c5-494f-c70f-f79d168dffe9");

    private Mock<ILogger<SharedStepService>> _loggerMock = null!;
    private Mock<ILogger<BaseWorkItemService>> _baseWorkItemServiceLoggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private Mock<IParserService> _parserServiceMock = null!;
    private Mock<IAttachmentService> _attachmentServiceMock = null!;

    private IBaseWorkItemService _baseWorkItemService = null!;
    private SharedStepService _sharedStepService = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<SharedStepService>>();
        _baseWorkItemServiceLoggerMock = new Mock<ILogger<BaseWorkItemService>>();
        _clientAdapterMock = new Mock<IClientAdapter>(MockBehavior.Strict);
        _parserServiceMock = new Mock<IParserService>(MockBehavior.Strict);
        _attachmentServiceMock = new Mock<IAttachmentService>(MockBehavior.Strict);

        _baseWorkItemService = new BaseWorkItemService(_baseWorkItemServiceLoggerMock.Object, _clientAdapterMock.Object);

        _sharedStepService = new SharedStepService(
            _loggerMock.Object,
            _clientAdapterMock.Object,
            _parserServiceMock.Object,
            _baseWorkItemService,
            _attachmentServiceMock.Object);
    }

    [Test]
    public async Task ImportSharedSteps_WhenNoSharedStepsProvided_ReturnsEmptyAndSkipsDependencies()
    {
        // Arrange
        var sections = new Dictionary<Guid, Guid>();
        var attributes = new Dictionary<Guid, TmsAttribute>();

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId, Array.Empty<Guid>(), sections, attributes);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _parserServiceMock.Verify(service => service.GetSharedStep(It.IsAny<Guid>()), Times.Never);
            _attachmentServiceMock.Verify(service => service.GetAttachments(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<SharedStep>()), Times.Never);
        });
    }

    [Test]
    public void ImportSharedSteps_WhenSectionIsMissing_ThrowsAndSkipsImport()
    {
        // Arrange
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Missing section",
            SectionId = SourceSectionId,
            Steps = new List<Step> {
                new() { Action = "Action" }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        // Act
        var exception = Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sharedStepService.ImportSharedSteps(
                ProjectId,
                new[] { stepFromParser.Id },
                new Dictionary<Guid, Guid>(),
                new Dictionary<Guid, TmsAttribute>()));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);

            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(stepFromParser.Id, It.IsAny<IEnumerable<string>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<SharedStep>()), Times.Never);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenSharedStepHasAttributes_PassesConvertedAttributesToClient()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var importedSharedStepId = Guid.NewGuid();

        var originalAttributes = new List<CaseAttribute> {
            new() { Id = attributeId, Value = "Original" }
        };

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Target",
            Type = "String",
            Options = new List<TmsAttributeOptions>()
        };

        var attributesMap = new Dictionary<Guid, TmsAttribute> {
            { attributeId, tmsAttribute }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = originalAttributes,
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;

        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(importedSharedStepId);

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            attributesMap);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(stepFromParser.Id), Is.True);
            Assert.That(result[stepFromParser.Id], Is.EqualTo(importedSharedStepId));

            Assert.That(ReferenceEquals(stepFromParser, sharedStepSent), Is.True);
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Id, Is.EqualTo(stepFromParser.Id));
            Assert.That(sharedStepSent.Name, Is.EqualTo(stepFromParser.Name));
            Assert.That(sharedStepSent.SectionId, Is.EqualTo(SourceSectionId));

            Assert.That(sharedStepSent.Attributes, Is.Not.Null);
            Assert.That(ReferenceEquals(sharedStepSent.Attributes, originalAttributes), Is.False);
            Assert.That(sharedStepSent.Attributes.Count, Is.EqualTo(originalAttributes.Count));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(originalAttributes[0].Value.ToString()));

            Assert.That(sharedStepSent.Attachments, Is.Not.Null);
            Assert.That(sharedStepSent.Attachments, Is.Empty);
            Assert.That(sharedStepSent.Steps, Is.Not.Null);
            Assert.That(sharedStepSent.Steps, Is.Empty);

            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(s => s.Id == stepFromParser.Id)), Times.Once);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenSharedStepHasAttachments_ConvertsAttachmentsToStrings()
    {
        // Arrange
        var attachmentGuid = Guid.NewGuid();

        var attachments = new Dictionary<string, Guid> {
            { "file1.txt", attachmentGuid },
            { "file2.txt", Guid.NewGuid() }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string> { "file1.txt", "file2.txt" }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;

        var importedSharedStepId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(importedSharedStepId);

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId, new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(stepFromParser.Id), Is.True);
            Assert.That(result[stepFromParser.Id], Is.EqualTo(importedSharedStepId));

            Assert.That(ReferenceEquals(stepFromParser, sharedStepSent), Is.True);
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Id, Is.EqualTo(stepFromParser.Id));
            Assert.That(sharedStepSent.Name, Is.EqualTo(stepFromParser.Name));
            Assert.That(sharedStepSent.SectionId, Is.EqualTo(SourceSectionId));

            Assert.That(sharedStepSent.Attributes, Is.Not.Null);
            Assert.That(sharedStepSent.Attributes, Is.Empty);

            Assert.That(sharedStepSent.Attachments, Is.Not.Null);
            Assert.That(ReferenceEquals(sharedStepSent.Attachments, stepFromParser.Attachments), Is.True);
            Assert.That(sharedStepSent.Attachments, Is.EqualTo(attachments.Values.Select(g => g.ToString()).ToList()));

            Assert.That(sharedStepSent.Steps, Is.Not.Null);
            Assert.That(sharedStepSent.Steps, Is.Empty);

            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(stepFromParser.Id, It.IsAny<IEnumerable<string>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(s => s.Id == stepFromParser.Id)), Times.Once);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenSharedStepHasSteps_UpdatesStepsWithAttachments()
    {
        // Arrange
        var originalSteps = new List<Step> {
            new() { Action = "Original action" }
        };

        var attachments = new Dictionary<string, Guid> {
            { "file.txt", Guid.NewGuid() }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = originalSteps,
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string> { "file.txt" }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;

        var importedSharedStepId = Guid.NewGuid();

        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(importedSharedStepId);

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId, new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(stepFromParser.Id), Is.True);
            Assert.That(result[stepFromParser.Id], Is.EqualTo(importedSharedStepId));

            Assert.That(ReferenceEquals(stepFromParser, sharedStepSent), Is.True);
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Id, Is.EqualTo(stepFromParser.Id));
            Assert.That(sharedStepSent.Name, Is.EqualTo(stepFromParser.Name));
            Assert.That(sharedStepSent.SectionId, Is.EqualTo(SourceSectionId));

            Assert.That(sharedStepSent.Attributes, Is.Not.Null);
            Assert.That(sharedStepSent.Attributes, Is.Empty);

            Assert.That(sharedStepSent.Attachments, Is.Not.Null);
            Assert.That(sharedStepSent.Attachments.Count, Is.EqualTo(1));
            Assert.That(sharedStepSent.Attachments[0], Is.EqualTo(attachments["file.txt"].ToString()));

            Assert.That(sharedStepSent.Steps, Is.Not.Null);
            Assert.That(ReferenceEquals(sharedStepSent.Steps, originalSteps), Is.True);
            Assert.That(sharedStepSent.Steps.Count, Is.EqualTo(originalSteps.Count));

            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(stepFromParser.Id, It.IsAny<IEnumerable<string>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(s => s.Id == stepFromParser.Id)), Times.Once);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenImportSucceeds_ReturnsMappedIds()
    {
        // Arrange
        var importedSharedStepId = Guid.NewGuid();

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;

        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(importedSharedStepId);

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId, new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.ContainsKey(stepFromParser.Id), Is.True);
            Assert.That(result[stepFromParser.Id], Is.EqualTo(importedSharedStepId));

            Assert.That(ReferenceEquals(stepFromParser, sharedStepSent), Is.True);
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Id, Is.EqualTo(stepFromParser.Id));
            Assert.That(sharedStepSent.Name, Is.EqualTo(stepFromParser.Name));
            Assert.That(sharedStepSent.SectionId, Is.EqualTo(SourceSectionId));

            Assert.That(sharedStepSent.Attributes, Is.Not.Null);
            Assert.That(sharedStepSent.Attributes, Is.Empty);

            Assert.That(sharedStepSent.Attachments, Is.Not.Null);
            Assert.That(sharedStepSent.Attachments, Is.Empty);

            Assert.That(sharedStepSent.Steps, Is.Not.Null);
            Assert.That(sharedStepSent.Steps, Is.Empty);

            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(stepFromParser.Id, It.IsAny<IEnumerable<string>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(s => s.Id == stepFromParser.Id)), Times.Once);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenMultipleSharedStepsProvided_ImportsEachExactlyOnce()
    {
        // Arrange
        var attributesMap = new Dictionary<Guid, TmsAttribute>();

        var firstSharedStep = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "First step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var secondSharedStep = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Second step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(firstSharedStep.Id))
            .ReturnsAsync(firstSharedStep);

        _parserServiceMock
            .Setup(service => service.GetSharedStep(secondSharedStep.Id))
            .ReturnsAsync(secondSharedStep);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(() => new Dictionary<string, Guid>());

        var importedFirstId = Guid.NewGuid();
        var importedSecondId = Guid.NewGuid();

        _clientAdapterMock
            .SetupSequence(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .ReturnsAsync(importedFirstId)
            .ReturnsAsync(importedSecondId);

        // Act
        var result = await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { firstSharedStep.Id, secondSharedStep.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            attributesMap);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.ContainsKey(firstSharedStep.Id), Is.True);
            Assert.That(result.ContainsKey(secondSharedStep.Id), Is.True);
            Assert.That(result[firstSharedStep.Id], Is.EqualTo(importedFirstId));
            Assert.That(result[secondSharedStep.Id], Is.EqualTo(importedSecondId));

            _parserServiceMock.Verify(service => service.GetSharedStep(firstSharedStep.Id), Times.Once);
            _parserServiceMock.Verify(service => service.GetSharedStep(secondSharedStep.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(firstSharedStep.Id, firstSharedStep.Attachments), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(secondSharedStep.Id, secondSharedStep.Attachments), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(step => step.Id == firstSharedStep.Id)), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.Is<SharedStep>(step => step.Id == secondSharedStep.Id)), Times.Once);
        });
    }

    //Check with attributes and attachments 
    [Test]
    public async Task ImportSharedSteps_WhenAttributeTypeIsOptions_ConvertsToOptionId()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var optionValue = "Option1";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "options",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = optionId, Value = optionValue, IsDefault = false }
            }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = optionValue }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(optionId.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttributeTypeIsMultipleOptions_ConvertsToOptionIds()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var optionId1 = Guid.NewGuid();
        var optionId2 = Guid.NewGuid();
        var optionValue1 = "Option1";
        var optionValue2 = "Option2";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "multipleOptions",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = optionId1, Value = optionValue1, IsDefault = false },
                new() { Id = optionId2, Value = optionValue2, IsDefault = false }
            }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = $"[\"{optionValue1}\",\"{optionValue2}\"]" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.InstanceOf<List<string>>());
            
            var valueList = (List<string>)sharedStepSent.Attributes[0].Value;
            Assert.That(valueList, Has.Count.EqualTo(2));
            Assert.That(valueList, Does.Contain(optionId1.ToString()));
            Assert.That(valueList, Does.Contain(optionId2.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttributeTypeIsCheckbox_ConvertsToBoolean()
    {
        // Arrange
        var attributeId = Guid.NewGuid();

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "checkbox",
            Options = new List<TmsAttributeOptions>()
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = "true" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(true));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttributeValueIsGuid_AddsUuidPrefix()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var guidValue = Guid.NewGuid();

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "String",
            Options = new List<TmsAttributeOptions>()
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = guidValue.ToString() }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo($"uuid {guidValue}"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttributeValueIsString_ReturnsAsIs()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var stringValue = "Test String Value";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "String",
            Options = new List<TmsAttributeOptions>()
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = stringValue }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(stringValue));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenMultipleAttributes_ConvertsAll()
    {
        // Arrange
        var attributeId1 = Guid.NewGuid();
        var attributeId2 = Guid.NewGuid();

        var tmsAttribute1 = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "String Attribute",
            Type = "String",
            Options = new List<TmsAttributeOptions>()
        };

        var tmsAttribute2 = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Checkbox Attribute",
            Type = "checkbox",
            Options = new List<TmsAttributeOptions>()
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId1, Value = "String Value" },
                new() { Id = attributeId2, Value = "true" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>
            {
                { attributeId1, tmsAttribute1 },
                { attributeId2, tmsAttribute2 }
            });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(2));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute1.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo("String Value"));
            Assert.That(sharedStepSent.Attributes[1].Id, Is.EqualTo(tmsAttribute2.Id));
            Assert.That(sharedStepSent.Attributes[1].Value, Is.EqualTo(true));
        });
    }

    [Test]
    public void ImportSharedSteps_WhenAttributeNotFoundInMap_ThrowsKeyNotFoundException()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = "Value" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        // Act & Assert
        var exception = Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sharedStepService.ImportSharedSteps(ProjectId,
                new[] { stepFromParser.Id },
                new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
                new Dictionary<Guid, TmsAttribute>()));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            _parserServiceMock.Verify(service => service.GetSharedStep(stepFromParser.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenMultipleOptionsHasNewOption_AddsOptionDynamically()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var existingOptionId = Guid.NewGuid();
        var existingOptionValue = "ExistingOption";
        var newOptionValue = "NewOption";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "multipleOptions",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = existingOptionId, Value = existingOptionValue, IsDefault = false }
            }
        };

        var updatedTmsAttribute = new TmsAttribute
        {
            Id = tmsAttribute.Id,
            Name = "Test Attribute",
            Type = "multipleOptions",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = existingOptionId, Value = existingOptionValue, IsDefault = false },
                new() { Id = Guid.NewGuid(), Value = newOptionValue, IsDefault = false }
            }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = $"[\"{existingOptionValue}\",\"{newOptionValue}\"]" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _clientAdapterMock
            .Setup(service => service.UpdateAttribute(It.IsAny<TmsAttribute>()))
            .ReturnsAsync(updatedTmsAttribute);

        _clientAdapterMock
            .Setup(service => service.GetProjectAttributeById(tmsAttribute.Id))
            .ReturnsAsync(updatedTmsAttribute);

        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            _clientAdapterMock.Verify(service => service.UpdateAttribute(It.Is<TmsAttribute>(a => a.Id == tmsAttribute.Id)), Times.Once);
            _clientAdapterMock.Verify(service => service.GetProjectAttributeById(tmsAttribute.Id), Times.Once);
            _baseWorkItemServiceLoggerMock.VerifyLogging("add it dynamically", LogLevel.Warning);
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenMultipleOptionsHasEmptyString_SkipsEmptyOption()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var optionValue = "Option1";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "multipleOptions",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = optionId, Value = optionValue, IsDefault = false }
            }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = $"[\"{optionValue}\",\"\"]" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.InstanceOf<List<string>>());
            var valueList = (List<string>)sharedStepSent.Attributes[0].Value;
            Assert.That(valueList, Has.Count.EqualTo(1));
            Assert.That(valueList, Does.Contain(optionId.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttributeTypeCaseInsensitive_ConvertsCorrectly()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var optionValue = "Option1";

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "OPTIONS",
            Options = new List<TmsAttributeOptions>
            {
                new() { Id = optionId, Value = optionValue, IsDefault = false }
            }
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = optionValue }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(optionId.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenCheckboxValueIsFalse_ConvertsToFalse()
    {
        // Arrange
        var attributeId = Guid.NewGuid();

        var tmsAttribute = new TmsAttribute
        {
            Id = Guid.NewGuid(),
            Name = "Test Attribute",
            Type = "checkbox",
            Options = new List<TmsAttributeOptions>()
        };

        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>(),
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = "false" }
            },
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(new Dictionary<string, Guid>());

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Attributes, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Attributes[0].Id, Is.EqualTo(tmsAttribute.Id));
            Assert.That(sharedStepSent.Attributes[0].Value, Is.EqualTo(false));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenStepHasActionAttachments_AddsAttachmentsToAction()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action text",
                    ActionAttachments = new List<string> { "image.jpg" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Contain(attachmentId.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenStepHasExpectedAttachments_AddsAttachmentsToExpected()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Expected = "Expected text",
                    ExpectedAttachments = new List<string> { "image.jpg" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Expected, Does.Contain(attachmentId.ToString()));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenStepHasTestDataAttachments_AddsAttachmentsToTestData()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    TestData = "Test data text",
                    TestDataAttachments = new List<string> { "data.txt" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "data.txt", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].TestData, Does.Contain("data.txt"));
            Assert.That(sharedStepSent.Steps[0].TestData, Does.Contain("File attached to test case"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenAttachmentNotFound_RemovesBrokenLink()
    {
        // Arrange
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action with <<<missing.jpg>>>",
                    ActionAttachments = new List<string> { "missing.jpg" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>();

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Not.Contain("<<<missing.jpg>>>"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenImageAttachment_AddsImageTag()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action text",
                    ActionAttachments = new List<string> { "image.png" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.png", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Contain($"<img src=\"/api/Attachments/{attachmentId}\">"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenActionContainsPlaceholder_ReplacesPlaceholderWithImageTag()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action text with <<<image.jpg>>> attachment",
                    ActionAttachments = new List<string> { "image.jpg" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Not.Contain("<<<image.jpg>>>"));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Contain($"<p> <img src=\"/api/Attachments/{attachmentId}\"> </p>"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenExpectedContainsPlaceholder_ReplacesPlaceholderWithImageTag()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Expected = "Expected text with <<<image.png>>> attachment",
                    ExpectedAttachments = new List<string> { "image.png" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.png", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Expected, Does.Not.Contain("<<<image.png>>>"));
            Assert.That(sharedStepSent.Steps[0].Expected, Does.Contain($"<p> <img src=\"/api/Attachments/{attachmentId}\"> </p>"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenTestDataContainsPlaceholder_ReplacesPlaceholderWithImageTag()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    TestData = "Test data with <<<data.txt>>> attachment",
                    TestDataAttachments = new List<string> { "data.txt" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "data.txt", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].TestData, Does.Not.Contain("<<<data.txt>>>"));
            Assert.That(sharedStepSent.Steps[0].TestData, Does.Contain($"<p> <img src=\"/api/Attachments/{attachmentId}\"> </p>"));
        });
    }

    [Test]
    public async Task ImportSharedSteps_WhenPlaceholderInsideHtmlTag_MovesImageTagOutsideHtmlTag()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var stepFromParser = new SharedStep
        {
            Id = Guid.NewGuid(),
            Name = "Shared step",
            SectionId = SourceSectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "<p>Action text with <<<image.jpg>>> inside tag</p>",
                    ActionAttachments = new List<string> { "image.jpg" }
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetSharedStep(stepFromParser.Id))
            .ReturnsAsync(stepFromParser);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(stepFromParser.Id, stepFromParser.Attachments))
            .ReturnsAsync(attachments);

        SharedStep? sharedStepSent = null;
        _clientAdapterMock
            .Setup(service => service.ImportSharedStep(ProjectId, TargetSectionId, It.IsAny<SharedStep>()))
            .Callback<Guid, Guid, SharedStep>((_, _, sharedStep) => sharedStepSent = sharedStep)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sharedStepService.ImportSharedSteps(ProjectId,
            new[] { stepFromParser.Id },
            new Dictionary<Guid, Guid> { { SourceSectionId, TargetSectionId } },
            new Dictionary<Guid, TmsAttribute>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sharedStepSent, Is.Not.Null);
            Assert.That(sharedStepSent!.Steps, Has.Count.EqualTo(1));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Not.Contain("<<<image.jpg>>>"));
            Assert.That(sharedStepSent.Steps[0].Action, Does.Contain($"<p> <img src=\"/api/Attachments/{attachmentId}\"> </p>"));
            // Verify that image tag is outside the original <p> tag
            Assert.That(sharedStepSent.Steps[0].Action, Does.Match(@"<p>Action text with\s+inside tag</p>\s*<p>\s*<img"));
        });
    }
}

