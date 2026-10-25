Liquid flow tests. They are .txt on purpose: tModLoader compiles every .cs file in the mod folder.

1) Core only (no Terraria types):
   Program.cs.txt + Common/Networks/Flow/*.cs

2) Topology builder (the game-layer code, run against small stand-ins for the Terraria types):
   Stubs.cs.txt + TopologyTests.cs.txt + TickTests.cs.txt + Common/Liquids/LiquidStack.cs + Common/Liquids/LiquidTankRules.cs
   + Common/Liquids/PipeConnectionHelper.cs + Common/Networks/Flow/*.cs + Common/Networks/FlowNetwork.cs
   + Common/Networks/LiquidWorldBanks.cs + Common/Networks/FlowNetworkDiagnostics.cs

Each set is its own program (both have a Program class), so compile them separately: copy the .txt files to .cs in a scratch
folder, compile with any C# 10+ compiler (dotnet SDK or csc) and run. Every check should print as passed.
