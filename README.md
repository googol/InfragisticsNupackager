This is a quick and dirty tool for packaging Infragistics dlls into nice Nuget packages.

It was made to demo the use of nuget packages for dependency management, and as such is not very nice at the moment. For example, it only supports 2014 volume 2 dlls, and ignores the build number so service releases will get mixed up.

Usage is simple, make sure Nuget.exe is callable by the program (either on path or in the same directory), put the program in the same directory as the dlls and run.