using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IntuneAppBuilder.Builders;
using File = System.IO.File;

namespace IntuneAppBuilder.Util
{
    /// <summary>
    ///     Helper methods.
    /// </summary>
    public static class ReflectionUtil
    {
        /// <summary>
        ///     Saves a common install.bat file to the working directory that will launch either a 64 bit or 32 bit setup.exe or
        ///     msi depending on the client operating system.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static async Task SaveInstallBatFileAsync(this IIntuneAppContentBuilder builder)
        {
            await typeof(ReflectionUtil).Assembly.SaveEmbeddedResourceAsFileAsync($"{typeof(ReflectionUtil).Namespace}.Install.bat", "Install.bat");
        }

        /// <summary>
        ///     Saves all embedded resources whose names are prefixed with the specified type full name to the working directory.
        /// </summary>
        /// <param name="relativeTo"></param>
        /// <param name="includeTypeNameInPrefix"></param>
        /// <returns></returns>
        public static async Task SaveEmbeddedResourceFilesAsync(this Type relativeTo, bool includeTypeNameInPrefix = true)
        {
            var prefix = $"{relativeTo.Namespace}.";
            if (includeTypeNameInPrefix) prefix += $"{relativeTo.Name}.";
            foreach (var resourceName in relativeTo.Assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = resourceName.Substring(prefix.Length);
                if (string.IsNullOrEmpty(Path.GetExtension(fileName))) fileName = $"{relativeTo.Name}.{fileName}";
                await relativeTo.Assembly.SaveEmbeddedResourceAsFileAsync(resourceName, fileName);
            }
        }

        public static async Task SaveEmbeddedResourceAsFileAsync(this Assembly assembly, string embeddedResourceName, string path = null)
        {
            using var resourceStream = assembly.GetManifestResourceStream(embeddedResourceName)
                                       ?? throw new InvalidOperationException("Could not find embedded resource.");
            using var ms = new MemoryStream();
            await resourceStream.CopyToAsync(ms);
            File.WriteAllBytes(path ?? embeddedResourceName, ms.ToArray());
        }
    }
}