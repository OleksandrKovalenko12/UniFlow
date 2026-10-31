using UniFlow.Infrastructure.Storage;

namespace UniFlow.Tests.Infrastructure;

public sealed class LocalFileStorageTests : IDisposable
{
    private const string Folder = "applications/abc";
    private static readonly byte[] Content = [1, 2, 3, 4, 5];

    private readonly string _testDir;
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "uniflow-tests", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_testDir, "storage");
        _storage = new LocalFileStorage(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private Task<string> SaveAsync(string folder = Folder, string extension = ".pdf") =>
        _storage.SaveAsync(new MemoryStream(Content), folder, extension);

    private string FullPath(string relativePath) => Path.Combine(_root, relativePath);

    private string CreateOutsideFile()
    {
        Directory.CreateDirectory(_testDir);
        var path = Path.Combine(_testDir, "outside.txt");
        File.WriteAllText(path, "secret");
        return path;
    }

    //Constructor

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingRoot_Throws(string? rootPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => new LocalFileStorage(rootPath!));
    }

    [Fact]
    public async Task Constructor_RootWithTrailingSeparator_StillWorks()
    {
        var storage = new LocalFileStorage(_root + Path.DirectorySeparatorChar);

        var path = await storage.SaveAsync(new MemoryStream(Content), Folder, ".pdf");

        Assert.True(File.Exists(FullPath(path)));
    }

    //SaveAsync

    [Fact]
    public async Task SaveAsync_ValidData_WritesFileAndReturnsRelativePath()
    {
        var path = await SaveAsync();

        Assert.StartsWith(Folder + "/", path);
        Assert.EndsWith(".pdf", path);
        Assert.False(Path.IsPathRooted(path));
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _));
        Assert.Equal(Content, File.ReadAllBytes(FullPath(path)));
    }

    [Fact]
    public async Task SaveAsync_SameInputTwice_CreatesTwoFiles()
    {
        var first = await SaveAsync();
        var second = await SaveAsync();

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(FullPath(first)));
        Assert.True(File.Exists(FullPath(second)));
    }

    [Theory]
    [InlineData(".a")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".abcdefghi")]
    public async Task SaveAsync_ValidExtension_UsesIt(string extension)
    {
        var path = await SaveAsync(extension: extension);

        Assert.EndsWith(extension, path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pdf")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".p/df")]
    [InlineData(".p\\df")]
    [InlineData(".p df")]
    [InlineData(".abcdefghij")]
    public async Task SaveAsync_InvalidExtension_ThrowsAndWritesNothing(string extension)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(extension: extension));

        Assert.Equal("extension", ex.ParamName);
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_EmptyFolder_Throws(string folder)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(folder: folder));

        Assert.Equal("folder", ex.ParamName);
    }

    [Fact]
    public async Task SaveAsync_NullContent_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _storage.SaveAsync(null!, Folder, ".pdf"));

        Assert.Equal("content", ex.ParamName);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("applications/../../outside")]
    [InlineData("../storage-evil")]
    public async Task SaveAsync_FolderOutsideRoot_ThrowsAndCreatesNothing(string folder)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(folder: folder));

        Assert.Equal("folder", ex.ParamName);
        Assert.False(Directory.Exists(_testDir));
    }

    [Fact]
    public async Task SaveAsync_AbsoluteFolder_ThrowsAndCreatesNothing()
    {
        var outside = Path.Combine(_testDir, "outside");

        await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(folder: outside));

        Assert.False(Directory.Exists(_testDir));
    }

    [Fact]
    public async Task SaveAsync_CopyFails_DeletesPartialFile()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _storage.SaveAsync(new MemoryStream(Content), Folder, ".pdf", cts.Token));

        Assert.Empty(Directory.GetFiles(FullPath(Folder)));
    }

    //OpenRead

    [Fact]
    public async Task OpenRead_SavedFile_ReturnsSameContent()
    {
        var path = await SaveAsync();

        using var stream = _storage.OpenRead(path);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal(Content, copy.ToArray());
    }

    [Fact]
    public async Task OpenRead_MissingFile_ThrowsFileNotFound()
    {
        await SaveAsync();

        Assert.Throws<FileNotFoundException>(() => _storage.OpenRead($"{Folder}/missing.pdf"));
    }

    //Path traversal (OpenRead + Delete)

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("applications/..")]
    [InlineData("../outside.txt")]
    [InlineData("applications/../../outside.txt")]
    [InlineData("../storage-evil/x.pdf")]
    public void OpenReadAndDelete_InvalidOrOutsidePath_Throw(string relativePath)
    {
        var openEx = Assert.Throws<ArgumentException>(() => _storage.OpenRead(relativePath));
        var deleteEx = Assert.Throws<ArgumentException>(() => _storage.Delete(relativePath));

        Assert.Equal("relativePath", openEx.ParamName);
        Assert.Equal("relativePath", deleteEx.ParamName);
    }

    [Fact]
    public void OpenReadAndDelete_AbsolutePath_ThrowAndKeepFile()
    {
        var outside = CreateOutsideFile();

        Assert.Throws<ArgumentException>(() => _storage.OpenRead(outside));
        Assert.Throws<ArgumentException>(() => _storage.Delete(outside));

        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void Delete_TraversalToExistingFile_DoesNotDeleteIt()
    {
        var outside = CreateOutsideFile();

        Assert.Throws<ArgumentException>(() => _storage.Delete("../outside.txt"));

        Assert.True(File.Exists(outside));
    }

    //Delete

    [Fact]
    public async Task Delete_SavedFile_RemovesIt()
    {
        var path = await SaveAsync();

        _storage.Delete(path);

        Assert.False(File.Exists(FullPath(path)));
    }

    [Fact]
    public void Delete_MissingFile_DoesNotThrow()
    {
        var ex = Record.Exception(() => _storage.Delete("applications/missing/x.pdf"));

        Assert.Null(ex);
    }
}