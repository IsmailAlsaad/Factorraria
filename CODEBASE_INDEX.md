# CODEBASE INDEX - Factorraria
generated 2026-10-17 | 209 .cs files | regenerate: `python index_codebase.py` (project root)

Legend: `+` public `#` protected `~` internal | s=static ab=abstract v=virtual ov=override c=const | `Name(Types)` method, `Name:Type` field/prop | `uses:` other project types used in file | `ns!=X` namespace differs from folder path (matters for `using`) | no line numbers: grep the name | private members and SetDefaults/SetStaticDefaults omitted

## Most-referenced types (files referencing them)
RecipeBook(22) LiquidTypeRegistry(22) BaseMachine(22) CustomRecipe(14) MachineUIStateBase(13) MachineUIElementEntry(12) LiquidStack(11) FactorrariaNet(11) RecipeOutputGroup(10) VirtualItemSystem(9) LiquidNetworkSystem(9) RecipeKnowledgeSystem(8) RecipeCatalog(8) FurnaceOffsetConfig(8) FactorrariaPacket(8) Cart(8) TileEntityHelper(7) RecipeOutputKey(7) RecipeBookLayout(7) Entry(7)

## Inheritance
Project bases -> derived:
- BaseMachine: DecayChamberTileEntity, ElectricConsumerMachine, ElectricProducerMachine, FurnaceTileEntity, HellforgeTileEntity, IceMachineTileEntity, LiquidDistillatorTileEntity, MagicMaterializerTileEntity
- BucketBase: FuelBucket, OilBucket
- ElectricConsumerMachine: AutohammerTileEntity, ExtractinatorTileEntity, MechanicalAssemblerTileEntity, MotorTileEntityBase, RoboticArmTileEntity, SolidifierTileEntity
- ElectricProducerMachine: GelBurnerTileEntity
- IElectricConsumer: ElectricConsumerMachine
- IElectricProducer: ElectricProducerMachine
- IMachineDynamicDraw: RoboticArmTileEntity
- IStatueAwakener: MagicMaterializerTileEntity
- ITilePlacedConsumer: PlacementLogConsumer
- IZoomScalable: RecipeBrowserPanel
- MachineUIStateBase: AutohammerUIState, DecayChamberUIState, ExtractinatorUIState, FurnaceUIState, GelBurnerUIState, HellforgeUIState, IceMachineUIState, LiquidDistillatorUIState, MechanicalAssemblerUIState, RoboticArmUIState, SolidifierUIState
- MotorTileBase: MotorMK1Tile
- MotorTileEntityBase: MotorMK1TileEntity
- PipeTileBase: PipeMK1Tile
tModLoader/other bases -> project types:
- ModSystem (39): AwakenedSpawnSystem, AwakenedStatueDrawSystem, AwakenedStatueRegistry, AwakenedStatueSystem, CartChestUISystem, CartMountBlocker, CartSystem, ChestConveyorModeUI, ChestConveyorSystem, ChestLootSystem, ConveyorPhysicsSystem, CustomWireSystem, JoinSyncSystem, LiquidNetworkDebugSystem, LiquidNetworkSystem, LiquidTypeRegistrationSystem, MachineDynamicDrawSystem, MachineOverhangSystem, MachineRegisterationSystem, MachineUISystem, MaterializerRightClickGuard, MechSuitNetSystem, MultiplayerBootProbeSystem, NetCoreSystem, NetRateLimiterSystem, PostWorldGenEdits, PowerGridDebugSystem, PowerGridSystem, RecipeBookSystem, RecipeKnowledgeSystem, RecipeSystem, RoboticArmAreaEditorSystem, RoboticArmDropCaptureSystem, RoboticArmGhostSystem, SiliconOreWorldGen, TilePlacedSystem, TimedCraftingSystem, VirtualItemSystem, WorldLiquidSources
- ModItem (32): BucketBase, ByriteAlloyItem, ByriteAlloySlabItem, CartItem, ClockwisePriorityConveyor, ClosedScrollItem, CokeItem, CopperWireItem, CounterClockwisePriorityConveyor, CrimtaneSheetItem, CsharpLogoItem, DemoniteSheetItem, GoldPlateItem, GoldWireItem, IronCutterItem, IsmailsCaneItem, LeadCutterItem, MechSuitItem, MechanicalArmItem, MechanicalAssemblerItem, MeteoritePanelItem, MotorMK1Item, PipeMK1Item, PureSiliconItem, RecipeParchmentItem, SiliconOreItem, SimpleCircuitItem, SteelBarItem, SteelScrewItem, TerrariaLogoItem, TinWireItem, TmodloaderLogoItem
- UIElement (12): ElectricityUIIcon, FireUIElement, LiquidTankUIElement, RecipeBookButton, RecipeBookDetail, RecipeBookDragHandle, RecipeBookEntry, RecipeBookHeaderRow, RecipeBookPanel, RecipeSearchBar, RecipeSelectHammerIcon, UIItemSlotWrapper
- ModTile (8): ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyorTile, MechanicalAssemblerTile, MotorTileBase, PipeTileBase, PureSiliconTile, RoboticArmTile, SiliconOreTile
- ModPlayer (7): CartEquipPlayer, CartInteractionPlayer, ExtractinatorBlockPlayer, FactorrariaPlayerSync, MechSuitPlayer, RecipeBookPlayer, TimedCraftingPlayer
- GlobalItem (6): CartModuleItemGlobal, IceBladeItem, RoboticArmDropCapture, VItemGlobalItem, VanillaCartItemGlobal, VanillaConveyorGlobalItem
- GlobalTile (6): AwakenedStatueGlobalTile, ChestConveyorGlobalTile, LiquidTopologyGlobalTile, MachineVisualOverride, MaterializerWireGuard, TilePlacedGlobalTile
- ModCommand (4): NetFloodCommand, NetJoinCommand, NetPingCommand, RecipeDebugCommand
- UIState (4): CartChestUIState, MachineUIStateBase, RecipeBookButtonState, RecipeBookState
- ModProjectile (3): IceBladeSwingProjectile, MechSuitArmProjectile, MechanicalArmProjectile
- byte (3): CustomWireType, FactorrariaPacket, PacketDirection
- GlobalNPC (2): AwakenedSpawnGlobalNPC, BossLootGlobal
- ModConfig (2): FurnaceOffsetConfig, NetworkConfig
- ModLiquid (2): FuelLiquid, OilLiquid
- ModLiquidFall (2): FuelLiquidFall, OilLiquidFall
- UIPanel (2): RecipeBrowserPanel, RecipeElement
- UIScrollbar (2): BookScrollbar, ZoomScrollbar
- GlobalLiquid (1): LiquidInteractionEdits
- GlobalProjectile (1): CartTrackHookBlocker
- GlobalWall (1): GlobalWallOverride
- IEquatable (1): RecipeOutputKey
- IPixelatedPrimitiveRenderer (1): IceBladeSwingProjectile
- Mod (1): Factorraria
- ModTileEntity (1): BaseMachine
- Particle (1): PuffExplosion

## Files

### (root)/
**Factorraria.cs** uses: FactorrariaNet, Handle
  class Factorraria : Mod
    +HandlePacket(BinaryReader,int)[ov]

### Common/Carts/
**Cart.cs** uses: CartChain, CartSkinTable, CartSkins, FuelLiquid, FuelModule, GelBurnerRecipeRegistry, LiquidStack, LiquidTypeRegistry, SkinAnimation, TrackData, TrackEnd, TrackSample
  enum CartModule
    {Empty, Chest, Terrarium, Motor}
  static class CartPhysics
    +WheelHalfBase:float[c] +HitboxWidth:int[c] +HitboxHeight:int[c] +MaxSpeed:float[c] +Drag:float[c] +BoostAccel:float[c] +ShoveSpeed:float[c] +MaxStep:float[c] +Gravity:float[c] +MaxFallSpeed:float[c] +GroundFriction:float[c] +RerailDelay:int[c] +BumpCooldown:int[c] +CollisionRestitution:float[c] +CollisionMinSpeed:float[c] +AnimStopSpeed:float[c] +AnimPixelsPerFrame:float[c] +AnimMaxFramesPerTick:float[c] +PickupPadding:float[c] +SpillGrabCooldown:int[c] +TerrariumDrainPerTick:int[c] +MaxCarriages:int[c] +ChainSpacing:float[c] +ChainSampleStep:float[c] +ChainCloseGapSpeed:float[c] +CouplingMaxRelativeSpeed:float[c] +MotorSpeedCap:float[c] +MotorAccel:float[c]
  class Cart
    +Skin:string +SkinType:int +Module:CartModule +ModuleItem:int +IsChestItem(Item)[s] +IsTerrariumItem(Item)[s] +ChestSlots:int[c] +TryAddToChest(Item,bool) +ChestItems:Item +SlotFilter:int +IsSlotFiltered(int) +HasFilterFor(int) +SlotAccepts(int,Item) +FilterKeepCount(int,Item) +SetFilterFromSlots() +NormalizeTank() +OverflowItems:List<Item> +Tank:LiquidStack +FuelSlotCap:int[c] +Fuel:Item +IsBurning:bool +FuelBurnFraction:float +AcceptsFuel(int)[s] +AcceptsFuel(Item)[s] +TryAddFuel(Item) +RestoreBurn(int,int) +IsLavaSkin(int)[s] +IsMechSkin(int)[s] +FuelLiquidOf(int)[s] +IsMotorSkin(int)[s] +UsesLava:bool +FuelLiquid:int +UsesLiquid:bool +IsFuelLiquid(int) +AlwaysRuns:bool +HasPanel:bool +SpeedMultiplier:float +AccelMultiplier:float +SpeedLimit:float +DriveSpeedCap:float +DriveAccel:float +LiquidPerTick:float +LiquidCapacity:float +SetModule(CartModule,int) +ModuleItemType(CartModule)[s] +CollectPickupItems() +Position:Vector2 +Speed:float +Velocity:Vector2 +OnTrack:bool +Rotation:float +Active:bool +Chain:CartChain +IsCarriage:bool +Body:Cart +CollisionMass:int +ChainSlack:float +ReplayScanIndex:int +LoadChainId:int +LoadChainSlot:int +Facing:int +AnimFrame:int +Bumped:event Action<Cart>[s] +Collided:event Action<Cart, Cart>[s] +FrameChanged:event Action<Cart, int, int>[s] +Driving:event Action<Cart>[s] +AlongSpeed:float +TryCollide(Cart,Cart)[s] +Hitbox:Rectangle +Update() +TickCarriage() +ReplayBump() +Shove(float) +TryRailPose(float,float,float,out float,out float)[s] +Save() +Load(TagCompound)[s]
**CartCargo.cs** uses: Cart, CartChestUIState, CartGeometry, CartModule, CartPhysics, LiquidStack, LiquidTypeRegistry, VirtualItem, VirtualItemSystem, WorldLiquidSources
  static class CartCargo
    +WindowColumns:int[c] +WindowRows:int[c] +Update(Cart)[s] +OnBump(Cart)[s] +GetWindow(Cart,out int,out int)[s] +PourTank(Cart)[s]
**CartChain.cs** uses: Cart, CartModule, CartPhysics, Direction
  class CartChain
    +Coupled:event Action<Cart, Cart>[s] +Motor:Cart +Carriages:List<Cart> +SaveId:int +Count:int +Attach(Cart,bool) +Remove(Cart) +Dissolve() +Record(Vector2,float,bool) +MarkBump() +UpdateCarriages() +AutoAttach(List<Cart>)[s] +RebuildFromSave(List<Cart>)[s]
    struct TrailSample
      +Position:Vector2 +Rotation:float +Direction:int +Bump:bool +Air:bool
**CartChestUI.cs** uses: Cart, CartModule, CartPhysics, FireUIElement, LiquidTankUIElement, MachineUISystem, RecipeBookSystem, UIItemSlotWrapper
  class CartChestUIState : UIState
    +Columns:int[c] +Rows:int[c] +Cart:Cart +Panel:UIElement +Zoom:float +PanelW:float +PanelH:float +GapAboveCart:float +OnInitialize()[ov] +SetZoomScale(float) +Update(GameTime)[ov]
    sealed class Entry
      +Element:UIElement +BasePosition:Vector2 +BaseSize:Vector2 +Button:UITextPanel<string> +Text:string
  class CartChestUISystem : ModSystem
    +IsOpen:bool[s] +OpenCart:Cart[s] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +Toggle(Cart)[s] +Close(bool)[s] +NotifyCartRemoved(Cart)[s] +TryShiftInsert(Item)[s] +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**CartEquipPlayer.cs**
  class CartEquipPlayer : ModPlayer
    +PostUpdate()[ov]
**CartGeometry.cs** uses: Cart, CartPhysics
  static class CartGeometry
    +Right(Cart)[s] +Up(Cart)[s] +BoxCenter(Cart)[s] +GetCorners(Cart,Vector2)[s] +Contains(Cart,Vector2,float)[s]
**CartInteractionPlayer.cs** uses: CartChestUISystem, CartSkinTable, CartSystem
  class CartInteractionPlayer : ModPlayer
    +CanUseItem(Item)[ov] +ShiftClickSlot(Item,int,int)[ov]
**CartMountBlocker.cs** uses: Cart
  class CartMountBlocker : ModSystem
    +Load()[ov] +Unload()[ov] +IsTrackTile(int,int)[s] +Log(string)[s] +IsTrackRightClick(Player)[s]
  class CartTrackHookBlocker : GlobalProjectile
    +InstancePerEntity:bool[ov] +AppliesToEntity(Projectile,bool)[ov] +OnSpawn(Projectile,IEntitySource)[ov] +GrappleCanLatchOnTo(Projectile,Player,int,int)[ov]
**CartSkinTable.cs** uses: Cart, CartItem, CartSkins, Factorraria
  static class CartSkinTable
    +FallbackIconScale:float[c] +RailSink:float[c] +GetHeldItem(Player)[s] +IsSkinItem(Item)[s] +IsPlaceable(Item)[s] +SkinTypeFor(Item)[s] +GetMountData(int)[s] +GetChainName(int)[s] +GetAnimation(int)[s] +GetPlacedSprite(int,int)[s]
    struct PlacedSprite
      +Texture:Texture2D +Source:Rectangle +Origin:Vector2 +Scale:float +Extra:Texture2D
    struct SkinAnimation
      +TotalFrames:int +StandFrame:int +RunStart:int +RunCount:int +AirFrame:int
**CartSkins.cs**
  static class CartSkins
    +VanillaPrefix:string[c] +DefaultKey:string[c] +KeyOf(int)[s] +TryTypeOf(string,out int)[s]
**CartSystem.cs** uses: Cart, CartCargo, CartChain, CartChestUISystem, CartGeometry, CartModule, CartPhysics, CartSkinTable, CartSkins, FurnaceOffsetConfig, LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry, PlacedSprite, TrackData, TrackSample
  class CartSystem : ModSystem
    +Carts:List<Cart>[s] +Unload()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +CheckPlacement(Player,int,int,out Vector2,out bool)[s] +SpawnCart(Vector2,int,CartModule)[s] +FindCartAtCursor(Player)[s] +PickUp(Player,Cart)[s]
**TrackData.cs**
  enum TrackEnd
    {None, Stop, Bounce, Ramp, Open}
  struct TrackSample
    +HasSurface:bool +SurfaceY:float +End:TrackEnd +BoostDirection:int +IsEmpty:bool
  static class TrackData
    +MaxSnap:float[c] +FailureReason:string[s] +EnsureLoaded()[s] +Reset()[s] +TryGetPressurePlate(float,float,out Point)[s] +TrySample(float,float,out TrackSample)[s]

### Common/ChestLoot/
**ChestLootDefinitions.cs** uses: ChestConditions, ChestLootRegistry, ClosedScrollItem, CokeItem, LootItem, SteelBarItem, VanillaChest
  static class ChestLootDefinitions
    +Register()[s]
**ChestLootRegistry.cs**
  enum VanillaChest
    {Wood, Gold, GoldLocked, Shadow, ShadowLocked, Ivy, Frozen, LivingWood, Skyware, Lihzahrd, Water}
  struct LootItem
    +Type:int +Min:int +Max:int +Weight:float
  static class ChestConditions
    +Surface:Func<Chest, bool>[s] +Underground:Func<Chest, bool>[s] +Cavern:Func<Chest, bool>[s] +Underworld:Func<Chest, bool>[s] +LeftHalf:Func<Chest, bool>[s] +RightHalf:Func<Chest, bool>[s]
  class ChestLootRule
    +Where(Func<Chest, bool>) +Clear() +Remove(int) +ReplaceItem(int,int) +Always(int,int,int) +Chance(float,int,int,int) +OneOf(LootItem) +OneOf(float,LootItem) +Custom(Action<Chest, UnifiedRandom>) ~Matches(Chest,int,int) ~Apply(Chest,UnifiedRandom)
  static class ChestLootRegistry
    +LogFoundChests:bool[s] +Rules:IReadOnlyList<ChestLootRule>[s] +ForAll()[s] +For(VanillaChest)[s] +For(VanillaChest)[s] +For(int,int)[s] +ForTile(int)[s] +ForMatching(Func<int, int, bool>)[s] +Clear()[s] +ApplyAll(UnifiedRandom,Action<string>)[s] +TryAdd(Chest,int,int)[s]
**ChestLootSystem.cs** uses: ChestLootDefinitions, ChestLootRegistry
  class ChestLootSystem : ModSystem
    +PostSetupContent()[ov] +PostWorldGen()[ov] +Unload()[ov]

### Common/Crafting/
**TimedCraftingPlayer.cs** uses: TimedCraftingSystem
  class TimedCraftingPlayer : ModPlayer
    +Active:Recipe +Progress:int +Duration:int +Fraction:float +Repeat:bool +Toggle(Recipe,bool) +NotifyFocusClick(Recipe,bool) +Cancel() +PostUpdate()[ov]
**TimedCraftingSystem.cs** uses: OutputSlotAnimation, TimedCraftingPlayer
  class TimedCraftingSystem : ModSystem
    +baseTicks:float +ticksPerUnit:float +countBonus:float +minTicks:int +maxTicks:int +grayMultiplier:float +rarityMultipliers:float +craftingListContext:int +deliveringCraft:bool +DrawSlotOrig(SpriteBatch,ref Item,int,Vector2,Color) +DrawSlotHook(DrawSlotOrig,SpriteBatch,ref Item,int,Vector2,Color) +Load()[ov] +RarityMultiplier(int) +GetDurationTicks(Recipe)

### Common/Globals/
**BossLootGlobal.cs** ns!=Factorraria.Common.NPCs
  class BossLootGlobal : GlobalNPC
    +ModifyNPCLoot(NPC,NPCLoot)[ov]
  static class NPCLootExtensions
    +RemoveLoot(NPCLoot,int)[s]
**ChestConveyorGlobalTile.cs** uses: ChestConveyorSystem, VirtualItemSystem
  class ChestConveyorGlobalTile : GlobalTile
    +PlaceInWorld(int,int,int,Item)[ov]
**GlobalWallOverride.cs**
  class GlobalWallOverride : GlobalWall
    +Drop(int,int,int,ref int)[ov]
**LiquidTopologyGlobalTile.cs** uses: LiquidNetworkSystem, PipeTierRegistry
  class LiquidTopologyGlobalTile : GlobalTile
    +PlaceInWorld(int,int,int,Item)[ov] +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov]

### Common/Knowledge/
**RecipeCatalog.cs** uses: RecipeBook, RecipeKey, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeOutputKey
  static class RecipeCatalog
    +Register(string,RecipeBook)[s] +Clear()[s] +KeyFor(string,RecipeOutputGroup)[s] +KeyFor(string,RecipeOutputKey)[s] +NameOf(RecipeBook)[s] +Entries()[s] +TryResolve(string,out string,out RecipeOutputGroup)[s] +MachineOf(string)[s] +TryRollLocked(UnifiedRandom,out string)[s]
    class Entry
      +Machine:string +Group:RecipeOutputGroup +Key:string
**RecipeDebugCommand.cs** uses: ClosedScrollItem, Entry, RecipeBookButtonState, RecipeCatalog, RecipeKnowledgeSystem, RecipeParchmentItem, RecipeState, RecipeVisibility, ScrollPool, ScrollPools
  class RecipeDebugCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**RecipeKey.cs** uses: RecipeOutputGroup, RecipeOutputKey
  static class RecipeKey
    +For(RecipeOutputGroup)[s] +For(RecipeOutputKey)[s]
**RecipeKnowledgeSystem.cs**
  class RecipeKnowledgeSystem : ModSystem
    +IsUnlocked(string)[s] +Version:int[s] +IsCrafted(string)[s] +MarkCrafted(string)[s] +CraftedKeys:IReadOnlyCollection<string>[s] +ResetCrafted()[s] +Unlock(string)[s] +UnlockedKeys:IReadOnlyCollection<string>[s] +ResetUnlocked()[s] +SeenVersion:int[s] +IsSeen(int)[s] +SeenItems:IReadOnlyCollection<int>[s] +MarkSeen(int)[s] +ResetSeen()[s] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +OnWorldUnload()[ov] +Unload()[ov]
**ScrollPoolDefinitions.cs** uses: LiquidTypeRegistry, ScrollPools, SteelBarItem
  static class ScrollPoolDefinitions
    +Register()[s]
**ScrollPools.cs** uses: RecipeCatalog, RecipeKnowledgeSystem, RecipeOutputKey
  class ScrollPool
    +Name:string +Hint:string +Entries:IReadOnlyList<Entry> +WithHint(string) +Add(string,int,float) +AddLiquid(string,int,float)
    struct Entry
      +Machine:string +Output:RecipeOutputKey +Weight:float +CatalogKey:string
  static class ScrollPools
    +All:IReadOnlyList<ScrollPool>[s] +Define(string,string)[s] +TryGet(string,out ScrollPool)[s] +HintFor(string)[s] +Clear()[s] +Validate(Action<string>)[s] +RemainingCount(ScrollPool)[s] +TryRoll(string,UnifiedRandom,out string)[s]

### Common/Liquids/
**LiquidInteractionEdits.cs** ns!=Factorraria.Common.Globals
  class LiquidInteractionEdits : GlobalLiquid
    +LiquidMerge(int,int,int,int)[ov]
**LiquidNetworkRegistries.cs** uses: FuelLiquid, MachineBiome, OilLiquid
  class LiquidTypeDefinition
    +Name:string +RenderColor:Color +VanillaTileLiquidId:byte? +IsWaterVariant:bool +IconPath:string
  static class LiquidTypeRegistry
    +definitions:List<LiquidTypeDefinition>[s] +Register(LiquidTypeDefinition)[s] +ToTileLiquidId(int)[s] +FromTileLiquidId(byte)[s] +Get(int)[s] +Water:int[s] +Lava:int[s] +Honey:int[s] +Shimmer:int[s] +Oil:int[s] +Fuel:int[s] +AnyWater:int[c] +SnowWater:int[s] +DesertWater:int[s] +JungleWater:int[s] +OceanWater:int[s] +UndergroundWater:int[s] +CavernWater:int[s] +CorruptionWater:int[s] +CrimsonWater:int[s] +HallowWater:int[s] +OasisWater:int[s] +WaterByBiome:Dictionary<MachineBiome, int>[s] +WaterFor(MachineBiome)[s] +IsWater(int)[s] +RegisterBiomeWater(MachineBiome,string,Color,string)[s]
  static class PipeTierRegistry
    +MaxFlowRateByTileType:Dictionary<int, float>[s] +Register(int,float)[s] +IsPipeTile(int)[s]
  class LiquidTypeRegistrationSystem : ModSystem
    +PostSetupContent()[ov] +Unload()[ov]
**LiquidStack.cs**
  class LiquidStack
    +LiquidType:int +Amount:float +Capacity:float +IsEmpty:bool +Filter:Func<int, bool> +Accepts(int)
  enum Direction
    {Up, Down, Left, Right}
  static class DirectionExtensions
    +ToOffset(Direction)[s] +Rotate90(Direction)[s] +Opposite(Direction)[s] +ToMaskBit(Direction)[s] +FromOffset(Point)[s] +IsPipeMouthOpen(int,int,Direction)[s]
**LiquidTextureCache.cs** uses: LiquidTypeDefinition, LiquidTypeRegistry
  static class LiquidRecolorer
    +Recolor(GraphicsDevice,Texture2D,Color,Color,float)[s]
  static class LiquidTextureCache
    +GetOrCreate(string,Texture2D,LiquidTypeDefinition,float)[s] +Clear()[s]
**MotorTileBase.cs** uses: LiquidNetworkSystem, MotorTileEntityBase, PowerGridSystem
  abstract class MotorTileBase<TEntity> : ModTile
    +CanPlace(int,int)[ov] +PlaceInWorld(int,int,Item)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CreateDust(int,int,ref int)[ov]
**MotorTileEntityBase.cs** ns!=Factorraria.Common.Machines uses: Direction, ElectricConsumerMachine, LiquidNetworkSystem, PipeTileBase
  abstract class MotorTileEntityBase : ElectricConsumerMachine
    +PumpStrength:float[ab] +PipeEquivalentMaxFlowRate:float[ab] +Facing:Direction +Update()[ov] #UpdateMotor()[v] +OnRightClick(int,int)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov]
**PipeConnectionHelper.cs** uses: BaseMachine, Direction, MotorTileEntityBase, PipeTierRegistry, TileEntityHelper
  static class PipeConnectionHelper
    +CanConnect(int,int,Direction)[s] +GetOpenSides(int,int,out bool,out bool,out bool,out bool)[s]
**PipeTileBase.cs** uses: LiquidNetworkSystem, PipeConnectionHelper, PipeTierRegistry
  abstract class PipeTileBase : ModTile
    #MaxFlowRate:float[ab] +CanPlace(int,int)[ov] +PlaceInWorld(int,int,Item)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +NearbyEffects(int,int,bool)[ov] +ReframeNeighbors(int,int)[s] +CreateDust(int,int,ref int)[ov]
**WorldLiquidSources.cs**
  class WorldLiquidSources : ModSystem
    +InfiniteThreshold:int[c] +IsInfiniteSource(Point)[s] +ComputeIsInfinite(Point)[s] +OnWorldUnload()[ov]

### Common/Machines/
**BaseMachine.cs** uses: CustomRecipe, FuelChannel, FuelModule, FuelTable, IElectricConsumer, IElectricProducer, LiquidFuelChannel, LiquidFuelModule, LiquidFuelTable, LiquidIngredient, LiquidNetworkSystem, LiquidStack, LiquidTypeRegistry, MachineBiome, MachineUIRegistry, MachineUISystem, PowerGridSystem, RecipeBook, RecipeCatalog, RecipeIngredient, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeOutputKey, VItemStacking, VirtualItem, VirtualItemSystem
  abstract class BaseMachine : ModTileEntity
    +isOn:bool +isWorking:bool #WorkProgress:int +CraftsFinished:int #WorkDuration:int[v] #OutputSlotCount:int[v] +SelectedRecipe:CustomRecipe +ManualGroup:RecipeOutputGroup +SetManualGroup(RecipeOutputGroup) #RecipeGroups:List<RecipeOutputGroup>[v] +OutputMaxStack:int +InsideBiome:MachineBiome +cornerPosition:Point16 +MachineCenter:Vector2 +MachineWidth:int +MachineHeight:int +MachineInitialized:bool #InputLiquidCount:int[v] #OutputLiquidCount:int[v] +Recipes:RecipeBook[v] #FuelPerCraft:int[v] #FuelChannels:FuelChannel[v] #AcceptedLiquidFuels:LiquidFuelTable[v] #LiquidFuelPerCraft:int[v] #LiquidFuelChannels:LiquidFuelChannel[v] #RecipeDuration(CustomRecipe)[v] +ResolveRecipeDuration(CustomRecipe) #CanStartCraft(CustomRecipe)[v] +TreatsWorldLiquidAsInfinite(int)[v] #AcceptsInputLiquid(int,int)[v] +FuelSlotIndex:int[c] +FirstIngredientSlot:int[c] +Fuel:FuelModule +FuelSlotCount:int +LiquidFuelCount:int #InputSlotCount:int[v] +IngredientStart:int +IngredientSlotCount:int #AutoIngredientCount:int[v] #ProgressiveIngredientSlots:bool[v] +ActiveIngredientCount:int #IntakeIngredientCount:int[v] #OnRecipeFinished(CustomRecipe)[v] +WorkFraction:float +GetFuelBurnFraction(int) +OnKill()[ov] +GetPendingOutputType(int) #IntakeTiles()[v] #TryGetIntakeSlot(int,out int,out int)[v] #GetPickupRecipes()[v] +IsOnConveyorFloor() +NotifyAnimationFrame(int) +OnPlaced(int,int)[v] +ResolveIncomingLiquid(int) +OnRightClick(int,int)[v] #OnAnimationFrameChanged(int,int)[v] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +IsTileValidForEntity(int,int)[ov] +Hook_AfterPlacement(int,int,int,int,int,int)[ov] +NearPlayerWidth:float +NearPlayerHeight:float +IsAnyPlayerNear(Vector2,float) +IsOnScreen(Vector2,float)[s] +DetermineBiomeAt(int,int)[s]
**BaseRecipeRegistery.cs** uses: CustomRecipe, LiquidIngredient, LiquidTypeRegistry, RecipeIngredient, RecipeOutputGroup
  class RecipeBook
    +All:List<CustomRecipe> +Groups:List<RecipeOutputGroup> +MaxIngredientCount:int +MinIngredientCount:int +ConsumesLiquid(int) +Add(CustomRecipe)[v] +ImportVanillaRecipes(int,Func<Recipe, bool>,bool) #ShouldImport(Recipe)[v] #ShouldDisableOriginal(Recipe)[v] #ConvertVanillaRecipe(Recipe)[v] +Rebuild()[v] +Clear() +DisableCustomRecipe(Recipe,bool) +DisableCustomRecipe(int,bool) +DisableVanillaRecipe(Recipe) +DisableVanillaRecipe(CustomRecipe,bool) +DisableVanillaRecipe(int,bool) +DisableRecipe(int,bool) +DisableRecipe(CustomRecipe,bool) +DisableRecipe(Recipe,bool)
**CustomRecipe.cs** uses: BaseMachine, LiquidStack, LiquidTypeRegistry
  struct RecipeIngredient
    +Type:int +Stack:int
  struct LiquidIngredient
    +LiquidType:int +Amount:float +IsAnyWater:bool +Matches(int)
  struct RecipeOutputKey : IEquatable<RecipeOutputKey>
    +IsLiquid:bool +Id:int +ForItem(int)[s] +ForLiquid(int)[s] +DisplayName:string +Equals(RecipeOutputKey) +Equals(object)[ov] +GetHashCode()[ov]
  class RecipeOutputGroup
    +Key:RecipeOutputKey +Recipes:List<CustomRecipe> +MaxInputCount:int +Add(CustomRecipe) +Build(IEnumerable<CustomRecipe>)[s]
  class CustomRecipe
    +Inputs:List<RecipeIngredient> +Outputs:List<RecipeIngredient> +LiquidInputs:List<LiquidIngredient> +LiquidOutputs:List<LiquidIngredient> +DurationTicks:int? +Output:RecipeIngredient +WithInput(int,int) +WithOutput(int,int) +UsesAnyWater:bool +WithLiquidInput(int,float) +WithAnyWaterInput(float) +WithLiquidOutput(int,float) +TakesTicks(int) +TryGetRecipeFromList(List<CustomRecipe>,List<Item>,out CustomRecipe,LiquidStack)[s] +TryGetPrimaryOutput(out RecipeOutputKey)
  class FuelTable
    +StackLimitRule:Func<int, int> +Add(int,int) +Contains(int) +UnitsOf(int) +StackLimit(int) +Clear()
  class FuelChannel
    +Table:FuelTable +UnitsPerCraft:int
  class FuelModule
    +SlotIndex:int +UnitsPerCraft:int +Remaining:int +Capacity:int +CanAccept(int) +StackLimit(int) +HasFuelAvailable(Item) +TryEnsureFuel(Item) +ConsumeCraft() +Consume(int) +GetBurnFraction(float) +Restore(int,int)
  class LiquidFuelTable
    +Add(int,float) +Contains(int) +AmountPerUnit(int) +Clear()
  class LiquidFuelChannel
    +Table:LiquidFuelTable +UnitsPerCraft:int
  class LiquidFuelModule
    +TankIndex:int +UnitsPerCraft:int +CanAccept(int) +AmountPerCraft(int) +HasFuelAvailable(LiquidStack) +ConsumeCraft(LiquidStack)
**ElectricMachine.cs** uses: BaseMachine, IElectricConsumer, IElectricProducer
  abstract class ElectricConsumerMachine : BaseMachine, IElectricConsumer
    +PowerDemand:float[ab] +useFraction:float +GetUseFraction()
  abstract class ElectricProducerMachine : BaseMachine, IElectricProducer
    +PowerSupply:float[ab] +useFraction:float +GetUseFraction()
**IElectric.cs**
  interface IElectricConsumer
    +PowerDemand:float +isWorking:bool +isOn:bool +useFraction:float +GetUseFraction()
  interface IElectricProducer
    +PowerSupply:float +isWorking:bool +isOn:bool +useFraction:float +GetUseFraction()
**IMachineDynamicDraw.cs**
  interface IMachineDynamicDraw
    +DynamicUpdate() +TryGetDynamicBounds(out Vector2,out float) +DynamicDraw(SpriteBatch) +DynamicDrawBehindTiles(SpriteBatch)
**MachineBiome.cs**
  enum MachineBiome
    {Unknown, Forest, Snow, Desert, Jungle, GlowingMushroom, Corruption, Crimson, Hallow, Underworld, Cavern, Underground, Sky, Ocean, ...}
**MachineDynamicDrawSystem.cs** uses: BaseMachine, IMachineDynamicDraw
  class MachineDynamicDrawSystem : ModSystem
    +Load()[ov] +Unload()[ov] +PostUpdateEverything()[ov] +PostDrawTiles()[ov]
**MachineOverhangSystem.cs** uses: BaseMachine, MachineCanvas, MachineVisualDefinition, MachineVisualRegistry, TileEntityHelper
  class MachineOverhangSystem : ModSystem
    +Load()[ov] +Unload()[ov] +PostDrawTiles()[ov]
**MachineVisualOverride.cs** uses: BaseMachine, Direction, LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry, MotorTileEntityBase, TileEntityHelper
  class LiquidOverlayLayer
    +SourceTexture:Asset<Texture2D> +GetLiquidStack:Func<BaseMachine, LiquidStack> +CacheKey:string
  class MachineCanvas
    +FootprintWidth:int +FootprintHeight:int +OnFrames:int +OffFrames:int +Padding:int +CanvasWidth:int +CanvasHeight:int +OriginX:float +OriginY:float +YOffset:float +Resolved:bool +Valid:bool +Error:string +CellSize:int +FrameStride:int +FrameCount(bool) +Resolve(Texture2D,Texture2D)
  class MachineVisualDefinition
    +GetEntity:Func<int, int, BaseMachine> +OnTexture:Asset<Texture2D> +OffTexture:Asset<Texture2D> +AltOnTexture:Asset<Texture2D> +UseAltOn:Func<BaseMachine, bool> +LiquidOverlays:List<LiquidOverlayLayer> +Canvas:MachineCanvas
  static class MachineVisualRegistry
    +Definitions:Dictionary<int, MachineVisualDefinition>[s] +Register<T>(int,string,string)[s] +RegisterAlternateOn(int,string,Func<BaseMachine, bool>)[s] +RegisterLiquidOverlay(int,string,Func<BaseMachine, LiquidStack>)[s] +RegisterOverhang<T>(int,string,string,int,int,float,float,int,int,float)[s]
  class MachineVisualOverride : GlobalTile
    +PreDraw(int,int,int,SpriteBatch)[ov] +RightClick(int,int,int)[ov] +PlaceInWorld(int,int,int,Item)[ov] +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov] +ModifyLight(int,int,int,ref float,ref float,ref float)[ov]
**RecipeVisibility.cs** uses: CustomRecipe, Entry, FurnaceOffsetConfig, RecipeCatalog, RecipeIngredient, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeOutputKey
  enum RecipeState
    {Hidden, DebugRevealed, FadedIngredient, FadedParchment, Crafted}
  static class RecipeVisibility
    +HeldVersion:int[s] +BuildIngredientIndex()[s] +ClearIndex()[s] +InvalidateHeld()[s] +GetState(string,RecipeOutputGroup)[s] +GetState(string)[s] +Poll()[s] +IsItemHeld(int)[s] +IsHeld(string)[s] +IsNameRevealedInBrowser(string,RecipeOutputGroup,RecipeState)[s] +IsProductCrafted(RecipeOutputKey)[s] +IsIngredientRevealed(int)[s] +IsLiquidRevealed(int)[s] +FindBookRecipeFor(RecipeOutputKey,string)[s] +InBook(RecipeState)[s]
**TileEntityHelper.cs** uses: FurnaceOffsetConfig, LiquidNetworkSystem, PowerGridSystem
  static class TileEntityHelper
    +TryGetEntityFromTile(int,int,out TileEntity,out Point16)[s] +GetOrCreateEntity<T>(int,int)[s] +AnimateTileEntity(SpriteBatch,Texture2D,int,int,float)[s] +GetAnimationFrame(Point16,int)[s]

### Common/Mounts/
**MechanicalArmIK.cs**
  static class MechanicalArmIK
    +SolveElbow(Vector2,Vector2,float,float,float,float)[s] +DrawSegment(SpriteBatch,Texture2D,Vector2,Vector2,Color,float)[s]

### Common/Net/
**FactorrariaNet.cs** uses: Direction, Factorraria, FactorrariaPacket, Handle, NetRateLimiter, NetworkConfig, PacketDirection
  static class FactorrariaNet
    +ReachSlackTiles:float[s] +IsServerSide:bool[s] +HasLocalClient:bool[s] +IsSinglePlayer:bool[s] +IsOwner(Player)[s] +InReach(Player,int,int)[s] +WriteItem(BinaryWriter,Item)[s] +ReadItem(BinaryReader)[s] +Register(FactorrariaPacket,PacketDirection,PacketHandler,int)[s] +Clear()[s] +Handle(BinaryReader,int)[s] +SendRequest(FactorrariaPacket,Action<BinaryWriter>)[s] +SendToClient(int,FactorrariaPacket,Action<BinaryWriter>)[s] +Broadcast(FactorrariaPacket,Action<BinaryWriter>,int)[s] +SendToNearby(Vector2,float,FactorrariaPacket,Action<BinaryWriter>,int)[s] +Log(string)[s]
    sealed class Route
      +Direction:PacketDirection +Handler:PacketHandler +Cost:int
**FactorrariaPacket.cs** uses: TilePlaced
  enum FactorrariaPacket : byte
    {Ping, PingReply, JoinRequest, JoinChunk, JoinDone, TilePlaced, MechSuitStateRequest, MechSuitState}
  enum PacketDirection : byte
    {ToServer, ToClient}
**FactorrariaPlayerSync.cs** uses: JoinSync
  class FactorrariaPlayerSync : ModPlayer
    +OnEnterWorld()[ov] +PlayerDisconnect()[ov] +CopyClientState(ModPlayer)[ov] +SyncPlayer(int,int,bool)[ov] +SendClientChanges(ModPlayer)[ov]
**JoinSync.cs** uses: Factorraria, FactorrariaNet, FactorrariaPacket, NetworkConfig
  static class JoinSync
    +ChunkSize:int[s] +MaxProviderBytes:int[s] +RequestCooldownTicks:uint[s] +Completed:bool[s] +AnnounceInChat:bool[s] +JoinCompleted:event Action[s] +Register(string,Action<BinaryWriter>,Action<BinaryReader>)[s] +RegisterDisconnectHook(Action<int>)[s] +Clear()[s] +RequestFullState()[s] +HandleRequest(BinaryReader,int)[s] +ServerTick()[s] +HandleChunk(BinaryReader,int)[s] +HandleDone(BinaryReader,int)[s] +OnPlayerDisconnect(int)[s]
    sealed class Provider
      +Key:string +Write:Action<BinaryWriter> +Read:Action<BinaryReader>
    sealed class Job
      +Token:int +NextProvider:int +Data:byte +Offset:int +TotalBytes:int
    sealed class Incoming
      +Data:byte +Filled:int
**JoinSyncSystem.cs** uses: FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection
  class JoinSyncSystem : ModSystem
    +TestProvidersEnabled:bool[s] +TestBlobSize:int[s] +LastTestCounter:int[s] +Load()[ov] +PostUpdateEverything()[ov] +Unload()[ov]
**MechSuitNet.cs** uses: FactorrariaNet, FactorrariaPacket, MechSuitPlayer, PacketDirection
  class MechSuitNetSystem : ModSystem
    +Load()[ov]
  static class MechSuitNet
    +ReportState(bool)[s] +SendState(int,bool,int,int)[s] +HandleRequest(BinaryReader,int)[s] +HandleState(BinaryReader,int)[s]
**MultiplayerBootProbeSystem.cs** uses: AwakenedStatueRegistry, BaseMachine, LiquidNetworkSystem, MachineVisualRegistry, Probe, TrackData
  class MultiplayerBootProbeSystem : ModSystem
    +EnableProbe:bool[s] +SummaryDelayTicks:int[s] +Load()[ov] +PostSetupContent()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
**NetCoreSystem.cs** uses: FactorrariaNet, FactorrariaPacket, PacketDirection
  class NetCoreSystem : ModSystem
    +Load()[ov] +Unload()[ov] +SendPing(Action<string>)[s]
**NetFloodCommand.cs** uses: FactorrariaNet, FactorrariaPacket
  class NetFloodCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**NetJoinCommand.cs** uses: FactorrariaNet, JoinSync
  class NetJoinCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**NetPingCommand.cs** uses: NetCoreSystem
  class NetPingCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**NetRateLimiter.cs** uses: FactorrariaNet, JoinSync, NetworkConfig
  static class NetRateLimiter
    +TryConsume(int,int)[s] +Reset(int)[s] +ResetAll()[s]
  class NetRateLimiterSystem : ModSystem
    +Load()[ov] +Unload()[ov]
**TilePlaced.cs** uses: FactorrariaNet, FactorrariaPacket
  interface ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  static class TilePlaced
    +Register(ITilePlacedConsumer)[s] +Clear()[s] +NotifyPlaced(int,int,int)[s] +RunConsumers(int,int,int,int)[s] +HandleRequest(BinaryReader,int)[s]
**TilePlacedGlobalTile.cs** uses: TilePlaced
  class TilePlacedGlobalTile : GlobalTile
    +PlaceInWorld(int,int,int,Item)[ov]
**TilePlacedSystem.cs** uses: FactorrariaNet, FactorrariaPacket, ITilePlacedConsumer, PacketDirection, TilePlaced
  class TilePlacedSystem : ModSystem
    +LogPlacements:bool[s] +Load()[ov] +Unload()[ov]
    sealed class PlacementLogConsumer : ITilePlacedConsumer
      +AppliesTo(int) +OnTilePlaced(int,int,int,int)

### Common/Networks/
**LiquidNetwork.cs** uses: BaseMachine, Direction, LiquidNetworkSystem, LiquidStack, LiquidTypeRegistry, MotorTileEntityBase, WorldLiquidSources
  struct PipeAttachment
    +Position:Point +PipePosition:Point +MouthDirection:Direction +Machine:BaseMachine
  struct MotorAttachment
    +Position:Point +Motor:MotorTileEntityBase
  class LiquidNetwork
    +PipeTiles:HashSet<Point> +MachineAttachments:List<PipeAttachment> +WorldLiquidAttachments:List<PipeAttachment> +Motors:List<MotorAttachment> +MaxFlowRate:float +LiquidType:int +Tick()
  struct LiquidEndpoint
    +IsWorld:bool +Machine:BaseMachine +MachineSlots:LiquidStack +WorldPos:Point +RateThisTick:float +ForMachine(BaseMachine,LiquidStack,float)[s] +ForWorld(Point,float)[s]
**PowerNetwork.cs** ns!=Factorraria.Common.PowerGrid uses: IElectricConsumer, IElectricProducer
  class PowerNetwork
    +electricConsumers:List<IElectricConsumer> +electricProducers:List<IElectricProducer> +Tick()
**PriorityConveyorNetwork.cs**
  class PriorityConveyorNetwork
    +Tiles:HashSet<Point> +FilteredItemId:int

### Common/Statues/
**AwakenedSpawnGlobalNPC.cs** uses: AwakenedLootDrop, AwakenedStatue, AwakenedStatueEntry, AwakenedStatueSystem, Entry
  class AwakenedSpawnGlobalNPC : GlobalNPC
    +InstancePerEntity:bool[ov] +Tagged:bool +Origin:Point16 +Entry:AwakenedStatueEntry +Release() +CheckActive(NPC)[ov] +OnKill(NPC)[ov]
**AwakenedSpawnSystem.cs** uses: AwakenedSpawnGlobalNPC, AwakenedStatue, AwakenedStatueSystem, Entry, IStatueAwakener
  class AwakenedSpawnSystem : ModSystem
    +MaxEnemiesPerStatue:int +SpawnPassTicks:int +RefillDelayTicks:int +NpcSlotMargin:int +SpawnOffset:Vector2 +PostUpdateNPCs()[ov]
**AwakenedStatueDrawSystem.cs** uses: AwakenedStatue, AwakenedStatueSystem, BaseMachine, Entry
  class AwakenedStatueDrawSystem : ModSystem
    +OutlinePulseSpeed:float +OutlineScaleAmplitude:float +OutlineMinOpacity:float +PadLeft:int +PadRight:int +PadTop:int +PadBottom:int +Load()[ov] +Unload()[ov]
**AwakenedStatueGlobalTile.cs** uses: AwakenedStatueRegistry, AwakenedStatueSystem
  class AwakenedStatueGlobalTile : GlobalTile
    +PreHitWire(int,int,int)[ov] +PlaceInWorld(int,int,int,Item)[ov] +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov]
**AwakenedStatueRegistry.cs** uses: AwakenedStatueEntries
  class AwakenedLootDrop
    +ItemType:int +Chance:float +Min:int +Max:int
  class AwakenedStatueEntry
    +TileType:int +Style:int +FlipHorizontal:bool +NpcTypes:int +HardmodeNpcs:List<int> +Spawns(int) +PickNpcType() +AddHardmodeList(int) +Sprite:Asset<Texture2D> +Outline:Asset<Texture2D> +DrawOffset:Vector2 +Loot:List<AwakenedLootDrop> +AddLoot(int,float,int,int)
  class AwakenedStatueRegistry : ModSystem
    +RightFacingStyleOffset:int +IsStatueTile(int) +EntryCount:int +TryGet(int,int,out AwakenedStatueEntry) +Add(int,int,int,string,string,Vector2) +Add(int,int,int,string,string,Vector2) +PostSetupContent()[ov] +Unload()[ov]
**AwakenedStatueSystem.cs** uses: AwakenedSpawnGlobalNPC, AwakenedStatueEntry, AwakenedStatueRegistry, Entry, IStatueAwakener
  class AwakenedStatue
    +Origin:Point16 +Entry:AwakenedStatueEntry +OwnerOrigin:Point16 +Width:int +Height:int +LiveEnemies:int +LastLiveEnemies:int +SpawnCooldownTicks:int +Center:Vector2
  class AwakenedStatueSystem : ModSystem
    +Awakened:IReadOnlyDictionary<Point16, AwakenedStatue> +OnWorldLoad()[ov] +OnWorldUnload()[ov] +QueueStatueCheck(int,int) +PostUpdateWorld()[ov] +IsAwakenedTile(int,int) +IsStillAwakened(AwakenedStatue) +RescanFor(IStatueAwakener) +ClearTagsFor(HashSet<Point16>)
**IStatueAwakener.cs**
  interface IStatueAwakener
    +IsAwakening:bool +AwakenerOrigin:Point16 +AwakenRadiusTiles:int +AwakenedStatueCount:int +AwakenedEnemyCount:int

### Common/Systems/
**ChestConveyorSystem.cs** uses: VItemStacking, VirtualItem, VirtualItemSystem
  enum ChestConveyorMode
    {InputOutput, Input, Output}
  enum ChestSideRole
    {None, Input, Output}
  class ChestConveyorEntry
    +TopLeft:Point +ChestIndex:int +Mode:ChestConveyorMode +Left:ChestSideRole +Right:ChestSideRole
  class ChestConveyorSystem : ModSystem
    +Enabled:bool[s] +HasListedChestAbove(int,int)[s] +QueueProbe(int,int,bool)[s] +CycleMode(ChestConveyorEntry)[s] +LoadWorldData(TagCompound)[ov] +OnWorldUnload()[ov] +Unload()[ov]
    struct Probe
      +Tile:Point +AllowAdd:bool
**ConveyorPhysicsSystem.cs** uses: ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyorTile
  class ConveyorPhysicsSystem : ModSystem
    +ConveyorSpeed:float[c] +PostUpdateNPCs()[ov]
**CustomWireSystem.cs** uses: CopperWireItem, IronCutterItem, LeadCutterItem, PowerGridSystem, TinWireItem
  enum CustomWireType : byte
    {None, Copper}
  class CustomWireSystem : ModSystem
    +AllWireTypes:CustomWireType[s] +WireGrid:Dictionary<Point16,CustomWireType>[s] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +HasWire(int,int,CustomWireType)[s] +AddWire(int,int,CustomWireType)[s] +RemoveWire(int,int,CustomWireType)[s] +PostDrawTiles()[ov]
**LiquidNetworkDebugSystem.cs** uses: FurnaceOffsetConfig, LiquidNetwork, LiquidNetworkSystem
  class LiquidNetworkDebugSystem : ModSystem
    +EnableDebugView:bool[s] +PostDrawTiles()[ov]
**LiquidNetworkSystem.cs** uses: BaseMachine, Direction, DirectionExtensions, LiquidNetwork, LiquidTextureCache, LiquidTypeRegistry, MotorAttachment, MotorTileEntityBase, PipeAttachment, PipeConnectionHelper, PipeTierRegistry, TileEntityHelper
  class LiquidNetworkSystem : ModSystem
    +AllPipeTiles:HashSet<Point>[s] +ActiveNetworks:List<LiquidNetwork>[s] +networkNeedsRebuilding:bool[s] +flowNeedsRecalculating:bool[s] +PostUpdateWorld()[ov] +RegisterPipeTile(Point)[s] +UnregisterPipeTile(Point)[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov]
  class PriorityQueueLite<T>
    +Enqueue(T,float) +TryDequeue(out T,out float)
**MachineRegisterationSystem.cs** ns!=Factorraria.Common.Machines uses: AutohammerRecipeRegistry, AutohammerTileEntity, AutohammerUIState, BaseMachine, CustomRecipe, DecayChamberRecipeRegistry, DecayChamberTileEntity, DecayChamberUIState, ExtractinatorRecipeRegistry, ExtractinatorTileEntity, ExtractinatorUIState, FurnaceRecipeRegistry, FurnaceTileEntity, FurnaceUIState, GelBurnerTileEntity, GelBurnerUIState, HellforgeRecipeRegistry, HellforgeTileEntity, HellforgeUIState, IceMachineRecipeRegistry, IceMachineTileEntity, IceMachineUIState, LiquidDistillatorRecipeRegistry, LiquidDistillatorTileEntity, LiquidDistillatorUIState, MachineUIRegistry, MachineVisualRegistry, MagicMaterializerRegistry, MagicMaterializerTileEntity, MechanicalAssemblerRecipeRegistry, MechanicalAssemblerTile, MechanicalAssemblerTileEntity, MechanicalAssemblerUIState, MotorMK1Tile, MotorMK1TileEntity, RecipeBook, RecipeCatalog, RecipeVisibility, RoboticArmTile, RoboticArmTileEntity, RoboticArmUIState, ScrollPoolDefinitions, ScrollPools, SolidifierRecipeRegistry, SolidifierTileEntity, SolidifierUIState
  class MachineRegisterationSystem : ModSystem
    +Load()[ov] +PostSetupContent()[ov] +PostAddRecipes()[ov] +Unload()[ov]
**MachineUISystem.cs** uses: BaseMachine, CartChestUISystem, Factorraria, MachineUIRegistry, MachineUIStateBase, RecipeBookSystem
  class MachineUISystem : ModSystem
    +OpenMachine:BaseMachine +Load()[ov] +OpenUI(int,int,int,BaseMachine) +CloseUI() +NotifyMachineKilled(BaseMachine) +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**PowerGridDebugSystem.cs** ns!=(global) uses: BaseMachine, FurnaceOffsetConfig, LiquidStack, PowerGridSystem
  class PowerGridDebugSystem : ModSystem
    +EnableDebugView:bool[s] +PostDrawTiles()[ov]
**PowerGridSystem.cs** uses: CustomWireSystem, CustomWireType, IElectricConsumer, IElectricProducer, PowerNetwork, TileEntityHelper
  class PowerGridSystem : ModSystem
    +AllMachines:HashSet<TileEntity>[s] +ActivePowerNetworks:List<PowerNetwork>[s] +gridNeedsRebuilding:bool[s] +PostUpdateWorld()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +RegisterMachineToMasterList(TileEntity)[s]
**RecipeBookSystem.cs** uses: CartChestUISystem, Factorraria, MachineUISystem, RecipeBookArt, RecipeBookButtonState, RecipeBookState, RecipeIcon
  class RecipeBookSystem : ModSystem
    +IsOpen:bool[s] +Open()[s] +OpenAt(string)[s] +Close(bool)[s] +Toggle()[s] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**RecipeSystem.cs** uses: RecipeBook
  class RecipeSystem : ModSystem
    +AddRecipes()[ov]
**VirtualItemSystem.cs** ns!=Factorraria.Content.VirtualItems uses: ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyorTile, FurnaceOffsetConfig, PriorityConveyorNetwork, VItemGlobalItem, VirtualItem
  class VirtualItemSystem : ModSystem
    +tileItemMap:Dictionary<Point, VirtualItem>[s] +ConveyorNetworkMap:Dictionary<Point, PriorityConveyorNetwork>[s] +IsValidItemID(int)[s] +RegisterItemTile(Point,VirtualItem)[s] +UnregisterItemTile(Point,VirtualItem)[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +PostDrawTiles()[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov] +SpawnVirtualItem(int,int,int,int)[s] +SpawnVirtualItem(Item,int,int)[s] +GetVirtualItemAtTile(int,int,VirtualItem)[s] +IsTileOccupied(int,int,VirtualItem)[s] +GetVItemsInRect(Rectangle,List<VirtualItem>)[s] +ConvertItemsToVItems()[s] +ConvertVItemsToItems(VirtualItem)[s] +CheckPlayerPickups()[s] +IsConveyorTile(int,int,out bool,out int)[s] +IsPriorityConveyorTile(int,int)[s] +RebuildNetworkAt(int,int,Point?)[s] +OnPriorityConveyorPlaced(int,int)[s] +OnPriorityConveyorKilled(int,int)[s] +SetConveyorFilter(int,int,int)[s] +TryGetConveyorFilter(int,int,out int)[s]

### Common/UI/
**ChestConveyorModeUI.cs** uses: ChestConveyorEntry, ChestConveyorMode, ChestConveyorSystem
  class ChestConveyorModeUI : ModSystem
    +ButtonBaseX:float[s] +ChestGridRows:float[s] +SlotPitch:float[s] +GapBelowGrid:float[s] +ButtonOffsetX:float[s] +ButtonOffsetY:float[s] +TextScale:float[s] +PadX:int[s] +PadY:int[s] +InputColor:Color[s] +OutputColor:Color[s] +SeparatorColor:Color[s] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
    struct Segment
      +Text:string +Color:Color
**MachineUIElementEntry.cs**
  class MachineUIElementEntry
    +Element:UIElement +BasePosition:Vector2 +BaseSize:Vector2 +AffectsPanelBounds:bool
**MachineUIRegistry.cs** uses: MachineUIStateBase
  static class MachineUIRegistry
    +Definitions:Dictionary<int, MachineUIStateBase>[s] +Register(int,MachineUIStateBase)[s]
**MachineUIStateBase.cs** uses: BaseMachine, LiquidTankUIElement, MachineUIElementEntry, OutputSlotAnimation, RecipeBook, RecipeBrowserPanel, RecipeSelectHammerIcon, UIItemSlotWrapper
  interface IZoomScalable
    +SetZoomScale(float)
  abstract class MachineUIStateBase : UIState
    +CurrentEntity:BaseMachine +Panel:UIElement #BasePanelSize:Vector2[v] +BasePanelOffset:Vector2[v] #BuildElements()[ab] +OnInitialize()[ov] +InitializeUIState() +OnActivate()[ov] #UpdateLayout()[v] +ContentOrigin:Vector2 +SetZoomScale(float) #SlotSize:float[c] #InputSlotEntry(int,Vector2,Func<bool>) #OutputSlotEntry(int,Vector2,Func<bool>) #FuelSlotEntry(int,Vector2) #FuelSlotEntry(Vector2) #FuelTankEntry(int,Vector2) #InputTankEntry(int,Vector2) #OutputTankEntry(int,Vector2) #AddRecipePicker(List<MachineUIElementEntry>,RecipeBook,Vector2,Vector2)
**RecipeIcon.cs** uses: LiquidTypeDefinition, LiquidTypeRegistry, RecipeOutputKey
  static class RecipeIcon
    +FadedTint:Color[s] +Clear()[s] +Draw(SpriteBatch,RecipeOutputKey,Vector2,float,float,bool)[s]
**UIItemSlotWrapper.cs** ns!=Factorraria.Content.UI uses: Handle
  class UIItemSlotWrapper : UIElement
    #DrawSelf(SpriteBatch)[ov]

### Common/UI/CustomUIElements/
**ElectricityUIIcon.cs**
  class ElectricityUIIcon : UIElement
    #DrawSelf(SpriteBatch)[ov]
**FireUIElement.cs** uses: FurnaceOffsetConfig
  class FireUIElement : UIElement
    #DrawSelf(SpriteBatch)[ov]
**LiquidTankUIElement.cs** uses: LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry
  class LiquidTankUIElement : UIElement
    #DrawSelf(SpriteBatch)[ov]
**OutputSlotAnimation.cs** uses: BaseMachine, RecipeIcon
  class OutputSlotAnimation
    +Draw(SpriteBatch,CalculatedStyle,Item) +DrawProgress(SpriteBatch,Vector2,float,int,int,float)
**RecipeSelectHammerIcon.cs** uses: BookDraw, IZoomScalable, RecipeBook, RecipeBookLayout, RecipeBookSystem, RecipeCatalog, RecipeIcon, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeState, RecipeVisibility
  class RecipeSelectHammerIcon : UIElement
    +OnInitialize()[ov] #DrawSelf(SpriteBatch)[ov] +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov]
  class RecipeBrowserPanel : UIPanel, IZoomScalable
    +showPanel:bool +recipeList:UIGrid +scrollbar:ZoomScrollbar +PopulateRecipeList() +OnInitialize()[ov] +Update(GameTime)[ov] +SetZoomScale(float) +Draw(SpriteBatch)[ov] #DrawSelf(SpriteBatch)[ov] +ContainsPoint(Vector2)[ov]
  class RecipeElement : UIPanel
    +OnSelected:Action<RecipeElement> +ProductName:string +Selected:bool +CurrentGroup:RecipeOutputGroup +Index:int +State:RecipeState +CatalogKey:string +CompareTo(object)[ov] +SetZoomScale(float) +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
  static class ScaledPanel
    +Draw(SpriteBatch,Rectangle,Color,Color,float)[s]
  class ZoomScrollbar : UIScrollbar
    +SetZoomScale(float) +LeftMouseDown(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
  class SearchBarTheme
    +Caret:Color Background, IdleBorder, FocusBorder, Text, Hint,
  class RecipeSearchBar : UIElement
    +SetZoomScale(float) +Unfocus() +LeftClick(UIMouseEvent)[ov] +RightClick(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]

### Common/UI/RecipeBookUI/
**BookScrollbar.cs** uses: BookDraw, RecipeBookLayout
  class BookScrollbar : UIScrollbar
    +Needed:bool +SetBookView(float,float) +LeftMouseDown(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookButtonState.cs** uses: FurnaceOffsetConfig, RecipeBookButton, RecipeBookLayout, RecipeBookPlayer
  class RecipeBookButtonState : UIState
    +Button:RecipeBookButton +OnInitialize()[ov] +ResetPosition()[s] +Update(GameTime)[ov]
**RecipeBookDetail.cs** uses: BookDraw, CustomRecipe, LiquidIngredient, LiquidTypeRegistry, RecipeBookLayout, RecipeBookState, RecipeIcon, RecipeIngredient, RecipeOutputGroup, RecipeOutputKey, RecipeState, RecipeVisibility
  class RecipeBookDetail : UIElement
    +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
    struct Cell
      +Icon:RecipeOutputKey +Count:string +Name:string +ItemType:int +Stack:int +Hidden:bool +NavKey:string
**RecipeBookDragHandle.cs** uses: BookDraw, RecipeBookLayout, RecipeBookState
  class RecipeBookDragHandle : UIElement
    +LeftMouseDown(UIMouseEvent)[ov] +RightClick(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookElements.cs** uses: RecipeBookArt, RecipeBookLayout, RecipeIcon, RecipeOutputGroup, RecipeState
  static class BookDraw
    +Rect(SpriteBatch,Rectangle,Color)[s] +Frame(SpriteBatch,Rectangle,Color,Color,int)[s] +Fit(string,float,float,out bool)[s] +Text(SpriteBatch,string,Vector2,Color,float)[s]
  class RecipeBookButton : UIElement
    +OnPressed:Action +MouseInside:bool +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
  class RecipeBookHeaderRow : UIElement
    #DrawSelf(SpriteBatch)[ov]
  class RecipeBookEntry : UIElement
    +Key:string +Machine:string +Group:RecipeOutputGroup +State:RecipeState +ProductName:string +Selected:bool +OnChosen:Action<string> +IsFaded:bool +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookLayout.cs** uses: SearchBarTheme
  static class RecipeBookLayout
    +ButtonX:int[c] +ButtonY:int[c] +ButtonSize:int[c] +ReferenceDefenseX:int[c] +ReferenceDefenseY:int[s] +ButtonOffsetX:int[c] +ButtonOffsetY:int[s] +PanelWidth:int[c] +PanelHeight:int[c] +BackgroundScale:float[s] +Margin:int[c] +Gutter:int[c] +TitleHeight:int[c] +UnknownName:string[c] +ScrollbarWidth:int[c] +HeaderRowHeight:int[c] +EntryRowHeight:int[c] +EntryIconBox:int[c] +LeftPage:Rectangle[s] +RightPage:Rectangle[s] +ListArea:Rectangle[s] +ScrollbarArea:Rectangle[s] +DragBar:Rectangle[s] +DragKeepVisible:int[c] +SearchArea:Rectangle[s] +DetailArea:Rectangle[s] +DetailHeaderHeight:int[c] +DetailCell:int[c] +DetailCellGap:int[c] +DetailPagerHeight:int[c] +DetailRecipeArea:Rectangle[s] +Text(string)[s] +MachineName(string)[s] +PageColor:Color[s] +PageBorder:Color[s] +CoverColor:Color[s] +TextColor:Color[s] +TextFadedColor:Color[s] +SearchBackground:Color[s] +SearchIdleBorder:Color[s] +SearchFocusBorder:Color[s] +ScrollTrack:Color[s] +ScrollTrackOutline:Color[s] +ScrollThumb:Color[s] +ScrollThumbHover:Color[s] +ScrollThumbDrag:Color[s] +SearchTheme:SearchBarTheme[s]
  static class RecipeBookArt
    +ButtonPath:string[c] +ButtonHoverPath:string[c] +BackgroundPath:string[c] +Button:Asset<Texture2D>[s] +ButtonHover:Asset<Texture2D>[s] +Background:Asset<Texture2D>[s] +Clear()[s]
**RecipeBookPlayer.cs**
  class RecipeBookPlayer : ModPlayer
    +ButtonOffset:Vector2? +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov]
**RecipeBookState.cs** uses: BookDraw, BookScrollbar, Entry, RecipeBookArt, RecipeBookDetail, RecipeBookDragHandle, RecipeBookEntry, RecipeBookHeaderRow, RecipeBookLayout, RecipeCatalog, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeSearchBar, RecipeState, RecipeVisibility
  class RecipeBookState : UIState
    +SelectedKey:string +SelectedMachine:string +SelectedGroup:RecipeOutputGroup +SelectedState:RecipeState +IsEmpty:bool +NoResults:bool +MouseInsidePanel:bool +OnInitialize()[ov] +OnActivate()[ov] +Update(GameTime)[ov] +ResetSearch() +PanelOffset:Vector2 +SetPanelOffset(Vector2) +ResetPanelOffset() +ScrollBy(float) +Select(string,bool) +GoTo(string)
  class RecipeBookPanel : UIElement
    +MouseInside:bool +Update(GameTime)[ov] +ScrollWheel(UIScrollWheelEvent)[ov] #DrawSelf(SpriteBatch)[ov]

### Common/VirtualItems/
**VItemGlobalItem.cs**
  class VItemGlobalItem : GlobalItem
    +InstancePerEntity:bool[ov] +ConveyorImmunityTimer:int +Update(Item,ref float,ref float)[ov]
**VItemStacking.cs** ns!=Factorraria.Content.VirtualItems
  static class VItemStacking
    +CanMerge(Item,Item)[s] +Insert(Item,int,int,Item,Func<int, Item, bool>,Action<int>)[s]
**VirtualItem.cs** ns!=Factorraria.Content.VirtualItems uses: VItemGlobalItem, VItemStacking, VirtualItemSystem
  class VirtualItem
    +itemType:int +stackSize:int +prefix:int +active:bool +pickupCooldown:int +cartGrabCooldown:int +frame:int +frameCounter:int +currentTileX:int +currentTileY:int +targetTileX:int +targetTileY:int +worldPosition:Vector2 +moveSpeed:float +Update() +SetTargetTile(int,int) +GetSourceRectangle(Texture2D) +Remove() +MoveVirtualItem() +TryMergeWithVirtualItem(VirtualItem)

### Common/WorldGeneration/
**PostWorldGenEdits.cs**
  class PostWorldGenEdits : ModSystem
    +Enabled:bool[s] +PostWorldGen()[ov]
**SiliconOreWorldGen.cs** uses: SiliconOreTile
  class SiliconOreWorldGen : ModSystem
    +DensityPerTile:double[s] +StrengthMin:int[s] +StrengthMax:int[s] +StepsMin:int[s] +StepsMax:int[s] +BottomMargin:int[s] +ModifyWorldGenTasks(List<GenPass>,ref double)[ov]

### Content/Configs/
**FurnaceOffsetConfig.cs**
  class FurnaceOffsetConfig : ModConfig
    +Mode:ConfigScope[ov] +OffsetX:float +OffsetY:float +AngleOffset:float +AngleOffset2:float +animationOffset:int +fireCropOffset:float +animationSpeedDivider:int +EnableDebugs:bool +RevealAllRecipes:bool +VItemOffset:float
**NetworkConfig.cs**
  class NetworkConfig : ModConfig
    +Current:NetworkConfig[s] +Mode:ConfigScope[ov] +ReachSlackTiles:float +RequestsPerSecond:int +RequestBurst:int +JoinChunkSize:int +JoinMaxProviderBytes:int +JoinRequestCooldownTicks:int +CartUpdatesPerSecond:int +CartInterestRadiusTiles:int +CartInterpolationBufferMs:int +MachineStateUpdatesPerSecond:int +LiquidBroadcastsPerTick:int

### Content/Items/Carts/
**CartItem.cs** uses: CartSystem
  class CartItem : ModItem
    +Texture:string[ov] +CanUseItem(Player)[ov] +UseItem(Player)[ov]
**CartModuleItemGlobal.cs** uses: Cart
  class CartModuleItemGlobal : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]
**VanillaCartItemGlobal.cs** uses: CartSkinTable
  class VanillaCartItemGlobal : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +CanUseItem(Item,Player)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]

### Content/Items/Conveyors/
**ClockwisePriorityConveyor.cs** uses: ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyor
  class ClockwisePriorityConveyor : ModItem
    +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov]
**CounterClockwisePriorityConveyor.cs** uses: ClockwisePriorityConveyor, CounterClockwisePriorityConveyorTile
  class CounterClockwisePriorityConveyor : ModItem
    +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov]
**VanillaConveyorGlobalItem.cs**
  class VanillaConveyorGlobalItem : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +CanRightClick(Item)[ov] +RightClick(Item,Player)[ov] +ConsumeItem(Item,Player)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]

### Content/Items/Discovery/
**ClosedScrollItem.cs** uses: RecipeParchmentItem, ScrollPools
  class ClosedScrollItem : ModItem
    +PoolName:string +CanRightClick()[ov] +RightClick(Player)[ov] +CanStack(Item)[ov] +ModifyTooltips(List<TooltipLine>)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +NetSend(BinaryWriter)[ov] +NetReceive(BinaryReader)[ov]
**RecipeParchmentItem.cs** uses: RecipeBookSystem, RecipeCatalog, RecipeKnowledgeSystem, RecipeOutputGroup, ScrollPools
  class RecipeParchmentItem : ModItem
    +PoolName:string +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov] +CanStack(Item)[ov] +ModifyTooltips(List<TooltipLine>)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +NetSend(BinaryWriter)[ov] +NetReceive(BinaryReader)[ov]

### Content/Items/Hooks/
**MechanicalArmItem.cs** uses: MechanicalArmProjectile, RoboticArmTile
  class MechanicalArmItem : ModItem
    +CanUseItem(Player)[ov] +HoldItem(Player)[ov] +UpdateInventory(Player)[ov] +Shoot(Player,EntitySource_ItemUse_WithAmmo,Vector2,Vector2,int,int,float)[ov]

### Content/Items/JokeItems/
**CsharpLogoItem.cs**
  class CsharpLogoItem : ModItem
    +PreDrawInWorld(SpriteBatch,Color,Color,ref float,ref float,int)[ov] +PreDrawInInventory(SpriteBatch,Vector2,Rectangle,Color,Color,Vector2,float)[ov]
**IsmailsCaneItem.cs**
  class IsmailsCaneItem : ModItem
    +HoldStyle(Player,Rectangle)[ov]
**TerrariaLogoItem.cs**
  class TerrariaLogoItem : ModItem
    +PreDrawInWorld(SpriteBatch,Color,Color,ref float,ref float,int)[ov] +PreDrawInInventory(SpriteBatch,Vector2,Rectangle,Color,Color,Vector2,float)[ov]
**TmodloaderLogoItem.cs** uses: CsharpLogoItem, TerrariaLogoItem
  class TmodloaderLogoItem : ModItem
    +PreDrawInWorld(SpriteBatch,Color,Color,ref float,ref float,int)[ov] +PreDrawInInventory(SpriteBatch,Vector2,Rectangle,Color,Color,Vector2,float)[ov] +AddRecipes()[ov]

### Content/Items/Liquids/Buckets/
**BucketBase.cs** ns!=ModLiquidExampleMod.Content.Items
  abstract class BucketBase : ModItem
    +BucketLiquidType:int +HoldItem(Player)[ov]
**FuelBucket.cs** uses: BucketBase, FuelLiquid
  class FuelBucket : BucketBase
**OilBucket.cs** uses: BucketBase, OilLiquid
  class OilBucket : BucketBase

### Content/Items/Liquids/Motors/
**MotorMK1Item.cs** uses: MotorMK1Tile
  class MotorMK1Item : ModItem

### Content/Items/Liquids/Pipes/
**PipeMK1Item.cs** uses: PipeMK1Tile
  class PipeMK1Item : ModItem

### Content/Items/Machines/
**MechanicalAssemblerItem.cs** uses: MechanicalAssemblerTile
  class MechanicalAssemblerItem : ModItem

### Content/Items/Materials/
**ByriteAlloyItem.cs**
  class ByriteAlloyItem : ModItem
**ByriteAlloySlabItem.cs**
  class ByriteAlloySlabItem : ModItem
**CokeItem.cs**
  class CokeItem : ModItem
**CrimtaneSheetItem.cs**
  class CrimtaneSheetItem : ModItem
**DemoniteSheetItem.cs**
  class DemoniteSheetItem : ModItem
**GoldPlateItem.cs**
  class GoldPlateItem : ModItem
**GoldWireItem.cs**
  class GoldWireItem : ModItem
**MeteoritePanelItem.cs**
  class MeteoritePanelItem : ModItem
**PureSiliconItem.cs** uses: PureSiliconTile
  class PureSiliconItem : ModItem
**SiliconOreItem.cs** uses: SiliconOreTile
  class SiliconOreItem : ModItem
**SimpleCircuitItem.cs**
  class SimpleCircuitItem : ModItem
**SteelBarItem.cs**
  class SteelBarItem : ModItem
**SteelScrewItem.cs**
  class SteelScrewItem : ModItem

### Content/Items/Mounts/
**MechSuitItem.cs** uses: FactorrariaNet, MechSuitArmProjectile, MechSuitNet
  class MechSuitItem : ModItem
    +UseItem(Player)[ov]
  static class MechSuitTileSearch
    +TryFindNearestAnchorTile(Vector2,float,HashSet<Point>,out Point)[s]
  struct MechArmSlot
    +ProjectileIndex:int +AnchorTile:Point +IsActive:bool
  class MechSuitPlayer : ModPlayer
    +MaxArms:int[c] +IsActive:bool +Activate() +Deactivate() +SetReportedState(bool) +PlayerDisconnect()[ov] +SyncPlayer(int,int,bool)[ov] +PreUpdateMovement()[ov]

### Content/Items/Weapons/
**IceBladeItem.cs** uses: IceBladeSwingProjectile
  class IceBladeItem : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +Shoot(Item,Player,EntitySource_ItemUse_WithAmmo,Vector2,Vector2,int,int,float)[ov]

### Content/Items/Wires/
**CopperWireItem.cs** uses: CustomWireSystem, CustomWireType
  class CopperWireItem : ModItem
    +UseItem(Player)[ov]
**IronCutterItem.cs** uses: CopperWireItem, CustomWireSystem, CustomWireType, TinWireItem
  class IronCutterItem : ModItem
    +UseItem(Player)[ov]
**LeadCutterItem.cs** uses: CopperWireItem, CustomWireSystem, CustomWireType, TinWireItem
  class LeadCutterItem : ModItem
    +UseItem(Player)[ov]
**TinWireItem.cs** uses: CustomWireSystem, CustomWireType
  class TinWireItem : ModItem
    +UseItem(Player)[ov]

### Content/Liquids/Fuel/
**FuelLiquid.cs**
  class FuelLiquid : ModLiquid
    +LiquidMerge(int,int,int)[ov] +ChooseWaterfallStyle(int,int)[ov] +OnPlayerSplash(Player,bool)[ov] +OnNPCSplash(NPC,bool)[ov] +OnProjectileSplash(Projectile,bool)[ov] +OnItemSplash(Item,bool)[ov]
  class FuelLiquidFall : ModLiquidFall

### Content/Liquids/Oil/
**OilLiquid.cs**
  class OilLiquid : ModLiquid
    +LiquidMerge(int,int,int)[ov] +ChooseWaterfallStyle(int,int)[ov] +OnPlayerSplash(Player,bool)[ov] +OnNPCSplash(NPC,bool)[ov] +OnProjectileSplash(Projectile,bool)[ov] +OnItemSplash(Item,bool)[ov]
  class OilLiquidFall : ModLiquidFall

### Content/Particles/
**PuffExplosion.cs**
  sealed class PuffExplosion : Particle
    +AtlasTextureName:string[ov] +Update()[ov]

### Content/Projectiles/Hooks/
**MechanicalArmProjectile.cs** uses: MechanicalArmIK
  class MechanicalArmProjectile : ModProjectile
    +Load()[ov] +Unload()[ov] +CanDamage()[ov] +ShouldUpdatePosition()[ov] +AI()[ov] +OnTileCollide(Vector2)[ov] +PreDraw(ref Color)[ov]

### Content/Projectiles/Mounts/
**MechSuitArmProjectile.cs** uses: MechSuitPlayer, MechanicalArmIK
  class MechSuitArmProjectile : ModProjectile
    +AnchorTile:Point +ArmBendSign:float +Latched:bool +Load()[ov] +Unload()[ov] +CanDamage()[ov] +ShouldUpdatePosition()[ov] +AI()[ov] +PreDraw(ref Color)[ov]

### Content/Projectiles/Weapons/
**IceBladeSwingProjectile.cs** uses: PuffExplosion
  class IceBladeSwingProjectile : ModProjectile, IPixelatedPrimitiveRenderer
    +ShouldUpdatePosition()[ov] +AI()[ov] +RenderPixelatedPrimitives(SpriteBatch) +PreDraw(ref Color)[ov]

### Content/Tiles/Conveyors/
**ClockwisePriorityConveyorTile.cs** uses: VirtualItemSystem
  class ClockwisePriorityConveyorTile : ModTile
    +Texture:string[ov] +NumDust(int,int,bool,ref int)[ov] +AnimateTile(ref int,ref int)[ov] +RightClick(int,int)[ov] +PlaceInWorld(int,int,Item)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CanDrop(int,int)[ov]
**CounterClockwisePriorityConveyorTile.cs** uses: VirtualItemSystem
  class CounterClockwisePriorityConveyorTile : ModTile
    +Texture:string[ov] +NumDust(int,int,bool,ref int)[ov] +AnimateTile(ref int,ref int)[ov] +RightClick(int,int)[ov] +PlaceInWorld(int,int,Item)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CanDrop(int,int)[ov]

### Content/Tiles/Machines/ElectricalConsumers/Autohammer/
**AutohammerRecipeRegistry.cs** uses: ByriteAlloyItem, ByriteAlloySlabItem, CopperWireItem, CrimtaneSheetItem, CustomRecipe, DemoniteSheetItem, GoldPlateItem, GoldWireItem, IronCutterItem, LeadCutterItem, MechanicalArmItem, MechanicalAssemblerItem, MeteoritePanelItem, RecipeBook, SimpleCircuitItem, TinWireItem
  class AutohammerRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**AutohammerTileEntity.cs** ns!=Factorraria.Content.Tiles.Machines.Autohammer uses: AutohammerRecipeRegistry, ElectricConsumerMachine, RecipeBook
  class AutohammerTileEntity : ElectricConsumerMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] +PowerDemand:float[ov] #InputSlotCount:int[ov] #OutputSlotCount:int[ov] +Update()[ov] #OnAnimationFrameChanged(int,int)[ov]
**AutohammerUIState.cs** ns!=Factorraria.Content.Tiles.Machines.Autohammer uses: AutohammerRecipeRegistry, AutohammerTileEntity, ElectricityUIIcon, MachineUIElementEntry, MachineUIStateBase
  class AutohammerUIState : MachineUIStateBase
    #BasePanelSize:Vector2[ov] #BuildElements()[ov]

### Content/Tiles/Machines/ElectricalConsumers/Extractinator/
**ExtractinatorBlockPlayer.cs**
  class ExtractinatorBlockPlayer : ModPlayer
    +PreUpdate()[ov] +PostUpdate()[ov] +OnEnterWorld()[ov]
**ExtractinatorRecipeRegistry.cs** uses: SiliconOreItem
  class ExtractinatorDrop
    +ItemType:int +Percent:double +Min:int +Max:int
  class ExtractinatorRecipe
    +Input:int +Drops:List<ExtractinatorDrop> +Drop(int,double,int,int) +TotalPercent:double +TryRoll(out int,out int)
  static class ExtractinatorRecipeRegistry
    +Recipes:IReadOnlyDictionary<int, ExtractinatorRecipe>[s] +TryGet(int,out ExtractinatorRecipe)[s] +BuildRecipes()[s]
**ExtractinatorTileEntity.cs** uses: ElectricConsumerMachine, ExtractinatorRecipe, ExtractinatorRecipeRegistry, VirtualItemSystem
  class ExtractinatorTileEntity : ElectricConsumerMachine
    +ValidTileType:int[ov] +PowerDemand:float[ov] #InputSlotCount:int[ov] #WorkDuration:int[ov] +Update()[ov] #TryGetIntakeSlot(int,out int,out int)[ov]
**ExtractinatorUIState.cs** uses: ElectricityUIIcon, ExtractinatorTileEntity, MachineUIElementEntry, MachineUIStateBase
  class ExtractinatorUIState : MachineUIStateBase
    #BuildElements()[ov]

### Content/Tiles/Machines/ElectricalConsumers/MechanicalAssembler/
**MechanicalAssemblerRecipeRegistry.cs** uses: CopperWireItem, CustomRecipe, GoldPlateItem, GoldWireItem, PureSiliconItem, RecipeBook, SimpleCircuitItem, SteelBarItem, SteelScrewItem, TinWireItem
  class MechanicalAssemblerRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**MechanicalAssemblerTile.cs** uses: MechanicalAssemblerItem, MechanicalAssemblerTileEntity
  class MechanicalAssemblerTile : ModTile
    +Texture:string[ov]
**MechanicalAssemblerTileEntity.cs** uses: ElectricConsumerMachine, MechanicalAssemblerRecipeRegistry, MechanicalAssemblerTile, RecipeBook
  class MechanicalAssemblerTileEntity : ElectricConsumerMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] +PowerDemand:float[ov] #OutputSlotCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
**MechanicalAssemblerUIState.cs** uses: ElectricityUIIcon, MachineUIElementEntry, MachineUIStateBase, MechanicalAssemblerRecipeRegistry, MechanicalAssemblerTileEntity
  class MechanicalAssemblerUIState : MachineUIStateBase
    #BuildElements()[ov]

### Content/Tiles/Machines/ElectricalConsumers/RoboticArm/
**RoboticArmAreaEditorSystem.cs** uses: MachineUISystem, RoboticArmTileEntity
  class RoboticArmAreaEditorSystem : ModSystem
    +MoveHandleOffset:Vector2 +ResizeHandleOffset:Vector2 +TexturePath:string +DraggingHandle:bool +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
    enum Handle
      {None, Move, Resize}
**RoboticArmDropCapture.cs** uses: RoboticArmTileEntity
  class RoboticArmDropCaptureSystem : ModSystem
    +Active:RoboticArmTileEntity +Run(RoboticArmTileEntity,Action) +Unload()[ov]
  class RoboticArmDropCapture : GlobalItem
    +OnSpawn(Item,IEntitySource)[ov]
**RoboticArmMotion.cs**
  class RoboticArmMotion
    +TrackSmoothTime:float +BendSmoothTime:float +IdleOffsetX:float +IdleOffsetY:float +SideDeadZone:float +ThrustDistance:float +ImpactPoint:float +HoldBackMax:float +GripDistance:float +InPositionDistance:float +Initialized:bool +Side:int +Head:Vector2 +Bend:float +Target:Vector2? +HasTarget:bool +Striking:bool +InPosition:bool +StrikeProgress:float +SetTarget(Vector2?) +StartStrike(int) +Update(Vector2,float,float,Item,bool)
**RoboticArmTile.cs** uses: MechanicalArmItem, RoboticArmTileEntity
  class RoboticArmTile : ModTile
    +Texture:string[ov] +Slope(int,int)[ov]
**RoboticArmTileEntity.cs** uses: ElectricConsumerMachine, IMachineDynamicDraw, RoboticArmMotion, RoboticArmTile, RoboticArmVisual, RoboticArmWorker, VirtualItemSystem
  class RoboticArmTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    +ValidTileType:int[ov] +PowerDemand:float[ov] #InputSlotCount:int[ov] #OutputSlotCount:int[ov] +UpperArmLength:float +ForearmLength:float +Reach:float +Shoulder:Vector2 +Visual:RoboticArmVisual +Motion:RoboticArmMotion +AreaX:int +AreaY:int +AreaW:int +AreaH:int +ReachTiles:int +AreaLimit:int +AreaTiles:Rectangle +SetArea(int,int,int,int) +IsTileInReach(int,int) +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +Update()[ov] +IsTool(Item) +IsPlaceable(Item) +IsAcceptedItem(Item) #IntakeTiles()[ov] #TryGetIntakeSlot(int,out int,out int)[ov] +CanDeliverDrops:bool +RouteDrop(Item) +DynamicUpdate() +TryGetDynamicBounds(out Vector2,out float) +DynamicDraw(SpriteBatch) +DynamicDrawBehindTiles(SpriteBatch)
**RoboticArmUIState.cs** uses: ElectricityUIIcon, MachineUIElementEntry, MachineUIStateBase, RoboticArmAreaEditorSystem, RoboticArmTileEntity, UIItemSlotWrapper
  class RoboticArmUIState : MachineUIStateBase
    #BuildElements()[ov]
**RoboticArmVisual.cs** uses: MechanicalArmIK, RoboticArmMotion
  class RoboticArmVisual
    +AimSmoothing:float +IdleAimTilt:float +SwingArc:float +SwingEndAngle:float +ClawPivotX:float +ClawPivotY:float +BlockScale:float +BeamThickness:float +JointScale:float +Update(RoboticArmMotion,Vector2,float,float,Item,bool) +DrawBehind(SpriteBatch,Vector2,Item,bool)
**RoboticArmWorker.cs** uses: BaseMachine, PureSiliconTile, RoboticArmDropCaptureSystem, RoboticArmMotion, RoboticArmTileEntity, SiliconOreTile, TileEntityHelper
  class RoboticArmGhostSystem : ModSystem
    +Unload()[ov]
  class RoboticArmWorker
    +RescanInterval:int +MinStrikeTicks:int +ReachTimeoutTicks:int +BlockedCooldownTicks:int +TreeStickyColumns:int +FootprintMustFitArea:bool +PlatformsInOpenAir:bool +DebugPlacement:bool +MaxStrikesPerTile:int +Tick()
    enum Phase
      {Idle, Reach, Strike}
    enum Job
      {Place, Pick, Axe, Wall}

### Content/Tiles/Machines/ElectricalConsumers/Solidifier/
**SolidifierRecipeRegistry.cs** ns!=Factorraria.Content.Tiles.Machines.Solidifier uses: CustomRecipe, LiquidTypeRegistry, RecipeBook
  static class SolidifierRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**SolidifierTileEntity.cs** ns!=Factorraria.Content.Tiles.Machines.Solidifier uses: ElectricConsumerMachine, RecipeBook, SolidifierRecipeRegistry
  class SolidifierTileEntity : ElectricConsumerMachine
    +ValidTileType:int[ov] +PowerDemand:float[ov] +Recipes:RecipeBook[ov] #InputLiquidCount:int[ov] #OutputSlotCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
**SolidifierUIState.cs** uses: ElectricityUIIcon, LiquidTankUIElement, MachineUIElementEntry, MachineUIStateBase, SolidifierTileEntity
  class SolidifierUIState : MachineUIStateBase
    #BasePanelSize:Vector2[ov] #BuildElements()[ov]

### Content/Tiles/Machines/ElectricalProducers/GelBurner/
**GelBurnerRecipeRegistry.cs** ns!=Factorraria.Content.Tiles.Machines.GelBurner uses: FuelTable
  static class GelBurnerRecipeRegistry
    +Fuels:FuelTable[s]
**GelBurnerTileEntity.cs** ns!=Factorraria.Content.Tiles.Machines.GelBurner uses: ElectricProducerMachine, FuelTable, GelBurnerRecipeRegistry
  class GelBurnerTileEntity : ElectricProducerMachine
    +ValidTileType:int[ov] +PowerSupply:float[ov] +GelFuels:FuelTable[s] #AcceptedFuels:FuelTable[ov] +Update()[ov] #OnAnimationFrameChanged(int,int)[ov]
**GelBurnerUIState.cs** ns!=Factorraria.Content.Tiles.Machines.GelBurner uses: ElectricityUIIcon, FireUIElement, GelBurnerTileEntity, MachineUIElementEntry, MachineUIStateBase
  class GelBurnerUIState : MachineUIStateBase
    #BasePanelSize:Vector2[ov] #BuildElements()[ov]

### Content/Tiles/Machines/GeneralMachines/DecayChamber/
**DecayChamberRecipeRegistry.cs** uses: CustomRecipe, LiquidTypeRegistry, RecipeBook
  static class DecayChamberRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**DecayChamberTileEntity.cs** uses: BaseMachine, DecayChamberRecipeRegistry, RecipeBook
  class DecayChamberTileEntity : BaseMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] #InputLiquidCount:int[ov] #OutputSlotCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
**DecayChamberUIState.cs** uses: DecayChamberRecipeRegistry, DecayChamberTileEntity, LiquidTankUIElement, MachineUIElementEntry, MachineUIStateBase
  class DecayChamberUIState : MachineUIStateBase
    #BuildElements()[ov]

### Content/Tiles/Machines/GeneralMachines/Furnace/
**FurnaceRecipeRegistry.cs** uses: CustomRecipe, FuelTable, PureSiliconItem, RecipeBook, SiliconOreItem
  static class FurnaceRecipeRegistry
    +Book:RecipeBook[s] +Fuels:FuelTable[s] +BuildRecipes()[s] +RegisterManualRecipes(RecipeBook)[s]
**FurnaceTileEntity.cs** uses: BaseMachine, FuelTable, FurnaceRecipeRegistry, RecipeBook
  class FurnaceTileEntity : BaseMachine
    +ValidTileType:int[ov] #AcceptedFuels:FuelTable[ov] +Recipes:RecipeBook[ov] #OutputSlotCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
**FurnaceUIState.cs** uses: FireUIElement, FurnaceRecipeRegistry, FurnaceTileEntity, MachineUIElementEntry, MachineUIStateBase
  class FurnaceUIState : MachineUIStateBase
    #BasePanelSize:Vector2[ov] +BasePanelOffset:Vector2[ov] #BuildElements()[ov] #UpdateLayout()[ov]

### Content/Tiles/Machines/GeneralMachines/Hellforge/
**HellforgeRecipeRegistry.cs** uses: CokeItem, CustomRecipe, FurnaceRecipeRegistry, LiquidFuelTable, LiquidTypeRegistry, PureSiliconItem, RecipeBook, SiliconOreItem, SteelBarItem
  static class HellforgeRecipeRegistry
    +Book:RecipeBook[s] +LiquidFuels:LiquidFuelTable[s] +BuildRecipes()[s]
**HellforgeTileEntity.cs** uses: BaseMachine, HellforgeRecipeRegistry, LiquidFuelTable, LiquidStack, LiquidTypeRegistry, RecipeBook
  class HellforgeTileEntity : BaseMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] #AcceptedLiquidFuels:LiquidFuelTable[ov] #OutputSlotCount:int[ov] +Update()[ov] #OnAnimationFrameChanged(int,int)[ov]
**HellforgeUIState.cs** uses: HellforgeRecipeRegistry, HellforgeTileEntity, MachineUIElementEntry, MachineUIStateBase
  class HellforgeUIState : MachineUIStateBase
    +BasePanelOffset:Vector2[ov] #BuildElements()[ov] #UpdateLayout()[ov]

### Content/Tiles/Machines/GeneralMachines/IceMachine/
**IceMachineRecipeRegistry.cs** ns!=Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine uses: CokeItem, CustomRecipe, LiquidTypeRegistry, RecipeBook
  static class IceMachineRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**IceMachineTileEntity.cs** ns!=Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine uses: BaseMachine, CustomRecipe, IceMachineRecipeRegistry, MachineBiome, RecipeBook
  class IceMachineTileEntity : BaseMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] #InputLiquidCount:int[ov] #OutputSlotCount:int[ov] #CanStartCraft(CustomRecipe)[ov] #OnAnimationFrameChanged(int,int)[ov]
**IceMachineUIState.cs** ns!=Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine uses: IceMachineTileEntity, LiquidTankUIElement, MachineUIElementEntry, MachineUIStateBase
  class IceMachineUIState : MachineUIStateBase
    #BasePanelSize:Vector2[ov] #BuildElements()[ov]

### Content/Tiles/Machines/GeneralMachines/LiquidDistillator/
**LiquidDistillatorRecipeRegistry.cs** uses: CustomRecipe, LiquidTypeRegistry, RecipeBook
  static class LiquidDistillatorRecipeRegistry
    +Book:RecipeBook[s] +BuildRecipes()[s]
**LiquidDistillatorTileEntity.cs** uses: BaseMachine, LiquidDistillatorRecipeRegistry, RecipeBook
  class LiquidDistillatorTileEntity : BaseMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] #InputLiquidCount:int[ov] #OutputLiquidCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
**LiquidDistillatorUIState.cs** uses: LiquidDistillatorTileEntity, LiquidTankUIElement, MachineUIElementEntry, MachineUIStateBase
  class LiquidDistillatorUIState : MachineUIStateBase
    #BuildElements()[ov]

### Content/Tiles/Machines/GeneralMachines/MagicMaterializer/
**AwakenedStatueEntries.cs** uses: AwakenedStatueRegistry
  static class AwakenedStatueEntries
    +Register(AwakenedStatueRegistry)[s]
**MagicMaterializerRegistry.cs** uses: LiquidFuelTable, LiquidTypeRegistry
  static class MagicMaterializerRegistry
    +LiquidFuels:LiquidFuelTable[s] +BuildRecipes()[s]
**MagicMaterializerTileEntity.cs** uses: AwakenedStatueSystem, BaseMachine, IStatueAwakener, LiquidFuelTable, LiquidStack, LiquidTypeRegistry, MagicMaterializerRegistry
  class MagicMaterializerTileEntity : BaseMachine, IStatueAwakener
    +ValidTileType:int[ov] #AcceptedLiquidFuels:LiquidFuelTable[ov] +ShimmerDrainPerSecond:float +AwakenRadiusTiles:int +IsAwakening:bool +AwakenerOrigin:Point16 +AwakenedStatueCount:int +AwakenedEnemyCount:int +BackgroundActive:bool +HasShimmer:bool +OnRightClick(int,int)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +TreatsWorldLiquidAsInfinite(int)[ov] +OnKill()[ov] +Update()[ov]
**MaterializerRightClickGuard.cs** uses: MachineVisualRegistry, TileEntityHelper
  class MaterializerRightClickGuard : ModSystem
    +Load()[ov] +Unload()[ov]
**MaterializerWireGuard.cs** uses: MachineVisualRegistry
  class MaterializerWireGuard : GlobalTile
    +PreHitWire(int,int,int)[ov]

### Content/Tiles/Machines/GeneralMachines/Motors/
**MotorMK1Tile.cs** uses: MotorMK1Item, MotorMK1TileEntity, MotorTileBase
  class MotorMK1Tile : MotorTileBase<MotorMK1TileEntity>
**MotorMK1TileEntity.cs** uses: MotorMK1Tile, MotorTileEntityBase
  class MotorMK1TileEntity : MotorTileEntityBase
    +ValidTileType:int[ov] +PumpStrength:float[ov] +PowerDemand:float[ov] +PipeEquivalentMaxFlowRate:float[ov] #UpdateMotor()[ov] #OnAnimationFrameChanged(int,int)[ov]

### Content/Tiles/Machines/GeneralMachines/Pipes/
**PipeMK1Tile.cs** uses: PipeMK1Item, PipeTileBase
  class PipeMK1Tile : PipeTileBase
    #MaxFlowRate:float[ov]

### Content/Tiles/Ores/
**PureSiliconTile.cs** uses: PureSiliconItem
  class PureSiliconTile : ModTile
**SiliconOreTile.cs** uses: SiliconOreItem
  class SiliconOreTile : ModTile

## Non-code files (png omitted; sprites follow <Name>_On/_Off/_On_FirstLiquid naming)
- (root)/: .gitattributes, .gitignore, LICENSE; Factorraria.csproj; PROJECT_CONTEXT.md; build.txt, description.txt, description_workshop.txt
- .cursor/rules/: factorraria.mdc, liquids-networks.mdc, machines.mdc, tml-api.mdc
- .cursor/tml/: EXAMPLEMOD_INDEX.md, TML_API_INDEX.md, TML_MAP.md
- Assets/Atlases/: Particles_Atlas.json
- Localization/: en-US.hjson, en-US_Mods.Factorraria.hjson
- Properties/: launchSettings.json
- docs/multiplayer/: ROADMAP.md, SURVEY_APPENDIX.md
- tools/: patch_engine.ps1
