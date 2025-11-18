using Importer.Models;
using Importer.Validators;
using Microsoft.Extensions.Options;

namespace ImporterTests;

[TestFixture]
public class AppConfigValidatorTests
{
    private AppConfigValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new AppConfigValidator();
    }

    [Test]
    public void Validate_WhenResultPathIsEmpty_ReturnsFailure()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = string.Empty,
            Tms = new TmsConfig
            {
                Url = "https://example.com",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Contains.Substring("ResultPath cannot be empty"));
        });
    }

    [Test]
    public void Validate_WhenResultPathIsNull_ReturnsFailure()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = null!,
            Tms = new TmsConfig
            {
                Url = "https://example.com",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Contains.Substring("ResultPath cannot be empty"));
        });
    }

    [Test]
    public void Validate_WhenTmsUrlIsEmpty_ReturnsFailure()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = string.Empty,
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Contains.Substring("Tms.Url must be a valid URL"));
        });
    }

    [Test]
    public void Validate_WhenTmsUrlIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "not-a-valid-url",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Contains.Substring("Tms.Url must be a valid URL"));
        });
    }

    [Test]
    public void Validate_WhenTmsUrlIsRelative_ReturnsFailure()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "/relative/path",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Contains.Substring("Tms.Url must be a valid URL"));
        });
    }

    [Test]
    public void Validate_WhenTimeoutIsZero_SetsDefaultTimeout()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "https://example.com",
                Timeout = 0
            }
        };
        const int expectedDefaultTimeout = 10 * 60; // 600 seconds

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(config.Tms.Timeout, Is.EqualTo(expectedDefaultTimeout));
        });
    }

    [Test]
    public void Validate_WhenTimeoutIsNonZero_DoesNotChangeTimeout()
    {
        // Arrange
        const int customTimeout = 120;
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "https://example.com",
                Timeout = customTimeout
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(config.Tms.Timeout, Is.EqualTo(customTimeout));
        });
    }

    [Test]
    public void Validate_WhenAllFieldsAreValid_ReturnsSuccess()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "https://example.com/api",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WhenUrlWithHttpScheme_ReturnsSuccess()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "http://example.com",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WhenUrlWithHttpsScheme_ReturnsSuccess()
    {
        // Arrange
        var config = new AppConfig
        {
            ResultPath = "/path/to/results",
            Tms = new TmsConfig
            {
                Url = "https://example.com:8080/api/v1",
                Timeout = 30
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.That(result.Succeeded, Is.True);
    }
}

