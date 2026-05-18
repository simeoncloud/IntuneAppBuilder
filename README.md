# IntuneAppBuilder

Package MSI and Win32 application packages as .intunewin format to Microsoft Intune with this cross-platform tool.

## Overview

Use the Simeon IntuneAppBuilder tool to create and deploy Microsoft Intune packages for MSI and Win32 applications. The
tool converts installation files into the .intunewin format that can then be published using the tool or uploaded
manually into the Intune Portal.

IntuneAppBuilder is an open source component from the Simeon Microsoft 365 Management toolset. Learn more about Simeon’s
full functionality at https://simeoncloud.com.

[![Build Status](https://github.com/simeoncloud/IntuneAppBuilder/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/simeoncloud/IntuneAppBuilder/actions/workflows/ci.yml?query=branch%3Amaster)

## Getting Started

1. **[Get .NET 8 SDK](https://dotnet.microsoft.com/download)** (or higher)

2. **Install** from an elevated command prompt

```
dotnet tool install -g IntuneAppBuilder.Console
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
- MyAppInstallFiles.intunewin - this file can be used directly to publish the app using
  the ```IntuneAppBuilder publish``` command
- MyAppInstallFiles.portal.intunewin - this file can be uploaded to the Intune Portal as a Win32 app

5. **Publish** an app

```
IntuneAppBuilder publish --source .\MyAppPackage\MyAppInstallFiles.intunewin.json
```

IntuneAppBuilder will publish the app content. You will be prompted to sign in to your tenant when publishing.

6. **Configure**

After publishing, you can find the app in your Intune portal and make any required changes (assigning, updating the
command line, detection rules, etc.).

## Authentication

IntuneAppBuilder uses
the [device code flow](https://learn.microsoft.com/en-us/azure/active-directory/develop/scenario-desktop-acquire-token-device-code-flow)
to authenticate by default. Optionally, an access token may be provided as a parameter to the publish command instead:

```
IntuneAppBuilder publish --source .\MyAppPackage\MyAppInstallFiles.intunewin.json --token <token>
```

## Notes

The Windows Installer COM service is used to retrieve information about MSIs if one is included in your application.
When the tool is running on a non-Windows system, the tool will log a warning and continue creating the package without
the additional MSI metadata.

## Running the integration tests

The `IntegrationTests` project publishes real apps to a Microsoft Intune tenant. It authenticates via the
Microsoft Graph **client credentials** (app-only) flow.

### One-time setup

1. Register an application in your Entra ID test tenant.
2. Grant the Microsoft Graph **application** permission `DeviceManagementApps.ReadWrite.All` and admin-consent it.
3. Create a client secret on the app registration.

### Run the tests

Set the following environment variables, then run `dotnet test`:

```
AadAuth:TenantId      - the Entra ID tenant id (GUID)
AadAuth:ClientId      - the registered app's client id (GUID)
AadAuth:ClientSecret  - the registered app's client secret value
```

```
dotnet test IntuneAppBuilder.sln --configuration Release
```

In CI the same values are sourced from the `TEST_AADAUTH_TENANT_ID`, `TEST_AADAUTH_CLIENT_ID`, and
`TEST_AADAUTH_CLIENT_SECRET` GitHub repository secrets.

The tests will create and then delete apps in the target tenant - use a sandbox tenant.

## Dependencies

- **Package Name**: [xunit](https://github.com/xunit/xunit) and [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit)
  - **Version**: 2.4.0
  - **Author**: xunit
  - **License**: [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
