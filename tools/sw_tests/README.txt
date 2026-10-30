Super weapon tests. They are .txt on purpose: tModLoader compiles every .cs file in the mod folder.

Patch S3.1 (assembly geometry, pure code):
   Program.cs.txt + Common/SuperWeapons/SuperWeaponAssembly.cs
   SuperWeaponAssembly.cs has no Terraria types, so it compiles on its own.

Copy Program.cs.txt to Program.cs in a scratch folder next to a console project (dotnet SDK), add a link to (or a copy of)
Common/SuperWeapons/SuperWeaponAssembly.cs, build and run. Every check prints "pass" and the last line says ALL PASSED.
Tip: a NuGet.Config with <packageSources><clear /></packageSources> lets `dotnet build` work offline.

Patch S4.1 (which weapon fires) adds checks to the same Program.cs.txt and needs one more file in the console project:
   Common/SuperWeapons/SuperWeaponTrigger.cs (no Terraria types either).

Later phases add their pure-code files to this folder.