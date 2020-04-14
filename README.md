# IntuneAppBuilder 

Use the Simeon IntuneAppBuilder tool to create and deploy Intune packages for MSI and Win32 applications. The tool converts installation files into the .intunewin format that can then be published using the tool or uploaded manually into the Intune Portal.  
 
IntuneAppBuilder is an open source component from the Simeon Microsoft 365 Management toolset. Learn more about Simeon’s full functionality at https://getsimeon.com.

[![Build Status](https://dev.azure.com/simeoncloud/Default/_apis/build/status/simeoncloud.IntuneAppBuilder?branchName=master)](https://dev.azure.com/simeoncloud/Default/_build/latest?definitionId=1&branchName=master)

## Getting Started

1. **[Get .NET Core 3.1](https://dotnet.microsoft.com/download)** (or higher)

2. **Install** from an elevated command prompt
```
dotnet tool install -g IntuneAppBuilder
IntuneAppBuilder [args]
```

3. **Run** from a command prompt to print usage instructions
```
IntuneAppBuilder 

Usage:
  IntuneAppBuilder [options] [command]

Options:
  --version         Show version information
  -?, -h, --help    Show help and usage information

Commands:
  pack
  publish

```

The tool can ```pack``` your app or ```publish``` an app you have previously packaged.

4. **Package** an app
```
IntuneAppBuilder pack --source .\MyAppInstallFiles --output .\MyAppPackage
```

You should see 3 files in the output folder:

- MyAppInstallFiles.intunewin.json - this file contains metadata about the packaged app
- MyAppInstallFiles.intunewin - this file can be used directly to publish the app using the ```IntuneAppBuilder publish``` command
- MyAppInstallFiles.portal.intunewin - this file can be uploaded to the Intune Portal as a Win32 app

5. **Publish** an app
```
IntuneAppBuilder publish --source .\MyAppPackage\MyAppInstallFiles.intunewin.json
```

IntuneAppBuilder will publish the app content. You will be prompted to sign in to your tenant when publishing. 

6. **Configure**

After publishing, you can find the app in your Intune portal and make any required changes (assigning, updating the command line, detection rules, etc.).

## Notes

The Windows Installer COM service is used to retrieve information about MSIs if one is included in your application. When the tool is running on a non-Windows system, the tool will log a warning and continue creating the package without the additional MSI metadata.
