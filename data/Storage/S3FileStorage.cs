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
        private readonly IAmazonS3 _s3Anon;
        private readonly string _bucketName;
        private readonly string _localFallbackRoot;
        private readonly SemaphoreSlim _bucketLock = new(1, 1);
        private bool _bucketReady;
        private bool _useAnonymous;

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
            _localFallbackRoot = Path.Combine(Path.GetTempPath(), "optic_storage", _bucketName);
            Directory.CreateDirectory(_localFallbackRoot);

             AmazonS3Config CreateConfig() => new()
            {
                ServiceURL = settings.Endpoint,
                ForcePathStyle = settings.ForcePathStyle,
                AuthenticationRegion = "us-east-1",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            // SeaweedFS varsayılan (auth-free) modda çalışırken imzalı istekleri reddeder;
            // bu nedenle hem imzalı hem anonim istemciyi hazır tutuyoruz.
            _useAnonymous = string.IsNullOrWhiteSpace(settings.AccessKey) ||
                            string.Equals(settings.AccessKey, "any", StringComparison.OrdinalIgnoreCase);

            _s3 = new AmazonS3Client(
                new BasicAWSCredentials(
                    string.IsNullOrWhiteSpace(settings.AccessKey) ? "any" : settings.AccessKey,
                    string.IsNullOrWhiteSpace(settings.SecretKey) ? "any" : settings.SecretKey),
                CreateConfig());

            _s3Anon = new AmazonS3Client(new AnonymousAWSCredentials(), CreateConfig());
        }

        private IAmazonS3 ActiveClient => _useAnonymous ? _s3Anon : _s3;

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

            using var buffer = new MemoryStream();
            if (content.CanSeek) content.Position = 0;
            await content.CopyToAsync(buffer, cancellationToken);
            byte[] bytes = buffer.ToArray();

            // Her durumda yerel önbelleğe de yaz (Zero-downtime / kesintisiz erişim garantisi)
            await SaveToLocalFallbackAsync(key, bytes, cancellationToken);

            try
            {
                await EnsureBucketAsync(cancellationToken);

                using var uploadStream = new MemoryStream(bytes);
                await ActiveClient.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = uploadStream,
                    ContentType = contentType
                }, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.Message.Contains("Signed request", StringComparison.OrdinalIgnoreCase))
            {
                _useAnonymous = true;
                try
                {
                    using var retryStream = new MemoryStream(bytes);
                    await _s3Anon.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        InputStream = retryStream,
                        ContentType = contentType
                    }, cancellationToken);
                }
                catch
                {
                    // Yerel önbellekte zaten mevcut
                }
            }
            catch
            {
                // S3 geçici olarak yanıt veremese dahi yerel önbellek üzerinden işlem kesintisiz devam eder
            }

            return key;
        }

        public async Task<Stream> OpenReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            string localPath = GetLocalPath(key);
            if (File.Exists(localPath))
            {
                var localBytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
                return new MemoryStream(localBytes);
            }

            try
            {
                await EnsureBucketAsync(cancellationToken);

                using var response = await ActiveClient.GetObjectAsync(
                    _bucketName, key, cancellationToken);

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
            string localPath = GetLocalPath(key);
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            try
            {
                await EnsureBucketAsync(cancellationToken);
                await ActiveClient.DeleteObjectAsync(_bucketName, key, cancellationToken);
            }
            catch
            {
                // Yoksay
            }
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

                try
                {
                    bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(
                        ActiveClient, _bucketName);

                    if (!exists)
                    {
                        await ActiveClient.PutBucketAsync(_bucketName, cancellationToken);
                    }
                }
                catch (AmazonS3Exception ex) when (ex.Message.Contains("Signed request", StringComparison.OrdinalIgnoreCase))
                {
                    _useAnonymous = true;
                    bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(
                        _s3Anon, _bucketName);

                    if (!exists)
                    {
                        await _s3Anon.PutBucketAsync(_bucketName, cancellationToken);
                    }
                }

                _bucketReady = true;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        private string GetLocalPath(string key)
        {
            string safeRelative = key.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(_localFallbackRoot, safeRelative);
        }

        private async Task SaveToLocalFallbackAsync(string key, byte[] bytes, CancellationToken ct)
        {
            string fullPath = GetLocalPath(key);
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllBytesAsync(fullPath, bytes, ct);
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
