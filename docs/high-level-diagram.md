
# High Level Flow Diagram

Let's take PowerShell package as an example.
Here is the flow diagram when you type "Find-Package PowerShell".

![find-package](find-package.png)


The flow diagram when you type "Install-Package PowerShell".

![install-package](install-package.png)

## Where Packages installed to?
| Package Type | Install Location                  
|:-------------|:----------------------------
| msi          |Default path, i.e., a particular .msi package decides. <br> Whether admin privilege is required depends on a particular package.   
| exe          |Default path, i.e., a particular .exe package decides.<br> Whether admin privilege is required depends on a particular package.
| PowerShell modules | Well-known PowerShell module install location             
| nupkg        | Default location: 	<br>$env:programfiles\NuGet\Packages\ <br>	$env:userprofile\NuGet\Packages\  <br> User can also specify the destination path in the json file if they want to install packages to different folder.
| Zip          | zip packages are installed under Destination\PackageName\PackageVersion. "Destination" is defined in json file. It's required.
