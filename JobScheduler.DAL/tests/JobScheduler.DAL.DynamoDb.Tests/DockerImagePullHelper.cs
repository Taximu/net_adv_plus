using System.Diagnostics;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// Ensures an image exists locally. Testcontainers' create step does not always pull before create on all Docker setups.
/// </summary>
internal static class DockerImagePullHelper
{
    public static async Task PullIfNeededAsync(string imageReference, CancellationToken cancellationToken = default)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"pull {imageReference}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        proc.Start();
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"docker pull {imageReference} failed (exit {proc.ExitCode}). Ensure Docker is running and you can pull from the registry. {err}");
        }
    }
}
