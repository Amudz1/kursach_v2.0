using AiAssistant.Infrastructure.Files;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class AttachmentStorageTests
{
    [Fact]
    public void TemporaryUploadsCanBePrunedWithoutBeingDownloadable()
    {
        var root = Path.Combine(Path.GetTempPath(), "budz-storage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var temporary = Guid.NewGuid().ToString("N") + ".upload";
        var stored = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var temporaryPath = Path.Combine(root, temporary);
        var storedPath = Path.Combine(root, stored);
        try
        {
            File.WriteAllText(temporaryPath, "Interrupted write");
            File.SetLastWriteTimeUtc(temporaryPath, now.UtcDateTime.AddHours(-2));
            File.WriteAllText(storedPath, "New file");
            var storage = new LocalAttachmentStorage(new AttachmentRoot(root));
            Assert.Equal(new[] { temporary }, storage.OldKeys(now.AddHours(-1)));
            Assert.Throws<ArgumentException>(() => storage.OpenRead(temporary));
            storage.Delete(temporary);
            Assert.False(File.Exists(temporaryPath));
            Assert.True(File.Exists(storedPath));
        }
        finally
        {
            File.Delete(temporaryPath);
            File.Delete(storedPath);
            Directory.Delete(root);
        }
    }
}
