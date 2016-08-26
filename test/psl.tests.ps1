#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#  Licensed under the Apache License, Version 2.0 (the "License");
#  you may not use this file except in compliance with the License.
#  You may obtain a copy of the License at
#  http://www.apache.org/licenses/LICENSE-2.0
#
#  Unless required by applicable law or agreed to in writing, software
#  distributed under the License is distributed on an "AS IS" BASIS,
#  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
#  See the License for the specific language governing permissions and
#  limitations under the License.
#
# ------------------ PackageManagement Test  -----------------------------------
ipmo "$PSScriptRoot\utility.psm1"
$InternalGallery = "https://dtlgalleryint.cloudapp.net/api/v2/"
$InternalSource = 'OneGetTestSource'


# set to this feed to bootstrap the testing version
$env:BootstrapProviderTestfeedUrl = "https://raw.githubusercontent.com/OneGet/ProviderRegistry/testing/providers.masterList.feed.swidtag"

$psl = "psl"
$location =".\testpsl.json"
$source ="psl"


import-packagemanagement

$provider = Get-PackageProvider

if($provider.Name -notcontains $psl)
{
    $a= Find-PackageProvider -Name $psl -verbose -ForceBootstrap

    if($a.Name -eq $psl)
    {
        Install-PackageProvider $psl -verbose -force    
    }
}

    
$sources = Get-PackageSource -ProviderName $psl
foreach ($s in $sources)
{
    Unregister-PackageSource -name $s.Name 
}

Register-PackageSource -name $source -ProviderName $psl -Location $location

Register-PackageSource -Name $InternalSource -Location $InternalGallery -ProviderName 'PowerShellGet' -Trusted -ForceBootstrap -ErrorAction SilentlyContinue




# ------------------------------------------------------------------------------
# Actual Tests:

Describe "psl testing" -Tags @('BVT', 'DRT') {
    AfterAll {
        #reset the environment variable
       $env:BootstrapProviderTestfeedUrl=""  
    }

    It "find-package" {

        $a=find-package -ProviderName $psl
        $a.Name -contains "node.js" | Should Be $true
        $a.Name -contains "Notepad++"  | Should Be $true


        $b= find-package -source $source
        $b.Name -contains "node.js"  | Should Be $true
        $b.Name -contains "Notepad++"  | Should Be $true

        $c= find-package -name node.js -ProviderName $psl
        $c.Name -contains "node.js"  | Should Be $true
        
    }

    It "find-package with version" {

        $a= find-package -name node.js -AllVersions -ProviderName $psl -source $source

        $a.Name -contains "node.js" | Should Be $true

        $a | ?{ $_.Version -eq "6.2.0" } | should not BeNullOrEmpty
        $a | ?{ $_.Version -eq "6.1.0" } | should not BeNullOrEmpty        
    }

    It "install-package with msi" {  
            
        $package = "node.js"

        $exist = get-package $package -ProviderName $psl -ErrorAction SilentlyContinue
        if($exist)  {
            Uninstall-package $package  -ProviderName $psl
        }


        $a= install-package -name $package  -ProviderName $psl -source $source -force -SkipHashValidation
        $a.Name -contains $package | Should Be $true
           
        $b = get-package $package -verbose -provider $psl
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package -verbose  -ProviderName $psl
        $c.Name -contains $package | Should Be $true 
   }
    
   It "install-package with zip" {  
    
        $package = "Sysinternals"

        $a= install-package -name $package  -ProviderName $psl -source $source -SkipHashValidation -force
        $a.Name -contains $package | Should Be $true
           
        $b = get-package $package -provider $psl
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package  -provider $psl
        $c.Name -contains $package | Should Be $true 
    }       
       

    It "install-package with exe" {  
    
        $package = "notepad++"

        $exist = get-package $package  -ProviderName $psl  -ErrorAction SilentlyContinue
        if($exist)  {
            Uninstall-package $package  -ProviderName $ps
        }

        $a= install-package -name $package  -ProviderName $psl -source $source -SkipHashValidation -force
        $a.Name -contains $package | Should Be $true
           
        $b = get-package $package -provider $psl
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package  -ProviderName $psl
        $c.Name -contains $package | Should Be $true       
    }       
       

    It "install-package with NuGet" {  
    
        $package = "jquery"

        # 3.0.0 of jQuery contains destination definition in psl.json
        $a= install-package -name $package  -ProviderName $psl -source $source  -force -RequiredVersion 3.0.0
        $a.Name -contains $package | Should Be $true
           
        $b = get-package $package -provider $psl  -RequiredVersion 3.0.0
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package -RequiredVersion 3.0.0
        $c.Name -contains $package | Should Be $true

        $d = get-package $package  -provider $psl -ErrorAction SilentlyContinue -WarningAction SilentlyContinue -RequiredVersion 3.0.0
        $d | ?{ $_.Version.ToString() -eq "3.0.0" } | should BeNullOrEmpty    
        
         # 2.2.4 of jQuery does not contain destination definition in psl.json
        $a= install-package -name $package  -ProviderName $psl -source $source  -force -RequiredVersion 2.2.4
        $a.Version -contains "2.2.4" | Should Be $true
           
        $b = get-package $package -provider $psl  -RequiredVersion 2.2.4
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package  -ProviderName $psl  -RequiredVersion 2.2.4
        $c.Name -contains $package | Should Be $true

        $d = get-package $package  -RequiredVersion 2.2.4  -provider $psl -ErrorAction SilentlyContinue -WarningAction SilentlyContinue           
        $d | ?{ $_.Version.ToString() -eq "2.2.4" } | should BeNullOrEmpty   
    }       
       
    It "install-package with PowerShellGet" {  
    
        $package = "GistProvider"

        $a= install-package -name $package  -ProviderName $psl -source $source  -force -RequiredVersion 1.6
        $a.Name -contains $package | Should Be $true
           
        $b = get-package $package -provider $psl -RequiredVersion 1.6
        $b.Name -contains $package | Should Be $true

        $c= Uninstall-package $package  -ProviderName $psl -RequiredVersion 1.6
        $c.Name -contains $package | Should Be $true

        $d = get-package $package  -provider $psl -RequiredVersion 1.6 -ErrorAction SilentlyContinue -WarningAction SilentlyContinue
        $d | ?{ $_.Version.ToString() -eq "1.6" } | should BeNullOrEmpty               
    }  

   It "install-package by passing in -source location - expected warning" {  
    
        $package = "Sysinternals"

        $a= install-package -name $package  -ProviderName $psl -source $location -SkipHashValidation -force -WarningVariable wv
        $a.Name -contains $package | Should Be $true
        $wv | Should Not BeNullOrEmpty
         
    } 
 }