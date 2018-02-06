/////////////////////////////////////////////////////////////////////
// ADDINS
/////////////////////////////////////////////////////////////////////

#addin "nuget:?package=NuGet.Core&version=2.14.0"
#addin "nuget:?package=Polly&version=5.0.6"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "nuget:?package=NuGet.CommandLine&version=4.4.1"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using Polly;
using NuGet;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "VitalElement/AvalonStudio.Toolchains.ClangToolchain";
var MasterBranch = "master";
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals(MainRepo, BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals(MasterBranch, BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "0.0.1";

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER") + "-alpha";
    }
}

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var zipRootDir = artifactsDir.Combine("zip");
var nugetRoot = artifactsDir.Combine("nuget");
var fileZipSuffix = ".zip";

private bool MoveFolderContents(string SourcePath, string DestinationPath)
{
   SourcePath = SourcePath.EndsWith(@"\") ? SourcePath : SourcePath + @"\";
   DestinationPath = DestinationPath.EndsWith(@"\") ? DestinationPath : DestinationPath + @"\";
 
   try
   {
      if (System.IO.Directory.Exists(SourcePath))
      {
         if (System.IO.Directory.Exists(DestinationPath) == false)
         {
            System.IO.Directory.CreateDirectory(DestinationPath);
         }
 
         foreach (string files in System.IO.Directory.GetFiles(SourcePath))
         {
            FileInfo fileInfo = new FileInfo(files);
            fileInfo.MoveTo(string.Format(@"{0}\{1}", DestinationPath, fileInfo.Name));
         }
 
         foreach (string drs in System.IO.Directory.GetDirectories(SourcePath))
         {
            System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(drs);
            if (MoveFolderContents(drs, DestinationPath + directoryInfo.Name) == false)
            {
               return false;
            }
         }
      }
      return true;
   }
   catch (Exception ex)
   {
      return false;
   }
}

///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////
var nuSpec = new NuGetPackSettings()
    {
        Id = "AvalonStudio.Toolchains.STM32.Gcc",
        Version = version,
        Authors = new [] { "VitalElement" },
        Owners = new [] { "Dan Walmsley" },
        LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
        ProjectUrl = new Uri("https://github.com/VitalElement/"),
        RequireLicenseAcceptance = false,
        Symbols = false,
        NoPackageAnalysis = true,
        Description = "STM32 GCC Based Toolchain for AvalonStudio",
        Copyright = "Copyright 2018",
        Tags = new [] { "gccdescription", "GCC", "AvalonStudio", "Toolchain", "STM32" },        
        Files = new []
        {
            new NuSpecContent { Source = "**", Target = "/" },
        },
        BasePath = Directory("gccdescriptions/"),
        OutputDirectory = nugetRoot
    };

///////////////////////////////////////////////////////////////////////////////
// TASKS
/////////////////////////////////////////////////////////////////////////////// 

Task("Clean")
.Does(()=>{    
    CleanDirectory(nugetRoot);
});

Task("Generate-NuGetPackages")
.Does(()=>{
    NuGetPack(nuSpec);    
});

Task("Publish-AppVeyorNuget")
    .IsDependentOn("Generate-NuGetPackages")        
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)    
    .Does(() =>
{
    var apiKey = EnvironmentVariable("APPVEYOR_NUGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("APPVEYOR_ACCOUNT_FEED_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }
    
    var nuspec = nuSpec;
    var settings  = nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));

    NuGetPush(settings, new NuGetPushSettings
    {
        Source = apiUrl,
        ApiKey = apiKey,
        Timeout = TimeSpan.FromMinutes(45)
    });    
});

Task("Default")    
    .IsDependentOn("Clean")    
    .IsDependentOn("Generate-NuGetPackages");
    //.IsDependentOn("Publish-AppVeyorNuget");
RunTarget(target);
