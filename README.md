# ReleaseCollector
Visual Studio extension (VSIX) that publishes your .NET projects for various systems and collects the files in one folder (bin/v... with the highest version number or bin/latest if no version folder was found).

Website: https://uwap.org/projects/release-collector

## Main features
- Publishing projects for multiple systems at once without creating any publish profiles
- Combining releases that use multiple files (like .dll files) into ZIP files (only the case with self-contained builds for Windows using libraries, the rest usually only need a single file)
- Collecting the releases (either single-file executables or ZIP files) in a single folder, named after the version (if present) and system (example: [Wrapper releases on GitHub](https://github.com/pmpwsk/Wrapper/releases))

## Installation
You can find this extension on the Visual Studio marketplace as: [ReleaseCollector](https://marketplace.visualstudio.com/items?itemName=uwap-org.uwap-ReleaseCollector)
Alternatively, you can download the .vsix file from the [releases on GitHub](https://github.com/pmpwsk/ReleaseCollector/releases) and open it to install the extension.

## Usage
To execute the command, go to: <code>Tools > Publish and collect</code> (if there are multiple projects in your solution, you might need to click on the desired project to select it!)

There are some settings under <code>Tools > Options > ReleaseCollector</code>, those are documented in their description.

## Plans for the future
- Support for libraries (as NuGet packages)
- Support for MAUI projects
- Support for Visual Studio extensions
