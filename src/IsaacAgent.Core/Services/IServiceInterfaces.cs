using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

public interface IModService
{
    Task<ModProject> CreateProjectAsync(string name, string parentDir, CancellationToken ct = default);
    Task<ModProject?> LoadProjectAsync(string dirPath, CancellationToken ct = default);
    Task SaveProjectAsync(ModProject project, CancellationToken ct = default);
    Task<ModFile?> ReadFileAsync(string dirPath, string relativePath, CancellationToken ct = default);
    Task WriteFileAsync(string dirPath, string relativePath, string content, CancellationToken ct = default);
    Task<List<ModFile>> ListFilesAsync(string dirPath, CancellationToken ct = default);
    Task<string> BuildModAsync(string dirPath, CancellationToken ct = default);
}

public interface IDiagnosticService
{
    Task<DiagnosticResult> AnalyzeAsync(string filePath, string content, CancellationToken ct = default);
    Task<DiagnosticResult> AnalyzeProjectAsync(string dirPath, CancellationToken ct = default);
}

public interface IKnowledgeService
{
    string SearchApi(string query);
    string GetCallbackInfo(string callbackName);
    string GetClassInfo(string className);
    string GetEnumInfo(string enumName);
    List<string> GetSuggestions(string partial);
}
