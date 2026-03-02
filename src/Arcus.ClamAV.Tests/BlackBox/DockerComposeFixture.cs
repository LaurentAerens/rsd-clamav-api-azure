using System.Diagnostics;

namespace Arcus.ClamAV.Tests.BlackBox;

/// <summary>
/// xUnit collection fixture that manages Docker Compose lifecycle for BlackBox tests.
/// Automatically starts containers before tests and cleans up after all tests complete.
/// </summary>
public class DockerComposeFixture : IDisposable
{
    private static readonly object _lifecycleLock = new();
    private static int _fixtureRefCount;
    private static bool _dockerLifecycleInitialized;
    private static bool _dockerStartedByFixture;

    private readonly string _workspaceRoot;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public string BaseUrl => _baseUrl;

    public DockerComposeFixture()
    {
        // Find workspace root (where docker-compose.yml is located)
        _workspaceRoot = FindWorkspaceRoot();
        _baseUrl = Environment.GetEnvironmentVariable("CONTAINER_BASE_URL") ?? "http://localhost:8080";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        lock (_lifecycleLock)
        {
            _fixtureRefCount++;

            if (_dockerLifecycleInitialized)
            {
                Console.WriteLine("♻️ Reusing existing Docker lifecycle for BlackBox tests");
                return;
            }

            Console.WriteLine("🐳 Starting Docker Compose for BlackBox tests...");
            try
            {
                _dockerStartedByFixture = StartDockerCompose();
                _dockerLifecycleInitialized = true;
            }
            catch
            {
                _fixtureRefCount--;
                throw;
            }
        }
    }

    private bool StartDockerCompose()
    {
        try
        {
            // Check if Docker is available
            if (!IsDockerAvailable())
            {
                throw new InvalidOperationException(
                    "Docker is not available. Please ensure Docker Desktop is installed and running.");
            }

            // If an instance is already healthy, reuse it instead of recreating resources.
            if (IsApiHealthy())
            {
                Console.WriteLine("♻️ Reusing already running healthy Docker container");
                return false;
            }

            // Stop any existing containers first
            CleanupExistingContainers();

            // Start containers in detached mode
            Console.WriteLine($"📦 Running docker-compose up from: {_workspaceRoot}");
            var output = RunDockerComposeCommand("up -d --build", captureOutput: true);
            Console.WriteLine(output);

            // Wait for the API to be healthy
            Console.WriteLine("⏳ Waiting for API to be healthy...");
            WaitForHealthEndpoint();
            Console.WriteLine("✅ Docker Compose services are ready!");
            return true;
        }
        catch (Exception ex)
        {
            // Another test run may be creating the same resources concurrently.
            // In that case, wait for the existing container to become healthy and reuse it.
            if (IsDockerResourceConflict(ex.Message))
            {
                Console.WriteLine("⚠️ Docker resources already in use by another run; waiting for healthy API and reusing it");
                WaitForHealthEndpoint();
                return false;
            }

            Console.WriteLine($"❌ Failed to start Docker Compose: {ex.Message}");
            // Try to show container logs for debugging
            try
            {
                var logs = RunDockerComposeCommand("logs", captureOutput: true);
                Console.WriteLine("Container logs:");
                Console.WriteLine(logs);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"Could not retrieve logs: {logEx.Message}");
            }

            throw new InvalidOperationException(
                "Failed to start Docker Compose. Ensure docker-compose.yml exists and Docker is running.", ex);
        }
    }

    private bool IsApiHealthy()
    {
        try
        {
            var response = _httpClient.GetAsync($"{_baseUrl}/healthz").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDockerResourceConflict(string message)
    {
        return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || message.Contains("already in use", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Conflict.", StringComparison.OrdinalIgnoreCase);
    }

    private void CleanupExistingContainers()
    {
        RunDockerComposeCommand("down --remove-orphans --volumes", captureOutput: true);
        Thread.Sleep(1000);

        var containerCheck = RunCommand(
            "docker",
            "ps -a --filter name=clamav-api --format \"{{.Names}}\"",
            captureOutput: true);
        if (containerCheck.Contains("clamav-api", StringComparison.OrdinalIgnoreCase))
        {
            RunCommand("docker", "rm -f clamav-api", captureOutput: true);
            Thread.Sleep(500);
        }

        var networkCheck = RunCommand(
            "docker",
            "network ls --filter name=rsd-clamav-api-azure_default --format \"{{.Name}}\"",
            captureOutput: true);
        if (networkCheck.Contains("rsd-clamav-api-azure_default", StringComparison.OrdinalIgnoreCase))
        {
            RunCommand("docker", "network rm rsd-clamav-api-azure_default", captureOutput: true);
            Thread.Sleep(500);
        }
    }

    private void WaitForHealthEndpoint()
    {
        const int maxAttempts = 60;
        const int delayMs = 2000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = _httpClient.GetAsync($"{_baseUrl}/healthz").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Health check passed (attempt {attempt})");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Container not ready yet
            }
            catch (OperationCanceledException)
            {
                // Request timeout, container not ready
            }

            if (attempt % 10 == 0)
            {
                Console.WriteLine($"⏳ Still waiting... (attempt {attempt}/{maxAttempts})");
            }

            Thread.Sleep(delayMs);
        }

        throw new TimeoutException(
            $"Container failed to become healthy after {maxAttempts} attempts ({maxAttempts * delayMs / 1000}s). " +
            $"Check that containers are running: docker-compose ps");
    }

    private bool IsDockerAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (FileNotFoundException)
        {
            return false;  // docker command not found
        }
        catch (Exception)
        {
            return false;  // Other process errors
        }
    }

    private string RunDockerComposeCommand(string arguments, bool captureOutput = false)
    {
        return RunCommand("docker", $"compose {arguments}", captureOutput);
    }

    private string RunCommand(string fileName, string arguments, bool captureOutput = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = _workspaceRoot,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = string.Empty;
        if (captureOutput)
        {
            output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
            {
                output += "\n" + error;
            }
        }

        process.WaitForExit();

        if (process.ExitCode != 0 && arguments.Contains("up", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
            $"docker-compose {arguments} failed with exit code {process.ExitCode}. Output: {output}");
        }

        return output;
    }

    private string FindWorkspaceRoot()
    {
        // Start from test assembly location and walk up to find docker-compose.yml
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDir != null)
        {
            if (File.Exists(Path.Join(currentDir.FullName, "docker-compose.yml")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find docker-compose.yml. Ensure it exists in the workspace root.");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();

        bool shouldCleanupContainers = false;
        lock (_lifecycleLock)
        {
            if (_fixtureRefCount > 0)
            {
                _fixtureRefCount--;
            }

            if (_fixtureRefCount == 0)
            {
                shouldCleanupContainers = _dockerLifecycleInitialized && _dockerStartedByFixture;
                _dockerLifecycleInitialized = false;
                _dockerStartedByFixture = false;
            }
        }

        if (shouldCleanupContainers)
        {
            try
            {
                Console.WriteLine("🧹 Cleaning up Docker Compose containers...");
                CleanupExistingContainers();
                Console.WriteLine("✅ Containers stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Failed to stop containers: {ex.Message}");
            }
        }
    }
}
