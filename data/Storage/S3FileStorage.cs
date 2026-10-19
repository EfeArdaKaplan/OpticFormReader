using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using core.Interfaces;
using Microsoft.Extensions.Options;

namespace data.Storage
{
    public class S3FileStorage : IFileStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucketName;
        private readonly SemaphoreSlim _bucketLock = new(1, 1);
        private bool _bucketReady;

        public S3FileStorage(IOptions<StorageOptions> options)
        {
            var settings = options.Value;

            if (string.IsNullOrWhiteSpace(settings.Endpoint) ||
                string.IsNullOrWhiteSpace(settings.BucketName))
            {
                throw new InvalidOperationException(
                    "Storage ayarları eksik: Endpoint ve BucketName gerekli.");
            }

            _bucketName = settings.BucketName;

            var config = new AmazonS3Config
            {
                ServiceURL = settings.Endpoint,
                ForcePathStyle = settings.ForcePathStyle,
                AuthenticationRegion = "us-east-1",

                // AWS dışındaki S3 uyumlu sunucular (SeaweedFS) için gerekli.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            _s3 = new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
                config);
        }

        public async Task<string> SaveAsync(
            Stream content,
            string extension,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            extension = NormalizeExtension(extension);
            folder = (folder ?? string.Empty).Trim().Trim('/');

            string fileName = $"{Guid.NewGuid():N}{extension}";
            string key = folder.Length == 0 ? fileName : $"{folder}/{fileName}";

            await EnsureBucketAsync(cancellationToken);

            // S3 isteği için akışın geri sarılabilir olması gerekir.
            Stream upload = content;
            if (!content.CanSeek)
            {
                var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellationToken);
                copy.Position = 0;
                upload = copy;
            }

            try
            {
                await _s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = upload,
                    ContentType = contentType
                }, cancellationToken);
            }
            finally
            {
                if (!ReferenceEquals(upload, content))
                {
                    await upload.DisposeAsync();
                }
            }

            return key;
        }

        public async Task<Stream> OpenReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            await EnsureBucketAsync(cancellationToken);

            try
            {
                using var response = await _s3.GetObjectAsync(
                    _bucketName, key, cancellationToken);

                // Dosyalar küçük (en fazla 10 MB), belleğe alıp bağlantıyı kapatıyoruz.
                var memory = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                return memory;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                throw new FileNotFoundException(
                    $"Dosya depoda bulunamadı: {key}", ex);
            }
        }

        public async Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            await EnsureBucketAsync(cancellationToken);
            await _s3.DeleteObjectAsync(_bucketName, key, cancellationToken);
        }

        public string GetPresignedUrl(
            string key,
            TimeSpan expiry,
            bool isUpload = false)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = isUpload ? HttpVerb.PUT : HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiry)
            };

            return _s3.GetPreSignedURL(request);
        }

        // Bucket yoksa ilk kullanımda oluşturur.
        private async Task EnsureBucketAsync(CancellationToken cancellationToken)
        {
            if (_bucketReady)
            {
                return;
            }

            await _bucketLock.WaitAsync(cancellationToken);
            try
            {
                if (_bucketReady)
                {
                    return;
                }

                bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(
                    _s3, _bucketName);

                if (!exists)
                {
                    await _s3.PutBucketAsync(_bucketName, cancellationToken);
                }

                _bucketReady = true;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        private static string NormalizeExtension(string extension)
        {
            extension = (extension ?? string.Empty).Trim().ToLowerInvariant();

            if (extension.Length == 0)
            {
                return string.Empty;
            }

            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }

            if (extension.Length > 8 || !extension.Skip(1).All(char.IsAsciiLetterOrDigit))
            {
                throw new ArgumentException("Geçersiz dosya uzantısı.", nameof(extension));
            }

            return extension;
        }
    }
}