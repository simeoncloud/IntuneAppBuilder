using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using Microsoft.Graph.Beta.Models;

namespace IntuneAppBuilder.Builders
{
    /// <summary>
    ///     Implementers support building a mobileApp package file.
    /// </summary>
    public interface IIntuneAppPackageBuilder
    {
        /// <summary>
        ///     The name of the app that this instance builds. This is matched against the displayName of a mobileApp when
        ///     publishing. If specified as a guid, is treated directly as a mobileApp id.
        /// </summary>
        string Name => GetType().Name.Replace("_", " ");

        /// <summary>
        ///     Builds an app package. The call to BuildAsync is invoked with Environment.CurrentDirectory set to a dedicated,
        ///     transient temp directory created by the caller.
        /// </summary>
        Task<IntuneAppPackage> BuildAsync(MobileLobApp app);
    }
}