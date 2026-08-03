using System.Security.Cryptography;
using System.Text;

namespace Tetris.Redecomp;

/// <summary>
/// A content fingerprint of a journal directory: every file, its length, and the
/// SHA-256 of its bytes. It exists to answer one question with evidence rather than
/// assurance — was the original journal modified? — so it is taken before the
/// re-decomposition reads it and again after.
/// </summary>
internal static class Fingerprint
{
    internal static string Of(string directory)
    {
        var builder = new StringBuilder();
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            // Read-share deliberately: a journal file may still be held open by the
            // process that wrote it, and a fingerprint must never need exclusive
            // access to something it only reads.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            builder
                .Append(Path.GetRelativePath(directory, path).Replace('\\', '/'))
                .Append("  ")
                .Append(new FileInfo(path).Length)
                .Append("  ")
                .Append(hash)
                .AppendLine();
        }

        return builder.ToString();
    }
}
