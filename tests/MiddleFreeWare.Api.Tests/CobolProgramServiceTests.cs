using FluentAssertions;
using MiddleFreeWare.Api.Models;
using MiddleFreeWare.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MiddleFreeWare.Api.Tests;

// ── Tests CobolProgramService ──────────────────────────────────
public class CobolProgramServiceTests
{
    private readonly Mock<ICobolRunner> _runnerMock;
    private readonly Mock<ILogger<CobolProgramService>> _loggerMock;
    private readonly CobolProgramService _service;

    public CobolProgramServiceTests()
    {
        _runnerMock  = new Mock<ICobolRunner>();
        _loggerMock  = new Mock<ILogger<CobolProgramService>>();
        _service     = new CobolProgramService(_runnerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task RunProgram_WithAllowedProgram_CallsRunner()
    {
        // Arrange
        var expected = new CobolExecutionResult
        {
            Success      = true,
            ProgramName  = "ex01_hello",
            Output       = "Bonjour, le monde !",
            ExitCode     = 0
        };
        _runnerMock.Setup(r => r.ExecuteAsync("ex01_hello", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var request = new CobolExecutionRequest { ProgramName = "ex01_hello" };

        // Act
        var result = await _service.RunProgramAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Bonjour, le monde !");
        _runnerMock.Verify(r => r.ExecuteAsync("ex01_hello", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunProgram_WithUnknownProgram_ReturnsFail()
    {
        // Arrange
        var request = new CobolExecutionRequest { ProgramName = "programme_inexistant" };

        // Act
        var result = await _service.RunProgramAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.ErrorOutput.Should().Contain("non autorisé");
        _runnerMock.Verify(r => r.ExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ex01_hello")]
    [InlineData("ex04_conditions")]
    [InlineData("ex14_debug")]
    public async Task RunProgram_AllowedPrograms_AreAccepted(string programName)
    {
        // Arrange
        _runnerMock.Setup(r => r.ExecuteAsync(programName, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CobolExecutionResult { Success = true, ExitCode = 0 });

        var request = new CobolExecutionRequest { ProgramName = programName };

        // Act
        var result = await _service.RunProgramAsync(request);

        // Assert
        result.ExitCode.Should().NotBe(-1);
    }

    [Fact]
    public async Task RunSource_WithInvalidProgramName_ReturnsFail()
    {
        // Arrange — nom avec injection de commande
        var result = await _service.RunSourceAsync("DISPLAY 'test'.", "../../etc/passwd");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorOutput.Should().Contain("invalide");
    }

    [Fact]
    public async Task ListPrograms_ReturnsNonEmptyList()
    {
        // Act
        var programs = await _service.ListProgramsAsync();

        // Assert
        programs.Should().NotBeEmpty();
        programs.Should().Contain("ex01_hello");
    }
}

// ── Tests ApiResponse ──────────────────────────────────────────
public class ApiResponseTests
{
    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var response = ApiResponse<string>.Ok("data", "message");

        response.Success.Should().BeTrue();
        response.Data.Should().Be("data");
        response.Message.Should().Be("message");
        response.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var response = ApiResponse<string>.Fail("erreur test");

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle().Which.Should().Be("erreur test");
    }

    [Fact]
    public void Fail_WithMultipleErrors_SetsAllErrors()
    {
        var errors = new List<string> { "Erreur 1", "Erreur 2" };
        var response = ApiResponse<string>.Fail(errors);

        response.Success.Should().BeFalse();
        response.Errors.Should().HaveCount(2);
    }
}

// ── Tests modèles ──────────────────────────────────────────────
public class CobolExecutionResultTests
{
    [Fact]
    public void NewResult_HasDefaultValues()
    {
        var result = new CobolExecutionResult();

        result.Success.Should().BeFalse();
        result.Output.Should().BeEmpty();
        result.ErrorOutput.Should().BeEmpty();
        result.ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
