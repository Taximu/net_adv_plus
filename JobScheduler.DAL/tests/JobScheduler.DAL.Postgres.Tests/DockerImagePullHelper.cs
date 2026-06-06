using System.Diagnostics;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>
/// Best-effort <c>docker pull</c> before Testcontainers starts a container.
/// Does not throw on failure: some engines (e.g. Rancher Desktop / containerd) return errors like
/// "unable to lease content: lease does not exist" even when <c>docker run</c> / Testcontainers can still use the image.
/// </summary>
internal static class DockerImagePullHelper
{
    private const string ExplicitPullEnv = "DAL_TESTCONTAINERS_EXPLICIT_PULL";

    public static async Task PullIfNeededAsync(string imageReference, CancellationToken cancellationToken = default)
    {
        var flag = Environment.GetEnvironmentVariable(ExplicitPullEnv);
        if (string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase))
            return;

        try
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
                WritePullWarning(imageReference, proc.ExitCode, err);
            }
        }
        catch (Exception ex)
        {
            WritePullWarning(imageReference, null, ex.Message);
        }
    }

    private static void WritePullWarning(string imageReference, int? exitCode, string detail)
    {
        var trimmed = string.IsNullOrWhiteSpace(detail) ? "(no stderr)" : detail.Trim();
        Console.Error.WriteLine(
            $"[JobScheduler.DAL.Postgres.Tests] Optional docker pull for '{imageReference}' did not succeed"
            + (exitCode.HasValue ? $" (exit {exitCode})" : string.Empty)
            + $": {trimmed}. Continuing — Testcontainers will pull or use a local image. "
            + $"To skip this step entirely, set {ExplicitPullEnv}=0. "
            + "If containers still fail to start, restart your Docker/Rancher engine or run: docker pull " + imageReference);
    }
}
