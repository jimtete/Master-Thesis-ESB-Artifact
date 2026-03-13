using System.IO;
using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.ContractsService;

public class ContractsExplorerService : IContractsExplorerService
{
    public async Task<FileExplorerNode> LoadTreeAsync(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path cannot be null or empty.", nameof(rootPath));
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Contracts root path was not found: {rootPath}");
        }

        return await Task.Run(() => BuildDirectoryNode(rootPath));
    }

    public async Task CreateDirectoryAsync(string parentPath, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentException("Parent path cannot be null or empty.", nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            throw new ArgumentException("Directory name cannot be null or empty.", nameof(directoryName));
        }

        if (!Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException($"Parent path was not found: {parentPath}");
        }

        var targetDirectoryPath = Path.Combine(parentPath, directoryName.Trim());
        
        await Task.Run(() => Directory.CreateDirectory(targetDirectoryPath));
    }

    public async Task CreateContractFileAsync(string parentPath, string contractName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentException("Parent path cannot be null or empty.", nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(contractName))
        {
            throw new ArgumentException("Contract name cannot be null or empty.", nameof(contractName));
        }

        if (!Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException($"Parent directory was not found: {parentPath}");
        }

        var normalizedFileName = NormalizeContractFileName(contractName);
        var filePath = Path.Combine(parentPath, normalizedFileName);

        if (!File.Exists(filePath))
        {
            await File.WriteAllTextAsync(filePath, string.Empty);
        }
    }

    private static string NormalizeContractFileName(string contractName)
    {
        var trimmedName = contractName.Trim();

        return trimmedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? trimmedName
            : $"{trimmedName}.json";
    }

    private static FileExplorerNode BuildDirectoryNode(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);

        var node = new FileExplorerNode
        {
            Name = directoryInfo.Name,
            FullPath = directoryInfo.FullName,
            IsDirectory = true,
            IsExpanded = true,
        };

        foreach (var subDirectory in directoryInfo.GetDirectories().OrderBy(d => d.Name))
        {
            node.Children.Add(BuildDirectoryNode(subDirectory.FullName));
        }

        foreach (var jsonFile in directoryInfo.GetFiles("*.json").OrderBy(f => f.Name))
        {
            node.Children.Add(new FileExplorerNode
            {
                Name = Path.GetFileNameWithoutExtension(jsonFile.Name),
                FullPath = jsonFile.FullName,
                IsDirectory = false,
            });
        }
        
        return node;
    }
}