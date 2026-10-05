using StudioManager.Core.Media;

namespace StudioManager.Core.Services;

public sealed class MediaExportService : IMediaExportService
{
    public string? FindRemovableDrive()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive is { DriveType: DriveType.Removable, IsReady: true })
                    return drive.RootDirectory.FullName;
            }
            catch
            {
                // skip inaccessible drives
            }
        }

        return null;
    }

    public Task<int> CopyTodaysFilesAsync(
        IEnumerable<string> sourceFolders,
        string destinationRoot,
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        Directory.CreateDirectory(destinationRoot);

        var count = 0;
        foreach (var folder in sourceFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var created = DateOnly.FromDateTime(File.GetCreationTime(file));
                if (created != day)
                    continue;

                var dest = Path.Combine(destinationRoot, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
                count++;
            }
        }

        return Task.FromResult(count);
    }
}
