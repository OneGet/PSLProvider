# Package Source List Provider (PSL)

PSL provider provides a way for enterprise administrators to configure package sources for their business through a PSL manifest file in json format, known as PSL json file or source list file.
Those package source locations are usually trusted, validated, approved or preferred before putting them in the PSL json file.


## Targeted Scenarios
- Allow administrators to control which packages and where to get them from based on their environments.
- Organizations like universities, can assemble the list of approved and course relevant packages (Nodejs, MongoDB for example) for their students to use.
It allows students to acquire one file that points them at the right location for packages.

## Supported Package Types
* zip
* msi
* nupkg
* PowerShell modules
* exe

## Prerequisites
- PSL works on windows 10 anniversary edition
- Down-level OSs with [WMF 5.1 preview][wmf5-1]
- Post TP5 Nano Server
- Unavailable on Linux or Mac

## How to Use the PSL

First you need to get the PSL provider on your machine. There are two ways to do that.

**1. You can clone the repo and build it by your own.**

```PowerShell
git clone https://github.com/OneGet/PSLProvider.git

# Open the .csproj file in Visual Studio and build it.
# After the successful build, you can import the provider assembly directly from the build location. For example,

import-packageprovider C:\psl\PSLProvider\output\Debug\bin\Microsoft.PackageManagement.PackageSourceListProvider.dll
```
Run `Get-PackageProvider`, you will see the `PSL` provider is listed from the output of Get-PackageProvider.

This experience is mainly for developers. For IT pros let's see the following.

**2. You can download the provider via Install-PackageProvider**

```PowerShell
Find-PackageProvider -name PSL
Install-PackageProvider -name PSL -verbose
Get-PackageProvider

```
To make the above scenario to work, your machine needs internet access. In case you do not internet access, you can follow the [instruction][nointernet] to get the provider.

Now we have the provider is installed and imported on your PowerShell session. As a first time experience, PSL is trying to download an sample psl.json source list file. In the example below,
If you hit `y`, PSL will download the psl.json,  saves it under $env:appdata, and register it for you.
```PowerShell
PS C:\Test> Find-Package -ProviderName PSL

Cannot find source list file 'C:\Users\jianyunt\AppData\Roaming\PSL\PSL.json'.
Do you want to download package source list from 'http://go.microsoft.com/fwlink/?LinkID=821777&clcid=0x409'?
[Y] Yes  [N] No  [S] Suspend  [?] Help (default is "Y"): y

Name                           Version          Source           Summary
----                           -------          ------           -------
PowerShell                     6.0.0.9          https://githu... Powershell

```
Now you can run `install-package` to install the PowerShell package on your machine, show below.
```PowerShell

install-package -name PowerShell -provider psl

```

## How to Create PSL Source List File
Let's take look at the syntax first.

### Syntax of PSL Source List File

If you open the sample psl.json file, you see the following:
```PowerShell
PS C:\Test> Get-Content  C:\Users\jianyunt\AppData\Roaming\PSL\PSL.json
{
        "PowerShell": {
                "name": "PowerShell_6.0.0.9",
                "displayName": "PowerShell",
                "source": "https://github.com/PowerShell/PowerShell/releases/download/v6.0.0-alpha.9/PowerShell_6.0.0.9-alpha.9-win10-x64.msi",
                "hash": {
                        "algorithm": "sha512",
                        "hashCode": "/9D5iBELrs3GFq2l50NYHVq85Ctor3jhqV25EfgqWubLrM2b5XEs8mO3b6IIc3GFoJPNtH1N6Qd2V8YR2v5NqQ=="
                },
                "summary": "Powershell",
                "type": "msi",
                "version": "6.0.0.9"
        }
}
```


If you want to manage more packages, you can add more entries to the existing psl.json.

For example, if you wish to install docker, you can add something like following to psl.json:
```json
"docker": {
  "source": "https://get.docker.com/builds/Windows/x86_64/docker-1.12.0.zip",
  "hash": {
          "algorithm": "sha512",
          "hashCode": "A64DB25C5B0CF30BDD8CA253CCBB5F5D97B4F8E68895CEE7AD0E908CAEAC1BA3F1FC0DA5FADA8111D393C7148562362EA465934E61B7E823935F9687E80D8091"
  },
  "summary": "Docker zip package",
  "type": "zip",
  "version": "1.12.0",
  "destination": "%programfiles%"
},
```

Let's use the above example to explain the syntax of the PSL json file.

- `docker`: the package name
- `source`: the location where the package will be downloaded from. Mandatory.
- `summary`: description of the package. Optional.
- `type`: package type. Mandatory. See [supported package types](#Supported-Package-Types)
- `version`: the version of the package to be installed. Mandatory.
- `algorithm`: hash algorithm. Currently Sha256, Sha512, and MD5 are supported.
- `hashCode`: hash code.

  **Please note that, for security consideration**
  - make sure sources to be added to psl json file are trusted and validated.
  - make sure you add file hash code of a package to the `hash` entry in the json file. You can use [Get-FileHash][hash] to generate hash code.


- `destination`: file path where the package will be installed. It applies to zip packages.
- `installArguments`: the arguments will be passed into install-package. Currently it is supported for 'exe' type of packages. for example, "installArguments": "/S",
- `unInstallAdditionalArguments`: arguments will be passed into to unInstall-package. Currently it is supported for 'exe' type of packages.


### Test json Source List File
You can check the syntax correctness of json file through [Json Validator][Validator].
Once passing the syntax checking, you can test if the json file works properly with PSL by following the steps below. Let's use docker as an example and assuming 'c:\test\mypsl.json' is the json file you created.

```PowerShell

Register-PackageSource -Name testpsl -ProviderName PSL -Location c:\test\mypsl.json
Get-PackageSource

Find-Package -ProviderName PSL
Install-Package -ProviderName PSL -Name docker
```

### Security Consideration
- As mentioned above, sources in the json file should be validated, trusted or approved by your organization.
- Add hash code per package to the json file
- If PSL json file is on a remote file share, it's required to be catalog signed. Save the .cat file under the same folder with the .json file.
  you can use [New-FileCatalog cmdlet][filehash] to create the catalog file.
  The cmdlet is available on win10 and downlevel OSs with [WMF 5.1 preview][wmf5-1]. For example,

  ```PowerShell
  PS C:\Test> New-FileCatalog -Path C:\test\PSL.json -CatalogFilePath C:\test\psl.cat

  Mode                LastWriteTime         Length Name
  ----                -------------         ------ ----
  -a----        8/25/2016   1:34 AM            444 psl.cat

  ```

- Only networking file share is supported. URL is not supported, For example, following will generate errors.

  ```PowerShell
  Find-Package -ProviderName PSL  -Source http://onegetblob.blob.core.windows.net/psl/psl.json
  Register-PackageSource -Name testpsl -ProviderName PSL -Location  http://onegetblob.blob.core.windows.net/psl/psl.json
  ```

- In your development phase, you can skip the hash validation by specifying `-SkipHashValidation` during the installation. For example,

  ```PowerShell
  Install-Package docker -SkipHashValidation
  ```



## Try it

Let's walk through the following, assuming you are on Windows 10 anniversary edition.
```PowerShell

# Find and install the provider
Find-PackageProvider -name PSL
Install-PackageProvider -name PSL

# Check to make sure the PSL provider is imported
Get-PackageProvider

PS C:\Test> Find-Package -ProviderName PSL

Cannot find source list file 'C:\Users\jianyunt\AppData\Roaming\PSL\PSL.json'.
Do you want to download package source list from 'http://go.microsoft.com/fwlink/?LinkID=821777&clcid=0x409'?
[Y] Yes  [N] No  [S] Suspend  [?] Help (default is "Y"): y

Name                           Version          Source           Summary
----                           -------          ------           -------
PowerShell                     6.0.0.9          https://githu... Powershell

```
Now you can run Install-Package to install the latest PowerShell bits on your machine.
As an example, let's open PSL.json file and add docker to it, it will look like below:

```json
PS C:\Test> Get-Content  C:\Users\jianyunt\AppData\Roaming\PSL\PSL.json
{
    "PowerShell": {
        "displayName": "PowerShell_6.0.0.9",  
        "source": "https://github.com/PowerShell/PowerShell/releases/download/v6.0.0-alpha.9/PowerShell_6.0.0.9-alpha.9-win10-x64.msi",
        "hash": {
                "algorithm": "sha512",
                "hashCode": "FFD0F988110BAECDC616ADA5E743581D5ABCE42B68AF78E1A95DB911F82A5AE6CBACCD9BE5712CF263B76FA208737185A093CDB47D4DE9077657C611DAFE4DA9"
            },
          "summary": "Powershell",
          "type": "msi",
          "version": "6.0.0.9"
        },
    "docker": {
        "source": "https://get.docker.com/builds/Windows/x86_64/docker-1.12.0.zip",
        "hash": {
                "algorithm": "sha512",
                "hashCode": "A64DB25C5B0CF30BDD8CA253CCBB5F5D97B4F8E68895CEE7AD0E908CAEAC1BA3F1FC0DA5FADA8111D393C7148562362EA465934E61B7E823935F9687E80D8091"
        },
          "summary": "Docker zip package",
          "type": "zip",
          "version": "1.12.0",
          "destination": "%programfiles%"
        }
}
```
Save the file, and let's try the following.

```PowerShell
PS C:\Test> Find-Package -ProviderName PSL

Name                           Version          Source           Summary
----                           -------          ------           -------
docker                         1.12.0           https://get.d... Docker zip package
PowerShell                     6.0.0.9          https://githu... Powershell


PS C:\Test> Install-Package docker -AddToPath

The package(s) come(s) from a package source that is not marked as trusted.
Are you sure you want to install software from 'https://get.docker.com/builds/Windows/x86_64/docker-1.12.0.zip'?
[Y] Yes  [A] Yes to All  [N] No  [L] No to All  [S] Suspend  [?] Help (default is "N"): y

Name                           Version          Source           Summary
----                           -------          ------           -------
docker                         1.12.0           https://get.d... Docker zip package



PS C:\Test> Install-Package docker -AddToPath

# "-AddToPath": adding the package path to your $env:path,
# so that after the install, you can launch docker.exe on your current session.
# The path to be added is destination\packagename\packageversion.

# Here the destination is provided in the json file.
# If you do not specify AddToPath, then $env:path will not be updated.
# This switch is used for zip package only.

PS C:\Test> Get-Package docker

Name                           Version          Source                           ProviderName
----                           -------          ------                           ------------
docker                         1.12.0           C:\Program Files\docker\1.12.0   PSL


PS C:\Test> Uninstall-Package docker -RemoveFromPath

Name                           Version          Source           Summary
----                           -------          ------           -------
docker                         1.12.0           https://get.d... Docker zip package


# "RemoveFromPath" is opposite to "AddToPath". It removes the path from $env:Path.
# If you do not add "RemoveFromPath" switch,  then $env:path will not be updated.


```

## Design
Now we learned how to use PSL.
See [its design flow diagram][diagram] if you have interests. Also see the [known issues][known-issue].

## Developing and Contributing
We welcome and appreciate contributions from the community. Please follow the [PowerShell contribution guidelines][guidelines].

## Legal and Licensing

PSL is licensed under the [MIT license][license].

## Code of Conduct


This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: http://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: http://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
[hash]: https://technet.microsoft.com/en-us/library/dn520872.aspx
[license]: https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt
[guidelines]:https://github.com/PowerShell/PowerShell/blob/master/.github/CONTRIBUTING.md
[filehash]: https://technet.microsoft.com/en-us/library/mt740490(v=wps.650).aspx
[wmf5-1]:https://blogs.msdn.microsoft.com/powershell/2016/07/16/announcing-windows-management-framework-wmf-5-1-preview/
[nointernet]: https://github.com/OneGet/oneget/wiki/Q-and-A
[Validator]: https://jsonformatter.curiousconcept.com/
[diagram]: ./docs/high-level-diagram.md
[known-issue]: ./docs/known-issue.md
