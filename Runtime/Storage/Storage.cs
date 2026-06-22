using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Persistence
{
    /// <summary>Text storage backend used by the persistence service.</summary>
    public interface ITextStorage
    {
        /// <summary>Returns whether a file exists.</summary>
        Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken);

        /// <summary>Reads text.</summary>
        Task<string> ReadTextAsync(string relativePath, CancellationToken cancellationToken);

        /// <summary>Writes text, replacing the target.</summary>
        Task WriteTextAsync(string relativePath, string text, CancellationToken cancellationToken);

        /// <summary>Deletes a file if present.</summary>
        Task DeleteAsync(string relativePath, CancellationToken cancellationToken);

        /// <summary>Moves a file.</summary>
        Task MoveAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite, CancellationToken cancellationToken);

        /// <summary>Lists files whose names begin with a prefix.</summary>
        Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken);
    }

    /// <summary>Local filesystem text storage rooted under a configured directory.</summary>
    public sealed class FileTextStorage : ITextStorage
    {
        private readonly string _rootPath;

        /// <summary>Creates file storage.</summary>
        public FileTextStorage(IPersistencePathProvider pathProvider)
        {
            if (pathProvider == null)
            {
                throw new ArgumentNullException(nameof(pathProvider));
            }

            _rootPath = Path.GetFullPath(pathProvider.GetRootPath());
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(File.Exists(Resolve(relativePath)));
        }

        /// <inheritdoc />
        public async Task<string> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
        {
            using (FileStream stream = new FileStream(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await reader.ReadToEndAsync();
            }
        }

        /// <inheritdoc />
        public async Task WriteTextAsync(string relativePath, string text, CancellationToken cancellationToken)
        {
            string path = Resolve(relativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Resolve(relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task MoveAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = Resolve(sourceRelativePath);
            string destination = Resolve(destinationRelativePath);
            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (overwrite && File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(source, destination);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(_rootPath))
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }

            IReadOnlyList<string> files = Directory.GetFiles(_rootPath, prefix + "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(files);
        }

        private string Resolve(string relativePath)
        {
            PathSafety.ThrowIfUnsafeSegment(relativePath, nameof(relativePath));
            return PathSafety.CombineUnderRoot(_rootPath, relativePath);
        }
    }

    /// <summary>Deterministic in-memory storage for tests and harnesses.</summary>
    public sealed class InMemoryTextStorage : ITextStorage
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets stored files for assertions.</summary>
        public Dictionary<string, string> Files => _files;

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_files.ContainsKey(relativePath));
        }

        /// <inheritdoc />
        public Task<string> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(relativePath, out string text))
            {
                throw new FileNotFoundException(relativePath);
            }

            return Task.FromResult(text);
        }

        /// <inheritdoc />
        public Task WriteTextAsync(string relativePath, string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files[relativePath] = text ?? string.Empty;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Remove(relativePath);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task MoveAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(sourceRelativePath, out string text))
            {
                throw new FileNotFoundException(sourceRelativePath);
            }

            if (!overwrite && _files.ContainsKey(destinationRelativePath))
            {
                throw new IOException("Destination exists.");
            }

            _files.Remove(sourceRelativePath);
            _files[destinationRelativePath] = text;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> files = _files.Keys
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(files);
        }
    }

    /// <summary>Operation names used by fault-injecting storage.</summary>
    public enum StorageFaultPoint
    {
        /// <summary>No operation.</summary>
        None,
        /// <summary>Before temporary write.</summary>
        BeforeWrite,
        /// <summary>During temporary write.</summary>
        DuringWrite,
        /// <summary>Before replacement move.</summary>
        BeforeMove,
        /// <summary>During replacement move.</summary>
        DuringMove
    }

    /// <summary>Fault-injecting storage wrapper for deterministic interruption tests.</summary>
    public sealed class FaultInjectingTextStorage : ITextStorage
    {
        private readonly ITextStorage _inner;

        /// <summary>Creates a wrapper.</summary>
        public FaultInjectingTextStorage(ITextStorage inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>Gets or sets the next fault point.</summary>
        public StorageFaultPoint NextFault { get; set; }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken) => _inner.ExistsAsync(relativePath, cancellationToken);

        /// <inheritdoc />
        public Task<string> ReadTextAsync(string relativePath, CancellationToken cancellationToken) => _inner.ReadTextAsync(relativePath, cancellationToken);

        /// <inheritdoc />
        public Task WriteTextAsync(string relativePath, string text, CancellationToken cancellationToken)
        {
            if (relativePath.Contains(".tmp.") && NextFault == StorageFaultPoint.BeforeWrite)
            {
                NextFault = StorageFaultPoint.None;
                throw new IOException("Injected failure before temporary write.");
            }

            if (relativePath.Contains(".tmp.") && NextFault == StorageFaultPoint.DuringWrite)
            {
                NextFault = StorageFaultPoint.None;
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("Injected failure during temporary write.");
            }

            return _inner.WriteTextAsync(relativePath, text, cancellationToken);
        }

        /// <inheritdoc />
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken) => _inner.DeleteAsync(relativePath, cancellationToken);

        /// <inheritdoc />
        public Task MoveAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite, CancellationToken cancellationToken)
        {
            if (destinationRelativePath.EndsWith(".json", StringComparison.Ordinal) && NextFault == StorageFaultPoint.BeforeMove)
            {
                NextFault = StorageFaultPoint.None;
                throw new IOException("Injected failure before replacement.");
            }

            if (destinationRelativePath.EndsWith(".json", StringComparison.Ordinal) && NextFault == StorageFaultPoint.DuringMove)
            {
                NextFault = StorageFaultPoint.None;
                throw new IOException("Injected failure during replacement.");
            }

            return _inner.MoveAsync(sourceRelativePath, destinationRelativePath, overwrite, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken) => _inner.ListAsync(prefix, cancellationToken);
    }
}
