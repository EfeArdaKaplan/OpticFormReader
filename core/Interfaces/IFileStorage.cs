namespace core.Interfaces
{
    public interface IFileStorage
    {
        // Dosyayı kaydeder, veritabanında saklanacak anahtarı (key) döner.
        Task<string> SaveAsync(
            Stream content,
            string extension,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default);

        // Anahtarı verilen dosyayı okur.
        Task<Stream> OpenReadAsync(
            string key,
            CancellationToken cancellationToken = default);

        // Anahtarı verilen dosyayı siler.
        Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default);
    }
}