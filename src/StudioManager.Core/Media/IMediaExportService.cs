namespace StudioManager.Core.Media;

/// <summary>
/// Copy today's media files from configured folders to a removable USB drive (HCP: CopyTodayFilesFromFolders).
/// </summary>
public interface IMediaExportService
{
    string? FindRemovableDrive();
    Task<int> CopyTodaysFilesAsync(IEnumerable<string> sourceFolders, string destinationRoot, DateOnly day, CancellationToken cancellationToken = default);
}
