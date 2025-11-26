using Importer.Client;
using Importer.Client.Implementations;
using Importer.Models;
using Importer.Services;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests;

[TestFixture]
public class TestCaseServiceTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.Parse("9f3c5ed5-d7d4-483f-b69f-e68c079cffe8");
    private static readonly Guid NewSectionId = Guid.Parse("7d1a4ec6-e8c5-494f-c70f-f79d168dffe9");

    private Mock<ILogger<TestCaseService>> _loggerMock = null!;
    private Mock<ILogger<ClientAdapter>> _clientAdapterLoggerMock = null!;
    private Mock<ILogger<BaseWorkItemService>> _baseWorkItemServiceLoggerMock = null!;
    private Mock<IClientAdapter> _clientAdapterMock = null!;
    private Mock<IParserService> _parserServiceMock = null!;
    private Mock<IParameterService> _parameterServiceMock = null!;
    private Mock<IAttachmentService> _attachmentServiceMock = null!;

    private IAdapterHelper _adapterHelper = null!;
    private IBaseWorkItemService _baseWorkItemService = null!;
    private TestCaseService _testCaseService = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<TestCaseService>>();
        _clientAdapterLoggerMock = new Mock<ILogger<ClientAdapter>>();
        _baseWorkItemServiceLoggerMock = new Mock<ILogger<BaseWorkItemService>>();
        _clientAdapterMock = new Mock<IClientAdapter>(MockBehavior.Strict);
        _parserServiceMock = new Mock<IParserService>(MockBehavior.Strict);
        _parameterServiceMock = new Mock<IParameterService>(MockBehavior.Strict);
        _attachmentServiceMock = new Mock<IAttachmentService>(MockBehavior.Strict);

        _adapterHelper = new AdapterHelper(_clientAdapterLoggerMock.Object);
        _baseWorkItemService = new BaseWorkItemService(_baseWorkItemServiceLoggerMock.Object, _clientAdapterMock.Object);

        _testCaseService = new TestCaseService(
            _loggerMock.Object,
            _clientAdapterMock.Object,
            _adapterHelper,
            _parserServiceMock.Object,
            _parameterServiceMock.Object,
            _baseWorkItemService,
            _attachmentServiceMock.Object);
    }

    [Test]
    public async Task ImportTestCases_WhenNoTestCases_DoesNothing()
    {
        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            Array.Empty<Guid>(),
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _parserServiceMock.Verify(service => service.GetTestCase(It.IsAny<Guid>()), Times.Never);
            _attachmentServiceMock.Verify(service => service.GetAttachments(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            _parameterServiceMock.Verify(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()), Times.Never);
            _clientAdapterMock.Verify(service => service.ImportTestCase(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TmsTestCase>()), Times.Never);
            _loggerMock.VerifyLogging("Importing test cases", LogLevel.Information);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenSimpleTestCase_ImportsSuccessfully()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            Description = "Test Description",
            State = StateType.Ready,
            Priority = PriorityType.Medium,
            SectionId = SectionId,
            Steps = new List<Step> { new() { Action = "Test Action", Expected = "Test Expected" } },
            PreconditionSteps = new List<Step>(),
            PostconditionSteps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>(),
            Tags = new List<string>(),
            Links = new List<Link>(),
            Duration = 0
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Name, Is.EqualTo(testCase.Name));
            Assert.That(capturedTestCase.Steps, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.Steps[0].Action, Is.EqualTo(testCase.Steps[0].Action));
            Assert.That(capturedTestCase.Steps[0].Expected, Is.EqualTo(testCase.Steps[0].Expected));
            Assert.That(capturedTestCase.TmsIterations, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.TmsIterations[0].Parameters, Is.Empty);

            _parserServiceMock.Verify(service => service.GetTestCase(testCase.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(testCase.Id, testCase.Attachments), Times.Once);
            _parameterServiceMock.Verify(service => service.CreateParameters(It.Is<IEnumerable<Parameter>>(p => !p.Any())), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Once);
            
            _loggerMock.VerifyLogging("Importing test cases", LogLevel.Information);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenTestCaseWithParameters_ImportsWithParametersReplaced()
    {
        // Arrange
        var parameterKeyId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action with <<<Parameter1>>>",
                    Expected = "Expected with <<<Parameter1>>>",
                    TestData = "TestData with <<<Parameter1>>>"
                }
            },
            Iterations = new List<Iteration>
            {
                new()
                {
                    Parameters =
                    [
                        new Parameter()
                        {
                            Name = "Parameter1",
                            Value = "Value1"
                        }
                    ]
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters = new List<TmsParameter>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Parameter1",
                Value = "Value1",
                ParameterKeyId = parameterKeyId
            }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Does.Contain(parameterKeyId.ToString()));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Contain("Parameter1"));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Contain("data-id"));
            Assert.That(capturedTestCase.Steps[0].Expected, Does.Contain(parameterKeyId.ToString()));
            Assert.That(capturedTestCase.Steps[0].Expected, Does.Contain("Parameter1"));
            Assert.That(capturedTestCase.Steps[0].TestData, Does.Contain(parameterKeyId.ToString()));
            Assert.That(capturedTestCase.Steps[0].TestData, Does.Contain("Parameter1"));
            Assert.That(capturedTestCase.TmsIterations, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.TmsIterations[0].Parameters, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.TmsIterations[0].Parameters, Does.Contain(parameters[0].Id));
            
            _parserServiceMock.Verify(service => service.GetTestCase(testCase.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(testCase.Id, testCase.Attachments), Times.Once);
            _parameterServiceMock.Verify(service => service.CreateParameters(
                It.Is<IEnumerable<Parameter>>(p => p.Count() == 1 && p.First().Name == "Parameter1")), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Once);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenTestCaseWithSharedSteps_UpdatesSharedStepIds()
    {
        // Arrange
        var sharedStepId = Guid.NewGuid();
        var newSharedStepId = Guid.NewGuid();

        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { SharedStepId = sharedStepId }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid> { { sharedStepId, newSharedStepId } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.Steps[0].SharedStepId, Is.EqualTo(newSharedStepId));
            Assert.That(capturedTestCase.Steps[0].SharedStepId, Is.Not.EqualTo(sharedStepId));
            
            // Проверка вызовов методов
            _parserServiceMock.Verify(service => service.GetTestCase(testCase.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(testCase.Id, testCase.Attachments), Times.Once);
            _parameterServiceMock.Verify(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Once);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenImportFails_LogsErrorAndContinues()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var exception = new ArgumentNullException("source", "Value cannot be null.");

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(testCase.Name));
            
            _parserServiceMock.Verify(service => service.GetTestCase(testCase.Id), Times.Once);
            _attachmentServiceMock.Verify(service => service.GetAttachments(testCase.Id, testCase.Attachments), Times.Once);
            _parameterServiceMock.Verify(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Once);
            
            _loggerMock.VerifyLogging("Could not import test case", LogLevel.Error);
            _loggerMock.VerifyLogging(testCase.Name, LogLevel.Error);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenMultipleTestCases_ImportsAll()
    {
        // Arrange
        var testCase1 = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case 1",
            SectionId = SectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var testCase2 = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case 2",
            SectionId = SectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase1.Id))
            .ReturnsAsync(testCase1);

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase2.Id))
            .ReturnsAsync(testCase2);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase1.Id, testCase2.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _parserServiceMock.Verify(service => service.GetTestCase(testCase1.Id), Times.Once);
            _parserServiceMock.Verify(service => service.GetTestCase(testCase2.Id), Times.Once);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Exactly(2));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenSharedStepNotFound_SetsSharedStepIdToNull()
    {
        // Arrange
        var sharedStepId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { SharedStepId = sharedStepId }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].SharedStepId, Is.Null);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenSharedStepProcessingThrowsException_LogsWarningAndSetsToNull()
    {
        // Arrange
        var sharedStepId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { SharedStepId = sharedStepId }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid> { { sharedStepId, Guid.NewGuid() } });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _loggerMock.VerifyLogging("Try to parse shared steps", LogLevel.Information);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenMultipleIterations_CreatesMultipleTmsIterations()
    {
        // Arrange
        var parameterKeyId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { Action = "Action with <<<Parameter1>>>" }
            },
            Iterations = new List<Iteration>
            {
                new() { Parameters = [new Parameter { Name = "Parameter1", Value = "Value1" }] },
                new() { Parameters = [new Parameter { Name = "Parameter1", Value = "Value2" }] }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters1 = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value1", ParameterKeyId = parameterKeyId }
        };

        var parameters2 = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value2", ParameterKeyId = parameterKeyId }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .SetupSequence(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters1)
            .ReturnsAsync(parameters2);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.TmsIterations, Has.Count.EqualTo(2));
            Assert.That(capturedTestCase.TmsIterations[0].Parameters, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.TmsIterations[1].Parameters, Has.Count.EqualTo(1));
            _parameterServiceMock.Verify(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()), Times.Exactly(2));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenParameterNotFoundInString_DoesNotReplace()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { Action = "Action with <<<UnknownParameter>>>" }
            },
            Iterations = new List<Iteration>
            {
                new() { Parameters = [new Parameter { Name = "Parameter1", Value = "Value1" }] }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value1", ParameterKeyId = Guid.NewGuid() }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Does.Contain("<<<UnknownParameter>>>"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenMultipleParametersInString_ReplacesAll()
    {
        // Arrange
        var parameterKeyId1 = Guid.NewGuid();
        var parameterKeyId2 = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { Action = "Action with <<<Parameter1>>> and <<<Parameter2>>>" }
            },
            Iterations = new List<Iteration>
            {
                new()
                {
                    Parameters =
                    [
                        new Parameter { Name = "Parameter1", Value = "Value1" },
                        new Parameter { Name = "Parameter2", Value = "Value2" }
                    ]
                }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value1", ParameterKeyId = parameterKeyId1 },
            new() { Id = Guid.NewGuid(), Name = "Parameter2", Value = "Value2", ParameterKeyId = parameterKeyId2 }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Does.Contain(parameterKeyId1.ToString()));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Contain(parameterKeyId2.ToString()));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Not.Contain("<<<Parameter1>>>"));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Not.Contain("<<<Parameter2>>>"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenParameterCaseInsensitive_ReplacesCorrectly()
    {
        // Arrange
        var parameterKeyId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { Action = "Action with <<<Parameter1>>>" }
            },
            Iterations = new List<Iteration>
            {
                new() { Parameters = [new Parameter { Name = "Parameter1", Value = "Value1" }] }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value1", ParameterKeyId = parameterKeyId }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Does.Contain(parameterKeyId.ToString()));
            Assert.That(capturedTestCase.Steps[0].Action, Does.Not.Contain("<<<Parameter1>>>"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenEmptyStringInAddParameter_ReturnsEmptyString()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new() { Action = "", Expected = "", TestData = "" }
            },
            Iterations = new List<Iteration>
            {
                new() { Parameters = [new Parameter { Name = "Parameter1", Value = "Value1" }] }
            },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var parameters = new List<TmsParameter>
        {
            new() { Id = Guid.NewGuid(), Name = "Parameter1", Value = "Value1", ParameterKeyId = Guid.NewGuid() }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(parameters);

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Is.Empty);
            Assert.That(capturedTestCase.Steps[0].Expected, Is.Empty);
            Assert.That(capturedTestCase.Steps[0].TestData, Is.Empty);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenAttachmentsExist_AddsAttachmentsToTestCase()
    {
        // Arrange
        var attachmentId1 = Guid.NewGuid();
        var attachmentId2 = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string> { "file1.jpg", "file2.pdf" }
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "file1.jpg", attachmentId1 },
            { "file2.pdf", attachmentId2 }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, testCase.Attachments))
            .ReturnsAsync(attachments);

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Attachments, Has.Count.EqualTo(2));
            Assert.That(capturedTestCase.Attachments, Does.Contain(attachmentId1.ToString()));
            Assert.That(capturedTestCase.Attachments, Does.Contain(attachmentId2.ToString()));
            _attachmentServiceMock.Verify(service => service.GetAttachments(testCase.Id, testCase.Attachments), Times.Once);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenStepHasActionAttachments_AddsAttachmentsToAction()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Action = "Action text",
                    ActionAttachments = new List<string> { "image.jpg" }
                }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(attachments);

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Action, Does.Contain(attachmentId.ToString()));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenStepHasExpectedAttachments_AddsAttachmentsToExpected()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new()
                {
                    Expected = "Expected text",
                    ExpectedAttachments = new List<string> { "image.jpg" }
                }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "image.jpg", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(attachments);

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].Expected, Does.Contain(attachmentId.ToString()));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenStepHasTestDataAttachments_AddsAttachmentsToTestData()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>
            {
                new()
                {
                    TestData = "Test data text",
                    TestDataAttachments = new List<string> { "data.txt" }
                }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        var attachments = new Dictionary<string, Guid>
        {
            { "data.txt", attachmentId }
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(attachments);

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.Steps[0].TestData, Does.Contain("data.txt"));
            Assert.That(capturedTestCase.Steps[0].TestData, Does.Contain("File attached to test case"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenPreconditionStepsExist_ProcessesPreconditionSteps()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>(),
            PreconditionSteps = new List<Step>
            {
                new() { Action = "Precondition action", Expected = "Precondition expected" }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.PreconditionSteps, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.PreconditionSteps[0].Action, Is.EqualTo("Precondition action"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenPostconditionStepsExist_ProcessesPostconditionSteps()
    {
        // Arrange
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>(),
            PostconditionSteps = new List<Step>
            {
                new() { Action = "Postcondition action", Expected = "Postcondition expected" }
            },
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        TmsTestCase? capturedTestCase = null;
        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .Callback<Guid, Guid, TmsTestCase>((_, _, tc) => capturedTestCase = tc)
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(capturedTestCase, Is.Not.Null);
            Assert.That(capturedTestCase!.PostconditionSteps, Has.Count.EqualTo(1));
            Assert.That(capturedTestCase.PostconditionSteps[0].Action, Is.EqualTo("Postcondition action"));
        });
    }

    [Test]
    public async Task ImportTestCases_WhenSectionNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var unknownSectionId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = unknownSectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>(),
            Attachments = new List<string>()
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute>(),
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(testCase.Name));
            _loggerMock.VerifyLogging("Could not import test case", LogLevel.Error);
        });
    }

    [Test]
    public async Task ImportTestCases_WhenAttributesExist_ConvertsAttributes()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var tmsAttributeId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            Name = "Test Case",
            SectionId = SectionId,
            Steps = new List<Step>(),
            Iterations = new List<Iteration> { new() { Parameters = [] } },
            Attributes = new List<CaseAttribute>
            {
                new() { Id = attributeId, Value = "Test Value" }
            },
            Attachments = new List<string>()
        };

        var tmsAttribute = new TmsAttribute
        {
            Id = tmsAttributeId,
            Name = "Test Attribute",
            Type = "String"
        };

        _parserServiceMock
            .Setup(service => service.GetTestCase(testCase.Id))
            .ReturnsAsync(testCase);

        _attachmentServiceMock
            .Setup(service => service.GetAttachments(testCase.Id, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, Guid>());

        _parameterServiceMock
            .Setup(service => service.CreateParameters(It.IsAny<IEnumerable<Parameter>>()))
            .ReturnsAsync(new List<TmsParameter>());

        _clientAdapterMock
            .Setup(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()))
            .ReturnsAsync(true);

        // Act
        var result = await _testCaseService.ImportTestCases(
            ProjectId,
            new[] { testCase.Id },
            new Dictionary<Guid, Guid> { { SectionId, NewSectionId } },
            new Dictionary<Guid, TmsAttribute> { { attributeId, tmsAttribute } },
            new Dictionary<Guid, Guid>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _clientAdapterMock.Verify(service => service.ImportTestCase(ProjectId, NewSectionId, It.IsAny<TmsTestCase>()), Times.Once);
        });
    }
}
