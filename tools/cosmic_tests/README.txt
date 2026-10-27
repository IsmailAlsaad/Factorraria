Cosmic radiation tests. They are .txt on purpose: tModLoader compiles every .cs file in the mod folder.

Patch C1 (heatmap, pure code):
   Program.cs.txt + Common/Cosmic/CosmicHeatmap.cs
   Program.cs.txt already contains a tiny stand-in for the CosmicColor enum, so do NOT also compile CosmicColor.cs (it needs Terraria types).

Copy the .txt file to .cs in a scratch folder next to a console project (any C# 10+ compiler, dotnet SDK or csc), compile and run.
Every check should print "pass", and the last line should be ALL PASSED.
Later phases add their pure-code files (ray tracer, cluster math) to this folder.