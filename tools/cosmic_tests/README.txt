Cosmic radiation tests. They are .txt on purpose: tModLoader compiles every .cs file in the mod folder.

Patch C1 (heatmap, pure code):
   Program.cs.txt + Common/Cosmic/CosmicHeatmap.cs
   Program.cs.txt already contains a tiny stand-in for the CosmicColor enum, so do NOT also compile CosmicColor.cs (it needs Terraria types).

Copy the .txt file to .cs in a scratch folder next to a console project (any C# 10+ compiler, dotnet SDK or csc), compile and run.
Every check should print "pass", and the last line should be ALL PASSED.
Patch C2 (ray tracer, receiver index, sampling maths, event heap, all pure code):
   Program_C2.cs.txt + Common/Cosmic/CosmicRayTracer.cs, CosmicReceiverIndex.cs, CosmicSampling.cs, CosmicEventHeap.cs
   Program_C2.cs.txt has its own CosmicColor stand-in too. It is a separate program from Program.cs.txt (both have a Main), so compile one at a time.
   It includes a brute-force check: every ray that can hit a receiver must start inside a sampling strip, and the expected number of hits must match simulating every particle in the band.

Patch C3 added CosmicRayTracer.Rotate (the child ray directions); its checks are at the end of Program_C2.cs.txt, same compile line as C2.

Later phases add their pure-code files (cluster math) to this folder.