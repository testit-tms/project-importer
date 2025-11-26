using Importer.Client;
using Importer.Client.Implementations;
using Importer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestIT.ApiClient.Api;
using TestIT.ApiClient.Client;
using TestIT.ApiClient.Model;

namespace ImporterTests
{
    [TestFixture]
    public class ClientAdapterTests
    {
        private Mock<ILogger<ClientAdapter>> _loggerMock = null!;
        private Mock<IOptions<AppConfig>> _appConfigMock = null!;
        private IAdapterHelper _adapterHelper = null!;
        private Mock<IAttachmentsApi> _attachmentsApiMock = null!;
        private Mock<IProjectsApi> _projectsApiMock = null!;
        private Mock<IProjectAttributesApi> _projectAttributesApiMock = null!;
        private Mock<IProjectSectionsApi> _projectSectionsApiMock = null!;
        private Mock<ISectionsApi> _sectionsApiMock = null!;
        private Mock<ICustomAttributesApi> _customAttributesApiMock = null!;
        private Mock<IWorkItemsApi> _workItemsApiMock = null!;
        private Mock<IParametersApi> _parametersApiMock = null!;
        private ClientAdapter _clientAdapter = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ClientAdapter>>();
            _appConfigMock = new Mock<IOptions<AppConfig>>();
            _attachmentsApiMock = new Mock<IAttachmentsApi>();
            _projectsApiMock = new Mock<IProjectsApi>();
            _projectAttributesApiMock = new Mock<IProjectAttributesApi>();
            _projectSectionsApiMock = new Mock<IProjectSectionsApi>();
            _sectionsApiMock = new Mock<ISectionsApi>();
            _customAttributesApiMock = new Mock<ICustomAttributesApi>();
            _workItemsApiMock = new Mock<IWorkItemsApi>();
            _parametersApiMock = new Mock<IParametersApi>();

            _adapterHelper = new AdapterHelper(_loggerMock.Object);

            _clientAdapter = new ClientAdapter(
                _loggerMock.Object,
                _appConfigMock.Object,
                _adapterHelper,
                _attachmentsApiMock.Object,
                _projectsApiMock.Object,
                _projectAttributesApiMock.Object,
                _projectSectionsApiMock.Object,
                _sectionsApiMock.Object,
                _customAttributesApiMock.Object,
                _workItemsApiMock.Object,
                _parametersApiMock.Object
            );
        }

        #region GetProject Tests

        [Test]
        public async Task GetProject_WithCustomProjectName_UsesCustomName()
        {
            // Arrange
            var customProjectName = "CustomProjectName";
            var searchedProjectName = "DifferentName";
            var projectId = Guid.NewGuid();

            var projects = new List<ProjectShortModel>
            {
                new ProjectShortModel(
                    id: projectId,
                    description: "",
                    name: customProjectName,
                    isFavorite: false,
                    testCasesCount: 0,
                    sharedStepsCount: 0,
                    checkListsCount: 0,
                    autoTestsCount: 0,
                    isDeleted: false,
                    createdDate: DateTime.UtcNow,
                    modifiedDate: null,
                    createdById: Guid.NewGuid(),
                    modifiedById: null,
                    globalId: 1,
                    type: new ProjectTypeModel()
                )
            };
            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.Is<ProjectsFilterModel>(filter => filter.Name == customProjectName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig
                {
                    ProjectName = customProjectName,
                    ImportToExistingProject = true
                }
            });

            // Act
            var result = await _clientAdapter.GetProject(searchedProjectName);

            // Assert
            Assert.That(result, Is.EqualTo(projectId));

            _projectsApiMock.Verify(x => x.ApiV2ProjectsSearchPostAsync(
                null, null, null!, null!, null!,
                It.Is<ProjectsFilterModel>(filter => filter.Name == customProjectName),
                It.IsAny<CancellationToken>()), Times.Once);

            _loggerMock.VerifyLogging($"Import by custom project name {customProjectName}", LogLevel.Information);
        }

        [Test]
        public async Task GetProject_WhenProjectFoundButNameDoesNotMatch_ReturnsGuidEmpty()
        {
            // Arrange
            var searchedProjectName = "MyProject";
            var differentProjectName = "DifferentProject";
            var projectId = Guid.NewGuid();

            var projects = new List<ProjectShortModel>
            {
                new ProjectShortModel(
                    id: projectId,
                    description: "",
                    name: differentProjectName,
                    isFavorite: false,
                    testCasesCount: 0,
                    sharedStepsCount: 0,
                    checkListsCount: 0,
                    autoTestsCount: 0,
                    isDeleted: false,
                    createdDate: DateTime.UtcNow,
                    modifiedDate: null,
                    createdById: Guid.NewGuid(),
                    modifiedById: null,
                    globalId: 1,
                    type: new ProjectTypeModel()
                )
            };

            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectsFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig()
            });

            // Act
            var result = await _clientAdapter.GetProject(searchedProjectName);

            // Assert
            Assert.That(result, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public async Task GetProject_WhenProjectNotFound_ReturnsGuidEmpty()
        {
            // Arrange
            var searchedProjectName = "NonExistentProject";

            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectsFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProjectShortModel>());

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig()
            });

            // Act
            var result = await _clientAdapter.GetProject(searchedProjectName);

            // Assert
            Assert.That(result, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void GetProject_WhenProjectFound_AndImportToExistingFalse_ThrowsException()
        {
            // Arrange
            var projectName = "ExistingProject";
            var projectId = Guid.NewGuid();

            var projects = new List<ProjectShortModel>
            {
                new ProjectShortModel(
                    id: projectId,
                    description: "",
                    name: projectName,
                    isFavorite: false,
                    testCasesCount: 0,
                    sharedStepsCount: 0,
                    checkListsCount: 0,
                    autoTestsCount: 0,
                    isDeleted: false,
                    createdDate: DateTime.UtcNow,
                    modifiedDate: null,
                    createdById: Guid.NewGuid(),
                    modifiedById: null,
                    globalId: 1,
                    type: new ProjectTypeModel()
                )
            };
            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectsFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig
                {
                    ImportToExistingProject = false
                }
            });

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetProject(projectName));

            Assert.That(exception!.Message, Is.EqualTo("Project with the same name already exists"));
            _loggerMock.VerifyLogging(projectName, LogLevel.Error, Times.AtLeastOnce());
        }

        [Test]
        public async Task GetProject_WhenProjectFound_AndImportToExistingTrue_ReturnsProjectId()
        {
            // Arrange
            var projectName = "ExistingProject";
            var projectId = Guid.NewGuid();

            var projects = new List<ProjectShortModel>
            {
                new ProjectShortModel(
                    id: projectId,
                    description: "",
                    name: projectName,
                    isFavorite: false,
                    testCasesCount: 0,
                    sharedStepsCount: 0,
                    checkListsCount: 0,
                    autoTestsCount: 0,
                    isDeleted: false,
                    createdDate: DateTime.UtcNow,
                    modifiedDate: null,
                    createdById: Guid.NewGuid(),
                    modifiedById: null,
                    globalId: 1,
                    type: new ProjectTypeModel()
                )
            };
            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectsFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig
                {
                    ImportToExistingProject = true
                }
            });

            // Act
            var result = await _clientAdapter.GetProject(projectName);

            // Assert
            Assert.That(result, Is.EqualTo(projectId));
            _loggerMock.VerifyLogging($"Got project {projectName} with id {projectId}", LogLevel.Information);
        }

        [Test]
        public void GetProject_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectName = "TestProject";
            var exceptionMessage = "API Error";

            _projectsApiMock
                .Setup(x => x.ApiV2ProjectsSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectsFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig()
            });

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetProject(projectName));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));
            _loggerMock.VerifyLoggingCalls(LogLevel.Error, 3);
        }

        #endregion

        #region CreateProject Tests

        [Test]
        public async Task CreateProject_WhenSuccessful_ReturnsProjectId()
        {
            // Arrange
            var projectName = "TestProject";
            var projectId = Guid.NewGuid();

            var projectModel = new ProjectModel(
                id: projectId,
                description: "",
                name: projectName,
                isFavorite: false,
                attributesScheme: new List<CustomAttributeModel>(),
                testPlansAttributesScheme: new List<CustomAttributeModel>(),
                testCasesCount: 0,
                sharedStepsCount: 0,
                checkListsCount: 0,
                autoTestsCount: 0,
                isDeleted: false,
                createdDate: DateTime.UtcNow,
                modifiedDate: null,
                createdById: Guid.NewGuid(),
                modifiedById: null,
                globalId: 1,
                type: new ProjectTypeModel());

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig { });

            _projectsApiMock
                .Setup(x => x.CreateProjectAsync(It.IsAny<CreateProjectApiModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectModel);

            // Act
            var result = await _clientAdapter.CreateProject(projectName);

            // Assert
            Assert.That(result, Is.EqualTo(projectId));

            _projectsApiMock.Verify(
                x => x.CreateProjectAsync(
                    It.Is<CreateProjectApiModel>(m => m.Name == projectName),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Creating project {projectName}", LogLevel.Information);
            _loggerMock.VerifyLogging($"Created project {projectName} with id {projectId}", LogLevel.Information);
        }

        [Test]
        public async Task CreateProject_WithCustomProjectName_ReturnsProjectId()
        {
            // Arrange
            var customProjectName = "CustomProject";
            var requestedProjectName = "DifferentName";
            var projectId = Guid.NewGuid();

            var projectModel = new ProjectModel(
                id: projectId,
                description: "",
                name: customProjectName,
                isFavorite: false,
                attributesScheme: new List<CustomAttributeModel>(),
                testPlansAttributesScheme: new List<CustomAttributeModel>(),
                testCasesCount: 0,
                sharedStepsCount: 0,
                checkListsCount: 0,
                autoTestsCount: 0,
                isDeleted: false,
                createdDate: DateTime.UtcNow,
                modifiedDate: null,
                createdById: Guid.NewGuid(),
                modifiedById: null,
                globalId: 1,
                type: new ProjectTypeModel());

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig
            {
                Tms = new TmsConfig
                {
                    ProjectName = customProjectName
                }
            });

            _projectsApiMock
                .Setup(x => x.CreateProjectAsync(It.IsAny<CreateProjectApiModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectModel);

            // Act
            var result = await _clientAdapter.CreateProject(requestedProjectName);

            // Assert
            Assert.That(result, Is.EqualTo(projectId));

            _projectsApiMock.Verify(
                x => x.CreateProjectAsync(
                    It.Is<CreateProjectApiModel>(m => m.Name == customProjectName),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void CreateProject_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectName = "TestProject";
            var exceptionMessage = "API Error";

            _appConfigMock.Setup(x => x.Value).Returns(new AppConfig { });

            _projectsApiMock
                .Setup(x => x.CreateProjectAsync(It.IsAny<CreateProjectApiModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.CreateProject(projectName));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not create project {projectName}: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region ImportSection Tests

        [Test]
        public async Task ImportSection_WhenSuccessful_ReturnsSectionId()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var sectionId = Guid.NewGuid();
            var sectionName = "TestSection";

            var section = new Section
            {
                Id = Guid.NewGuid(),
                Name = sectionName,
                PreconditionSteps = new List<Step>(),
                PostconditionSteps = new List<Step>()
            };

            var sectionModel = new SectionWithStepsModel(
                id: sectionId,
                name: sectionName,
                createdById: Guid.NewGuid(),
                createdDate: DateTime.UtcNow,
                attachments: new List<AttachmentModel>(),
                preconditionSteps: new List<StepModel>(),
                postconditionSteps: new List<StepModel>(),
                projectId: projectId,
                parentId: parentSectionId,
                modifiedDate: null,
                modifiedById: null
            );

            _sectionsApiMock
                .Setup(x => x.CreateSectionAsync(It.IsAny<SectionPostModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sectionModel);

            // Act
            var result = await _clientAdapter.ImportSection(projectId, parentSectionId, section);

            // Assert
            Assert.That(result, Is.EqualTo(sectionId));

            _sectionsApiMock.Verify(
                x => x.CreateSectionAsync(
                    It.Is<SectionPostModel>(m => 
                        m.Name == sectionName &&
                        m.ProjectId == projectId &&
                        m.ParentId == parentSectionId),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Importing section {sectionName}", LogLevel.Information);
            _loggerMock.VerifyLogging($"Imported section {sectionName} with id {sectionId}", LogLevel.Information);
        }

        [Test]
        public async Task ImportSection_WithIncludesStepsInRequest_ReturnsSectionId()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var sectionId = Guid.NewGuid();
            var sectionName = "SectionWithSteps";

            var section = new Section
            {
                Id = Guid.NewGuid(),
                Name = sectionName,
                PreconditionSteps = new List<Step>
                {
                    new Step { Action = "PreconditionAction", Expected = "PreconditionExpected" }
                },
                PostconditionSteps = new List<Step>
                {
                    new Step { Action = "PostconditionAction", Expected = "PostconditionExpected" }
                }
            };

            var sectionModel = new SectionWithStepsModel(
                id: sectionId,
                name: sectionName,
                createdById: Guid.NewGuid(),
                createdDate: DateTime.UtcNow,
                attachments: new List<AttachmentModel>(),
                preconditionSteps: new List<StepModel>(),
                postconditionSteps: new List<StepModel>(),
                projectId: projectId,
                parentId: parentSectionId,
                modifiedDate: null,
                modifiedById: null
            );

            _sectionsApiMock
                .Setup(x => x.CreateSectionAsync(It.IsAny<SectionPostModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sectionModel);

            // Act
            var result = await _clientAdapter.ImportSection(projectId, parentSectionId, section);

            // Assert
            Assert.That(result, Is.EqualTo(sectionId));

            _sectionsApiMock.Verify(
                x => x.CreateSectionAsync(
                    It.Is<SectionPostModel>(m =>
                        m.Name == sectionName &&
                        m.PreconditionSteps.Count == 1 &&
                        m.PostconditionSteps.Count == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void ImportSection_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var sectionName = "TestSection";
            var exceptionMessage = "API Error";

            var section = new Section
            {
                Id = Guid.NewGuid(),
                Name = sectionName,
                PreconditionSteps = new List<Step>(),
                PostconditionSteps = new List<Step>()
            };

            _sectionsApiMock
                .Setup(x => x.CreateSectionAsync(It.IsAny<SectionPostModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.ImportSection(projectId, parentSectionId, section));

            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));
            _loggerMock.VerifyLogging($"Could not import section {sectionName}: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region ImportAttribute Tests

        [Test]
        public async Task ImportAttribute_WhenSuccessful_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "TestAttribute";

            var attribute = new Models.Attribute
            {
                Id = Guid.NewGuid(),
                Name = attributeName,
                Type = AttributeType.String,
                IsRequired = true,
                IsActive = true,
            };

            var attributeModel = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                type: CustomAttributeTypesEnum.String,
                options: new List<CustomAttributeOptionModel>(),
                isRequired: true,
                isEnabled: true,
                isGlobal: true);

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalPostAsync(It.IsAny<GlobalCustomAttributePostModel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributeModel);

            // Act
            var result = await _clientAdapter.ImportAttribute(attribute);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.Type, Is.EqualTo(CustomAttributeTypesEnum.String.ToString()));
                Assert.That(result.IsRequired, Is.EqualTo(true));
                Assert.That(result.IsEnabled, Is.EqualTo(true));

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesGlobalPostAsync(
                        It.Is<GlobalCustomAttributePostModel>(m =>
                            m.Name == attributeName &&
                            m.Type == CustomAttributeTypesEnum.String &&
                            m.IsRequired == true &&
                            m.IsEnabled == true),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Importing attribute {attributeName}", LogLevel.Information);
                _loggerMock.VerifyLogging($"Imported attribute {attributeName} with id {attributeId}", LogLevel.Information);
            });
        }

        [Test]
        public async Task ImportAttribute_WithOptionsNull_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "OptionsAttribute";

            var attribute = new Models.Attribute
            {
                Id = Guid.NewGuid(),
                Name = attributeName,
                Type = AttributeType.Options,
                IsRequired = false,
                IsActive = true,
                Options = new List<string>()
            };

            var attributeModel = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                isRequired: false,
                isEnabled: true,
                type: CustomAttributeTypesEnum.Options,
                options: new List<CustomAttributeOptionModel>());

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalPostAsync(It.IsAny<GlobalCustomAttributePostModel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributeModel);

            // Act
            var result = await _clientAdapter.ImportAttribute(attribute);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.Type, Is.EqualTo(CustomAttributeTypesEnum.Options.ToString()));
                Assert.That(result.IsRequired, Is.EqualTo(false));
                Assert.That(result.IsEnabled, Is.EqualTo(true));

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesGlobalPostAsync(
                        It.Is<GlobalCustomAttributePostModel>(m =>
                            m.Type == CustomAttributeTypesEnum.Options &&
                            m.Options.Count == 1 &&
                            m.Options[0].Value == "null"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            });

        }

        [Test]
        public async Task ImportAttribute_WithOptionsValues_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "OptionsAttributeWithValues";

            var attribute = new Models.Attribute
            {
                Id = Guid.NewGuid(),
                Name = attributeName,
                Type = AttributeType.Options,
                IsRequired = true,
                IsActive = true,
                Options = new List<string> { "Red", "Green", "Blue" }
            };

            var attributeModel = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                isRequired: true,
                isEnabled: true,
                type: CustomAttributeTypesEnum.Options,
                options: new List<CustomAttributeOptionModel>
                {
                    new CustomAttributeOptionModel(
                        id: Guid.NewGuid(),
                        value: "Red",
                        isDefault: false),
                    new CustomAttributeOptionModel(
                        id: Guid.NewGuid(),
                        value: "Green",
                        isDefault: false),
                    new CustomAttributeOptionModel(
                        id: Guid.NewGuid(),
                        value: "Blue",
                        isDefault: false)
                });

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalPostAsync(It.IsAny<GlobalCustomAttributePostModel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributeModel);

            // Act
            var result = await _clientAdapter.ImportAttribute(attribute);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.Type, Is.EqualTo(CustomAttributeTypesEnum.Options.ToString()));
                Assert.That(result.IsRequired, Is.EqualTo(true));
                Assert.That(result.IsEnabled, Is.EqualTo(true));
                Assert.That(result.Options.Count, Is.EqualTo(3));
                Assert.That(result.Options[0].Value, Is.EqualTo("Red"));
                Assert.That(result.Options[1].Value, Is.EqualTo("Green"));
                Assert.That(result.Options[2].Value, Is.EqualTo("Blue"));

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesGlobalPostAsync(
                        It.Is<GlobalCustomAttributePostModel>(m =>
                            m.Type == CustomAttributeTypesEnum.Options &&
                            m.Options.Count == 3 &&
                            m.Options[0].Value == "Red" &&
                            m.Options[1].Value == "Green" &&
                            m.Options[2].Value == "Blue"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Importing attribute {attributeName}", LogLevel.Information);
                _loggerMock.VerifyLogging($"Imported attribute {attributeName} with id {attributeId}", LogLevel.Information);
            });
        }

        [Test]
        public void ImportAttribute_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var attribute = new Models.Attribute
            {
                Id = Guid.NewGuid(),
                Name = "TestAttribute",
                Type = AttributeType.String,
                IsRequired = false,
                IsActive = true,
                Options = new List<string>()
            };

            var exceptionMessage = "API Error";
            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalPostAsync(It.IsAny<GlobalCustomAttributePostModel>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.ImportAttribute(attribute));

            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));
            _loggerMock.VerifyLogging($"Could not import attribute {attribute.Name}: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region GetAttribute Tests

        [Test]
        public async Task GetAttribute_WhenSuccessful_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "TestAttribute";

            var attributeModel = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                isRequired: true,
                isEnabled: true,
                type: CustomAttributeTypesEnum.Options,
                options: new List<CustomAttributeOptionModel>
                {
                    new CustomAttributeOptionModel(
                        id: Guid.NewGuid(),
                        value: "Option1",
                        isDefault: false)
                });

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributeModel);

            // Act
            var result = await _clientAdapter.GetAttribute(attributeId);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.IsRequired, Is.True);
                Assert.That(result.IsEnabled, Is.True);
                Assert.That(result.Type, Is.EqualTo(CustomAttributeTypesEnum.Options.ToString()));
                Assert.That(result.Options.Count, Is.EqualTo(1));
                Assert.That(result.Options[0].Value, Is.EqualTo("Option1"));

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Getting attribute {attributeId}", LogLevel.Information);
            });
        }

        [Test]
        public void GetAttribute_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var exceptionMessage = "API Error";

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetAttribute(attributeId));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not get attribute {attributeId}: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region ImportSharedStep Tests

        [Test]
        public async Task ImportSharedStep_WhenSuccessful_ReturnsGuid()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var sharedStepId = Guid.NewGuid();
            var sharedStepName = "TestSharedStep";

            var sharedStep = new SharedStep
            {
                Id = Guid.NewGuid(),
                Name = sharedStepName,
                Description = "Description",
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                Steps = new List<Step>
                {
                    new Step { Action = "Action", Expected = "Expected" }
                },
                Attributes = new List<CaseAttribute>()
                {
                    new CaseAttribute { Id =  Guid.NewGuid(), Value = "test" }
                },
                Links = new List<Models.Link>()
                {
                    new Models.Link{ Title = "TitleLink", Type = Models.LinkType.Related, Url = "TestUrl" }
                },
                Tags = new List<string> { "tag1", "tag2" },
                Attachments = new List<string>()
            };

            var workItemResult = new WorkItemApiResult(
                id: sharedStepId,
                globalId: 1,
                versionId: Guid.NewGuid(),
                versionNumber: 1,
                projectId: projectId,
                sectionId: parentSectionId,
                name: sharedStepName,
                description: "Description",
                sourceType: WorkItemSourceTypeApiModel.Manual,
                entityTypeName: WorkItemEntityTypeApiModel.SharedSteps,
                duration: 0,
                medianDuration: 0,
                state: WorkItemStateApiModel.Ready,
                priority: WorkItemPriorityApiModel.Medium,
                isAutomated: false,
                attributes: new Dictionary<string, object>(),
                tags: new List<TagModel>(),
                sectionPreconditionSteps: new List<StepModel>(),
                sectionPostconditionSteps: new List<StepModel>(),
                preconditionSteps: new List<StepModel>(),
                steps: new List<StepModel>(),
                postconditionSteps: new List<StepModel>(),
                iterations: new List<IterationModel>(),
                autoTests: new List<AutoTestModel>(),
                attachments: new List<AttachmentModel>(),
                links: new List<LinkModel>(),
                externalIssues: new List<ExternalIssueApiResult>(),
                createdDate: DateTime.UtcNow,
                createdById: Guid.NewGuid(),
                modifiedDate: null,
                modifiedById: null,
                isDeleted: false
            );

            _workItemsApiMock
                .Setup(x => x.ApiV2WorkItemsPostAsync(It.IsAny<CreateWorkItemApiModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(workItemResult);

            // Act
            var result = await _clientAdapter.ImportSharedStep(projectId, parentSectionId, sharedStep);

            // Assert
            Assert.That(result, Is.EqualTo(sharedStepId));

            _workItemsApiMock.Verify(
                x => x.ApiV2WorkItemsPostAsync(
                    It.Is<CreateWorkItemApiModel>(m =>
                        m.Name == sharedStepName &&
                        m.EntityTypeName == WorkItemEntityTypeApiModel.SharedSteps &&
                        m.Priority == WorkItemPriorityApiModel.Medium &&
                        m.SectionId == parentSectionId &&
                        m.Description == "Description" &&
                        m.Steps.Count == 1 &&
                        m.Tags.Count == 2 &&
                        m.Attributes.Count == 1 &&
                        m.Links.Count == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Imported shared step {sharedStepName} with id {sharedStepId}", LogLevel.Information);
        }

        [Test]
        public void ImportSharedStep_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();

            var sharedStep = new SharedStep
            {
                Id = Guid.NewGuid(),
                Name = "TestSharedStep",
                Description = "Description",
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                Steps = new List<Models.Step>(),
                Attributes = new List<CaseAttribute>(),
                Links = new List<Models.Link>(),
                Tags = new List<string>(),
                Attachments = new List<string>()
            };

            var exceptionMessage = "API Error";
            _workItemsApiMock
                .Setup(x => x.ApiV2WorkItemsPostAsync(It.IsAny<CreateWorkItemApiModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.ImportSharedStep(projectId, parentSectionId, sharedStep));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not import shared step {sharedStep.Name}: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region ImportTestCase Tests

        [Test]
        public async Task ImportTestCase_WhenSuccessful_ReturnsTrue()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var testCaseId = Guid.NewGuid();
            var duplicateAttributeId = Guid.NewGuid();
            var testCaseName = "TestCase";

            var testCase = new TmsTestCase
            {
                Id = Guid.NewGuid(),
                Name = testCaseName,
                Description = "Test Description",
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                Duration = 0,
                Steps = new List<Step>
                {
                    new Step { SharedStepId = Guid.NewGuid(), Action = "Action1", Expected = "Expected1", TestData = "Data1" }
                },
                PreconditionSteps = new List<Step>
                {
                    new Step { Action = "PreAction", Expected = "PreExpected" }
                },
                PostconditionSteps = new List<Step>
                {
                    new Step { Action = "PostAction", Expected = "PostExpected" }
                },
                Attributes = new List<CaseAttribute>
                {
                    new CaseAttribute { Id = Guid.NewGuid(), Value = "FirstValue" },
                    new CaseAttribute { Id = Guid.NewGuid(), Value = "SecondValue" },
                    new CaseAttribute { Id = Guid.NewGuid(), Value = null! },
                    new CaseAttribute { Id = Guid.NewGuid(), Value = "" },
                    new CaseAttribute { Id = duplicateAttributeId, Value = "FirstDuplicateValue" },
                    new CaseAttribute { Id = duplicateAttributeId, Value = "SecondDuplicateValue" }
                },
                Tags = new List<string> { "tag1", "tag2" },
                Links = new List<Models.Link>
                {
                    new Models.Link { Url = "http://example.com", Title = "Example", Description = "Link", Type = Models.LinkType.Related }
                },
                Attachments = new List<string> { Guid.NewGuid().ToString() },
                TmsIterations = new List<TmsIterations>
                {
                    new TmsIterations { Parameters = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } }
                }
            };

            var workItemResult = new WorkItemApiResult(
                id: testCaseId,
                globalId: 1,
                versionId: Guid.NewGuid(),
                versionNumber: 1,
                projectId: projectId,
                sectionId: parentSectionId,
                name: testCaseName,
                description: "Test Description",
                sourceType: WorkItemSourceTypeApiModel.Manual,
                entityTypeName: WorkItemEntityTypeApiModel.TestCases,
                duration: 60000,
                medianDuration: 0,
                state: WorkItemStateApiModel.Ready,
                priority: WorkItemPriorityApiModel.Medium,
                isAutomated: false,
                attributes: new Dictionary<string, object>(),
                tags: new List<TagModel>(),
                sectionPreconditionSteps: new List<StepModel>(),
                sectionPostconditionSteps: new List<StepModel>(),
                preconditionSteps: new List<StepModel>(),
                steps: new List<StepModel>(),
                postconditionSteps: new List<StepModel>(),
                iterations: new List<IterationModel>(),
                autoTests: new List<AutoTestModel>(),
                attachments: new List<AttachmentModel>(),
                links: new List<LinkModel>(),
                externalIssues: new List<ExternalIssueApiResult>(),
                createdDate: DateTime.UtcNow,
                createdById: Guid.NewGuid(),
                modifiedDate: null,
                modifiedById: null,
                isDeleted: false
            );

            _workItemsApiMock
                .Setup(x => x.ApiV2WorkItemsPostAsync(It.IsAny<CreateWorkItemApiModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(workItemResult);

            // Act
            var result = await _clientAdapter.ImportTestCase(projectId, parentSectionId, testCase);

            // Assert
            Assert.That(result, Is.True);

            _workItemsApiMock.Verify(
                x => x.ApiV2WorkItemsPostAsync(
                    It.Is<CreateWorkItemApiModel>(m =>
                        m.Name == testCaseName &&
                        m.EntityTypeName == WorkItemEntityTypeApiModel.TestCases &&
                        m.Priority == WorkItemPriorityApiModel.Medium &&
                        m.SectionId == parentSectionId &&
                        m.Description == "Test Description" &&
                        m.Steps.Count == 1 &&
                        m.PreconditionSteps.Count == 1 &&
                        m.PostconditionSteps.Count == 1 &&
                        m.Tags.Count == 2 &&
                        m.Attributes.Count == 3 &&
                        m.Links.Count == 1 &&
                        m.Attachments.Count == 1 &&
                        m.Duration == 60000),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Importing test case {testCaseName}", LogLevel.Information);
            _loggerMock.VerifyLogging($"Imported test case {testCaseName} with id {testCaseId}", LogLevel.Information);
        }

        [Test]
        public void ImportTestCase_WhenAdapterHelperThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var parentSectionId = Guid.NewGuid();
            var testCaseName = "TestCase";
            var exceptionMessage = "API Error";

            var testCase = new TmsTestCase
            {
                Id = Guid.NewGuid(),
                Name = testCaseName,
                State = StateType.Ready,
                Priority = PriorityType.Medium,
                Duration = 0,
                Steps = new List<Step>(),
                PreconditionSteps = new List<Step>(),
                PostconditionSteps = new List<Step>(),
                Attributes = new List<CaseAttribute>(),
                Tags = new List<string>(),
                Links = new List<Models.Link>(),
                Attachments = new List<string>(),
                TmsIterations = new List<TmsIterations>()
            };

            _workItemsApiMock
                .Setup(x => x.ApiV2WorkItemsPostAsync(It.IsAny<CreateWorkItemApiModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.ImportTestCase(projectId, parentSectionId, testCase));

            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));
            _loggerMock.VerifyLogging($"Could not import test case {testCaseName}", LogLevel.Error, Times.AtLeastOnce());
        }

        #endregion

        #region GetRootSectionId Tests

        [Test]
        public async Task GetRootSectionId_WhenSuccessful_ReturnsRootSectionId()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var rootSectionId = Guid.NewGuid();

            var sections = new List<SectionModel>
            {
                new SectionModel(
                    id: rootSectionId,
                    name: "Root Section",
                    createdById: Guid.NewGuid(),
                    createdDate: DateTime.UtcNow,
                    projectId: projectId,
                    parentId: null,
                    isDeleted: false,
                    modifiedDate: null,
                    modifiedById: null
                )
            };

            _projectSectionsApiMock
                .Setup(x => x.GetSectionsByProjectIdAsync(projectId.ToString(),
                null, null, null!, null!, null!,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(sections);

            // Act
            var result = await _clientAdapter.GetRootSectionId(projectId);

            // Assert
            Assert.That(result, Is.EqualTo(rootSectionId));

            _projectSectionsApiMock.Verify(
                x => x.GetSectionsByProjectIdAsync(projectId.ToString(),
                null, null, null!, null!, null!,
                It.IsAny<CancellationToken>()), Times.Once);

            _loggerMock.VerifyLogging("Getting root section id", LogLevel.Information);
        }

        [Test]
        public void GetRootSectionId_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var exceptionMessage = "API Error";

            _projectSectionsApiMock
                .Setup(x => x.GetSectionsByProjectIdAsync(projectId.ToString(),
                null, null, null!, null!, null!,
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetRootSectionId(projectId));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging("Could not get root section id: API Error", LogLevel.Error);
        }

        #endregion

        #region GetProjectAttributes Tests

        [Test]
        public async Task GetProjectAttributes_WhenSuccessful_ReturnsTmsAttributes()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "TestAttribute";
            var valueOption = "Option1";

            var attributes = new List<CustomAttributeSearchResponseModel>
            {
                new CustomAttributeSearchResponseModel(
                    workItemUsage: new List<ProjectShortestModel>(),
                    testPlanUsage: new List<ProjectShortestModel>(),
                    id: attributeId,
                    name: attributeName,
                    isRequired: true,
                    isEnabled: true,
                    type: CustomAttributeTypesEnum.Options,
                    options: new List<CustomAttributeOptionModel>
                    {
                        new CustomAttributeOptionModel(
                            id: Guid.NewGuid(),
                            value: valueOption,
                            isDefault: false)
                    },
                    isGlobal: true)
            };

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.Is<CustomAttributeSearchQueryModel>(q => q.IsGlobal == true && q.IsDeleted == false),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributes);

            // Act
            var result = await _clientAdapter.GetProjectAttributes();

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].Id, Is.EqualTo(attributeId));
                Assert.That(result[0].Name, Is.EqualTo(attributeName));
                Assert.That(result[0].Type, Is.EqualTo(CustomAttributeTypesEnum.Options.ToString()));
                Assert.That(result[0].IsRequired, Is.True);
                Assert.That(result[0].IsEnabled, Is.True);
                Assert.That(result[0].IsGlobal, Is.True);
                Assert.That(result[0].Options.Count, Is.EqualTo(1));
                Assert.That(result[0].Options[0].Value, Is.EqualTo(valueOption));

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesSearchPostAsync(
                        null, null, null!, null!, null!,
                        It.Is<CustomAttributeSearchQueryModel>(q => q.IsGlobal == true && q.IsDeleted == false),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging("Getting project attributes", LogLevel.Information);
            });
        }

        [Test]
        public void GetProjectAttributes_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var exceptionMessage = "API Error";

            _customAttributesApiMock
               .Setup(x => x.ApiV2CustomAttributesSearchPostAsync(
                   null, null, null!, null!, null!,
                   It.Is<CustomAttributeSearchQueryModel>(q => q.IsGlobal == true && q.IsDeleted == false),
                   It.IsAny<CancellationToken>()))
               .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetProjectAttributes());
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not get project attributes: {exceptionMessage}", LogLevel.Error);
        }

        #endregion

        #region GetRequiredProjectAttributesByProjectId Tests

        [Test]
        public async Task GetRequiredProjectAttributesByProjectId_WhenSuccessful_ReturnsTmsAttributes()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var attributeId = Guid.NewGuid();
            var valueOption = "Option1";
            var attributeName = "RequiredAttribute";

            var attributes = new List<CustomAttributeGetModel>
            {
                new CustomAttributeGetModel(
                    id: attributeId,
                    name: attributeName,
                    isRequired: true,
                    isEnabled: true,
                    type: CustomAttributeTypesEnum.Options,
                    options: new List<CustomAttributeOptionModel>
                    {
                        new CustomAttributeOptionModel(
                            id: Guid.NewGuid(),
                            value: valueOption,
                            isDefault: false)
                    },
                    isGlobal: true)
            };

            _projectAttributesApiMock
                .Setup(x => x.SearchAttributesInProjectAsync(
                    projectId.ToString(),
                    null, null, null!, null!, null!,
                    It.Is<ProjectAttributesFilterModel>(f =>
                        f.Name == "" &&
                        f.IsRequired == true &&
                        f.Types.Contains(CustomAttributeTypesEnum.Options)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributes);

            // Act
            var result = await _clientAdapter.GetRequiredProjectAttributesByProjectId(projectId);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].Id, Is.EqualTo(attributeId));
                Assert.That(result[0].Name, Is.EqualTo(attributeName));
                Assert.That(result[0].IsRequired, Is.True);
                Assert.That(result[0].IsEnabled, Is.True);
                Assert.That(result[0].IsGlobal, Is.True);
                Assert.That(result[0].Options.Count, Is.EqualTo(1));
                Assert.That(result[0].Options[0].Value, Is.EqualTo(valueOption));

                _projectAttributesApiMock.Verify(
                    x => x.SearchAttributesInProjectAsync(
                        projectId.ToString(),
                        null, null, null!, null!, null!,
                        It.Is<ProjectAttributesFilterModel>(f =>
                            f.Name == "" &&
                            f.IsRequired == true &&
                            f.Types.Contains(CustomAttributeTypesEnum.Options)),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Getting required project attributes by project id {projectId}", LogLevel.Information);
            });
        }

        [Test]
        public void GetRequiredProjectAttributesByProjectId_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var exceptionMessage = "API Error";

            _projectAttributesApiMock
                .Setup(x => x.SearchAttributesInProjectAsync(
                    It.IsAny<string>(),
                    null, null, null!, null!, null!,
                    It.IsAny<ProjectAttributesFilterModel>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.GetRequiredProjectAttributesByProjectId(projectId));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not get required project attributes by project id {projectId}: API Error", LogLevel.Error);
        }

        #endregion

        #region GetProjectAttributeById Tests

        [Test]
        public async Task GetProjectAttributeById_WhenSuccessful_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var valueOption = "Option1";
            var attributeName = "TestAttribute";

            var attributeModel = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                isRequired: true,
                isEnabled: true,
                type: CustomAttributeTypesEnum.Options,
                options: new List<CustomAttributeOptionModel>
                {
                    new CustomAttributeOptionModel(
                        id: Guid.NewGuid(),
                        value: valueOption,
                        isDefault: true)
                },
                isGlobal: true);

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(attributeModel);

            // Act
            var result = await _clientAdapter.GetProjectAttributeById(attributeId);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.IsRequired, Is.True);
                Assert.That(result.IsEnabled, Is.True);
                Assert.That(result.IsGlobal, Is.True);
                Assert.That(result.Options.Count, Is.EqualTo(1));
                Assert.That(result.Options[0].Value, Is.EqualTo(valueOption));
                Assert.That(result.Options[0].IsDefault, Is.True);

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Getting project attribute by id {attributeId}", LogLevel.Information);
            });
        }

        [Test]
        public void GetProjectAttributeById_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var exceptionMessage = "API Error";

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesIdGetAsync(attributeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.GetProjectAttributeById(attributeId));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not get project attribute by id {attributeId}: API Error", LogLevel.Error);
        }

        #endregion

        #region AddAttributesToProject Tests

        [Test]
        public async Task AddAttributesToProject_WhenSuccessful_CallsApi()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var attributeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            _projectsApiMock
                .Setup(x => x.AddGlobalAttributesToProjectAsync(projectId.ToString(), attributeIds, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _clientAdapter.AddAttributesToProject(projectId, attributeIds);

            // Assert
            _projectsApiMock.Verify(
                x => x.AddGlobalAttributesToProjectAsync(projectId.ToString(), attributeIds, It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging("Adding attributes to project", LogLevel.Information);
        }

        [Test]
        public void AddAttributesToProject_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var attributeIds = new List<Guid> { Guid.NewGuid() };
            var exceptionMessage = "API Error";

            _projectsApiMock
                .Setup(x => x.AddGlobalAttributesToProjectAsync(It.IsAny<string>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.AddAttributesToProject(projectId, attributeIds));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging("Could not add attributes to project: API Error", LogLevel.Error);
        }

        #endregion

        #region UpdateAttribute Tests

        [Test]
        public async Task UpdateAttribute_WhenSuccessful_ReturnsTmsAttribute()
        {
            // Arrange
            var attributeId = Guid.NewGuid();
            var attributeName = "UpdatedAttribute";
            var valueOption = "Option1";

            var inputAttribute = new TmsAttribute
            {
                Id = attributeId,
                Name = attributeName,
                IsEnabled = true,
                IsRequired = false,
                Options = new List<TmsAttributeOptions>
                {
                    new TmsAttributeOptions { Id = Guid.NewGuid(), Value = "Option1", IsDefault = true }
                }
            };

            var apiResponseAttribute = new CustomAttributeModel(
                id: attributeId,
                name: attributeName,
                isRequired: false,
                isEnabled: true,
                type: CustomAttributeTypesEnum.Options,
                options: new List<CustomAttributeOptionModel>
                {
                    new CustomAttributeOptionModel(
                        id: inputAttribute.Options[0].Id,
                        value: valueOption,
                        isDefault: true)
                },
                isGlobal: true);

            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalIdPutAsync(
                    attributeId,
                    It.IsAny<GlobalCustomAttributeUpdateModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(apiResponseAttribute);

            // Act
            var result = await _clientAdapter.UpdateAttribute(inputAttribute);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(attributeId));
                Assert.That(result.Name, Is.EqualTo(attributeName));
                Assert.That(result.IsEnabled, Is.True);
                Assert.That(result.IsRequired, Is.False);
                Assert.That(result.Options.Count, Is.EqualTo(1));
                Assert.That(result.Options[0].Value, Is.EqualTo(valueOption));
                Assert.That(result.Options[0].IsDefault, Is.True);

                _customAttributesApiMock.Verify(
                    x => x.ApiV2CustomAttributesGlobalIdPutAsync(
                        attributeId,
                        It.Is<GlobalCustomAttributeUpdateModel>(m => 
                            m.Name == attributeName &&
                            m.IsEnabled == true &&
                            m.IsRequired == false &&
                            m.Options.Count == 1),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Updating attribute {attributeName}", LogLevel.Information);
            });
        }

        [Test]
        public void UpdateAttribute_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var attribute = new TmsAttribute
            {
                Id = Guid.NewGuid(),
                Name = "TestAttribute",
                IsEnabled = true,
                IsRequired = false,
                Options = new List<TmsAttributeOptions>()
            };

            var exceptionMessage = "API Error";
            _customAttributesApiMock
                .Setup(x => x.ApiV2CustomAttributesGlobalIdPutAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<GlobalCustomAttributeUpdateModel>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.UpdateAttribute(attribute));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not update attribute {attribute.Name}: API Error", LogLevel.Error);
        }

        #endregion

        #region UpdateProjectAttribute Tests

        [Test]
        public async Task UpdateProjectAttribute_WhenSuccessful_CallsApi()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var attribute = new TmsAttribute
            {
                Id = Guid.NewGuid(),
                Name = "ProjectAttribute",
                IsEnabled = true,
                IsRequired = false,
                Options = new List<TmsAttributeOptions>
                {
                    new TmsAttributeOptions { Id = Guid.NewGuid(), Value = "Value1", IsDefault = false }
                }
            };

            _projectAttributesApiMock
                .Setup(x => x.UpdateProjectsAttributeAsync(
                    projectId.ToString(),
                    It.IsAny<CustomAttributePutModel>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _clientAdapter.UpdateProjectAttribute(projectId, attribute);

            // Assert
            _projectAttributesApiMock.Verify(
                x => x.UpdateProjectsAttributeAsync(
                    projectId.ToString(),
                    It.Is<CustomAttributePutModel>(m => 
                        m.Id == attribute.Id &&
                        m.Name == attribute.Name &&
                        m.IsEnabled == attribute.IsEnabled &&
                        m.IsRequired == attribute.IsRequired &&
                        m.Options.Count == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Updating project attribute {attribute.Name}", LogLevel.Information);
        }

        [Test]
        public void UpdateProjectAttribute_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var attribute = new TmsAttribute
            {
                Id = Guid.NewGuid(),
                Name = "TestAttribute",
                IsEnabled = true,
                IsRequired = false,
                Options = new List<TmsAttributeOptions>()
            };

            var exceptionMessage = "API Error";
            _projectAttributesApiMock
                .Setup(x => x.UpdateProjectsAttributeAsync(
                    It.IsAny<string>(),
                    It.IsAny<CustomAttributePutModel>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.UpdateProjectAttribute(projectId, attribute));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not update attribute {attribute.Name}: API Error", LogLevel.Error);
        }

        #endregion

        #region UploadAttachment Tests

        [Test]
        public async Task UploadAttachment_WhenSuccessful_ReturnsAttachmentId()
        {
            // Arrange
            var fileName = "test.txt";
            var attachmentId = Guid.NewGuid();
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

            var attachmentModel = new AttachmentModel(
                fileId: "test-file-id",
                type: "text/plain",
                size: 12,
                createdDate: DateTime.UtcNow,
                modifiedDate: null,
                createdById: Guid.NewGuid(),
                modifiedById: null,
                name: fileName,
                id: attachmentId
            );

            _attachmentsApiMock
                .Setup(x => x.ApiV2AttachmentsPostAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(attachmentModel);

            // Act
            var result = await _clientAdapter.UploadAttachment(fileName, content);

            // Assert
            Assert.That(result, Is.EqualTo(attachmentId));

            _attachmentsApiMock.Verify(
                x => x.ApiV2AttachmentsPostAsync(
                    It.Is<FileParameter>(fp => fp != null),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _loggerMock.VerifyLogging($"Uploading attachment {fileName}", LogLevel.Debug);
        }

        [Test]
        public void UploadAttachment_WithNullFileName_ThrowsArgumentNullException()
        {
            // Arrange
            string fileName = null!;
            var content = new MemoryStream();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _clientAdapter.UploadAttachment(fileName, content));

            Assert.That(exception!.ParamName, Is.EqualTo("fileName"));
        }

        [Test]
        public void UploadAttachment_WithEmptyFileName_ThrowsArgumentNullException()
        {
            // Arrange
            var fileName = "";
            var content = new MemoryStream();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _clientAdapter.UploadAttachment(fileName, content));

            Assert.That(exception!.ParamName, Is.EqualTo("fileName"));
        }

        [Test]
        public void UploadAttachment_WithNullContent_ThrowsArgumentNullException()
        {
            // Arrange
            var fileName = "test.txt";
            Stream content = null!;

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _clientAdapter.UploadAttachment(fileName, content));

            Assert.That(exception!.ParamName, Is.EqualTo("content"));
        }

        [Test]
        public void UploadAttachment_WhenAdapterHelperThrowsException_LogsAndRethrows()
        {
            // Arrange
            var fileName = "test.txt";
            var content = new MemoryStream();
            var exceptionMessage = "Upload failed";

            _attachmentsApiMock
                .Setup(x => x.ApiV2AttachmentsPostAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(
                async () => await _clientAdapter.UploadAttachment(fileName, content));

            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));
            _loggerMock.VerifyLogging($"Could not upload attachment {fileName}", LogLevel.Error, Times.AtLeastOnce());
        }

        #endregion

        #region CreateParameter Tests

        [Test]
        public async Task CreateParameter_WhenSuccessful_ReturnsTmsParameter()
        {
            // Arrange
            var parameterName = "TestParameter";
            var parameterValue = "TestValue";
            var parameterId = Guid.NewGuid();
            var parameterKeyId = Guid.NewGuid();

            var inputParameter = new Parameter
            {
                Name = parameterName,
                Value = parameterValue
            };

            var apiResponse = new ParameterApiResult(
                id: parameterId,
                parameterKeyId: parameterKeyId,
                name: parameterName,
                value: parameterValue,
                createdDate: DateTime.UtcNow,
                createdById: Guid.NewGuid(),
                modifiedDate: null,
                modifiedById: null,
                isDeleted: false,
                projectIds: new List<Guid> { Guid.NewGuid() }
            );

            _parametersApiMock
                .Setup(x => x.CreateParameterAsync(
                    It.Is<CreateParameterApiModel>(m => m.Name == parameterName && m.Value == parameterValue),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(apiResponse);

            // Act
            var result = await _clientAdapter.CreateParameter(inputParameter);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Id, Is.EqualTo(parameterId));
                Assert.That(result.Name, Is.EqualTo(parameterName));
                Assert.That(result.Value, Is.EqualTo(parameterValue));
                Assert.That(result.ParameterKeyId, Is.EqualTo(parameterKeyId));

                _parametersApiMock.Verify(
                    x => x.CreateParameterAsync(
                        It.Is<CreateParameterApiModel>(m => m.Name == parameterName && m.Value == parameterValue),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Creating parameter {parameterName}", LogLevel.Information);
            });
        }

        [Test]
        public async Task CreateParameter_WithNullValue_UsesNA()
        {
            // Arrange
            var parameterName = "TestParameter";
            var parameterId = Guid.NewGuid();
            var parameterKeyId = Guid.NewGuid();

            var inputParameter = new Parameter
            {
                Name = parameterName,
                Value = null!
            };

            var apiResponse = new ParameterApiResult(
                id: parameterId,
                parameterKeyId: parameterKeyId,
                name: parameterName,
                value: "N/A",
                createdDate: DateTime.UtcNow,
                createdById: Guid.NewGuid(),
                modifiedDate: null,
                modifiedById: null,
                isDeleted: false,
                projectIds: new List<Guid> { Guid.NewGuid() }
            );

            _parametersApiMock
                .Setup(x => x.CreateParameterAsync(
                    It.Is<CreateParameterApiModel>(m => m.Name == parameterName && m.Value == "N/A"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(apiResponse);

            // Act
            var result = await _clientAdapter.CreateParameter(inputParameter);

            // Assert
            Assert.That(result.Value, Is.EqualTo("N/A"));
        }

        [Test]
        public async Task CreateParameter_WithEmptyValue_UsesNA()
        {
            // Arrange
            var parameterName = "TestParameter";
            var parameterId = Guid.NewGuid();
            var parameterKeyId = Guid.NewGuid();

            var inputParameter = new Parameter
            {
                Name = parameterName,
                Value = "   " // Whitespace only
            };

            var apiResponse = new ParameterApiResult(
                id: parameterId,
                parameterKeyId: parameterKeyId,
                name: parameterName,
                value: "N/A",
                createdDate: DateTime.UtcNow,
                createdById: Guid.NewGuid(),
                modifiedDate: null,
                modifiedById: null,
                isDeleted: false,
                projectIds: new List<Guid> { Guid.NewGuid() }
            );

            _parametersApiMock
                .Setup(x => x.CreateParameterAsync(
                    It.Is<CreateParameterApiModel>(m => m.Name == parameterName && m.Value == "N/A"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(apiResponse);

            // Act
            var result = await _clientAdapter.CreateParameter(inputParameter);

            // Assert
            Assert.That(result.Value, Is.EqualTo("N/A"));
        }

        [Test]
        public void CreateParameter_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var parameter = new Parameter
            {
                Name = "TestParameter",
                Value = "TestValue"
            };

            var exceptionMessage = "API Error";
            _parametersApiMock
                .Setup(x => x.CreateParameterAsync(It.IsAny<CreateParameterApiModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.CreateParameter(parameter));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not create parameter {parameter.Name}: API Error", LogLevel.Error);
        }

        #endregion

        #region GetParameter Tests

        [Test]
        public async Task GetParameter_WhenSuccessful_ReturnsTmsParameters()
        {
            // Arrange
            var parameterName = "TestParameter";
            var parameterId1 = Guid.NewGuid();
            var parameterId2 = Guid.NewGuid();
            var parameterKeyId = Guid.NewGuid();

            var apiResponse = new List<ParameterApiResult>
            {
                new ParameterApiResult(
                    id: parameterId1,
                    parameterKeyId: parameterKeyId,
                    name: parameterName,
                    value: "Value1",
                    createdDate: DateTime.UtcNow,
                    createdById: Guid.NewGuid(),
                    modifiedDate: null,
                    modifiedById: null,
                    isDeleted: false,
                    projectIds: new List<Guid> { Guid.NewGuid() }
                ),
                new ParameterApiResult(
                    id: parameterId2,
                    parameterKeyId: parameterKeyId,
                    name: parameterName,
                    value: "Value2",
                    createdDate: DateTime.UtcNow,
                    createdById: Guid.NewGuid(),
                    modifiedDate: null,
                    modifiedById: null,
                    isDeleted: false,
                    projectIds: new List<Guid> { Guid.NewGuid() }
                )
            };

            _parametersApiMock
                .Setup(x => x.ApiV2ParametersSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.Is<ParametersFilterApiModel>(f => f.Name == parameterName && f.IsDeleted == false),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(apiResponse);

            // Act
            var result = await _clientAdapter.GetParameter(parameterName);

            // Assert
            Assert.Multiple(() => {
                Assert.That(result.Count, Is.EqualTo(2));
                Assert.That(result[0].Id, Is.EqualTo(parameterId1));
                Assert.That(result[0].Name, Is.EqualTo(parameterName));
                Assert.That(result[0].Value, Is.EqualTo("Value1"));
                Assert.That(result[1].Id, Is.EqualTo(parameterId2));
                Assert.That(result[1].Value, Is.EqualTo("Value2"));

                _parametersApiMock.Verify(
                    x => x.ApiV2ParametersSearchPostAsync(
                        null, null, null!, null!, null!,
                        It.Is<ParametersFilterApiModel>(f => f.Name == parameterName && f.IsDeleted == false),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _loggerMock.VerifyLogging($"Getting parameter {parameterName}", LogLevel.Information);
            });
        }

        [Test]
        public void GetParameter_WhenApiThrowsException_LogsAndRethrows()
        {
            // Arrange
            var parameterName = "TestParameter";
            var exceptionMessage = "API Error";

            _parametersApiMock
                .Setup(x => x.ApiV2ParametersSearchPostAsync(
                    null, null, null!, null!, null!,
                    It.IsAny<ParametersFilterApiModel>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(async () => await _clientAdapter.GetParameter(parameterName));
            Assert.That(exception!.Message, Is.EqualTo(exceptionMessage));

            _loggerMock.VerifyLogging($"Could not get parameter {parameterName}: API Error", LogLevel.Error);
        }

        #endregion
        
    }
}
