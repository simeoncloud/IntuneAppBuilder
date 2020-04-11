using System.Threading.Tasks;
using IntuneAppBuilder.Domain;

namespace IntuneAppBuilder.Services
{
    /// <summary>
    ///     Functionality for interacting with Intune APIs.
    /// </summary>
    public interface IIntuneAppPublishingService
    {
        /// <summary>
        ///  Uploads a file for a mobileApp and sets it as the current contentVersion.
        /// </summary>
        /// <param name="package">A package created using the packaging service.</param>
        /// <returns></returns>
        Task PublishAsync(MobileLobAppContentFilePackage package);
    }
}