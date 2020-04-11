using System.IO;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;

namespace IntuneAppBuilder.Services
{
    /// <summary>
    ///     Functionality for packaging Intune apps.
    /// </summary>
    public interface IIntuneAppPackagingService
    {
        /// <summary>
        ///     Creates an intunewin package from a file or directory for use with a mobileApp.
        /// </summary>
        Task<MobileLobAppContentFilePackage> BuildPackageAsync(string sourcePath = ".", string setupFilePath = null);

        /// <summary>
        ///     Packages an intunewin file for direct uploading through the portal. Essentially just zips the existing intunewin
        ///     file with a specific folder structure.
        /// </summary>
        Task BuildPackageForPortalAsync(MobileLobAppContentFilePackage package, Stream outputStream);
    }
}