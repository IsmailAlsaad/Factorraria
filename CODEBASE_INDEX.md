# CODEBASE INDEX - Factorraria
generated 2026-10-26 | 287 .cs files | regenerate: `python index_codebase.py` (project root)

Legend: `+` public `#` protected `~` internal | s=static ab=abstract v=virtual ov=override c=const | `Name(Types)` method, `Name:Type` field/prop | `uses:` other project types used in file | `ns!=X` namespace differs from folder path (matters for `using`) | no line numbers: grep the name | private members and SetDefaults/SetStaticDefaults omitted

## Most-referenced types (files referencing them)
FactorrariaNet(60) BaseMachine(43) LiquidTypeRegistry(31) FactorrariaPacket(31) PacketDirection(27) RecipeBook(25) Cart(21) Factorraria(19) CustomRecipe(17) LiquidStack(16) TileEntityHelper(15) MachineUIStateBase(15) MachineUIElementEntry(14) CartModule(14) VirtualItemSystem(13) LiquidNetworkSystem(13) JoinSync(13) CartSystem(13) RecipeOutputGroup(11) RecipeCatalog(11)

## Inheritance
Project bases -> derived:
- BaseMachine: DecayChamberTileEntity, ElectricConsumerMachine, ElectricProducerMachine, FurnaceTileEntity, HellforgeTileEntity, IceMachineTileEntity, LiquidDistillatorTileEntity, MagicMaterializerTileEntity
- BucketBase: FuelBucket, OilBucket
- ElectricConsumerMachine: AutohammerTileEntity, ExtractinatorTileEntity, MechanicalAssemblerTileEntity, MotorTileEntityBase, PowerPoleTileEntityBase, RoboticArmTileEntity, SolidifierTileEntity
- ElectricProducerMachine: GelBurnerTileEntity
- IElectricConsumer: ElectricConsumerMachine
- IElectricProducer: ElectricProducerMachine
- IMachineDynamicDraw: ExtractinatorTileEntity, RoboticArmTileEntity
- IMotorTile: MotorTileBase
- IStatueAwakener: MagicMaterializerTileEntity
- ITilePlacedConsumer: AwakenedStatuePlacedConsumer, ChestConveyorPlacedConsumer, LiquidTopologyPlacedConsumer, MachinePlacedConsumer, MotorPlacedConsumer, PipePlacedConsumer, PlacementLogConsumer, PriorityConveyorPlacedConsumer
- IZoomScalable: RecipeBrowserPanel
- MachineUIStateBase: AutohammerUIState, DecayChamberUIState, ExtractinatorUIState, FurnaceUIState, GelBurnerUIState, HellforgeUIState, IceMachineUIState, LiquidDistillatorUIState, MagicMaterializerUIState, MechanicalAssemblerUIState, PowerPoleUIState, RoboticArmUIState, SolidifierUIState
- MotorTileBase: MotorMK1Tile
- MotorTileEntityBase: MotorMK1TileEntity
- PipeTileBase: PipeMK1Tile
- PowerPoleTileBase: PlatinumPowerPoleTile, WoodenPowerPoleTile
- PowerPoleTileEntityBase: PlatinumPowerPoleTileEntity, WoodenPowerPoleTileEntity
tModLoader/other bases -> project types:
- ModSystem (76): AwakenedSpawnSystem, AwakenedStatueDrawSystem, AwakenedStatueNetSystem, AwakenedStatueRegistry, AwakenedStatueSystem, BucketPourSystem, CartChestUISystem, CartInputNetSystem, CartMapHeadHider, CartMapHoverSystem, CartMapNetSystem, CartMapSystem, CartMountBlocker, CartNetSystem, CartPanelSystem, CartSoundSystem, CartSystem, CartTestFeed, CartWorldGen, ChestConveyorModeUI, ChestConveyorNetSystem, ChestConveyorSystem, ChestLootSystem, ConveyorFilterNetSystem, ConveyorPhysicsSystem, CustomWireNetSystem, CustomWireSystem, ElectricityHoverTip, GhostDiagnosticsSystem, JoinSyncSystem, LiquidBroadcastSystem, LiquidNetworkDebugSystem, LiquidNetworkSystem, LiquidTypeRegistrationSystem, MachineBootstrapSystem, MachineDetailSystem, MachineDynamicDrawSystem, MachineLightSyncSystem, MachineOverhangSystem, MachinePlacementInfoSystem, MachineRecipeSelectSystem, MachineRegisterationSystem, MachineSlotEditSystem, MachineTransitionSystem, MachineUISystem, MaterializerRightClickGuard, MaterializerToggleSystem, MechSuitNetSystem, MeteorHeadHardmodeLootSystem, MotorRotateSystem, MultiplayerBootProbeSystem, NetCoreSystem, NetRateLimiterSystem, OilWorldGen, PostWorldGenEdits, PowerGridDebugSystem, PowerGridSystem, PowerPoleCrossboardSystem, RecipeBookKeybindSystem, RecipeBookSystem, RecipeKnowledgeNetSystem, RecipeKnowledgeSystem, RecipeSystem, RoboticArmAreaEditorSystem, RoboticArmAreaNetSystem, RoboticArmDeferredEditSystem, RoboticArmDropCaptureSystem, RoboticArmGhostSystem, RoboticArmNet, SiliconOreWorldGen, TilePlacedConsumersSystem, TilePlacedSystem, TimedCraftingSystem, VItemNetSystem, VirtualItemSystem, WorldLiquidSources
- ModItem (35): BucketBase, ByriteAlloyItem, ByriteAlloySlabItem, CartItem, ClockwisePriorityConveyor, ClosedScrollItem, CokeItem, CopperWireItem, CounterClockwisePriorityConveyor, CrimtaneSheetItem, CsharpLogoItem, DemoniteSheetItem, GoldPlateItem, GoldWireItem, IronCutterItem, IsmailsCaneItem, LeadCutterItem, MechSuitItem, MechanicalArmItem, MechanicalAssemblerItem, MeteorHeadStatueItem, MeteoritePanelItem, MotorMK1Item, PipeMK1Item, PlatinumPowerPoleItem, PureSiliconItem, RecipeParchmentItem, SiliconOreItem, SimpleCircuitItem, SteelBarItem, SteelScrewItem, TerrariaLogoItem, TinWireItem, TmodloaderLogoItem, WoodenPowerPoleItem
- byte (18): CartAction, CartMapMode, CartPanelAction, CartPanelCloseReason, CartSoundKind, CustomWireType, DetailCloseReason, EditFlags, EditFlags, FactorrariaPacket, FlowDir, KnowledgeKind, MachineSlotKind, MachineTransition, PacketDirection, ParchmentFailure, PendingKind, RequestCheck
- ModCommand (14): ArmSendCommand, CartDiagnosticsCommand, FlowNetworkCommand, GhostCheckCommand, LiquidBroadcastCommand, LiquidPipeCommand, MachineSnapshotCommand, NetFloodCommand, NetJoinCommand, NetPingCommand, RecipeDebugCommand, SlotEditTestCommand, VItemTestCommand, WireCountCommand
- UIElement (12): ElectricityUIIcon, FireUIElement, LiquidTankUIElement, RecipeBookButton, RecipeBookDetail, RecipeBookDragHandle, RecipeBookEntry, RecipeBookHeaderRow, RecipeBookPanel, RecipeSearchBar, RecipeSelectHammerIcon, UIItemSlotWrapper
- ModTile (11): ClockwisePriorityConveyorTile, CoalTile, CounterClockwisePriorityConveyorTile, MechanicalAssemblerTile, MeteorHeadStatueTile, MotorTileBase, PipeTileBase, PowerPoleTileBase, PureSiliconTile, RoboticArmTile, SiliconOreTile
- ModPlayer (9): CartEquipPlayer, CartInteractionPlayer, ExtractinatorBlockPlayer, FactorrariaPlayerSync, MechSuitPlayer, MechanicalArmPlayer, RecipeBookKeybindPlayer, RecipeBookPlayer, TimedCraftingPlayer
- GlobalItem (8): BucketPourGlobalItem, CartModuleItemGlobal, DecayChamberItemTooltip, MagicMaterializerItemTooltip, RoboticArmDropCapture, VItemGlobalItem, VanillaCartItemGlobal, VanillaConveyorGlobalItem
- GlobalTile (7): AwakenedStatueGlobalTile, ChestConveyorGlobalTile, LiquidTopologyGlobalTile, MachineVisualOverride, MachineWireGlobalTile, MaterializerWireGuard, TilePlacedGlobalTile
- GlobalNPC (4): AwakenedSpawnGlobalNPC, BossLootGlobal, CartPassengerGlobalNPC, MeteorHeadStatueGlobalNPC
- UIState (4): CartChestUIState, MachineUIStateBase, RecipeBookButtonState, RecipeBookState
- ModConfig (3): CartMapConfig, FurnaceOffsetConfig, NetworkConfig
- ModLiquid (2): FuelLiquid, OilLiquid
- ModLiquidFall (2): FuelLiquidFall, OilLiquidFall
- ModProjectile (2): MechSuitArmProjectile, MechanicalArmProjectile
- UIPanel (2): RecipeBrowserPanel, RecipeElement
- UIScrollbar (2): BookScrollbar, ZoomScrollbar
- GlobalLiquid (1): LiquidInteractionEdits
- GlobalProjectile (1): CartTrackHookBlocker
- GlobalWall (1): GlobalWallOverride
- IEquatable (1): RecipeOutputKey
- IItemDropRuleCondition (1): FirstKingSlimeKillCondition
- Mod (1): Factorraria
- ModMapLayer (1): CartMapLayer
- ModTileEntity (1): BaseMachine
- Particle (1): PuffExplosion

## Files

### (root)/
**Factorraria.cs** uses: FactorrariaNet, Handle
  class Factorraria : Mod
    +HandlePacket(BinaryReader,int)[ov]

### Common/Carts/
**Cart.cs** uses: CartCargoNet, CartChain, CartPassengers, CartSkinTable, CartSkins, CartSound, CartSoundKind, Factorraria, FuelLiquid, FuelModule, GelBurnerRecipeRegistry, LiquidStack, LiquidTypeRegistry, Record, SkinAnimation, TrackData, TrackEnd, TrackSample
  enum CartModule
    {Empty, Chest, Terrarium, Motor}
  static class CartPhysics
    +WheelHalfBase:float[c] +HitboxWidth:int[c] +HitboxHeight:int[c] +MaxSpeed:float[c] +Drag:float[c] +BoostAccel:float[c] +ShoveSpeed:float[c] +MaxStep:float[c] +Gravity:float[c] +MaxFallSpeed:float[c] +GroundFriction:float[c] +RerailDelay:int[c] +RerailLandedReach:float[s] +DerailGuardRadius:float[s] +RerailStepSlack:float[s] +DerailTurnEase:float[s] +DerailMaxTilt:float[s] +BumpCooldown:int[c] +CollisionRestitution:float[c] +CollisionMinSpeed:float[c] +AnimStopSpeed:float[c] +AnimPixelsPerFrame:float[c] +AnimMaxFramesPerTick:float[c] +PickupPadding:float[c] +SpillGrabCooldown:int[c] +TerrariumDrainPerTick:int[c] +MaxCarriages:int[c] +ChainSpacing:float[c] +ChainSampleStep:float[c] +ChainCloseGapSpeed:float[c] +CouplingMaxRelativeSpeed:float[c] +MotorSpeedCap:float[c] +MotorAccel:float[c] +PassengerPickupCooldown:int[s] +PassengerGraceTicks:int[s] +PassengerPickupPadding:float[s] +PassengerPickupRange:float[s] +PassengerFloorLift:float[s] +PassengerWallHeight:float[s] +PassengerMinVisible:float[s] +PassengerMinInside:float[s] +PassengerLaunchMinSpeed:float[s] +PassengerLaunchMaxSpeed:float[s] +PassengerLaunchLift:float[s] +PassengerRestoreTries:int[s] +PassengerRestoreRange:float[s]
  class Cart
    +Skin:string +SkinType:int +Module:CartModule +PassengerNpc:int +PendingPassenger:TagCompound +PassengerRestoreTries:int +ModuleItem:int +IsChestItem(Item)[s] +IsTerrariumItem(Item)[s] +ChestSlots:int[c] +TryAddToChest(Item,bool) +ChestItems:Item +SlotFilter:int +IsSlotFiltered(int) +HasFilterFor(int) +SlotAccepts(int,Item) +FilterKeepCount(int,Item) +SetFilterFromSlots() +NormalizeTank() +OverflowItems:List<Item> +Tank:LiquidStack +FuelSlotCap:int[c] +Fuel:Item +IsBurning:bool +FuelBurnFraction:float +AcceptsFuel(int)[s] +AcceptsFuel(Item)[s] +TryAddFuel(Item) +RestoreBurn(int,int) +IsLavaSkin(int)[s] +IsMechSkin(int)[s] +FuelLiquidOf(int)[s] +IsMotorSkin(int)[s] +UsesLava:bool +FuelLiquid:int +UsesLiquid:bool +IsFuelLiquid(int) +AlwaysRuns:bool +HasPanel:bool +SpeedMultiplier:float +AccelMultiplier:float +SpeedLimit:float +DriveSpeedCap:float +DriveAccel:float +LiquidPerTick:float +LiquidCapacity:float +SetModule(CartModule,int) +ModuleItemType(CartModule)[s] +CollectPickupItems() +Position:Vector2 +Speed:float +Velocity:Vector2 +OnTrack:bool +Rotation:float +Active:bool +Id:int +PlacedByPlayer:bool +Chain:CartChain +IsCarriage:bool +Body:Cart +CollisionMass:int +ChainSlack:float +ReplayScanIndex:int +LoadChainId:int +LoadChainSlot:int +Facing:int +AnimFrame:int +Bumped:event Action<Cart>[s] +Collided:event Action<Cart, Cart>[s] +FrameChanged:event Action<Cart, int, int>[s] +Driving:event Action<Cart>[s] +AlongSpeed:float +TryCollide(Cart,Cart)[s] +Hitbox:Rectangle +Update() +TickCarriage() +ReplayBump() +Shove(float) +TryRailPose(float,float,float,out float,out float)[s] +NetBurning:bool +NetBurnFraction:float +NetMotorId:int +NetSlot:int +HasNetState:bool +ApplyNetState(Vector2,Vector2,float,int,bool) +ClientStep() +Save() +Load(TagCompound)[s]
**CartCargo.cs** uses: Cart, CartCargoNet, CartChestUIState, CartGeometry, CartModule, CartPassengers, CartPhysics, CartSound, CartSoundKind, LiquidBroadcastSystem, LiquidStack, LiquidTypeRegistry, VirtualItem, VirtualItemSystem, WorldLiquidSources
  static class CartCargo
    +WindowColumns:int[c] +WindowRows:int[c] +Update(Cart)[s] +OnBump(Cart)[s] +GetWindow(Cart,out int,out int)[s] +PourTank(Cart)[s]
**CartChain.cs** uses: Cart, CartModule, CartPhysics, Direction, Record
  class CartChain
    +Coupled:event Action<Cart, Cart>[s] +Motor:Cart +Carriages:List<Cart> +SaveId:int +Count:int +SetReplicaCarriages(List<Cart>) +Attach(Cart,bool) +Remove(Cart) +Dissolve() +Record(Vector2,float,bool) +MarkBump() +UpdateCarriages() +AutoAttach(List<Cart>)[s] +RebuildFromSave(List<Cart>)[s]
    struct TrailSample
      +Position:Vector2 +Rotation:float +Direction:int +Bump:bool +Air:bool
**CartChestUI.cs** uses: Cart, CartModule, CartPanelNet, CartPhysics, CartSlotBinding, CartSlotEdit, CartSystem, Factorraria, FireUIElement, LiquidTankUIElement, MachineUISystem, RecipeBookSystem, UIItemSlotWrapper
  class CartChestUIState : UIState
    +Columns:int[c] +Rows:int[c] +Cart:Cart +Panel:UIElement +Zoom:float +PanelW:float +PanelH:float +GapAboveCart:float +OnInitialize()[ov] +SetZoomScale(float) +Update(GameTime)[ov]
    sealed class Entry
      +Element:UIElement +BasePosition:Vector2 +BaseSize:Vector2 +Button:UITextPanel<string> +Text:string
  class CartChestUISystem : ModSystem
    +IsOpen:bool[s] +OpenCart:Cart[s] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +Toggle(Cart)[s] +Close(bool)[s] +NotifyCartRemoved(Cart)[s] +TryShiftInsert(Item,int)[s] +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**CartEquipPlayer.cs** uses: FactorrariaNet
  class CartEquipPlayer : ModPlayer
    +PostUpdate()[ov]
**CartGeometry.cs** uses: Cart, CartPhysics
  static class CartGeometry
    +Right(Cart)[s] +Up(Cart)[s] +BoxCenter(Cart)[s] +GetCorners(Cart,Vector2)[s] +Contains(Cart,Vector2,float)[s]
**CartInteractionPlayer.cs** uses: BucketPour, Cart, CartChestUISystem, CartSkinTable, CartSystem
  class CartInteractionPlayer : ModPlayer
    +CanUseItem(Item)[ov] +ShiftClickSlot(Item,int,int)[ov]
**CartMapHeadHider.cs** uses: CartMapConfig, CartPassengerGlobalNPC
  class CartMapHeadHider : ModSystem
    +Load()[ov] +Unload()[ov]
**CartMapHoverSystem.cs** uses: Cart, CartChestUIState, CartMapMode, CartMapSettings, CartSystem
  class CartMapHoverSystem : ModSystem
    +HoveredCartId:int[s] +HoverZoom:float +GapAboveCursor:float +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +PostDrawFullscreenMap(ref string)[ov]
**CartMapIcons.cs** uses: CartMapConfig, CartPassengers, CartPhysics, Factorraria
  static class CartMapIcons
    +ModuleScalePercent:int[s] +NpcMaxSizePercentOfBody:int[s] +ModuleBottomPercent:int[s] +Get(int,int,int)[s] +Rebuild()[s] +Unload()[s]
**CartMapLayer.cs** uses: Cart, CartMapConfig, CartMapEntry, CartMapHoverSystem, CartMapIcons, CartMapNet, CartMapSystem, CartModule
  enum CartMapMode : byte
    {Off, All, MotorOnly}
  static class CartMapSettings
    +Mode:CartMapMode[s] +Cycle()[s] +Describe(CartMapMode)[s]
  class CartMapLayer : ModMapLayer
    +GetDefaultPosition()[ov] +Draw(ref MapOverlayDrawContext,ref string)[ov]
**CartMapSystem.cs** uses: CartMapConfig, CartMapIcons, CartMapMode, CartMapSettings
  class CartMapSystem : ModSystem
    +ButtonSize:int +Unload()[ov] +DrawButton(ref string)
**CartMountBlocker.cs** uses: Cart
  class CartMountBlocker : ModSystem
    +Load()[ov] +Unload()[ov] +IsTrackTile(int,int)[s] +Log(string)[s] +IsTrackRightClick(Player)[s]
  class CartTrackHookBlocker : GlobalProjectile
    +InstancePerEntity:bool[ov] +AppliesToEntity(Projectile,bool)[ov] +OnSpawn(Projectile,IEntitySource)[ov] +GrappleCanLatchOnTo(Projectile,Player,int,int)[ov]
**CartPassengerGlobalNPC.cs** uses: CartPassengers
  class CartPassengerGlobalNPC : GlobalNPC
    +InstancePerEntity:bool[ov] +CarrierCartId:int +PickupCooldown:int +GraceTicks:int +IsCarried:bool +PreAI(NPC)[ov] +PostAI(NPC)[ov] +PreDraw(NPC,SpriteBatch,Vector2,Color)[ov] +CheckActive(NPC)[ov] +CanHitPlayer(NPC,Player,ref int)[ov] +SendExtraAI(NPC,BitWriter,BinaryWriter)[ov] +ReceiveExtraAI(NPC,BitReader,BinaryReader)[ov]
**CartPassengers.cs** uses: Cart, CartGeometry, CartModule, CartPassengerGlobalNPC, CartPhysics, CartSound, CartSoundKind, CartSystem
  static class CartPassengers
    +CanCarry(NPC)[s] +GetPassenger(Cart)[s] +Update(Cart)[s] +OnBump(Cart)[s] +Release(Cart,bool)[s] +GetExtraLift(NPC)[s] +Pin(Cart,NPC)[s] +ClientTick()[s] +DrawingPassenger:a dedicated server never draws) bool[s] +CollectRiders()[s] +DrawRider(Cart)[s] +Save(Cart)[s]
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
**CartSystem.cs** uses: BucketPour, Cart, CartCargo, CartChain, CartChestUISystem, CartGeometry, CartInputNet, CartModule, CartNet, CartPassengers, CartPhysics, CartSkinTable, CartSkins, CartSound, CartSoundKind, FurnaceOffsetConfig, LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry, PlacedSprite, TrackData, TrackSample
  class CartSystem : ModSystem
    +Carts:List<Cart>[s] +NextCartId:int[s] +AssignId(Cart)[s] +FindById(int)[s] +Unload()[ov] +OnWorldLoad()[ov] +ClearWorld()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +ClientInterpolate()[s] +CheckPlacement(Player,int,int,out Vector2,out bool)[s] +SpawnCart(Vector2,int,CartModule)[s] +FindCartAtCursor(Player)[s] +GetInstallModule(Item,Cart,out CartModule)[s] +IsInInteractRange(Player,Cart,float)[s] +PickUp(Player,Cart)[s]
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
  class FirstKingSlimeKillCondition : IItemDropRuleCondition
    +CanDrop(DropAttemptInfo) +CanShowItemDropInUI() +GetConditionDescription()
**ChestConveyorGlobalTile.cs**
  class ChestConveyorGlobalTile : GlobalTile
**GlobalWallOverride.cs**
  class GlobalWallOverride : GlobalWall
    +Drop(int,int,int,ref int)[ov]
**LiquidTopologyGlobalTile.cs** uses: LiquidNetworkSystem, PipeTierRegistry
  class LiquidTopologyGlobalTile : GlobalTile
    +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov] +MarkIfNextToPipe(int,int)[s]

### Common/Knowledge/
**LiquidBiomeVisits.cs** uses: LiquidTypeRegistry, MachineBiome, RecipeKnowledgeNet, RecipeKnowledgeSystem
  static class LiquidBiomeVisits
    +PollTicks:int[s] +Register()[s] +Set(int,MachineBiome)[s] +Clear()[s] +HasRule(int)[s] +IsValidMarker(int)[s] +Poll(Player)[s]
**RecipeArtDefinitions.cs** uses: RecipeArtRegistry
  static class RecipeArtDefinitions
    +Register()[s]
**RecipeArtRegistry.cs**
  static class RecipeArtRegistry
    +Machine:string[c] +All:IReadOnlyList<Entry>[s] +Problems:IReadOnlyList<string>[s] +KeyFor(string)[s] +Contains(string)[s] +TryGet(string,out Entry)[s] +Register(string)[s] +Clear()[s]
    class Entry
      +Name:string +TexturePath:string +Key:string +Texture:Texture2D
**RecipeCatalog.cs** uses: FuelTable, LiquidFuelTable, RecipeArtRegistry, RecipeBook, RecipeKey, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeOutputKey
  static class RecipeCatalog
    +Register(string,RecipeBook)[s] +Clear()[s] +RegisterFuels(string,FuelTable,LiquidFuelTable,FuelGroup)[s] +FuelsOf(string)[s] +AllFuelItemTypes()[s] +EntriesWithArt()[s] +Exists(string)[s] +KeyFor(string,RecipeOutputGroup)[s] +KeyFor(string,RecipeOutputKey)[s] +NameOf(RecipeBook)[s] +Entries()[s] +TryResolve(string,out string,out RecipeOutputGroup)[s] +MachineOf(string)[s] +TryRollLocked(UnifiedRandom,out string)[s]
    class Entry
      +Machine:string +Group:RecipeOutputGroup +Key:string +Art:Entry
    class FuelEntry
      +Icon:RecipeOutputKey +LabelKey:string +Items:int
    class FuelGroup
      +LabelKey:string +IconItem:int +Members:HashSet<int>
**RecipeDebugCommand.cs** uses: ClosedScrollItem, Entry, RecipeBookButtonState, RecipeCatalog, RecipeKnowledgeSystem, RecipeParchmentItem, RecipeState, RecipeVisibility, ScrollPool, ScrollPools
  class RecipeDebugCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**RecipeKey.cs** uses: RecipeOutputGroup, RecipeOutputKey
  static class RecipeKey
    +For(RecipeOutputGroup)[s] +For(RecipeOutputKey)[s]
**RecipeKnowledgeSystem.cs**
  class RecipeKnowledgeSystem : ModSystem
    +IsUnlocked(string)[s] +Version:int[s] +IsCrafted(string)[s] +MarkCrafted(string)[s] +CraftedKeys:IReadOnlyCollection<string>[s] +ResetCrafted()[s] +Unlock(string)[s] +UnlockedKeys:IReadOnlyCollection<string>[s] +ResetUnlocked()[s] +SeenVersion:int[s] +IsSeen(int)[s] +LiquidMarker(int)[s] +IsLiquidSeen(int)[s] +SeenItems:IReadOnlyCollection<int>[s] +MarkSeen(int)[s] +ResetSeen()[s] +ReplaceAll(IEnumerable<string>,IEnumerable<string>,IEnumerable<int>)[s] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +OnWorldUnload()[ov] +Unload()[ov]
**ScrollPoolDefinitions.cs** uses: ByriteAlloyItem, ClockwisePriorityConveyor, CokeItem, CounterClockwisePriorityConveyor, GoldWireItem, LiquidTypeRegistry, MechanicalArmItem, MechanicalAssemblerItem, PipeMK1Item, ScrollPools, SimpleCircuitItem, SteelBarItem
  static class ScrollPoolDefinitions
    +Register()[s]
**ScrollPools.cs** uses: RecipeArtRegistry, RecipeCatalog, RecipeKnowledgeSystem, RecipeOutputKey
  class ScrollPool
    +Name:string +Hint:string +Entries:IReadOnlyList<Entry> +WithHint(string) +Add(string,int,float) +AddLiquid(string,int,float) +AddArt(string,float)
    struct Entry
      +Machine:string +Output:RecipeOutputKey +Weight:float +ArtName:string +CatalogKey:string
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
**LiquidTankRules.cs** uses: LiquidStack
  static class LiquidTankRules
    +FindSinkSlot(LiquidStack,int)[s]
**LiquidTextureCache.cs** uses: LiquidTypeDefinition, LiquidTypeRegistry
  static class LiquidRecolorer
    +Recolor(GraphicsDevice,Texture2D,Color,Color,float)[s]
  static class LiquidTextureCache
    +GetOrCreate(string,Texture2D,LiquidTypeDefinition,float)[s] +Clear()[s]
**MotorTileBase.cs** uses: IMotorTile, LiquidNetworkSystem, MotorTileEntityBase, PowerGridSystem
  abstract class MotorTileBase<TEntity> : ModTile, IMotorTile
    +CanPlace(int,int)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CreateDust(int,int,ref int)[ov]
**MotorTileEntityBase.cs** ns!=Factorraria.Common.Machines uses: Direction, ElectricConsumerMachine, Factorraria, LiquidNetworkSystem, MotorRotate, PipeTileBase
  abstract class MotorTileEntityBase : ElectricConsumerMachine
    +PumpStrength:float[ab] +PipeEquivalentMaxFlowRate:float[ab] +Facing:Direction +Update()[ov] #UpdateMotor()[v] +OnRightClick(int,int)[ov] #WriteSnapshotExtra(BinaryWriter)[ov] #ReadSnapshotExtra(BinaryReader)[ov] #DescribeSnapshotExtra()[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov]
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
**BaseMachine.cs** uses: CustomRecipe, FactorrariaNet, FuelChannel, FuelModule, FuelTable, IElectricConsumer, IElectricProducer, LiquidFuelChannel, LiquidFuelModule, LiquidFuelTable, LiquidIngredient, LiquidNetworkSystem, LiquidStack, LiquidTypeRegistry, MachineBiome, MachineDetail, MachineDetailSystem, MachineLightSync, MachineLightSyncSystem, MachinePlacement, MachinePlacementInfo, MachineSlotKind, MachineSnapshot, MachineTransition, MachineUIRegistry, MachineUISystem, MachineVisualDefinition, PowerGridSystem, RecipeBook, RecipeCatalog, RecipeIngredient, RecipeKnowledgeNet, RecipeOutputGroup, RecipeOutputKey, VItemStacking, VirtualItem, VirtualItemSystem
  abstract class BaseMachine : ModTileEntity
    +WireToggle:bool +isOn:bool +isWorking:bool #WorkProgress:int +CraftsFinished:int #WorkDuration:int[v] #OutputSlotCount:int[v] +SelectedRecipe:CustomRecipe +ManualGroup:RecipeOutputGroup +SetManualGroup(RecipeOutputGroup) #RecipeGroups:List<RecipeOutputGroup>[v] +OutputMaxStack:int +OutputBufferMultiplier:int +IngredientBufferMultiplier:int +InsideBiome:MachineBiome +cornerPosition:Point16 +MachineCenter:Vector2 +MachineWidth:int +MachineHeight:int +MachineInitialized:bool +SlotsOf(MachineSlotKind) +AcceptsSlotItem(MachineSlotKind,int,Item)[v] +GetSlotCap(MachineSlotKind,int,Item)[v] +OutputCapFor(int) #InputLiquidCount:int[v] #OutputLiquidCount:int[v] +Recipes:RecipeBook[v] #FuelPerCraft:int[v] #FuelChannels:FuelChannel[v] #AcceptedLiquidFuels:LiquidFuelTable[v] #LiquidFuelPerCraft:int[v] #LiquidFuelChannels:LiquidFuelChannel[v] #RecipeDuration(CustomRecipe)[v] +ResolveRecipeDuration(CustomRecipe) #CanStartCraft(CustomRecipe)[v] +TreatsWorldLiquidAsInfinite(int)[v] #AcceptsInputLiquid(int,int)[v] +FuelSlotIndex:int[c] +FirstIngredientSlot:int[c] +Fuel:FuelModule +FuelSlotCount:int +LiquidFuelCount:int #InputSlotCount:int[v] +IngredientStart:int +IngredientSlotCount:int #AutoIngredientCount:int[v] #ProgressiveIngredientSlots:bool[v] +ActiveIngredientCount:int +ExtraIngredientIndex:int +IsExtraIngredientSlot(int) +SettledIngredientCount:int #IntakeIngredientCount:int[v] +CanBeWireToggled:bool +ToggleByWire() +Update()[ov] #OnRecipeFinished(CustomRecipe)[v] +WorkFraction:float +GetFuelBurnFraction(int) +OnKill()[ov] +GetPendingOutputType(int) #IntakeTiles()[v] #TryGetIntakeSlot(int,out int,out int)[v] #GetPickupRecipes()[v] +IsOnConveyorFloor() +NotifyAnimationFrame(int) +OnPlaced(int,int)[v] +ResolveIncomingLiquid(int) +OnRightClick(int,int)[v] #OnAnimationFrameChanged(int,int)[v] +Transition:MachineTransition +TransitionStartTick:uint #OnStartup()[v] #OnShutdown()[v] ~UpdateVisualTransition(MachineVisualDefinition) +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +NetReceive(BinaryReader)[ov] +BuildSnapshot() #WriteSnapshotExtra(BinaryWriter)[v] #ReadSnapshotExtra(BinaryReader)[v] #DescribeSnapshotExtra()[v] +BuildLightState(int,uint) +ApplyLightState(BinaryReader) +LastDetailTick:uint +BuildDetail() +ApplyDetail(BinaryReader) +PowerNetDemand:float +PowerNetSupply:float +PowerNetLinked:bool +DescribeSnapshot() +Hook_AfterPlacement(int,int,int,int,int,int)[ov] +NetPlaceEntityAttempt(int,int)[ov] +OnNetPlace()[ov] +NearPlayerWidth:float +NearPlayerHeight:float +IsAnyPlayerNear(Vector2,float) +IsOnScreen(Vector2,float)[s] +DetermineBiomeAt(int,int)[s]
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
    +StackLimitRule:Func<int, int> +Add(int,int) +Contains(int) +UnitsOf(int) +StackLimit(int) +ItemTypes:IEnumerable<int> +Clear()
  class FuelChannel
    +Table:FuelTable +UnitsPerCraft:int
  class FuelModule
    +SlotIndex:int +UnitsPerCraft:int +Remaining:int +Capacity:int +CanAccept(int) +StackLimit(int) +HasFuelAvailable(Item) +TryEnsureFuel(Item) +ConsumeCraft() +Consume(int) +GetBurnFraction(float) +Restore(int,int)
  class LiquidFuelTable
    +Add(int,float) +Contains(int) +AmountPerUnit(int) +LiquidTypes:IEnumerable<int> +Clear()
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
**MachineOverhangSystem.cs** uses: BaseMachine, LiquidOverlayLayer, LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry, MachineCanvas, MachineTransition, MachineVisualDefinition, MachineVisualRegistry, TileEntityHelper
  class MachineOverhangSystem : ModSystem
    +Load()[ov] +Unload()[ov] +PostDrawTiles()[ov]
**MachineTransitionSystem.cs** uses: BaseMachine, MachineVisualDefinition, MachineVisualRegistry
  class MachineTransitionSystem : ModSystem
    +PostUpdateEverything()[ov]
**MachineVisualOverride.cs** uses: BaseMachine, Direction, Factorraria, FurnaceOffsetConfig, LiquidStack, LiquidTextureCache, LiquidTypeDefinition, LiquidTypeRegistry, MachinePlacement, MachineRemoval, MachineUIRegistry, MachineUISystem, MotorTileEntityBase, TileEntityHelper
  enum MachineTransition : byte
    {None, Startup, Shutdown}
  class LiquidOverlayLayer
    +SourceTexture:Asset<Texture2D> +GetLiquidStack:Func<BaseMachine, LiquidStack> +CacheKey:string +StartupTexture:Asset<Texture2D> +ShutdownTexture:Asset<Texture2D> +StartupCacheKey:string +ShutdownCacheKey:string +TryGetTexture(MachineTransition,out Texture2D,out string)
  class MachineCanvas
    +FootprintWidth:int +FootprintHeight:int +OnFrames:int +OffFrames:int +Padding:int +CanvasWidth:int +CanvasHeight:int +OriginX:float +OriginY:float +YOffset:float +Resolved:bool +Valid:bool +Error:string +CellSize:int +FrameStride:int +FrameCount(bool) +Resolve(Texture2D,Texture2D)
  class MachineVisualDefinition
    +GetEntity:Func<int, int, BaseMachine> +FindEntity:Func<int, int, BaseMachine> +ResolveEntity(int,int) +OnTexture:Asset<Texture2D> +OffTexture:Asset<Texture2D> +AltOnTexture:Asset<Texture2D> +UseAltOn:Func<BaseMachine, bool> +LiquidOverlays:List<LiquidOverlayLayer> +Canvas:MachineCanvas +TileType:int +StartupTexture:Asset<Texture2D> +ShutdownTexture:Asset<Texture2D> +GetTransitionFrameCount(MachineTransition) +GetTransitionTicksPerFrame(MachineTransition) +GetTransitionDuration(MachineTransition) +TryGetActiveTransition(BaseMachine,out Texture2D,out int,out MachineTransition)
  static class MachineVisualRegistry
    +Definitions:Dictionary<int, MachineVisualDefinition>[s] +Register<T>(int,string,string,string,string)[s] +RegisterAlternateOn(int,string,Func<BaseMachine, bool>)[s] +RegisterLiquidOverlay(int,string,Func<BaseMachine, LiquidStack>,string,string)[s] ~Warn(string)[s] +RegisterOverhang<T>(int,string,string,int,int,float,float,int,int,float,string,string)[s]
  class MachineVisualOverride : GlobalTile
    +PreDraw(int,int,int,SpriteBatch)[ov] +RightClick(int,int,int)[ov] +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov] +ModifyLight(int,int,int,ref float,ref float,ref float)[ov]
**MachineWireGlobalTile.cs** uses: BaseMachine, TileEntityHelper
  class MachineWireGlobalTile : GlobalTile
    +PreHitWire(int,int,int)[ov]
**RecipeVisibility.cs** uses: CustomRecipe, Entry, FuelEntry, FurnaceOffsetConfig, LiquidBiomeVisits, RecipeCatalog, RecipeIngredient, RecipeKnowledgeNet, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeOutputKey
  enum RecipeState
    {Hidden, DebugRevealed, FadedIngredient, FadedParchment, Crafted}
  static class RecipeVisibility
    +HeldVersion:int[s] +IsIngredient(int)[s] +BuildIngredientIndex()[s] +ClearIndex()[s] +InvalidateHeld()[s] +GetState(string,RecipeOutputGroup)[s] +GetState(string)[s] +Poll()[s] +IsItemHeld(int)[s] +IsHeld(string)[s] +IsNameRevealedInBrowser(string,RecipeOutputGroup,RecipeState)[s] +IsProductCrafted(RecipeOutputKey)[s] +IsIngredientRevealed(int)[s] +IsLiquidRevealed(int)[s] +IsMachineCrafted(string)[s] +IsFuelRevealed(string,FuelEntry)[s] +FindBookRecipeFor(RecipeOutputKey,string)[s] +InBook(RecipeState)[s]
**TileEntityHelper.cs** uses: FurnaceOffsetConfig, LiquidNetworkSystem, PowerGridSystem
  static class TileEntityHelper
    +TryGetEntityFromTile(int,int,out TileEntity,out Point16)[s] +GetOrCreateEntity<T>(int,int)[s] +AnimateTileEntity(SpriteBatch,Texture2D,int,int,float,int)[s] +GetAnimationFrame(Point16,int)[s]

### Common/Mounts/
**MechanicalArmIK.cs**
  static class MechanicalArmIK
    +SolveElbow(Vector2,Vector2,float,float,float,float)[s] +DrawSegment(SpriteBatch,Texture2D,Vector2,Vector2,Color,float)[s]

### Common/Net/
**AwakenedStatueNet.cs** uses: AwakenedStatue, AwakenedStatueEntry, AwakenedStatueRegistry, AwakenedStatueSystem, Entry, FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection
  class AwakenedStatueNetSystem : ModSystem
    +Load()[ov]
  static class AwakenedStatueNet
    +MaxJoinEntries:int[s] +MaxChangesPerPacket:int[s] +BroadcastDelta(List<Point16>,List<AwakenedStatue>)[s] +HandleChanged(BinaryReader,int)[s] +WriteAll(BinaryWriter)[s] +ReadAll(BinaryReader)[s]
**BucketPour.cs** uses: BaseMachine, BucketBase, Cart, CartModule, CartSystem, FactorrariaNet, FactorrariaPacket, LiquidStack, LiquidTypeRegistry, MachineRequest, PacketDirection, RequestCheck, TileEntityHelper
  static class BucketPour
    +BucketUnits:float[s] +Reset()[s] +TryGetBucketLiquid(Item,out int)[s] +IsBottomless(int)[s] +TryFindMachineTank(BaseMachine,int,out LiquidStack,out int)[s] +CanPourCart(Cart,int)[s] +TryPourAtCursor(Player,Item)[s] +HandleRequest(BinaryReader,int)[s] +HandleResult(BinaryReader,int)[s] +ClientTick()[s]
  class BucketPourGlobalItem : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +CanUseItem(Item,Player)[ov]
  class BucketPourSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
**CartCargoNet.cs** uses: FactorrariaNet
  static class CartCargoNet
    +ChestWorldWhole:int[s] +ChestWorldPartial:int[s] +ChestRefused:int[s] +FuelWorldWhole:int[s] +FuelWorldPartial:int[s] +ChestVItems:int[s] +FuelVItems:int[s] +ItemSyncs:int[s] +LiquidMarks:int[s] +PlatesHit:int[s] +NoteChestRefused(int,int)[s] +Describe()[s]
**CartDiagnostics.cs** uses: Cart, CartCargoNet, CartInputNet, CartModule, CartNet, CartPanelNet, CartSkins, CartSystem, FactorrariaNet
  class CartDiagnosticsCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
  class CartTestFeed : ModSystem
    +Start(Vector2)[s] +Stop()[s] +PostUpdateEverything()[ov] +OnWorldUnload()[ov]
**CartInputNet.cs** uses: Cart, CartModule, CartPhysics, CartSkinTable, CartSystem, FactorrariaNet, FactorrariaPacket, PacketDirection
  class CartInputNetSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
  enum CartAction : byte
    {Place, Install, Shove, PickUp}
  static class CartInputNet
    +ServerPlaced:int[s] +ServerInstalled:int[s] +ServerShoved:int[s] +ServerPickedUp:int[s] +ServerRefused:int[s] +ClientSent:int[s] +ClientAccepted:int[s] +ClientRefused:int[s] +ClientConsumed:int[s] +ClientConsumeMissed:int[s] +Reset()[s] +Describe()[s] +RequestPlace(Player,Item)[s] +RequestInstallOrShove(Item,Cart)[s] +RequestPickUp(Cart)[s] +HandleRequest(BinaryReader,int)[s] +HandleResult(BinaryReader,int)[s] +ClientTick()[s]
    sealed class Result
      +Action:CartAction +Accepted:bool +ItemType:int
**CartMapNet.cs** uses: Cart, CartModule, CartNet, CartPassengers, CartSkins, CartSystem, FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection
  sealed class CartMapEntry
    +Id:int +TileX:float +TileY:float +Module:CartModule +Facing:int +SkinType:int +ModuleItemType:int +PassengerNpcType:int +PassengerKey:string +SkinKey:string +ModuleItemKey:string
  static class CartMapNet
    +ResendIntervalTicks:int[s] +MaxEntries:int[s] +LocalRefreshTicks:int[s] +ServerSent:int[s] +ClientReceived:int[s] +Reset()[s] +OnDisconnect(int)[s] +Collect(List<CartMapEntry>)[s] +ServerTick()[s] +HandleSnapshot(BinaryReader,int)[s]
  class CartMapNetSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
**CartNet.cs** uses: Cart, CartChain, CartChestUISystem, CartModule, CartPassengers, CartSkins, CartSystem, Factorraria, FactorrariaNet, FactorrariaPacket, JoinSync, NetworkConfig, PacketDirection
  class CartNetSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
  static class CartNet
    +ServerDeltas:int[s] +ServerStates:int[s] +ServerRemoves:int[s] +ServerMovePackets:int[s] +ServerMoves:int[s] +ClientDeltas:int[s] +ClientCreated:int[s] +ClientUpdated:int[s] +ClientRemoves:int[s] +ClientMovePackets:int[s] +ClientMoves:int[s] +ClientMissed:int[s] +Reset()[s] +Describe()[s] +Subscribe()[s] +HandleSubscribe(BinaryReader,int)[s] +OnDisconnect(int)[s] +IsSubscribed(int)[s] +ServerTick()[s] +HandleDelta(BinaryReader,int)[s] +HandleMove(BinaryReader,int)[s] +ClientTick()[s]
    sealed class Known
      +Skin:string +Module:CartModule +ModuleItem:int +TankType:int +TankAmount:float +HasTank:bool +Burning:bool +MotorId:int +Slot:int +Position:Vector2 +Velocity:Vector2 +Rotation:float +Facing:int +OnTrack:bool
    sealed class ClientView
      +Known:Dictionary<int, Known> +ResetPending:bool
    sealed class StateEntry
      +Id:int +Skin:string +Module:CartModule +ModuleItemKey:string +HasTank:bool +TankType:int +TankAmount:float +Burning:bool +MotorId:int +Slot:int +Motion:MoveEntry
    sealed class MoveEntry
      +Id:int +Position:Vector2 +Velocity:Vector2 +Rotation:float +Facing:int +OnTrack:bool
    sealed class DeltaBatch
      +Reset:bool +States:List<StateEntry> +Removes:List<int>
    sealed class MoveBatch
      +Moves:List<MoveEntry>
**CartPanelEdit.cs** uses: Cart, CartModule, CartPanelAction, CartPanelNet, CartPanelSystem, CartSystem, FactorrariaNet, FactorrariaPacket, MachineSlotEdit, MachineSnapshot
  static class CartSlotEdit
    +ServerAccepted:int[s] +ServerRejected:int[s] +ServerInserts:int[s] +ServerInsertedItems:int[s] +ServerFilters:int[s] +ClientSent:int[s] +ClientConfirmed:int[s] +ClientRolledBack:int[s] +ClientInsertsSent:int[s] +ClientInsertsAccepted:int[s] +ClientInsertsRefused:int[s] +ClientInsertUnmatched:int[s] +WriteSlotItem(BinaryWriter,Item)[s] +ReadSlotItem(BinaryReader)[s] +IsBusy:bool[s] +PredictFuel(ref Item,Item,Item)[s] +SendEdit(Cart,bool,int,Item,Item,Item,Item,int)[s] +SendQuickTake(Cart,bool,int,Item)[s] +ClientInsert(Cart,Item,int)[s] +ClientSetFilter(Cart)[s] +ServerSlotEdit(int,int,BinaryReader)[s] +ServerInsert(int,int,BinaryReader)[s] +ServerSetFilter(int,int)[s] +HandleResult(BinaryReader,int)[s] +Update(int)[s] +ReapplyPending(Cart)[s] +Reset()[s]
    enum EditFlags : byte
      {None, QuickTake}
    enum PendingKind : byte
      {Slot, QuickTake, Insert}
    sealed class Pending
      +Seq:int +CartId:int +Kind:PendingKind +Fuel:bool +Index:int +SlotBefore:Item +SlotAfter:Item +CursorBefore:Item +CursorAfter:Item +FilterBefore:int +FilterAfter:int +InventorySlot:int +Tick:uint
    sealed class Result
      +CartId:int +Seq:int +Accepted:bool +Kind:PendingKind +Fuel:bool +Index:int +Slot:Item +Filter:int +Moved:int
**CartPanelNet.cs** uses: Cart, CartModule, CartSlotEdit, CartSystem, FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection
  enum CartPanelAction : byte
    {Open, Close, SlotEdit, Insert, SetFilter}
  enum CartPanelCloseReason : byte
    {Closed, Gone, OutOfReach, Dead, Full, Rejected}
  sealed class CartSlotBinding
    +GetCart:Func<Cart> +Fuel:bool +Index:int
  class CartPanelSystem : ModSystem
    +ScanIntervalTicks:int +MaxSubscribersPerCart:int +CloseRangeTiles:float +KeepReachExtraTiles:float +AnswerTimeoutTicks:int +Load()[ov] +OnOpenRequest(int,int) +OnCloseRequest(int,int) +IsSubscribed(int,Cart) +InKeepReach(Player,Cart) +Push(Cart) +PostUpdateEverything()[ov] +DescribeSubscriptions() +OnWorldLoad()[ov] +OnWorldUnload()[ov] +Unload()[ov]
    sealed class Subscriber
      +Player:int +SentVersion:uint
    sealed class Record
      +Cart:Cart +Id:int +Last:byte +Version:uint +Subscribers:List<Subscriber>
  static class CartPanelNet
    +ServerOpened:int[s] +ServerEnded:int[s] +ServerRefusedOpen:int[s] +ServerDetails:int[s] +ServerDetailBytes:long[s] +ClientReceived:int[s] +ClientApplied:int[s] +ClientMissed:int[s] +ClientFailed:int[s] +ClientClosedByServer:int[s] +NeedsDetail(Cart)[s] +ResetClient()[s] +ClientOpen(Cart)[s] +ClientClose(Cart)[s] +IsReady(int)[s] +TakeServerClose(int)[s] +HandleRequest(BinaryReader,int)[s] +BuildPayload(Cart)[s] +HandleDetail(BinaryReader,int)[s] +HandleClosed(BinaryReader,int)[s] +ClientTick()[s] +Describe()[s]
    sealed class DetailPacket
      +CartId:int +Bytes:byte
**CartSound.cs** uses: FactorrariaNet, FactorrariaPacket, PacketDirection
  enum CartSoundKind : byte
    {Bump, Collide, Couple, Pour, PressurePlate, PassengerPickup, PassengerRelease}
  static class CartSound
    +NearbyTiles:int[s] +MinTicksBetween:int[s] +Play(CartSoundKind,Vector2)[s] +ResetServerState()[s] +PlayLocal(CartSoundKind,Vector2)[s]
  class CartSoundSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
    struct Pending
      +Kind:CartSoundKind +Position:Vector2
**ChestConveyorNet.cs** uses: ChestConveyorEntry, ChestConveyorMode, ChestConveyorSystem, FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection
  class ChestConveyorNetSystem : ModSystem
    +Load()[ov]
  static class ChestConveyorNet
    +MaxJoinEntries:int[s] +BroadcastEntry(Point,bool,ChestConveyorMode)[s] +RequestCycle(Point)[s] +HandleCycleRequest(BinaryReader,int)[s] +HandleChanged(BinaryReader,int)[s] +WriteAll(BinaryWriter)[s] +ReadAll(BinaryReader)[s]
**ConveyorFilterNet.cs** uses: FactorrariaNet, FactorrariaPacket, JoinSync, PacketDirection, PriorityConveyorNetwork, VirtualItemSystem
  class ConveyorFilterNetSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov]
  static class ConveyorFilterNet
    +ServerAccepted:int[s] +ServerRefused:int[s] +ServerBroadcasts:int[s] +ClientSent:int[s] +ClientSnapshots:int[s] +Reset()[s] +Describe()[s] +MarkDirty()[s] +RequestSet(int,int,int)[s] +HandleRequest(BinaryReader,int)[s] +ServerTick()[s] +WriteSnapshot(BinaryWriter)[s] +HandleSnapshot(BinaryReader,int)[s] +ReadSnapshot(BinaryReader)[s] +ClientTick()[s]
**CustomWireNet.cs** uses: CopperWireItem, CustomWireSystem, CustomWireType, FactorrariaNet, FactorrariaPacket, GoldWireItem, JoinSync, PacketDirection, TinWireItem
  class CustomWireNetSystem : ModSystem
    +Load()[ov]
  static class CustomWireNet
    +MaxJoinWires:int[s] +RequestEdit(bool,int,int,CustomWireType)[s] +HandleEdit(BinaryReader,int)[s] +HandleChanged(BinaryReader,int)[s] +WriteAll(BinaryWriter)[s] +ReadAll(BinaryReader)[s]
  class WireCountCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**FactorrariaNet.cs** uses: Direction, Factorraria, FactorrariaPacket, Handle, NetRateLimiter, NetworkConfig, PacketDirection
  static class FactorrariaNet
    +ReachSlackTiles:float[s] +IsServerSide:bool[s] +HasLocalClient:bool[s] +IsSinglePlayer:bool[s] +IsOwner(Player)[s] +SyncPlayerNow(Player)[s] +SyncInventoryItem(Player,Item)[s] +InReach(Player,int,int)[s] +WriteItem(BinaryWriter,Item)[s] +ReadItem(BinaryReader)[s] +Register(FactorrariaPacket,PacketDirection,PacketHandler,int)[s] +Clear()[s] +Handle(BinaryReader,int)[s] +SendRequest(FactorrariaPacket,Action<BinaryWriter>)[s] +SendToClient(int,FactorrariaPacket,Action<BinaryWriter>)[s] +Broadcast(FactorrariaPacket,Action<BinaryWriter>,int)[s] +SendToNearby(Vector2,float,FactorrariaPacket,Action<BinaryWriter>,int)[s] +Log(string)[s]
    sealed class Route
      +Direction:PacketDirection +Handler:PacketHandler +Cost:int
**FactorrariaPacket.cs** uses: CartSound, MachineDetail, TilePlaced
  enum FactorrariaPacket : byte
    {Ping, PingReply, JoinRequest, JoinChunk, JoinDone, TilePlaced, MechSuitStateRequest, MechSuitState, WireEditRequest, WireChanged, KnowledgeDelta, SeenDelta, SeenReport, ReadParchmentRequest, ...}
  enum PacketDirection : byte
    {ToServer, ToClient}
**FactorrariaPlayerSync.cs** uses: JoinSync, MachineDetailSystem
  class FactorrariaPlayerSync : ModPlayer
    +OnEnterWorld()[ov] +PlayerDisconnect()[ov] +CopyClientState(ModPlayer)[ov] +SyncPlayer(int,int,bool)[ov] +SendClientChanges(ModPlayer)[ov]
**GhostDiagnostics.cs** uses: FactorrariaNet, FactorrariaPacket, MachinePlacement, PacketDirection, RoboticArmAreaNet, RoboticArmNet, RoboticArmWorker, VirtualItem, VirtualItemSystem
  static class GhostDiagnostics
    +RouteDropConversions:int[s] +WorldItemSyncs:int[s] +WorldItemSyncsInactive:int[s] +ChecksHandled:int[s] +GhostWorldItemsFound:int[s] +GhostVItemsFound:int[s] +OnRouteDrop(int,int,int,int,int,int)[s] +OnWorldItemSync(int,string)[s] +DescribeLocal()[s] +SendCheck(Player)[s] +HandleCheck(BinaryReader,int)[s]
  class GhostDiagnosticsSystem : ModSystem
    +Load()[ov]
  class GhostCheckCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
  class ArmSendCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
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
**LiquidBroadcast.cs** uses: FactorrariaNet, NetworkConfig
  class LiquidBroadcastSystem : ModSystem
    +Marked:int[s] +SentWater:int[s] +SentSquares:int[s] +PeakQueue:int[s] +TicksOverCap:int[s] +Mark(int,int)[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov] +Describe()[s]
  class LiquidBroadcastCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**LiquidPipeDiagnostics.cs** uses: FactorrariaNet, FlowNetwork, FlowNetworkDiagnostics, FlowSink, FlowSource, LiquidNetworkSystem, LiquidTypeRegistry, MachineVisualRegistry, MotorTileEntityBase, PipeTierRegistry, TileEntityHelper
  static class LiquidPipeDiagnostics
    +Summary()[s] +Report(bool)[s]
  class LiquidPipeCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**MachineBootstrapSystem.cs** uses: FactorrariaNet, MachinePlacement, MachineVisualRegistry, TileEntityHelper
  class MachineBootstrapSystem : ModSystem
    +ColumnsPerTick:int[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
**MachineDetail.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineRecipeSelect, MachineRequest, MachineSlotEdit, MachineUISystem, PacketDirection, RequestCheck, TileEntityHelper
  enum DetailCloseReason : byte
    {Closed, Killed, OutOfReach, Dead, Full, Rejected}
  class MachineDetailSystem : ModSystem
    +ScanIntervalTicks:int +MaxSubscribersPerMachine:int +KeepReachExtraTiles:float +Load()[ov] +OnSubscribeRequest(int,int,int,bool) +Push(BaseMachine) +IsSubscribed(int,BaseMachine) +NotifyMachineKilled(BaseMachine)[s] +OnPlayerLeft(int)[s] +PostUpdateEverything()[ov] ~WithinReach(Player,BaseMachine,float)[s] +DescribeSubscriptions() +OnWorldUnload()[ov] +Unload()[ov]
    sealed class Subscriber
      +Player:int +SentVersion:uint
    sealed class Record
      +Machine:BaseMachine +Corner:Point16 +Last:byte +Version:uint +Subscribers:List<Subscriber>
  static class MachineDetail
    +FreshTicks:int[s] +Opened:int[s] +Ended:int[s] +Rejected:int[s] +Sent:int[s] +SentBytes:long[s] +ClosedSent:int[s] +Received:int[s] +Applied:int[s] +Missed:int[s] +Failed:int[s] +ClosedReceived:int[s] +SendSubscribe(Point16,bool)[s] +HandleSubscribe(BinaryReader,int)[s] +HandleDetail(BinaryReader,int)[s] +HandleClosed(BinaryReader,int)[s] +Describe()[s]
**MachineLightSync.cs** uses: BaseMachine, CustomRecipe, FactorrariaNet, FactorrariaPacket, LiquidStack, LiquidTypeRegistry, MachineSnapshot, PacketDirection, RecipeBook
  class MachineLightSyncSystem : ModSystem
    +ScanIntervalTicks:int +ProgressStepTicks:int +RangeHalfWidth:float +RangeHalfHeight:float +MaxPacketBytes:int +PruneIntervalTicks:int +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
    sealed class Record
      +Last:byte +Version:uint +SentTo:uint +RecipeRef:CustomRecipe +RecipeFingerprint:uint
  static class MachineLightSync
    +PacketsSent:int[s] +EntriesSent:int[s] +BytesSent:long[s] +EntriesReceived:int[s] +Applied:int[s] +Missed:int[s] +Failed:int[s] +TankSteps:int[s] +HandleLightUpdate(BinaryReader,int)[s] +QuantizeUse(float)[s] +QuantizeAmount(LiquidStack)[s] +WriteTanks(LiquidStack,BinaryWriter)[s] +ReadTanks(LiquidStack,BinaryReader)[s] +FingerprintOrZero(CustomRecipe)[s] +FindRecipe(RecipeBook,uint)[s] +ClearCaches()[s]
    sealed class FingerprintMap
      +Count:int +ByFingerprint:Dictionary<uint, CustomRecipe>
**MachinePlacement.cs** uses: BaseMachine, FactorrariaNet, ITilePlacedConsumer, LiquidNetworkSystem, MachineVisualDefinition, MachineVisualRegistry, PowerGridSystem, TileEntityHelper
  static class MachinePlacement
    +RejectNextEntityRequest:bool[s] +ServerPlace(int,int,int)[s] +EnsureEntity(int,int)[s] +Share(BaseMachine)[s] +Register(BaseMachine)[s]
  sealed class MachinePlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
**MachinePlacementInfo.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineBiome, MachinePlacement, MachineVisualRegistry, PacketDirection, TileEntityHelper
  class MachinePlacementInfoSystem : ModSystem
    +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
  static class MachinePlacementInfo
    +WaitTicks:int[s] +MaxEntries:int[s] +SendBiome(int,int)[s] +SendBiomeForVanillaTile(int,int,int)[s] +OnServerPlaced(BaseMachine)[s] +HandleBiomeReport(BinaryReader,int)[s] +Prune()[s] +Clear()[s]
**MachineRecipeSelect.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineDetail, MachineDetailSystem, MachineRequest, PacketDirection, RecipeBook, RecipeCatalog, RecipeOutputGroup, RequestCheck
  static class MachineRecipeSelect
    +CorruptNextRequest:bool[s] +ServerAccepted:int[s] +ServerRejected:int[s] +ClientSent:int[s] +ClientRolledBack:int[s] +ResolveGroup(BaseMachine,string)[s] +Select(BaseMachine,RecipeOutputGroup)[s] +HandleRequest(BinaryReader,int)[s] +HandleResult(BinaryReader,int)[s] +Update(int)[s] +ReapplyPending(BaseMachine)[s] +Reset()[s] +Describe()[s]
    sealed class Pending
      +Seq:int +Corner:Point16 +Group:RecipeOutputGroup +Tick:uint
    sealed class Result
      +Corner:Point16 +Seq:int +Accepted:bool +HasKey:bool +Key:string
  class MachineRecipeSelectSystem : ModSystem
    +AnswerTimeoutTicks:int +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
**MachineRemoval.cs** uses: BaseMachine, FactorrariaNet, TileEntityHelper
  static class MachineRemoval
    +OnTileKilled(int,int)[s]
**MachineRequest.cs** uses: BaseMachine, MachineDetailSystem, MachinePlacement
  enum RequestCheck : byte
    {Ok, Ignored, Refused}
  static class MachineRequest
    +Validate(int,int,int,float,out BaseMachine,out Point16)[s]
**MachineSlotEdit.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineDetailSystem, MachineRequest, MachineSnapshot, PacketDirection, RequestCheck
  enum MachineSlotKind : byte
    {Input, Output}
  sealed class MachineSlotBinding
    +Machine:Func<BaseMachine> +Kind:MachineSlotKind +Index:int
  static class MachineSlotEdit
    +CorruptNextRequest:bool[s] +ServerAccepted:int[s] +ServerRejected:int[s] +ClientSent:int[s] +ClientConfirmed:int[s] +ClientRolledBack:int[s] +SameStack(Item,Item)[s] +ClampAfterClick(BaseMachine,MachineSlotKind,int,ref Item,Item,Item)[s] +GiveOverflow(Item)[s] +IsBusy:bool[s] +SendEdit(BaseMachine,MachineSlotKind,int,Item,Item,Item,Item,Item)[s] +SendQuickTake(BaseMachine,MachineSlotKind,int,Item)[s] +HandleRequest(BinaryReader,int)[s] +HandleResult(BinaryReader,int)[s] +Update(int)[s] +ReapplyPendingEdit(BaseMachine)[s] +Reset()[s] +Describe()[s]
    enum EditFlags : byte
      {None, QuickTake}
    sealed class Pending
      +Seq:int +Corner:Point16 +Kind:MachineSlotKind +Index:int +SlotBefore:Item +SlotAfter:Item +CursorBefore:Item +CursorAfter:Item +Overflow:Item +QuickTake:bool +Overlay:bool +Tick:uint
    sealed class Result
      +Corner:Point16 +Seq:int +Accepted:bool +Kind:MachineSlotKind +Index:int +Slot:Item
  class MachineSlotEditSystem : ModSystem
    +AnswerTimeoutTicks:int +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
**MachineSnapshot.cs** uses: BaseMachine, CustomRecipe, LiquidIngredient, LiquidStack, LiquidTypeRegistry, RecipeBook, RecipeCatalog, RecipeIngredient, RecipeOutputGroup, RecipeOutputKey
  static class MachineSnapshot
    +Sent:int[s] +SentBytes:long[s] +Received:int[s] +ReceivedBytes:long[s] +Failed:int[s] +WriteItems(Item,BinaryWriter)[s] +ReadItems(Item,BinaryReader)[s] +WriteLiquids(LiquidStack,BinaryWriter)[s] +ReadLiquids(LiquidStack,BinaryReader)[s] +WriteRecipes(BaseMachine,BinaryWriter)[s] +ReadRecipes(BaseMachine,BinaryReader)[s] +Fingerprint(CustomRecipe)[s] +SafeName(int)[s] +Digest(byte)[s] +DescribeItems(Item)[s] +DescribeLiquids(LiquidStack)[s] +DescribeRecipe(BaseMachine)[s] +CountSent(int)[s] +CountReceived(int)[s] +CountFailed()[s]
    sealed class GroupMap
      +Count:int +ByKey:Dictionary<string, RecipeOutputGroup>
**MachineSnapshotCommand.cs** uses: BaseMachine, FactorrariaNet, MachineDetail, MachineLightSync, MachinePlacement, MachineSnapshot
  class MachineSnapshotCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**MaterializerToggle.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineRequest, MagicMaterializerTileEntity, PacketDirection, RequestCheck
  static class MaterializerToggle
    +ServerChanged:int[s] +ServerRefused:int[s] +ClientSent:int[s] +ClientApplied:int[s] +Request(MagicMaterializerTileEntity)[s] +HandleRequest(BinaryReader,int)[s] +HandleChanged(BinaryReader,int)[s] +Update()[s] +Reset()[s] +Describe()[s]
  class MaterializerToggleSystem : ModSystem
    +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
**MechSuitNet.cs** uses: FactorrariaNet, FactorrariaPacket, MechSuitPlayer, PacketDirection
  class MechSuitNetSystem : ModSystem
    +Load()[ov]
  static class MechSuitNet
    +ReportState(bool)[s] +SendState(int,bool,int,int)[s] +HandleRequest(BinaryReader,int)[s] +HandleState(BinaryReader,int)[s]
**MotorRotate.cs** uses: BaseMachine, Direction, FactorrariaNet, FactorrariaPacket, LiquidNetworkSystem, MachineRequest, MotorTileEntityBase, PacketDirection, PipeTileBase, RequestCheck
  static class MotorRotate
    +ServerRotated:int[s] +ServerRefused:int[s] +ServerAlreadyThere:int[s] +ClientSent:int[s] +ClientApplied:int[s] +Request(MotorTileEntityBase)[s] +HandleRequest(BinaryReader,int)[s] +HandleChanged(BinaryReader,int)[s] +Update()[s] +Reset()[s] +Describe()[s]
    sealed class Change
      +Corner:Point16 +Facing:Direction
  class MotorRotateSystem : ModSystem
    +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
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
**RecipeKnowledgeNet.cs** uses: FactorrariaNet, FactorrariaPacket, JoinSync, LiquidBiomeVisits, PacketDirection, Pending, RecipeBookSystem, RecipeCatalog, RecipeKnowledgeSystem, RecipeParchmentItem, RecipeVisibility, ScrollPools
  class RecipeKnowledgeNetSystem : ModSystem
    +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov]
  enum ParchmentReadResult
    {Pending, Read, Failed}
  enum ParchmentFailure : byte
    {None, NoParchment, Exhausted, Unresolved, CannotRead}
  static class RecipeKnowledgeNet
    +SeenBatchTicks:int[s] +MaxSeenPerPacket:int[s] +ReadTimeoutTicks:int[s] +MaxPoolNameLength:int[s] +MaxJoinEntries:int[s] +ReadPending:bool[s] +ClearPending()[s] +ServerMarkCrafted(string)[s] +HandleKnowledgeDelta(BinaryReader,int)[s] +ReportSeen(int)[s] +ReportLiquidSeen(int)[s] +Update()[s] +HandleSeenReport(BinaryReader,int)[s] +HandleSeenDelta(BinaryReader,int)[s] +RequestReadParchment(string)[s] +HandleReadRequest(BinaryReader,int)[s] +HandleReadResult(BinaryReader,int)[s] +WriteAll(BinaryWriter)[s] +ReadAll(BinaryReader)[s]
    enum KnowledgeKind : byte
      {Unlocked, Crafted}
**RoboticArmAreaNet.cs** uses: BaseMachine, FactorrariaNet, FactorrariaPacket, MachineDetailSystem, MachineRequest, PacketDirection, RequestCheck, RoboticArmAreaEditorSystem, RoboticArmNet, RoboticArmTileEntity
  static class RoboticArmAreaNet
    +RejectNext:bool[s] +ServerAccepted:int[s] +ServerRejected:int[s] +ServerQueries:int[s] +ServerBroadcasts:int[s] +ClientSent:int[s] +ClientQueries:int[s] +ClientApplied:int[s] +ClientRestored:int[s] +Commit(RoboticArmTileEntity,Rectangle)[s] +RequestRefresh(RoboticArmTileEntity)[s] +HandleRequest(BinaryReader,int)[s] +HandleChanged(BinaryReader,int)[s] +Update(int)[s] +Reset()[s] +Describe()[s]
    sealed class Pending
      +Seq:int +Corner:Point16 +Tick:uint +IsQuery:bool
    sealed class Result
      +Corner:Point16 +Seq:int +Accepted:bool +HasArea:bool +H:int X, Y, W,
  class RoboticArmAreaNetSystem : ModSystem
    +AnswerTimeoutTicks:int +MinRequestSpacingTicks:int +Load()[ov] +PostUpdateEverything()[ov] +OnWorldUnload()[ov] +Unload()[ov]
**SlotEditTestCommand.cs** uses: MachineRecipeSelect, MachineSlotEdit, MaterializerToggle
  class SlotEditTestCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**TilePlaced.cs** uses: FactorrariaNet, FactorrariaPacket, MachinePlacementInfo
  interface ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  static class TilePlaced
    +Register(ITilePlacedConsumer)[s] +Clear()[s] +NotifyPlaced(int,int,int)[s] +RunConsumers(int,int,int,int)[s] +HandleRequest(BinaryReader,int)[s]
**TilePlacedConsumers.cs** ns!=Factorraria.Common.Liquids uses: AwakenedStatueRegistry, AwakenedStatueSystem, ChestConveyorSystem, ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyorTile, FactorrariaNet, ITilePlacedConsumer, LiquidNetworkSystem, LiquidTopologyGlobalTile, MachinePlacedConsumer, PipeTierRegistry, PowerGridSystem, TilePlaced, VirtualItemSystem
  interface IMotorTile
  class TilePlacedConsumersSystem : ModSystem
    +LogConsumers:bool[s] +Load()[ov] ~Log(string,int,int,int)[s]
  sealed class ChestConveyorPlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  sealed class LiquidTopologyPlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  sealed class AwakenedStatuePlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  sealed class PipePlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  sealed class MotorPlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
  sealed class PriorityConveyorPlacedConsumer : ITilePlacedConsumer
    +AppliesTo(int) +OnTilePlaced(int,int,int,int)
**TilePlacedGlobalTile.cs** uses: TilePlaced
  class TilePlacedGlobalTile : GlobalTile
    +PlaceInWorld(int,int,int,Item)[ov]
**TilePlacedSystem.cs** uses: FactorrariaNet, FactorrariaPacket, ITilePlacedConsumer, PacketDirection, TilePlaced
  class TilePlacedSystem : ModSystem
    +LogPlacements:bool[s] +Load()[ov] +Unload()[ov]
    sealed class PlacementLogConsumer : ITilePlacedConsumer
      +AppliesTo(int) +OnTilePlaced(int,int,int,int)
**VItemNet.cs** uses: ConveyorFilterNet, Factorraria, FactorrariaNet, FactorrariaPacket, JoinSync, NetworkConfig, PacketDirection, VirtualItem, VirtualItemSystem
  class VItemNetSystem : ModSystem
    +Load()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +PostUpdateEverything()[ov]
  static class VItemNet
    +ServerPackets:int[s] +ServerSpawns:int[s] +ServerUpdates:int[s] +ServerRemoves:int[s] +ClientPackets:int[s] +ClientSpawns:int[s] +ClientUpdates:int[s] +ClientRemoves:int[s] +ClientMissed:int[s] +Reset()[s] +Describe()[s] +Subscribe()[s] +HandleSubscribe(BinaryReader,int)[s] +OnDisconnect(int)[s] +ServerTick()[s] +HandleDelta(BinaryReader,int)[s] +ClientTick()[s]
    sealed class Known
      +Type:int +Stack:int +TileX:int +TileY:int +Step:float
    sealed class ClientView
      +Known:Dictionary<int, Known> +ResetPending:bool
    sealed class SpawnEntry
      +Id:int +Payload:Item +Position:Vector2 +TileX:int +TileY:int +Step:float
    sealed class UpdateEntry
      +Id:int +Stack:int +Payload:Item +Position:Vector2 +TileX:int +TileY:int +Step:float
    sealed class Batch
      +Reset:bool +Spawns:List<SpawnEntry> +Updates:List<UpdateEntry> +Removes:List<int>
**VItemTestCommand.cs** uses: FactorrariaNet, VirtualItemSystem
  class VItemTestCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]

### Common/Networks/
**FlowNetwork.cs** uses: BaseMachine, Direction, FlowDir, FlowNode, FlowPort, FlowRoute, FlowRouteFinder, FlowRouteMerger, FlowSolver, FlowTransfer, LiquidStack, LiquidTankRules, LiquidTypeRegistry, LiquidWorldBanks, MotorTileEntityBase, PipeConnectionHelper, PipeTierRegistry, TileEntityHelper, WorldLiquidSources
  sealed class FlowSource
    +Machine:BaseMachine +Tank:LiquidStack +TankIndex:int +WorldPos:Point +Owner:int +IsWorld:bool
  sealed class FlowSink
    +Machine:BaseMachine +WorldPos:Point +Owner:int +IsWorld:bool
  sealed class FlowNetwork
    +Positions:Point +Nodes:FlowNode +Motors:MotorTileEntityBase +PassRate:float +Sources:List<FlowSource> +Sinks:List<FlowSink> +SourceAttach:FlowPort +SinkAttach:FlowPort +SourcePortsOfAttach:int +SinkPortsOfAttach:int +Routes:List<FlowRoute> +Solver:FlowSolver +Cap:float +DecayPerClimbPerMinute:float +BuildMs:double +RefreshMs:double +RefreshRoutes() +TickFailed:bool +LastTransfers:IReadOnlyList<FlowTransfer> +Tick(LiquidWorldBanks)
  static class FlowNetworkBuilder
    +BuildAll(HashSet<Point>)[s]
    sealed class PortTables
      +Sources:List<FlowSource> +Sinks:List<FlowSink> +SourceAttach:List<FlowPort> +SinkAttach:List<FlowPort> +SourcePortsOfAttach:List<int > +SinkPortsOfAttach:List<int > +AttachMachine(int,Direction,BaseMachine) +AttachWorld(int,Direction,Point)
**FlowNetworkDiagnostics.cs** uses: BaseMachine, Factorraria, FactorrariaNet, FlowNetwork, FlowRoute, FlowSink, FlowSource, LiquidNetworkSystem, LiquidTypeRegistry
  static class FlowNetworkDiagnostics
    +Describe(FlowSource)[s] +Describe(FlowSink)[s] +Report(bool)[s]
  class FlowNetworkCommand : ModCommand
    +Command:string[ov] +Type:CommandType[ov] +Usage:string[ov] +Description:string[ov] +Action(CommandCaller,string,string)[ov]
**LiquidWorldBanks.cs** uses: Factorraria, LiquidBroadcastSystem, LiquidTypeRegistry
  sealed class LiquidWorldBanks
    +WithdrawBankCount:int +DepositBankCount:int +WorldSourceOffer(Point) +WorldSinkSpace(Point,int) +AddWithdraw(Point,float) +AddDeposit(Point,int,float) +FlushAll() +Clear()
    struct DepositBank
      +Amount:float +Type:int
**PowerNetwork.cs** ns!=Factorraria.Common.PowerGrid uses: BaseMachine, IElectricConsumer, IElectricProducer
  class PowerNetwork
    +electricConsumers:List<IElectricConsumer> +electricProducers:List<IElectricProducer> +Tick()
**PriorityConveyorNetwork.cs**
  class PriorityConveyorNetwork
    +Tiles:HashSet<Point> +FilteredItemId:int

### Common/Networks/Flow/
**FlowRouteFinder.cs** uses: FlowDir, FlowDirs, FlowNode, FlowPort, FlowRoute
  static class FlowRouteFinder
    +Build(FlowNode,FlowPort,FlowPort,float,float)[s]
**FlowRouteMerger.cs** uses: FlowDir, FlowDirs, FlowNode, FlowRoute
  static class FlowLinks
    +OnAxis(FlowNode,FlowDir)[s] +CanLink(FlowNode,FlowNode,FlowDir)[s]
  static class FlowRouteMerger
    +Merge(IReadOnlyList<FlowRoute>,int,int)[s]
**FlowSolver.cs** uses: FlowRoute, FlowTransfer, Route
  sealed class FlowSolver
    +ActiveType:int +Solve(IReadOnlyList<FlowRoute>,float,int,int,Func<int, int, float>,float,List<FlowTransfer>)
**FlowTypes.cs** uses: Route
  enum FlowDir : byte
    {Up, Down, Left, Right}
  static class FlowDirs
    +Dx:int[s] +Dy:int[s] +Opposite(FlowDir)[s] +SameAxis(FlowDir,FlowDir)[s]
  struct FlowNode
    +Y:int X, +IsMotor:bool +MotorOn:bool +Facing:FlowDir +Strength:float
  struct FlowPort
    +Node:int +Mouth:FlowDir +Owner:int
  sealed class FlowRoute
    +Dst:int Src, +Climbs:int +Pumps:int +RatePerMinute:float +RatePerTick:float
  struct FlowTransfer
    +Type:int Route, Src, Dst, +Amount:float

### Common/Statues/
**AwakenedSpawnGlobalNPC.cs** uses: AwakenedLootDrop, AwakenedStatue, AwakenedStatueEntry, AwakenedStatueSystem, CartPassengerGlobalNPC, Entry
  class AwakenedSpawnGlobalNPC : GlobalNPC
    +InstancePerEntity:bool[ov] +Tagged:bool +Origin:Point16 +DespawnDistanceTiles:float +Entry:AwakenedStatueEntry +Release() +CheckActive(NPC)[ov] +OnKill(NPC)[ov]
**AwakenedSpawnSystem.cs** uses: AwakenedSpawnGlobalNPC, AwakenedStatue, AwakenedStatueSystem, Entry, IStatueAwakener
  class AwakenedSpawnSystem : ModSystem
    +MaxEnemiesPerStatue:int +SpawnPassTicks:int +RefillDelayTicks:int +NpcSlotMargin:int +SpawnOffset:Vector2 +PostUpdateNPCs()[ov]
**AwakenedStatueDrawSystem.cs** uses: AwakenedStatue, AwakenedStatueSystem, BaseMachine, Entry
  class AwakenedStatueDrawSystem : ModSystem
    +OutlinePulseSpeed:float +OutlineScaleAmplitude:float +OutlineMinOpacity:float +PadLeft:int +PadRight:int +PadTop:int +PadBottom:int +Load()[ov] +Unload()[ov]
**AwakenedStatueGlobalTile.cs** uses: AwakenedStatueRegistry, AwakenedStatueSystem
  class AwakenedStatueGlobalTile : GlobalTile
    +PreHitWire(int,int,int)[ov] +KillTile(int,int,int,ref bool,ref bool,ref bool)[ov]
**AwakenedStatueRegistry.cs** uses: AwakenedStatueEntries
  class AwakenedLootDrop
    +ItemType:int +Chance:float +Min:int +Max:int
  class AwakenedStatueEntry
    +TileType:int +Style:int +FlipHorizontal:bool +NpcTypes:int +HardmodeNpcs:List<int> +Spawns(int) +PickNpcType() +AddHardmodeList(int) +Sprite:Asset<Texture2D> +Outline:Asset<Texture2D> +DrawOffset:Vector2 +Loot:List<AwakenedLootDrop> +AddLoot(int,float,int,int)
  class AwakenedStatueRegistry : ModSystem
    +RightFacingStyleOffset:int +IsStatueTile(int) +EntryCount:int +TryGet(int,int,out AwakenedStatueEntry) +Add(int,int,int,string,string,Vector2) +Add(int,int,int,string,string,Vector2,int) +PostSetupContent()[ov] +Unload()[ov]
**AwakenedStatueSystem.cs** uses: AwakenedSpawnGlobalNPC, AwakenedStatueEntry, AwakenedStatueNet, AwakenedStatueRegistry, Entry, IStatueAwakener
  class AwakenedStatue
    +Origin:Point16 +Entry:AwakenedStatueEntry +OwnerOrigin:Point16 +Width:int +Height:int +LiveEnemies:int +LastLiveEnemies:int +SpawnCooldownTicks:int +Center:Vector2
  class AwakenedStatueSystem : ModSystem
    +Awakened:IReadOnlyDictionary<Point16, AwakenedStatue> +OnWorldLoad()[ov] +OnWorldUnload()[ov] +QueueStatueCheck(int,int) +PostUpdateWorld()[ov] +IsAwakenedTile(int,int) +IsStillAwakened(AwakenedStatue) +RescanFor(IStatueAwakener) +ClearTagsFor(HashSet<Point16>) +SetReplicaStatue(Point16,bool,AwakenedStatueEntry) +ReplaceReplica(List<KeyValuePair<Point16, AwakenedStatueEntry>>) +CountStatuesInRadius(IStatueAwakener)
**IStatueAwakener.cs**
  interface IStatueAwakener
    +IsAwakening:bool +AwakenerOrigin:Point16 +AwakenRadiusTiles:int +AwakenedStatueCount:int +AwakenedEnemyCount:int

### Common/Systems/
**ChestConveyorSystem.cs** uses: ChestConveyorNet, VItemStacking, VirtualItem, VirtualItemSystem
  enum ChestConveyorMode
    {InputOutput, Input, Output}
  enum ChestSideRole
    {None, Input, Output}
  class ChestConveyorEntry
    +TopLeft:Point +ChestIndex:int +Mode:ChestConveyorMode +Left:ChestSideRole +Right:ChestSideRole
  class ChestConveyorSystem : ModSystem
    +Enabled:bool[s] +ProbeRetryTicks:int[s] +HasListedChestAbove(int,int)[s] +QueueProbe(int,int,bool)[s] +Entries:IEnumerable<ChestConveyorEntry>[s] +CycleMode(ChestConveyorEntry)[s] +TryCycleModeAt(Point,out ChestConveyorMode)[s] +SetReplicaEntry(Point,bool,ChestConveyorMode)[s] +ReplaceReplica(List<KeyValuePair<Point, ChestConveyorMode>>)[s] +LoadWorldData(TagCompound)[ov] +OnWorldUnload()[ov] +Unload()[ov]
    struct Probe
      +Tile:Point +AllowAdd:bool +Age:int
**ConveyorPhysicsSystem.cs** uses: ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyorTile, FactorrariaNet
  class ConveyorPhysicsSystem : ModSystem
    +ConveyorSpeed:float[c] +NpcSyncTicks:int[s] +ItemSyncTicks:int[s] +LogStats:bool[s] +PostUpdateNPCs()[ov]
**CustomWireSystem.cs** uses: CopperWireItem, GoldWireItem, IronCutterItem, LeadCutterItem, PowerGridSystem, TinWireItem
  enum CustomWireType : byte
    {None, Copper}
  class CustomWireSystem : ModSystem
    +AllWireTypes:CustomWireType[s] +WireGrid:Dictionary<Point16,CustomWireType>[s] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +HasWire(int,int,CustomWireType)[s] +AddWire(int,int,CustomWireType)[s] +RemoveWire(int,int,CustomWireType)[s] +WireAt(int,int)[s] +SetWireAt(int,int,CustomWireType)[s] +ReplaceAll(List<KeyValuePair<Point16, CustomWireType>>)[s] +PostDrawTiles()[ov]
**LiquidNetworkDebugSystem.cs** uses: BaseMachine, Direction, FlowDir, FlowNetwork, FlowPort, FlowRoute, FlowSink, FlowSource, FlowTransfer, FurnaceOffsetConfig, LiquidNetworkSystem, LiquidStack, LiquidTypeRegistry
  class LiquidNetworkDebugSystem : ModSystem
    +EnableDebugView:bool[s] +PostDrawTiles()[ov]
**LiquidNetworkSystem.cs** uses: Factorraria, FlowNetwork, FlowNetworkBuilder, LiquidPipeDiagnostics, LiquidTextureCache, LiquidWorldBanks, PipeTierRegistry
  class LiquidNetworkSystem : ModSystem
    +AllPipeTiles:HashSet<Point>[s] +networkNeedsRebuilding:bool[s] +FlowNetworks:List<FlowNetwork>[s] +WorldBanks:LiquidWorldBanks[s] +RebuildCount:int[s] +PostUpdateWorld()[ov] +RegisterPipeTile(Point)[s] +UnregisterPipeTile(Point)[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov]
**MachineRegisterationSystem.cs** ns!=Factorraria.Common.Machines uses: AutohammerRecipeRegistry, AutohammerTileEntity, AutohammerUIState, BaseMachine, CustomRecipe, DecayChamberRecipeRegistry, DecayChamberTileEntity, DecayChamberUIState, ExtractinatorRecipeRegistry, ExtractinatorTileEntity, ExtractinatorUIState, FuelGroup, FurnaceRecipeRegistry, FurnaceTileEntity, FurnaceUIState, GelBurnerTileEntity, GelBurnerUIState, HellforgeRecipeRegistry, HellforgeTileEntity, HellforgeUIState, IceMachineRecipeRegistry, IceMachineTileEntity, IceMachineUIState, LiquidBiomeVisits, LiquidDistillatorRecipeRegistry, LiquidDistillatorTileEntity, LiquidDistillatorUIState, MachineUIRegistry, MachineVisualRegistry, MagicMaterializerRegistry, MagicMaterializerTileEntity, MagicMaterializerUIState, MechanicalAssemblerRecipeRegistry, MechanicalAssemblerTile, MechanicalAssemblerTileEntity, MechanicalAssemblerUIState, MotorMK1Tile, MotorMK1TileEntity, PlatinumPowerPoleTile, PlatinumPowerPoleTileEntity, PowerPoleUIState, RecipeArtDefinitions, RecipeArtRegistry, RecipeBook, RecipeCatalog, RecipeVisibility, RoboticArmTile, RoboticArmTileEntity, RoboticArmUIState, ScrollPoolDefinitions, ScrollPools, SolidifierRecipeRegistry, SolidifierTileEntity, SolidifierUIState, WoodenPowerPoleTile, WoodenPowerPoleTileEntity
  class MachineRegisterationSystem : ModSystem
    +Load()[ov] +PostSetupContent()[ov] +PostAddRecipes()[ov] +Unload()[ov]
**MachineUISystem.cs** uses: BaseMachine, CartChestUISystem, Factorraria, FactorrariaNet, MachineDetail, MachineDetailSystem, MachineUIRegistry, MachineUIStateBase, RecipeBookSystem, TileEntityHelper
  class MachineUISystem : ModSystem
    +OpenMachine:BaseMachine +Load()[ov] +OpenUI(int,int,int,BaseMachine) +CloseUI() +RequestOpenWhenReplicaArrives(int,int) +NotifyMachineKilled(BaseMachine) +RequestCloseFromServer(int,int,bool) +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**PowerGridDebugSystem.cs** ns!=(global) uses: BaseMachine, FurnaceOffsetConfig, LiquidStack, PowerGridSystem
  class PowerGridDebugSystem : ModSystem
    +EnableDebugView:bool[s] +PostDrawTiles()[ov]
**PowerGridSystem.cs** uses: CustomWireSystem, CustomWireType, FactorrariaNet, IElectricConsumer, IElectricProducer, PowerNetwork, TileEntityHelper
  class PowerGridSystem : ModSystem
    +AllMachines:HashSet<TileEntity>[s] +ActivePowerNetworks:List<PowerNetwork>[s] +gridNeedsRebuilding:bool[s] +PostUpdateWorld()[ov] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +RegisterMachineToMasterList(TileEntity)[s]
**RecipeBookKeybindSystem.cs** uses: RecipeBookLayout, RecipeBookSystem
  class RecipeBookKeybindSystem : ModSystem
    +ItemSearchKey:ModKeybind[s] +ToggleKey:ModKeybind[s] +Load()[ov] +Unload()[ov] +ItemSearchKeyName()[s]
  class RecipeBookKeybindPlayer : ModPlayer
    +ProcessTriggers(TriggersSet)[ov]
**RecipeBookSystem.cs** uses: CartChestUISystem, Factorraria, MachineUISystem, RecipeBookArt, RecipeBookButtonState, RecipeBookState, RecipeIcon
  class RecipeBookSystem : ModSystem
    +IsOpen:bool[s] +Open()[s] +OpenAt(string)[s] +OpenIngredientSearch(int)[s] +Close(bool)[s] +Toggle()[s] +Load()[ov] +Unload()[ov] +OnWorldUnload()[ov] +UpdateUI(GameTime)[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
**RecipeSystem.cs** uses: RecipeBook
  class RecipeSystem : ModSystem
    +AddRecipes()[ov]
**VirtualItemSystem.cs** ns!=Factorraria.Content.VirtualItems uses: ClockwisePriorityConveyorTile, ConveyorFilterNet, CounterClockwisePriorityConveyorTile, Factorraria, FurnaceOffsetConfig, GhostDiagnostics, PriorityConveyorNetwork, VItemGlobalItem, VItemNet, VirtualItem
  class VirtualItemSystem : ModSystem
    +tileItemMap:Dictionary<Point, VirtualItem>[s] +vItemsById:Dictionary<int, VirtualItem>[s] +WorldItemsConverted:int[s] +PlayerPickups:int[s] +ConveyorNetworkMap:Dictionary<Point, PriorityConveyorNetwork>[s] +IsValidItemID(int)[s] +RegisterItemTile(Point,VirtualItem)[s] +UnregisterItemTile(Point,VirtualItem)[s] +AddReplica(VirtualItem)[s] +TryGetById(int,out VirtualItem)[s] +ForgetId(VirtualItem)[s] +Describe()[s] +OnWorldLoad()[ov] +OnWorldUnload()[ov] +SaveWorldData(TagCompound)[ov] +LoadWorldData(TagCompound)[ov] +PostDrawTiles()[ov] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov] +SpawnVirtualItem(int,int,int,int)[s] +SpawnVirtualItem(Item,int,int)[s] +GetVirtualItemAtTile(int,int,VirtualItem)[s] +IsTileOccupied(int,int,VirtualItem)[s] +GetVItemsInRect(Rectangle,List<VirtualItem>)[s] +ConvertItemsToVItems()[s] +SyncWorldItem(int,string)[s] +ConvertVItemsToItems(VirtualItem)[s] +CheckPlayerPickups()[s] +IsConveyorTile(int,int,out bool,out int)[s] +IsPriorityConveyorTile(int,int)[s] +RebuildNetworkAt(int,int,Point?)[s] +OnPriorityConveyorPlaced(int,int)[s] +OnPriorityConveyorKilled(int,int)[s] +SetConveyorFilter(int,int,int)[s] +ReplaceFilterNetworks(List<KeyValuePair<int, List<Point>>>)[s] +TryGetConveyorFilter(int,int,out int)[s]

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
**MachineUIStateBase.cs** uses: BaseMachine, LiquidTankUIElement, MachineRecipeSelect, MachineSlotBinding, MachineSlotKind, MachineUIElementEntry, OutputSlotAnimation, RecipeBook, RecipeBrowserPanel, RecipeSelectHammerIcon, UIItemSlotWrapper
  interface IZoomScalable
    +SetZoomScale(float)
  abstract class MachineUIStateBase : UIState
    +CurrentEntity:BaseMachine +Panel:UIElement #BasePanelSize:Vector2[v] +BasePanelOffset:Vector2[v] #BuildElements()[ab] +OnInitialize()[ov] +InitializeUIState() +OnActivate()[ov] #UpdateLayout()[v] +ContentOrigin:Vector2 +SetZoomScale(float) #SlotSize:float[c] #InputSlotEntry(int,Vector2,Func<bool>) #OutputSlotEntry(int,Vector2,Func<bool>) #FuelSlotEntry(int,Vector2) #FuelSlotEntry(Vector2) #FuelTankEntry(int,Vector2) #InputTankEntry(int,Vector2) #OutputTankEntry(int,Vector2) #AddRecipePicker(List<MachineUIElementEntry>,RecipeBook,Vector2,Vector2)
**RecipeIcon.cs** uses: LiquidTypeDefinition, LiquidTypeRegistry, RecipeOutputKey
  static class RecipeIcon
    +FadedTint:Color[s] +Clear()[s] +Draw(SpriteBatch,RecipeOutputKey,Vector2,float,float,bool)[s]
**UIItemSlotWrapper.cs** ns!=Factorraria.Content.UI uses: BaseMachine, Cart, CartPanelNet, CartSlotBinding, CartSlotEdit, Factorraria, Handle, MachineSlotBinding, MachineSlotEdit
  class UIItemSlotWrapper : UIElement
    +FadedWhen:Func<bool> #DrawSelf(SpriteBatch)[ov]

### Common/UI/CustomUIElements/
**ElectricityHoverTip.cs**
  class ElectricityHoverTip : ModSystem
    +Show(string,Color)[s] +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov] +Unload()[ov]
**ElectricityUIIcon.cs** uses: BaseMachine, ElectricityHoverTip
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
**RecipeSelectHammerIcon.cs** uses: BookDraw, IZoomScalable, RecipeBook, RecipeBookLayout, RecipeBookSystem, RecipeCatalog, RecipeIcon, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeState, RecipeVisibility, State
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
    +SetZoomScale(float) +Unfocus() +HoverText:Func<string> +SetText(string) +LeftClick(UIMouseEvent)[ov] +RightClick(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]

### Common/UI/RecipeBookUI/
**BookScrollbar.cs** uses: BookDraw, RecipeBookLayout
  class BookScrollbar : UIScrollbar
    +Needed:bool +SetBookView(float,float) +LeftMouseDown(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookButtonState.cs** uses: FurnaceOffsetConfig, RecipeBookButton, RecipeBookLayout, RecipeBookPlayer
  class RecipeBookButtonState : UIState
    +Button:RecipeBookButton +OnInitialize()[ov] +ResetPosition()[s] +Update(GameTime)[ov]
**RecipeBookDetail.cs** uses: BookDraw, CustomRecipe, Entry, FuelEntry, LiquidIngredient, LiquidTypeRegistry, RecipeArtRegistry, RecipeBookLayout, RecipeBookState, RecipeCatalog, RecipeIcon, RecipeIngredient, RecipeOutputGroup, RecipeOutputKey, RecipeState, RecipeVisibility
  class RecipeBookDetail : UIElement
    +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
    struct Cell
      +Icon:RecipeOutputKey +Count:string +Name:string +ItemType:int +Stack:int +Hidden:bool +NavKey:string
**RecipeBookDragHandle.cs** uses: BookDraw, RecipeBookLayout, RecipeBookState
  class RecipeBookDragHandle : UIElement
    +LeftMouseDown(UIMouseEvent)[ov] +RightClick(UIMouseEvent)[ov] +Update(GameTime)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookElements.cs** uses: Entry, RecipeArtRegistry, RecipeBookArt, RecipeBookLayout, RecipeBookPlayer, RecipeIcon, RecipeOutputGroup, RecipeState, State
  static class BookDraw
    +Rect(SpriteBatch,Rectangle,Color)[s] +Frame(SpriteBatch,Rectangle,Color,Color,int)[s] +Fit(string,float,float,out bool)[s] +Text(SpriteBatch,string,Vector2,Color,float)[s]
  class RecipeBookButton : UIElement
    +OnPressed:Action +MouseInside:bool +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
  class RecipeBookHeaderRow : UIElement
    +Machine:string +Collapsed:bool +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
  class RecipeBookEntry : UIElement
    +Key:string +Machine:string +Group:RecipeOutputGroup +State:RecipeState +ProductName:string +Favorite:bool +Art:Entry +Selected:bool +OnChosen:Action<string> +IsFaded:bool +Update(GameTime)[ov] +LeftClick(UIMouseEvent)[ov] #DrawSelf(SpriteBatch)[ov]
**RecipeBookLayout.cs** uses: SearchBarTheme
  static class RecipeBookLayout
    +ButtonX:int[c] +ButtonY:int[c] +ButtonSize:int[c] +ReferenceDefenseX:int[c] +ReferenceDefenseY:int[s] +ButtonOffsetX:int[c] +ButtonOffsetY:int[s] +PanelWidth:int[c] +PanelHeight:int[c] +BackgroundScale:float[s] +Margin:int[c] +Gutter:int[c] +TitleHeight:int[c] +UnknownName:string[c] +ScrollbarWidth:int[c] +HeaderRowHeight:int[c] +EntryRowHeight:int[c] +EntryIconBox:int[c] +StarHitSize:int[c] +StarRightPad:int[c] +StarRect(Rectangle)[s] +LeftPage:Rectangle[s] +RightPage:Rectangle[s] +ListArea:Rectangle[s] +ScrollbarArea:Rectangle[s] +DragBar:Rectangle[s] +DragKeepVisible:int[c] +SearchArea:Rectangle[s] +DetailArea:Rectangle[s] +DetailHeaderHeight:int[c] +DetailCell:int[c] +DetailCellGap:int[c] +DetailPagerHeight:int[c] +DetailRecipeArea:Rectangle[s] +Text(string)[s] +MachineName(string)[s] +PageColor:Color[s] +PageBorder:Color[s] +CoverColor:Color[s] +TextColor:Color[s] +TextFadedColor:Color[s] +FavoriteRowColor:Color[s] +SearchBackground:Color[s] +SearchIdleBorder:Color[s] +SearchFocusBorder:Color[s] +ScrollTrack:Color[s] +ScrollTrackOutline:Color[s] +ScrollThumb:Color[s] +ScrollThumbHover:Color[s] +ScrollThumbDrag:Color[s] +SearchTheme:SearchBarTheme[s]
  static class RecipeBookArt
    +ButtonPath:string[c] +ButtonHoverPath:string[c] +BackgroundPath:string[c] +StarOffPath:string[c] +StarOffHighlightedPath:string[c] +StarOnPath:string[c] +StarOnHighlightedPath:string[c] +Button:Asset<Texture2D>[s] +ButtonHover:Asset<Texture2D>[s] +Background:Asset<Texture2D>[s] +Star(bool,bool)[s] +Clear()[s]
**RecipeBookPlayer.cs** uses: LiquidBiomeVisits, RecipeKnowledgeSystem
  class RecipeBookPlayer : ModPlayer
    +ButtonOffset:Vector2? +FavoritesVersion:int +IsFavorite(string) +ToggleFavorite(string) +PostUpdate()[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov]
**RecipeBookState.cs** uses: BookDraw, BookScrollbar, CustomRecipe, Entry, LiquidIngredient, LiquidTypeRegistry, RecipeArtRegistry, RecipeBookArt, RecipeBookDetail, RecipeBookDragHandle, RecipeBookEntry, RecipeBookHeaderRow, RecipeBookKeybindSystem, RecipeBookLayout, RecipeBookPlayer, RecipeCatalog, RecipeIngredient, RecipeKnowledgeSystem, RecipeOutputGroup, RecipeSearchBar, RecipeState, RecipeVisibility, State
  class RecipeBookState : UIState
    +SelectedKey:string +SelectedMachine:string +SelectedGroup:RecipeOutputGroup +SelectedArt:Entry +SelectedState:RecipeState +IsEmpty:bool +NoResults:bool +MouseInsidePanel:bool +OnInitialize()[ov] +OnActivate()[ov] +Update(GameTime)[ov] +ResetSearch() +SetSearch(string) +PanelOffset:Vector2 +SetPanelOffset(Vector2) +ResetPanelOffset() +ScrollBy(float) +Select(string,bool) +GoTo(string)
  class RecipeBookPanel : UIElement
    +MouseInside:bool +Update(GameTime)[ov] +ScrollWheel(UIScrollWheelEvent)[ov] #DrawSelf(SpriteBatch)[ov]

### Common/VirtualItems/
**VItemGlobalItem.cs** uses: CoalTile
  class VItemGlobalItem : GlobalItem
    +InstancePerEntity:bool[ov] +ConveyorImmunityTimer:int +Update(Item,ref float,ref float)[ov]
**VItemStacking.cs** ns!=Factorraria.Content.VirtualItems
  static class VItemStacking
    +CanMerge(Item,Item)[s] +Insert(Item,int,int,Item,Func<int, Item, bool>,Action<int>)[s]
**VirtualItem.cs** ns!=Factorraria.Content.VirtualItems uses: VItemGlobalItem, VItemStacking, VirtualItemSystem
  class VirtualItem
    +itemType:int +stackSize:int +prefix:int +active:bool +Id:int +ReplicaStep:float +StepPerTick:float +pickupCooldown:int +cartGrabCooldown:int +frame:int +frameCounter:int +currentTileX:int +currentTileY:int +targetTileX:int +targetTileY:int +worldPosition:Vector2 +moveSpeed:float +Update() +CreateReplica(int,Item,Vector2,int,int,float)[s] +ApplyReplicaState(Item,int,Vector2,int,int,float) +ReplicaTick() +SetTargetTile(int,int) +GetSourceRectangle(Texture2D) +Remove() +MoveVirtualItem() +TryMergeWithVirtualItem(VirtualItem)

### Common/WorldGeneration/
**CartWorldGen.Biomes.cs** uses: Cart, CartChain, CartModule, CartSystem, LiquidTypeRegistry, MachineBiome, SiliconOreItem
  partial class CartWorldGen
    +BiomeScanRadius:int +BiomeMinTiles:int +DesertAmberChance:double +HellcartChance:double +ShroomCartChance:double +GemCartChance:double +GoldChestShare2:double +TerrariumEmptyChance:double +TerrariumWaterChance:double +JungleHoneyChance:double +TerrariumMaxAmount:int
    enum CartBiome
      {Forest, Jungle, Desert, Snow}
    enum CartDepth
      {Surface, Underground, LavaLayer}
    struct CartSpot
      +Biome:CartBiome +Depth:CartDepth +BelowRockLayer:bool
    class LootEntry
      +Pool:int +Min:int +Max:int +Chance:double +Slots:int +Distinct:bool
**CartWorldGen.cs** uses: CartPhysics, TrackData, TrackSample
  partial class CartWorldGen : ModSystem
    +Enabled:bool +SpawnChancePerTile:double +LinkedChance:double +ChestChance:double +TerrariumChance:double +GoldChestShare:double +MinSpacingTiles:int +MaxCartsPerTrack:int +FlatMarginPx:int +HeadroomRowsMargin:int +OreSlots:int +OreMin:int +ModifyWorldGenTasks(List<GenPass>,ref double)[ov]
**OilWorldGen.cs** uses: OilLiquid
  class OilWorldGen : ModSystem
    +Enabled:bool +PoolDepth:int +FloorThickness:int +RimMargin:int +MinPoolDepthHere:int +PocketAttemptsPerTile:double +PocketExponent:double +PocketMinDepthFraction:double +PocketRadiusXMin:double +PocketRadiusXMax:double +PocketRadiusYMin:double +PocketRadiusYMax:double +PocketMaxCells:int +ExcludeHive:bool +PoolFraction:double +ShellThickness:int +ShellFullFraction:double +ShellEndFraction:double +OvalInset:int +ScanMarginTop:int +ScanMarginBottom:int +MinColumnHeight:int +SmoothRadius:int +PocketMaxChance:double +PocketChanceExponent:double +PocketCellsBottom:int +PocketCellsTop:int +MaxLiquidBodyCells:int +ModifyWorldGenTasks(List<GenPass>,ref double)[ov]
**PostWorldGenEdits.cs** uses: CoalTile
  class PostWorldGenEdits : ModSystem
    +Enabled:bool[s] +PostWorldGen()[ov]
**SiliconOreWorldGen.cs** uses: SiliconOreTile
  class SiliconOreWorldGen : ModSystem
    +DensityPerTile:double[s] +StrengthMin:int[s] +StrengthMax:int[s] +StepsMin:int[s] +StepsMax:int[s] +BottomMargin:int[s] +ModifyWorldGenTasks(List<GenPass>,ref double)[ov]

### Content/Configs/
**CartMapConfig.cs** uses: CartMapIcons
  class CartMapConfig : ModConfig
    +Current:CartMapConfig[s] +Mode:ConfigScope[ov] +IconSize:float +OutlineThickness:float +ButtonX:int +ButtonY:int +InvertIconDirection:bool +FlipLeftFacingSkins:bool +HideCarriedNpcHead:bool +OnChanged()[ov]
**FurnaceOffsetConfig.cs**
  class FurnaceOffsetConfig : ModConfig
    +Mode:ConfigScope[ov] +OffsetX:float +OffsetY:float +AngleOffset:float +AngleOffset2:float +animationOffset:int +fireCropOffset:float +animationSpeedDivider:int +EnableDebugs:bool +RevealAllRecipes:bool +VItemOffset:float
**NetworkConfig.cs**
  class NetworkConfig : ModConfig
    +Current:NetworkConfig[s] +Mode:ConfigScope[ov] +ReachSlackTiles:float +RequestsPerSecond:int +RequestBurst:int +JoinChunkSize:int +JoinMaxProviderBytes:int +JoinRequestCooldownTicks:int +CartUpdatesPerSecond:int +CartInterestRadiusTiles:int +CartInterpolationBufferMs:int +MachineStateUpdatesPerSecond:int +LiquidBroadcastsPerTick:int +VItemInterestHalfWidthTiles:int +VItemInterestHalfHeightTiles:int +VItemMaxEntriesPerPacket:int

### Content/Items/Carts/
**CartItem.cs** uses: CartInputNet, CartSkinTable, CartSystem, Factorraria
  class CartItem : ModItem
    +Texture:string[ov] +CanUseItem(Player)[ov] +UseItem(Player)[ov]
**CartModuleItemGlobal.cs** uses: Cart
  class CartModuleItemGlobal : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]
**VanillaCartItemGlobal.cs** uses: CartSkinTable
  class VanillaCartItemGlobal : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +CanUseItem(Item,Player)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]

### Content/Items/Conveyors/
**ClockwisePriorityConveyor.cs** uses: ClockwisePriorityConveyorTile, CounterClockwisePriorityConveyor, FactorrariaNet
  class ClockwisePriorityConveyor : ModItem
    +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov]
**CounterClockwisePriorityConveyor.cs** uses: ClockwisePriorityConveyor, CounterClockwisePriorityConveyorTile, FactorrariaNet
  class CounterClockwisePriorityConveyor : ModItem
    +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov]
**VanillaConveyorGlobalItem.cs** uses: FactorrariaNet
  class VanillaConveyorGlobalItem : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +CanRightClick(Item)[ov] +RightClick(Item,Player)[ov] +ConsumeItem(Item,Player)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]

### Content/Items/Discovery/
**ClosedScrollItem.cs** uses: RecipeParchmentItem, ScrollPools
  class ClosedScrollItem : ModItem
    +PoolName:string +CanRightClick()[ov] +RightClick(Player)[ov] +CanStack(Item)[ov] +ModifyTooltips(List<TooltipLine>)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +NetSend(BinaryWriter)[ov] +NetReceive(BinaryReader)[ov]
**RecipeParchmentItem.cs** uses: ParchmentReadResult, RecipeKnowledgeNet, ScrollPools
  class RecipeParchmentItem : ModItem
    +PoolName:string +CanRightClick()[ov] +RightClick(Player)[ov] +ConsumeItem(Player)[ov] +CanStack(Item)[ov] +ModifyTooltips(List<TooltipLine>)[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +NetSend(BinaryWriter)[ov] +NetReceive(BinaryReader)[ov]

### Content/Items/Hooks/
**MechanicalArmItem.cs** uses: MechanicalArmProjectile, RoboticArmTile
  class MechanicalArmItem : ModItem
    +ResetToHookMode(Item)[s] +CanShoot(Player)[ov] +CanUseItem(Player)[ov] +HoldItem(Player)[ov] +UpdateInventory(Player)[ov]
**MechanicalArmPlayer.cs** uses: MechanicalArmItem
  class MechanicalArmPlayer : ModPlayer
    +PostUpdate()[ov]

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
**PlatinumPowerPoleItem.cs** uses: PlatinumPowerPoleTile
  class PlatinumPowerPoleItem : ModItem
**WoodenPowerPoleItem.cs** uses: WoodenPowerPoleTile
  class WoodenPowerPoleItem : ModItem

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
**GoldWireItem.cs** uses: CustomWireNet, CustomWireSystem, CustomWireType, FactorrariaNet
  class GoldWireItem : ModItem
    +CanUseItem(Player)[ov] +UseItem(Player)[ov]
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
    +MaxArms:int[c] +IsActive:bool +Activate() +Deactivate() +SetReportedState(bool) +PlayerDisconnect()[ov] +SyncPlayer(int,int,bool)[ov] +PostUpdateRunSpeeds()[ov] +PreUpdateMovement()[ov] +PostUpdate()[ov]

### Content/Items/Statues/
**MeteorHeadStatueItem.cs** uses: MeteorHeadStatueTile
  class MeteorHeadStatueItem : ModItem
    +Texture:string[ov]

### Content/Items/Wires/
**CopperWireItem.cs** uses: CustomWireNet, CustomWireSystem, CustomWireType, FactorrariaNet
  class CopperWireItem : ModItem
    +CanUseItem(Player)[ov] +UseItem(Player)[ov]
**IronCutterItem.cs** uses: CustomWireNet, CustomWireSystem, CustomWireType, FactorrariaNet
  class IronCutterItem : ModItem
    +UseItem(Player)[ov]
**LeadCutterItem.cs** uses: CustomWireNet, CustomWireSystem, CustomWireType, FactorrariaNet
  class LeadCutterItem : ModItem
    +UseItem(Player)[ov]
**TinWireItem.cs** uses: CustomWireNet, CustomWireSystem, CustomWireType, FactorrariaNet
  class TinWireItem : ModItem
    +CanUseItem(Player)[ov] +UseItem(Player)[ov]

### Content/Liquids/Fuel/
**FuelLiquid.cs** uses: OilLiquid
  class FuelLiquid : ModLiquid
    +LiquidMerge(int,int,int)[ov] +ChooseWaterfallStyle(int,int)[ov] +OnPlayerSplash(Player,bool)[ov] +OnNPCSplash(NPC,bool)[ov] +OnProjectileSplash(Projectile,bool)[ov] +OnItemSplash(Item,bool)[ov]
  class FuelLiquidFall : ModLiquidFall

### Content/Liquids/Oil/
**OilLiquid.cs** uses: FuelLiquid
  class OilLiquid : ModLiquid
    +LiquidMerge(int,int,int)[ov] +ChooseWaterfallStyle(int,int)[ov] +OnPlayerSplash(Player,bool)[ov] +OnNPCSplash(NPC,bool)[ov] +OnProjectileSplash(Projectile,bool)[ov] +OnItemSplash(Item,bool)[ov]
  class OilLiquidFall : ModLiquidFall

### Content/Particles/
**PuffExplosion.cs**
  sealed class PuffExplosion : Particle
    +AtlasTextureName:string[ov] +Update()[ov]

### Content/Projectiles/Hooks/
**MechanicalArmProjectile.cs** uses: FactorrariaNet, MechanicalArmIK
  class MechanicalArmProjectile : ModProjectile
    +Load()[ov] +Unload()[ov] +CanUseGrapple(Player)[ov] +CanDamage()[ov] +ShouldUpdatePosition()[ov] +AI()[ov] +OnKill(int)[ov] +OnTileCollide(Vector2)[ov] +PreDraw(ref Color)[ov]

### Content/Projectiles/Mounts/
**MechSuitArmProjectile.cs** uses: MechSuitPlayer, MechanicalArmIK
  class MechSuitArmProjectile : ModProjectile
    +AnchorTile:Point +ArmBendSign:float +Latched:bool +Load()[ov] +Unload()[ov] +CanDamage()[ov] +ShouldUpdatePosition()[ov] +AI()[ov] +PreDraw(ref Color)[ov]

### Content/Tiles/Conveyors/
**ClockwisePriorityConveyorTile.cs** uses: ConveyorFilterNet, VirtualItemSystem
  class ClockwisePriorityConveyorTile : ModTile
    +Texture:string[ov] +NumDust(int,int,bool,ref int)[ov] +AnimateTile(ref int,ref int)[ov] +RightClick(int,int)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CanDrop(int,int)[ov]
**CounterClockwisePriorityConveyorTile.cs** uses: ConveyorFilterNet, VirtualItemSystem
  class CounterClockwisePriorityConveyorTile : ModTile
    +Texture:string[ov] +NumDust(int,int,bool,ref int)[ov] +AnimateTile(ref int,ref int)[ov] +RightClick(int,int)[ov] +KillTile(int,int,ref bool,ref bool,ref bool)[ov] +CanDrop(int,int)[ov]

### Content/Tiles/Machines/ElectricalConsumers/Autohammer/
**AutohammerRecipeRegistry.cs** uses: ByriteAlloyItem, ByriteAlloySlabItem, ClockwisePriorityConveyor, CopperWireItem, CounterClockwisePriorityConveyor, CrimtaneSheetItem, CustomRecipe, DemoniteSheetItem, GoldPlateItem, GoldWireItem, IronCutterItem, LeadCutterItem, MechanicalArmItem, MechanicalAssemblerItem, MeteoritePanelItem, PlatinumPowerPoleItem, RecipeBook, SimpleCircuitItem, TinWireItem, WoodenPowerPoleItem
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
**ExtractinatorTileEntity.cs** uses: ElectricConsumerMachine, ExtractinatorRecipe, ExtractinatorRecipeRegistry, IMachineDynamicDraw, MachineUISystem, VirtualItemSystem
  class ExtractinatorTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    +ValidTileType:int[ov] +PowerDemand:float[ov] #InputSlotCount:int[ov] #WorkDuration:int[ov] +InputBuffer:int +Update()[ov] +DynamicUpdate() +TryGetDynamicBounds(out Vector2,out float) +DynamicDraw(SpriteBatch) #TryGetIntakeSlot(int,out int,out int)[ov]
**ExtractinatorUIState.cs** uses: ElectricityUIIcon, ExtractinatorTileEntity, MachineUIElementEntry, MachineUIStateBase
  class ExtractinatorUIState : MachineUIStateBase
    #BuildElements()[ov]

### Content/Tiles/Machines/ElectricalConsumers/MechanicalAssembler/
**MechanicalAssemblerRecipeRegistry.cs** uses: CopperWireItem, CustomRecipe, GoldPlateItem, GoldWireItem, MeteorHeadStatueItem, MotorMK1Item, PipeMK1Item, PureSiliconItem, RecipeBook, SimpleCircuitItem, SteelBarItem, SteelScrewItem, TinWireItem
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

### Content/Tiles/Machines/ElectricalConsumers/PowerPoles/
**PlatinumPowerPoleTile.cs** uses: PlatinumPowerPoleItem, PlatinumPowerPoleTileEntity, PowerPoleTileBase, PowerPoleTileEntityBase
  class PlatinumPowerPoleTile : PowerPoleTileBase
    #PoleHeight:int[ov] #ArtName:string[ov] +PlacementEntity:PowerPoleTileEntityBase[ov] #DropItemType:int[ov] #MapColor:Color[ov] #BreakDust:int[ov]
**PlatinumPowerPoleTileEntity.cs** uses: PlatinumPowerPoleTile, PoleBoard, PowerPoleTileEntityBase
  class PlatinumPowerPoleTileEntity : PowerPoleTileEntityBase
    +ValidTileType:int[ov] +PowerDemand:float[ov] +Boards:PoleBoard[ov]
**PoleBoard.cs**
  class PoleBoard
    +TexturePath:string +Size:Vector2 +LinkPoints:Vector2 +AttachOffset:Vector2 +Pivot:Vector2
**PowerPoleCrossboardSystem.cs** uses: BaseMachine, PoleBoard, PowerPoleTileBase, PowerPoleTileEntityBase
  class PowerPoleCrossboardSystem : ModSystem
    +MaxSwayRadians:float +WobbleRadians:float +WobbleSpeed:float +Unload()[ov] +GetSwayAngle(Point16,int) +PostDrawTiles()[ov]
**PowerPoleTileBase.cs** uses: PowerPoleTileEntityBase
  abstract class PowerPoleTileBase : ModTile
    #PoleHeight:int[ab] #ArtName:string[ab] +PlacementEntity:PowerPoleTileEntityBase[ab] #DropItemType:int[ab] #MapColor:Color[ab] #BreakDust:int[ab] +Texture:string[ov]
**PowerPoleTileEntityBase.cs** uses: ElectricConsumerMachine, PoleBoard
  abstract class PowerPoleTileEntityBase : ElectricConsumerMachine
    #InputSlotCount:int[ov] #OutputSlotCount:int[ov] +Boards:PoleBoard[ab] +LinkPointCount:int +GetLinkPointBoardIndex(int,out int) +GetLinkPointWorld(int) +GetLinkPointWorldSwayed(int,float) +Update()[ov]
**PowerPoleUIState.cs** uses: ElectricityUIIcon, MachineUIElementEntry, MachineUIStateBase, PowerPoleTileEntityBase
  class PowerPoleUIState : MachineUIStateBase
    #BuildElements()[ov]
**WoodenPowerPoleTile.cs** uses: PowerPoleTileBase, PowerPoleTileEntityBase, WoodenPowerPoleItem, WoodenPowerPoleTileEntity
  class WoodenPowerPoleTile : PowerPoleTileBase
    #PoleHeight:int[ov] #ArtName:string[ov] +PlacementEntity:PowerPoleTileEntityBase[ov] #DropItemType:int[ov] #MapColor:Color[ov] #BreakDust:int[ov]
**WoodenPowerPoleTileEntity.cs** uses: PoleBoard, PowerPoleTileEntityBase, WoodenPowerPoleTile
  class WoodenPowerPoleTileEntity : PowerPoleTileEntityBase
    +ValidTileType:int[ov] +PowerDemand:float[ov] +Boards:PoleBoard[ov]

### Content/Tiles/Machines/ElectricalConsumers/RoboticArm/
**RoboticArmAreaEditorSystem.cs** uses: MachineUISystem, RoboticArmAreaNet, RoboticArmTileEntity
  class RoboticArmAreaEditorSystem : ModSystem
    +MoveHandleOffset:Vector2 +ResizeHandleOffset:Vector2 +TexturePath:string +DraggingHandle:bool +IsDraggingArm(RoboticArmTileEntity) +ModifyInterfaceLayers(List<GameInterfaceLayer>)[ov]
    enum Handle
      {None, Move, Resize}
**RoboticArmDeferredEdits.cs**
  class RoboticArmDeferredEditSystem : ModSystem
    +Enqueue(Action) +PostUpdateEverything()[ov] +ClearWorld()[ov] +Unload()[ov]
**RoboticArmDropCapture.cs** uses: RoboticArmTileEntity
  class RoboticArmDropCaptureSystem : ModSystem
    +Active:RoboticArmTileEntity +Run(RoboticArmTileEntity,Action) +Unload()[ov]
  class RoboticArmDropCapture : GlobalItem
    +OnSpawn(Item,IEntitySource)[ov]
**RoboticArmMotion.cs**
  class RoboticArmMotion
    +TrackSmoothTime:float +BendSmoothTime:float +IdleOffsetX:float +IdleOffsetY:float +SideDeadZone:float +ThrustDistance:float +ImpactPoint:float +HoldBackMax:float +GripDistance:float +InPositionDistance:float +Initialized:bool +Side:int +Head:Vector2 +Bend:float +Target:Vector2? +HasTarget:bool +Striking:bool +StrikeDuration:int +InPosition:bool +StrikeProgress:float +SetTarget(Vector2?) +StartStrike(int) +Update(Vector2,float,float,Item,bool)
**RoboticArmNet.cs** uses: FactorrariaNet, FactorrariaPacket, PacketDirection, RoboticArmMotion, RoboticArmTileEntity
  class RoboticArmNet : ModSystem
    +NearbyTiles:float[c] +Sent:int[s] +Received:int[s] +Missed:int[s] +EffectsSent:int[s] +SoundsPlayed:int[s] +Describe()[s] +Load()[ov] +Unload()[ov] +SendTileAction(Vector2,int,int,int,int)[s] +SendPlaceSound(Vector2,int,int)[s] +PostUpdateEverything()[ov]
    struct State
      +X:short +Y:short +HasTarget:bool +TargetX:short +TargetY:short +StrikeTicks:int +HeldType:ushort
    class Tracker
      +Tick(RoboticArmTileEntity)
**RoboticArmTile.cs** uses: MechanicalArmItem, RoboticArmTileEntity
  class RoboticArmTile : ModTile
    +Texture:string[ov] +Slope(int,int)[ov]
**RoboticArmTileEntity.cs** uses: ElectricConsumerMachine, Factorraria, GhostDiagnostics, IMachineDynamicDraw, MachineSlotKind, RoboticArmMotion, RoboticArmNet, RoboticArmTile, RoboticArmVisual, RoboticArmWorker, Tracker, VirtualItem, VirtualItemSystem
  class RoboticArmTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    +ValidTileType:int[ov] +PowerDemand:float[ov] #InputSlotCount:int[ov] #OutputSlotCount:int[ov] +UpperArmLength:float +ForearmLength:float +Reach:float +Shoulder:Vector2 +Visual:RoboticArmVisual +Motion:RoboticArmMotion +HeldForVisual:Item +ApplyNetState(bool,int,int,int,int) +AreaX:int +AreaY:int +AreaW:int +AreaH:int +ReachTiles:int +AreaLimit:int +AreaTiles:Rectangle +SetArea(int,int,int,int) +IsTileInReach(int,int) #WriteSnapshotExtra(BinaryWriter)[ov] #ReadSnapshotExtra(BinaryReader)[ov] #DescribeSnapshotExtra()[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +Update()[ov] +IsTool(Item) +IsPlaceable(Item) +IsAcceptedItem(Item) +AcceptsSlotItem(global::MachineSlotKind,int,Item)[ov] #IntakeTiles()[ov] #TryGetIntakeSlot(int,out int,out int)[ov] +CanDeliverDrops:bool +RouteDrop(Item) +DynamicUpdate() +TryGetDynamicBounds(out Vector2,out float) +DynamicDraw(SpriteBatch) +DynamicDrawBehindTiles(SpriteBatch)
**RoboticArmUIState.cs** uses: ElectricityUIIcon, MachineSlotBinding, MachineSlotKind, MachineUIElementEntry, MachineUIStateBase, RoboticArmAreaEditorSystem, RoboticArmTileEntity, UIItemSlotWrapper
  class RoboticArmUIState : MachineUIStateBase
    #BuildElements()[ov]
**RoboticArmVisual.cs** uses: MechanicalArmIK, RoboticArmMotion
  class RoboticArmVisual
    +AimSmoothing:float +IdleAimTilt:float +SwingArc:float +SwingEndAngle:float +ClawPivotX:float +ClawPivotY:float +BlockScale:float +BeamThickness:float +JointScale:float +Update(RoboticArmMotion,Vector2,float,float,Item,bool) +DrawBehind(SpriteBatch,Vector2,Item,bool)
**RoboticArmWorker.cs** uses: BaseMachine, FactorrariaNet, PureSiliconTile, RoboticArmDeferredEditSystem, RoboticArmDropCaptureSystem, RoboticArmMotion, RoboticArmNet, RoboticArmTileEntity, SiliconOreTile, TileEntityHelper
  class RoboticArmGhostSystem : ModSystem
    +Unload()[ov]
  class RoboticArmWorker
    +RescanInterval:int +MinStrikeTicks:int +ReachTimeoutTicks:int +BlockedCooldownTicks:int +TreeStickyColumns:int +FootprintMustFitArea:bool +PlatformsInOpenAir:bool +DebugPlacement:bool +MaxStrikesPerTile:int +Reaches:int +ReachTimeouts:int +Strikes:int +TilesChanged:int +Placements:int +BroadcastTileSquares:bool[s] +BroadcastTileEffects:bool[s] +TileSquares:int +TileSquareTiles:int +Describe() +Tick()
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
    +ValidTileType:int[ov] +PowerSupply:float[ov] +GelFuels:FuelTable[s] #AcceptedFuels:FuelTable[ov] +Update()[ov] #OnAnimationFrameChanged(int,int)[ov] #OnStartup()[ov] #OnShutdown()[ov]
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
  class DecayChamberItemTooltip : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]
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
**HellforgeRecipeRegistry.cs** uses: ByriteAlloyItem, CokeItem, CustomRecipe, FurnaceRecipeRegistry, LiquidFuelTable, LiquidTypeRegistry, PureSiliconItem, RecipeBook, SiliconOreItem, SteelBarItem
  static class HellforgeRecipeRegistry
    +Book:RecipeBook[s] +LiquidFuels:LiquidFuelTable[s] +BuildRecipes()[s]
**HellforgeTileEntity.cs** uses: BaseMachine, HellforgeRecipeRegistry, LiquidFuelTable, RecipeBook
  class HellforgeTileEntity : BaseMachine
    +ValidTileType:int[ov] +Recipes:RecipeBook[ov] #AcceptedLiquidFuels:LiquidFuelTable[ov] #OutputSlotCount:int[ov] #OnAnimationFrameChanged(int,int)[ov]
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
**AwakenedStatueEntries.cs** uses: AwakenedStatueRegistry, MeteorHeadStatueTile
  static class AwakenedStatueEntries
    +Register(AwakenedStatueRegistry)[s]
**MagicMaterializerItemTooltip.cs**
  class MagicMaterializerItemTooltip : GlobalItem
    +AppliesToEntity(Item,bool)[ov] +ModifyTooltips(Item,List<TooltipLine>)[ov]
**MagicMaterializerRegistry.cs** uses: LiquidFuelTable, LiquidTypeRegistry
  static class MagicMaterializerRegistry
    +LiquidFuels:LiquidFuelTable[s] +BuildRecipes()[s]
**MagicMaterializerTileEntity.cs** uses: AwakenedStatueSystem, BaseMachine, IStatueAwakener, LiquidFuelTable, LiquidStack, LiquidTypeRegistry, MagicMaterializerRegistry, MaterializerToggle
  class MagicMaterializerTileEntity : BaseMachine, IStatueAwakener
    +ValidTileType:int[ov] #AcceptedLiquidFuels:LiquidFuelTable[ov] +ShimmerDrainPerSecond:float +AwakenRadiusTiles:int +IsAwakening:bool +StatueScanIntervalTicks:int +AwakenerOrigin:Point16 +AwakenedStatueCount:int +AwakenedEnemyCount:int +BackgroundActive:bool +HasShimmer:bool +OnRightClick(int,int)[ov] #WriteSnapshotExtra(BinaryWriter)[ov] #ReadSnapshotExtra(BinaryReader)[ov] #DescribeSnapshotExtra()[ov] +SaveData(TagCompound)[ov] +LoadData(TagCompound)[ov] +TreatsWorldLiquidAsInfinite(int)[ov] +OnKill()[ov] +Update()[ov]
**MagicMaterializerUIState.cs** uses: MachineUIElementEntry, MachineUIStateBase
  class MagicMaterializerUIState : MachineUIStateBase
    #BuildElements()[ov]
**MaterializerRightClickGuard.cs** uses: MachineVisualRegistry
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
**CoalTile.cs**
  class CoalTile : ModTile
**PureSiliconTile.cs** uses: PureSiliconItem
  class PureSiliconTile : ModTile
**SiliconOreTile.cs** uses: SiliconOreItem
  class SiliconOreTile : ModTile

### Content/Tiles/Statues/
**MeteorHeadStatueGlobalNPC.cs**
  class MeteorHeadStatueGlobalNPC : GlobalNPC
    +AppliesToEntity(NPC,bool)[ov] +ModifyNPCLoot(NPC,NPCLoot)[ov]
  class MeteorHeadHardmodeLootSystem : ModSystem
    +Load()[ov] +Unload()[ov]
**MeteorHeadStatueTile.cs** uses: Direction, MeteorHeadStatueItem
  class MeteorHeadStatueTile : ModTile
    +Texture:string[ov] +HitWire(int,int)[ov]

## Non-code files (png omitted; sprites follow <Name>_On/_Off/_On_FirstLiquid naming)
- (root)/: .gitattributes, .gitignore, LICENSE; Factorraria.csproj; PROJECT_CONTEXT.md; build.txt, description.txt, description_workshop.txt
- .cursor/rules/: factorraria.mdc, liquids-networks.mdc, machines.mdc, tml-api.mdc
- .cursor/tml/: EXAMPLEMOD_INDEX.md, TML_API_INDEX.md, TML_MAP.md
- Assets/Atlases/: Particles_Atlas.json
- Localization/: en-US.hjson, en-US_Mods.Factorraria.hjson
- Properties/: launchSettings.json
- docs/liquid_overhaul/: ROADMAP.md
- docs/multiplayer/: ROADMAP.md, SURVEY_APPENDIX.md
- docs/poles/: ROADMAP.md
- tools/: patch_engine.ps1
- tools/flow_tests/: Program.cs.txt, README.txt, Stubs.cs.txt, TickTests.cs.txt, TopologyTests.cs.txt
