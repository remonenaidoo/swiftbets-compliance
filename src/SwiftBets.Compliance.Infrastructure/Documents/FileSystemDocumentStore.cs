using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SwiftBets.Compliance.Application.Ports;

namespace SwiftBets.Compliance.Infrastructure.Documents;

/// <summary>
/// Object storage on a mounted volume, for environments without an S3-compatible store. Keys are restricted to the
/// shape the handler writes, so a key can never reach outside the root.
/// </summary>
public sealed partial class FileSystemDocumentStore(IOptions<DocumentOptions> options) : IDocumentStore
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".part";
        await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null);
    }

    private string PathFor(string key) =>
        SafeKey().IsMatch(key) ? Path.Combine(options.Value.Root, key) : throw new ArgumentException("Storage key is not allowed.", nameof(key));

    [GeneratedRegex("^[a-z]+(/[0-9a-f]{32}){1,3}$")]
    private static partial Regex SafeKey();
}
