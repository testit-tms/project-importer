using Importer.Client;
using Importer.Models;
using Importer.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace ImporterTests
{
	[TestFixture]
	public class ParameterServiceTests
	{
		private Mock<ILogger<ParameterService>> _loggerMock = null!;
		private Mock<IClientAdapter> _clientAdapterMock = null!;
		private ParameterService _service = null!;

        private Parameter _parameter1 = null!;
        private Parameter _parameter2 = null!;
        private TmsParameter _existingParameter = null!;

        [SetUp]
		public void SetUp()
		{
			_loggerMock = new Mock<ILogger<ParameterService>>();
			_clientAdapterMock = new Mock<IClientAdapter>(MockBehavior.Strict);
			_service = new ParameterService(_loggerMock.Object, _clientAdapterMock.Object);

            _parameter1 = new Parameter
            {
                Name = "TestParam1",
                Value = "Value1"
            };


            _parameter2 = new Parameter
            {
                Name = "TestParam2",
                Value = "Value2"
            };

            _existingParameter = new TmsParameter
            {
                Id = Guid.NewGuid(),
                Name = "TestParam1",
                Value = "Value1"
            };
        }

		[Test]
		public async Task CreateParameters_WhenEmptyInput_LogsAndReturnsEmpty()
		{
			// Act
			var result = await _service.CreateParameters(Array.Empty<Parameter>());

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Is.Not.Null);
				Assert.That(result, Is.Empty);

				_clientAdapterMock.Verify(a => a.GetParameter(It.IsAny<string>()), Times.Never);
				_clientAdapterMock.Verify(a => a.CreateParameter(It.IsAny<Parameter>()), Times.Never);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WithMultipleParameters_HandlesExistingAndCreate()
		{
			// Arrange
			var createdForSecondTmsParameter = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = _parameter2.Name,
                Value = _parameter2.Value
            };

			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { _existingParameter });

			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter2.Name))
				.ReturnsAsync(new List<TmsParameter>());

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter2))
				.ReturnsAsync(createdForSecondTmsParameter);

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1, _parameter2 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(2));

				Assert.That(result[0].Id, Is.EqualTo(_existingParameter.Id));
				Assert.That(result[0].Name, Is.EqualTo(_existingParameter.Name));
				Assert.That(result[0].Value, Is.EqualTo(_existingParameter.Value));

				Assert.That(result[1].Id, Is.EqualTo(createdForSecondTmsParameter.Id));
				Assert.That(result[1].Name, Is.EqualTo(createdForSecondTmsParameter.Name));
				Assert.That(result[1].Value, Is.EqualTo(createdForSecondTmsParameter.Value));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.GetParameter(_parameter2.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Never);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter2), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("already exist", LogLevel.Debug, Times.Once());
			});
		}

		[Test]
		public void CreateParameters_WhenGetParameterThrows_PropagatesException()
		{
			// Arrange
			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ThrowsAsync(new Exception("Failed to get parameter"));

			// Act
			var ex = Assert.ThrowsAsync<Exception>(async () => await _service.CreateParameters(new[] { _parameter1 }));

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(ex!.Message, Is.EqualTo("Failed to get parameter"));
				_clientAdapterMock.Verify(a => a.CreateParameter(It.IsAny<Parameter>()), Times.Never);
				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WithSameNameDifferentValues_UsesExistingAndCreatesSecond()
		{
			// Arrange
			var sameNameDifferentValue = new Parameter {
                Name = _parameter1.Name,
                Value = "AnotherValue"
            };

			var createdTmsParameter = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = sameNameDifferentValue.Name,
                Value = sameNameDifferentValue.Value
            };

			_clientAdapterMock
				.SetupSequence(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { _existingParameter })
				.ReturnsAsync(new List<TmsParameter> { _existingParameter });

			_clientAdapterMock
				.Setup(a => a.CreateParameter(sameNameDifferentValue))
				.ReturnsAsync(createdTmsParameter);

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1, sameNameDifferentValue });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(2));

				Assert.That(result[0].Id, Is.EqualTo(_existingParameter.Id));
				Assert.That(result[0].Value, Is.EqualTo(_existingParameter.Value));

				Assert.That(result[1].Id, Is.EqualTo(createdTmsParameter.Id));
				Assert.That(result[1].Value, Is.EqualTo(createdTmsParameter.Value));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Exactly(2));
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Never);
				_clientAdapterMock.Verify(a => a.CreateParameter(sameNameDifferentValue), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("already exist", LogLevel.Debug, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenParameterExists_ReturnsExistingNotCallCreate()
		{
            // Arrange
            var notTmsParametr = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = "TestParam2",
                Value = "N/A"
            };

            _clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { _existingParameter, notTmsParametr });

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(1));
				Assert.That(result[0].Id, Is.EqualTo(_existingParameter.Id));
				Assert.That(result[0].Name, Is.EqualTo(_existingParameter.Name));
				Assert.That(result[0].Value, Is.EqualTo(_existingParameter.Value));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(It.IsAny<Parameter>()), Times.Never);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("already exist", LogLevel.Debug, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenParameterNotExists_CreatesAndReturnsNew()
		{
            // Arrange
            var newTmsParameter = new TmsParameter
            {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = _parameter1.Value
            };

			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter>());

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter1))
				.ReturnsAsync(newTmsParameter);

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(1));
				Assert.That(result[0].Id, Is.EqualTo(newTmsParameter.Id));
				Assert.That(result[0].Name, Is.EqualTo(newTmsParameter.Name));
				Assert.That(result[0].Value, Is.EqualTo(newTmsParameter.Value));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenCreateThrows_UsesNAFallbackAndLogsErrorAndDebug()
		{
            // Arrange
            var otherTmsParameter = new TmsParameter
            {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = "other"
            };

            var naTmsParameter = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = "N/A"
            };

            _clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { otherTmsParameter, naTmsParameter });

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter1))
				.ThrowsAsync(new Exception("boom"));

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(1));
				Assert.That(result[0].Id, Is.EqualTo(naTmsParameter.Id));
				Assert.That(result[0].Value, Is.EqualTo("N/A"));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("Failed to create parameter", LogLevel.Error, Times.Once());
				_loggerMock.VerifyLogging("already exist with N/A", LogLevel.Debug, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenCreateThrows_UsesEmptyStringFallback()
		{
            // Arrange
            var otherTmsParameter = new TmsParameter
            {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = "other"
            };

            var emptyTmsParameter = new TmsParameter
            {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = string.Empty
            };

			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { otherTmsParameter, emptyTmsParameter });

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter1))
				.ThrowsAsync(new Exception("fail"));

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(1));
				Assert.That(result[0].Id, Is.EqualTo(emptyTmsParameter.Id));
				Assert.That(result[0].Value, Is.EqualTo(string.Empty));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("Failed to create parameter", LogLevel.Error, Times.Once());
				_loggerMock.VerifyLogging("already exist with empty string", LogLevel.Debug, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenCreateThrows_AndNoFallback_SkipsAdding()
		{
			// Arrange
			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter>
				{
					new TmsParameter { Id = Guid.NewGuid(), Name = _parameter1.Name, Value = "other1" },
					new TmsParameter { Id = Guid.NewGuid(), Name = _parameter1.Name, Value = "other2" }
				});

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter1))
				.ThrowsAsync(new Exception("err"));

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Is.Not.Null);
				Assert.That(result, Is.Empty);

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("Failed to create parameter", LogLevel.Error, Times.Once());
			});
		}

		[Test]
		public async Task CreateParameters_WhenBothNAAndEmptyPresent_PrefersNA()
		{
			// Arrange
			var naTmsParameter = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = "N/A"
            };

			var emptyTmsParameter = new TmsParameter {
                Id = Guid.NewGuid(),
                Name = _parameter1.Name,
                Value = string.Empty
            };

			_clientAdapterMock
				.Setup(a => a.GetParameter(_parameter1.Name))
				.ReturnsAsync(new List<TmsParameter> { emptyTmsParameter, naTmsParameter });

			_clientAdapterMock
				.Setup(a => a.CreateParameter(_parameter1))
				.ThrowsAsync(new Exception("x"));

			// Act
			var result = await _service.CreateParameters(new[] { _parameter1 });

			// Assert
			Assert.Multiple(() =>
			{
				Assert.That(result, Has.Count.EqualTo(1));
				Assert.That(result[0].Value, Is.EqualTo("N/A"));
				Assert.That(result[0].Id, Is.EqualTo(naTmsParameter.Id));

				_clientAdapterMock.Verify(a => a.GetParameter(_parameter1.Name), Times.Once);
				_clientAdapterMock.Verify(a => a.CreateParameter(_parameter1), Times.Once);

				_loggerMock.VerifyLogging("Creating parameters", LogLevel.Information, Times.Once());
				_loggerMock.VerifyLogging("Failed to create parameter", LogLevel.Error, Times.Once());
				_loggerMock.VerifyLogging("already exist with N/A", LogLevel.Debug, Times.Once());
			});
		}
	}
}


