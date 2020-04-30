using System.IO;

namespace IntuneAppBuilder.IntegrationTests.Util
{
    internal static class FileUtil
    {
        /// <summary>
        ///     Creates directory if it doesn't exist, empties directory if it does.
        /// </summary>
        /// <param name="directory"></param>
        public static void CreateEmptyDirectory(this DirectoryInfo directory)
        {
            if (!directory.Exists) directory.Create();
            else
                foreach (var fsi in directory.EnumerateFileSystemInfos())
                    if (fsi is DirectoryInfo di) di.Delete(true);
                    else fsi.Delete();
        }
    }
}