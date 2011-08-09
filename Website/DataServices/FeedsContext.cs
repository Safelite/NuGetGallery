using System.Data.Entity;
using System.Linq;

namespace NuGetGallery 
{
    public class FeedsContext 
    {
        private IEntityRepository<Package> packageRepo;

        public FeedsContext(IEntityRepository<Package> packageRepo) 
        {
            this.packageRepo = packageRepo;
        }

        public IQueryable<FeedPackage> Packages 
        {
            get 
            {
                return packageRepo.GetAll()
                    .Include(p => p.PackageRegistration)
                    .Include(p => p.Authors)
                    .Include(p => p.Dependencies)
                    .Include(p => p.Reviews)
                    .Select(p => new FeedPackage
                    {
                        Id = p.PackageRegistration.Id,
                        Version = p.Version,
                        Authors = p.FlattenedAuthors,
                        Copyright = p.Copyright,
                        Created = p.Created,
                        Dependencies = p.FlattenedDependencies,
                        Description = p.Description,
                        DownloadCount = p.PackageRegistration.DownloadCount,
                        ExternalPackageUri = p.ExternalPackageUrl,
                        // TODO: build the gallery details URL for real
                        GalleryDetailsUrl = "http://localhost",
                        IconUrl = p.IconUrl,
                        IsLatestVersion = p.IsLatest,
                        LastUpdated = p.LastUpdated,
                        LicenseUrl = p.LicenseUrl,
                        PackageHash = p.Hash,
                        PackageHashAlgorithm = p.HashAlgorithm,
                        PackageSize = p.PackageFileSize,
                        ProjectUrl = p.ProjectUrl,
                        Published = p.Published,
                        Rating = p.PackageRegistration.RatingMean,
                        RatingsCount = p.PackageRegistration.RatingCount,
                        // TODO: build the report abuse URL for real
                        ReportAbuseUrl = "http://localhost",
                        RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                        Summary = p.Summary,
                        Tags = p.Tags,
                        Title = p.Title,
                        VersionDownloadCount = p.DownloadCount,
                        VersionRating = p.Reviews.Average(pr => pr.Rating),
                        VersionRatingsCount = p.Reviews.Sum(pr => pr.Rating),
                    })
                    .AsQueryable<FeedPackage>();
            }
        }
    }
}
