using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.Modules;

namespace Terraria.ObjectData;

/// <summary>
/// Contains tile properties for multitiles dictating how they are placed and how they behave. Multitiles are non-terrain tiles.
/// <para /> TileObjectData supports alternate placements as well as tile style-specific behaviors.
/// <para /> The <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#tileobjectdata-or-frameimportantmultitiles">TileObjectData section of the Basic Tile wiki guide</see> has visuals and guides teaching how to use the TileObjectData system.
/// </summary>
public class TileObjectData
{
	internal TileObjectData _parent;

	private bool _linkedAlternates;

	private bool _usesCustomCanPlace;

	private TileObjectAlternatesModule _alternates;

	private AnchorDataModule _anchor;

	private AnchorTypesModule _anchorTiles;

	private LiquidDeathModule _liquidDeath;

	private LiquidPlacementModule _liquidPlacement;

	private TilePlacementHooksModule _placementHooks;

	private TileObjectSubTilesModule _subTiles;

	private TileObjectDrawModule _tileObjectDraw;

	private TileObjectStyleModule _tileObjectStyle;

	private TileObjectBaseModule _tileObjectBase;

	private TileObjectCoordinatesModule _tileObjectCoords;

	private bool _hasOwnAlternates;

	private bool _hasOwnAnchor;

	private bool _hasOwnAnchorTiles;

	private bool _hasOwnLiquidDeath;

	private bool _hasOwnLiquidPlacement;

	private bool _hasOwnPlacementHooks;

	private bool _hasOwnSubTiles;

	private bool _hasOwnTileObjectBase;

	private bool _hasOwnTileObjectDraw;

	private bool _hasOwnTileObjectStyle;

	private bool _hasOwnTileObjectCoords;

	internal static List<TileObjectData> _data;

	private static TileObjectData _baseObject;

	private static bool readOnlyData;

	/// <summary>
	/// Contains the tile properties for the current tile. This is used to control the behavior, size, and other properties of a non-terrain tiles.
	/// <para /> Tiles using a TileObjectData are referred to as multitiles.
	/// The <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#tileobjectdata-or-frameimportantmultitiles">Basic Tile Guide on the wiki</see> has more information about multitiles.
	/// <para /> After all properties are set, call <see cref="M:Terraria.ObjectData.TileObjectData.addTile(System.Int32)" /> to register the data to the tile type.
	/// <para /> <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" /> and <see cref="F:Terraria.ObjectData.TileObjectData.newAlternate" /> are used to provide subtile and alternate-specific properties. Alternates should be setup before subtiles.
	/// </summary>
	public static TileObjectData newTile;

	/// <summary>
	/// Contains the tile properties specific to a subtile, also known as a tile style, of the current <see cref="F:Terraria.ObjectData.TileObjectData.newTile" />.
	/// <para /> Subtiles allow placed tile styles to have specific behaviors. This is most commonly used to declare certain tile styles as immune to lava or water. Another common usage is to declare specific tiles a tile style can anchor to.
	/// The <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#newsubtile---tile-styles">newSubTile section of the Tile wiki page</see> has more information about subtiles.
	/// <para /> Calling <see cref="M:Terraria.ObjectData.TileObjectData.addSubTile(System.Int32)" /> registers the alternate placement.
	/// <para /> If there are alternate placements (<see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" />) for this tile, make sure to register those first and to set <see cref="P:Terraria.ObjectData.TileObjectData.LinkedAlternates" /> to true after copying the base tile.
	/// </summary>
	public static TileObjectData newSubTile;

	/// <summary>
	/// Contains the tile properties specific to an alternate placement of the current <see cref="F:Terraria.ObjectData.TileObjectData.newTile" />.
	/// <para /> Alternate placements allow each style to be placed in multiple ways, using alternate sprites or alternate anchors.  Mostly useful for tiles that can anchor in multiple ways, such as how torches can anchor to tiles to the left, right, or bottom as well as to walls.
	/// The <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#newalternate---alternate-placement-styles">newAlternate section of the Tile wiki page</see> has more information about alternate placements.
	/// <para /> Calling <see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" /> registers the alternate placement.
	/// <para /> If there are subtiles (<see cref="M:Terraria.ObjectData.TileObjectData.addSubTile(System.Int32)" />) for this tile, make sure to do those after alternates are registered and to set <see cref="P:Terraria.ObjectData.TileObjectData.LinkedAlternates" /> to true on newSubTile after copying the base tile.
	/// </summary>
	public static TileObjectData newAlternate;

	public static TileObjectData StyleSwitch;

	public static TileObjectData StyleTorch;

	/// <summary> Has left and right alternate placements. </summary>
	public static TileObjectData Style4x2;

	public static TileObjectData Style2x2;

	public static TileObjectData Style1x2;

	/// <summary> Anchors to tile below. </summary>
	public static TileObjectData Style1x1;

	public static TileObjectData StyleAlch;

	public static TileObjectData StyleDye;

	public static TileObjectData Style2x1;

	public static TileObjectData Style6x3;

	public static TileObjectData StyleSmallCage;

	public static TileObjectData StyleOnTable1x1;

	/// <summary> Anchors to tiles above. </summary>
	public static TileObjectData Style1x2Top;

	/// <summary> Is 1x3. </summary>
	public static TileObjectData Style1xX;

	/// <summary> Is 2x3. </summary>
	public static TileObjectData Style2xX;

	public static TileObjectData Style3x2;

	public static TileObjectData Style3x3;

	public static TileObjectData Style3x4;

	public static TileObjectData Style5x4;

	/// <summary> Anchors to walls. </summary>
	public static TileObjectData Style3x3Wall;

	/// <summary>
	/// Call on <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" /> only. If set to true, this subtile will be populated with its own copy of the <see cref="P:Terraria.ObjectData.TileObjectData.Alternates" /> data. This will allow changes made to this subtile to apply to alternate placements of this tile style as well.
	/// <para /> This must be set to true after <c>TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);</c> and before adjusting any other properties. This should always be set for any subtile of a tile that has alternate placements.
	/// <para /> Defaults to false.
	/// </summary>
	public bool LinkedAlternates
	{
		get
		{
			return _linkedAlternates;
		}
		set
		{
			WriteCheck();
			if (value && !_hasOwnAlternates)
			{
				_hasOwnAlternates = true;
				_alternates = new TileObjectAlternatesModule(_alternates);
			}
			_linkedAlternates = value;
		}
	}

	/// <summary>
	/// If true, this tile will use anchors to decide if the tile can be placed, otherwise it will fallback to being placed like a terrain tile. Aside from platforms and torches, all multitiles set this to true.
	/// <para /> Defaults to false, but most template styles set this to true. If not copying from a template style or existing tile <b>be sure to set this to true</b>.
	/// </summary>
	public bool UsesCustomCanPlace
	{
		get
		{
			return _usesCustomCanPlace;
		}
		set
		{
			WriteCheck();
			_usesCustomCanPlace = value;
		}
	}

	internal List<TileObjectData> Alternates
	{
		get
		{
			if (_alternates == null)
			{
				return _baseObject.Alternates;
			}
			return _alternates.data;
		}
		set
		{
			if (!_hasOwnAlternates)
			{
				_hasOwnAlternates = true;
				_alternates = new TileObjectAlternatesModule(_alternates);
			}
			_alternates.data = value;
		}
	}

	/// <summary>
	/// Defines the anchors needed above this tile. Defaults to null.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#anchorbottomanchorleftanchorrightanchortop">Anchor section of the Basic Tile Guide on the wiki</see> for more information.
	/// </summary>
	public AnchorData AnchorTop
	{
		get
		{
			if (_anchor == null)
			{
				return _baseObject.AnchorTop;
			}
			return _anchor.top;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchor)
			{
				if (_anchor.top == value)
				{
					return;
				}
				_hasOwnAnchor = true;
				_anchor = new AnchorDataModule(_anchor);
			}
			_anchor.top = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].AnchorTop = value;
				}
			}
		}
	}

	/// <summary>
	/// Defines the anchors needed below this tile. Defaults to null, but most template styles will have this set already if copied from.
	/// <para /> Most typical furniture will define an AnchorBottom spanning the width of the multitile requiring solid tiles: <c>TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, TileObjectData.newTile.Width, 0);</c>
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#anchorbottomanchorleftanchorrightanchortop">Anchor section of the Basic Tile Guide on the wiki</see> for more information.
	/// </summary>
	public AnchorData AnchorBottom
	{
		get
		{
			if (_anchor == null)
			{
				return _baseObject.AnchorBottom;
			}
			return _anchor.bottom;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchor)
			{
				if (_anchor.bottom == value)
				{
					return;
				}
				_hasOwnAnchor = true;
				_anchor = new AnchorDataModule(_anchor);
			}
			_anchor.bottom = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].AnchorBottom = value;
				}
			}
		}
	}

	/// <summary>
	/// Defines the anchors needed to the left of this tile. Defaults to null.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#anchorbottomanchorleftanchorrightanchortop">Anchor section of the Basic Tile Guide on the wiki</see> for more information.
	/// </summary>
	public AnchorData AnchorLeft
	{
		get
		{
			if (_anchor == null)
			{
				return _baseObject.AnchorLeft;
			}
			return _anchor.left;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchor)
			{
				if (_anchor.left == value)
				{
					return;
				}
				_hasOwnAnchor = true;
				_anchor = new AnchorDataModule(_anchor);
			}
			_anchor.left = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].AnchorLeft = value;
				}
			}
		}
	}

	/// <summary>
	/// Defines the anchors needed to the right of this tile. Defaults to null.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#anchorbottomanchorleftanchorrightanchortop">Anchor section of the Basic Tile Guide on the wiki</see> for more information.
	/// </summary>
	public AnchorData AnchorRight
	{
		get
		{
			if (_anchor == null)
			{
				return _baseObject.AnchorRight;
			}
			return _anchor.right;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchor)
			{
				if (_anchor.right == value)
				{
					return;
				}
				_hasOwnAnchor = true;
				_anchor = new AnchorDataModule(_anchor);
			}
			_anchor.right = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].AnchorRight = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, then this multitile requires walls behind each individual tile to place. Defaults to false.
	/// </summary>
	public bool AnchorWall
	{
		get
		{
			if (_anchor == null)
			{
				return _baseObject.AnchorWall;
			}
			return _anchor.wall;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchor)
			{
				if (_anchor.wall == value)
				{
					return;
				}
				_hasOwnAnchor = true;
				_anchor = new AnchorDataModule(_anchor);
			}
			_anchor.wall = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].AnchorWall = value;
				}
			}
		}
	}

	/// <summary>
	/// Limits which tile types are valid for anchoring to a specific set of types.
	/// </summary>
	public int[] AnchorValidTiles
	{
		get
		{
			if (_anchorTiles == null)
			{
				return _baseObject.AnchorValidTiles;
			}
			return _anchorTiles.tileValid;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchorTiles)
			{
				if (value.deepCompare(_anchorTiles.tileValid))
				{
					return;
				}
				_hasOwnAnchorTiles = true;
				_anchorTiles = new AnchorTypesModule(_anchorTiles);
			}
			_anchorTiles.tileValid = value;
			if (!_linkedAlternates)
			{
				return;
			}
			for (int i = 0; i < _alternates.data.Count; i++)
			{
				int[] anchorValidTiles = value;
				if (value != null)
				{
					anchorValidTiles = (int[])value.Clone();
				}
				_alternates.data[i].AnchorValidTiles = anchorValidTiles;
			}
		}
	}

	/// <summary>
	/// Dictates specific tile types that are not valid for anchoring to.
	/// </summary>
	public int[] AnchorInvalidTiles
	{
		get
		{
			if (_anchorTiles == null)
			{
				return _baseObject.AnchorInvalidTiles;
			}
			return _anchorTiles.tileInvalid;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchorTiles)
			{
				if (value.deepCompare(_anchorTiles.tileInvalid))
				{
					return;
				}
				_hasOwnAnchorTiles = true;
				_anchorTiles = new AnchorTypesModule(_anchorTiles);
			}
			_anchorTiles.tileInvalid = value;
			if (!_linkedAlternates)
			{
				return;
			}
			for (int i = 0; i < _alternates.data.Count; i++)
			{
				int[] anchorInvalidTiles = value;
				if (value != null)
				{
					anchorInvalidTiles = (int[])value.Clone();
				}
				_alternates.data[i].AnchorInvalidTiles = anchorInvalidTiles;
			}
		}
	}

	/// <summary>
	/// Tiles contained in this array are tiles that will satisfy the <see cref="F:Terraria.Enums.AnchorType.AlternateTile" /> anchor type. This can be used to provide specific additional valid anchors for a specific alternate placement or tile style.
	/// </summary>
	public int[] AnchorAlternateTiles
	{
		get
		{
			if (_anchorTiles == null)
			{
				return _baseObject.AnchorAlternateTiles;
			}
			return _anchorTiles.tileAlternates;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchorTiles)
			{
				if (value.deepCompare(_anchorTiles.tileInvalid))
				{
					return;
				}
				_hasOwnAnchorTiles = true;
				_anchorTiles = new AnchorTypesModule(_anchorTiles);
			}
			_anchorTiles.tileAlternates = value;
			if (!_linkedAlternates)
			{
				return;
			}
			for (int i = 0; i < _alternates.data.Count; i++)
			{
				int[] anchorAlternateTiles = value;
				if (value != null)
				{
					anchorAlternateTiles = (int[])value.Clone();
				}
				_alternates.data[i].AnchorAlternateTiles = anchorAlternateTiles;
			}
		}
	}

	/// <summary>
	/// Unimplemented, has no effect.
	/// </summary>
	public int[] AnchorValidWalls
	{
		get
		{
			if (_anchorTiles == null)
			{
				return _baseObject.AnchorValidWalls;
			}
			return _anchorTiles.wallValid;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnAnchorTiles)
			{
				_hasOwnAnchorTiles = true;
				_anchorTiles = new AnchorTypesModule(_anchorTiles);
			}
			_anchorTiles.wallValid = value;
			if (!_linkedAlternates)
			{
				return;
			}
			for (int i = 0; i < _alternates.data.Count; i++)
			{
				int[] anchorValidWalls = value;
				if (value != null)
				{
					anchorValidWalls = (int[])value.Clone();
				}
				_alternates.data[i].AnchorValidWalls = anchorValidWalls;
			}
		}
	}

	/// <summary>
	/// If true, this tile will break when in contact with water. This supersedes the <see cref="F:Terraria.Main.tileWaterDeath" /> value.
	/// <para />By assigning to this on <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" /> this can be used to allow specific tile styles to be immune to water. Modders should try to keep this and <see cref="F:Terraria.Main.tileWaterDeath" /> in sync and only deviate from that common value for specific tile styles that should behave differently if any. <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L57">ExampleTorch</see> serves as an example of this and <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#conditional-behavior">the Conditional Behavior section of the Basic Tile wiki page</see> also has more information on this concept.
	/// <para /> Defaults to false.
	/// </summary>
	public bool WaterDeath
	{
		get
		{
			if (_liquidDeath == null)
			{
				return _baseObject.WaterDeath;
			}
			return _liquidDeath.water;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnLiquidDeath)
			{
				if (_liquidDeath.water == value)
				{
					return;
				}
				_hasOwnLiquidDeath = true;
				_liquidDeath = new LiquidDeathModule(_liquidDeath);
			}
			_liquidDeath.water = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].WaterDeath = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, this tile will break when in contact with lava. This supersedes the <see cref="F:Terraria.Main.tileLavaDeath" /> value.
	/// <para />By assigning to this on <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" /> this can be used to allow specific tile styles to be immune to lava. Modders should try to keep this and <see cref="F:Terraria.Main.tileLavaDeath" /> in sync and only deviate from that common value for specific tile styles that should behave differently if any. <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L57">ExampleTorch</see> serves as an example of this and <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#conditional-behavior">the Conditional Behavior section of the Basic Tile wiki page</see> also has more information on this concept.
	/// <para /> Defaults to false, but many of the templates default this to true.
	/// </summary>
	public bool LavaDeath
	{
		get
		{
			if (_liquidDeath == null)
			{
				return _baseObject.LavaDeath;
			}
			return _liquidDeath.lava;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnLiquidDeath)
			{
				if (_liquidDeath.lava == value)
				{
					return;
				}
				_hasOwnLiquidDeath = true;
				_liquidDeath = new LiquidDeathModule(_liquidDeath);
			}
			_liquidDeath.lava = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].LavaDeath = value;
				}
			}
		}
	}

	/// <summary>
	/// Controls if a tile can be placed in water. Typically used in conjunction with <see cref="P:Terraria.ObjectData.TileObjectData.WaterDeath" /> for consistency. Many tiles with visual flames, for example, tend to not allow themselves to be placed in water.
	/// <para /><see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L57">ExampleTorch</see> has an underwater style that is specifically placeable underwater.
	/// <para /> Defaults to <see cref="F:Terraria.Enums.LiquidPlacement.Allowed" />.
	/// </summary>
	public LiquidPlacement WaterPlacement
	{
		get
		{
			if (_liquidPlacement == null)
			{
				return _baseObject.WaterPlacement;
			}
			return _liquidPlacement.water;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnLiquidPlacement)
			{
				if (_liquidPlacement.water == value)
				{
					return;
				}
				_hasOwnLiquidPlacement = true;
				_liquidPlacement = new LiquidPlacementModule(_liquidPlacement);
			}
			_liquidPlacement.water = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].WaterPlacement = value;
				}
			}
		}
	}

	/// <summary>
	/// Controls if a tile can be placed in lava. Typically used in conjunction with <see cref="P:Terraria.ObjectData.TileObjectData.LavaDeath" /> for consistency. Aside from obsidian furniture, most tiles in the game can not be placed in lava. Many tiles can't be placed in lava but won't break in lava, while others will break.
	/// <para /><see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L57">ExampleTorch</see> has a style that is specifically placeable in lava.
	/// <para /> Defaults to <see cref="F:Terraria.Enums.LiquidPlacement.NotAllowed" />.
	/// </summary>
	public LiquidPlacement LavaPlacement
	{
		get
		{
			if (_liquidPlacement == null)
			{
				return _baseObject.LavaPlacement;
			}
			return _liquidPlacement.lava;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnLiquidPlacement)
			{
				if (_liquidPlacement.lava == value)
				{
					return;
				}
				_hasOwnLiquidPlacement = true;
				_liquidPlacement = new LiquidPlacementModule(_liquidPlacement);
			}
			_liquidPlacement.lava = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].LavaPlacement = value;
				}
			}
		}
	}

	/// <summary>
	/// Designates a method to call to determine if the tile can't be placed by the player at the location. This method is only called on the client placing the tile. This is used by chest tiles to see if the chest limit has been reached and by pylons to check if the pylon has already been placed.
	/// </summary>
	public PlacementHook HookCheckIfCanPlace
	{
		get
		{
			if (_placementHooks == null)
			{
				return _baseObject.HookCheckIfCanPlace;
			}
			return _placementHooks.check;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnPlacementHooks)
			{
				_hasOwnPlacementHooks = true;
				_placementHooks = new TilePlacementHooksModule(_placementHooks);
			}
			_placementHooks.check = value;
		}
	}

	/// <summary> Unused. </summary>
	public PlacementHook HookPostPlaceEveryone
	{
		get
		{
			if (_placementHooks == null)
			{
				return _baseObject.HookPostPlaceEveryone;
			}
			return _placementHooks.postPlaceEveryone;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnPlacementHooks)
			{
				_hasOwnPlacementHooks = true;
				_placementHooks = new TilePlacementHooksModule(_placementHooks);
			}
			_placementHooks.postPlaceEveryone = value;
		}
	}

	/// <summary>
	/// Designates a method to call after this tile is placed by a player. This method facilitates binding extra data to a placed tile. This method is only called on the client placing the tile. This is used by chest tiles to place the actual <see cref="T:Terraria.Chest" /> and by other tiles to place a <see cref="T:Terraria.DataStructures.TileEntity" />.
	/// </summary>
	public PlacementHook HookPostPlaceMyPlayer
	{
		get
		{
			if (_placementHooks == null)
			{
				return _baseObject.HookPostPlaceMyPlayer;
			}
			return _placementHooks.postPlaceMyPlayer;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnPlacementHooks)
			{
				_hasOwnPlacementHooks = true;
				_placementHooks = new TilePlacementHooksModule(_placementHooks);
			}
			_placementHooks.postPlaceMyPlayer = value;
		}
	}

	/// <summary>
	/// Designates a method that will run instead of the usual code that places each individual tile of a mutitile in the world. Used only be <see cref="F:Terraria.ID.TileID.ChristmasTree" /> due to its complicated logic.
	/// </summary>
	public PlacementHook HookPlaceOverride
	{
		get
		{
			if (_placementHooks == null)
			{
				return _baseObject.HookPlaceOverride;
			}
			return _placementHooks.placeOverride;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnPlacementHooks)
			{
				_hasOwnPlacementHooks = true;
				_placementHooks = new TilePlacementHooksModule(_placementHooks);
			}
			_placementHooks.placeOverride = value;
		}
	}

	internal List<TileObjectData> SubTiles
	{
		get
		{
			if (_subTiles == null)
			{
				return _baseObject.SubTiles;
			}
			return _subTiles.data;
		}
		set
		{
			if (!_hasOwnSubTiles)
			{
				_hasOwnSubTiles = true;
				_subTiles = new TileObjectSubTilesModule();
			}
			if (value == null)
			{
				_subTiles.data = null;
			}
			else
			{
				_subTiles.data = value;
			}
		}
	}

	/// <summary>
	/// Offsets the drawing of this tile in the Y direction. Can be used to draw a tile at a location other than the default. One example is how <see cref="F:Terraria.ID.TileID.Switches" /> appear slightly embedded into the tile they are anchored to. Another example is how the alternate placement of banners for  platforms shifts the drawing up.
	/// <para /> For modded tiles this affects both the placement preview and placed tiles.
	/// <para /> Read <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#drawyoffset">this section on the wiki</see> for a visual explanation of this.
	/// <para /> Defaults to 0.
	/// </summary>
	public int DrawYOffset
	{
		get
		{
			if (_tileObjectDraw == null)
			{
				return DrawYOffset;
			}
			return _tileObjectDraw.yOffset;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectDraw)
			{
				if (_tileObjectDraw.yOffset == value)
				{
					return;
				}
				_hasOwnTileObjectDraw = true;
				_tileObjectDraw = new TileObjectDrawModule(_tileObjectDraw);
			}
			_tileObjectDraw.yOffset = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawYOffset = value;
				}
			}
		}
	}

	/// <summary>
	/// Offsets the drawing of this tile in the X direction. Can be used to draw a tile at a location other than the default. One example is how <see cref="F:Terraria.ID.TileID.Switches" /> appear slightly embedded into the tile they are anchored to.
	/// <para /> Read <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#drawxoffset">this section on the wiki</see> for a visual explanation of this.
	/// <para /> Defaults to 0.
	/// </summary>
	public int DrawXOffset
	{
		get
		{
			if (_tileObjectDraw == null)
			{
				return DrawXOffset;
			}
			return _tileObjectDraw.xOffset;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectDraw)
			{
				if (_tileObjectDraw.xOffset == value)
				{
					return;
				}
				_hasOwnTileObjectDraw = true;
				_tileObjectDraw = new TileObjectDrawModule(_tileObjectDraw);
			}
			_tileObjectDraw.xOffset = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawXOffset = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, the tile placement preview will draw this tile flipped horizontally at even X tile coordinates. This effect must be replicated in <see cref="M:Terraria.ModLoader.ModTile.SetSpriteEffects(System.Int32,System.Int32,Microsoft.Xna.Framework.Graphics.SpriteEffects@)" /> to work for the placed tile. The <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleLamp.cs#L66">ExampleLamp.SetSpriteEffects method</see> shows an example of this required code.
	/// <para /> Use this for visual variety and to break up visual repetition. Tiles with normal left and right placements, such as <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/Furniture/ExampleChair.cs">ExampleChair</see>, will use alternate placements influenced by the player direction rather than this.
	/// <para /> Note that this should only be used for multitiles that are 1 tile wide, the effect will not work correctly for wider tiles.
	/// <para /> Defaults to false.
	/// </summary>
	public bool DrawFlipHorizontal
	{
		get
		{
			if (_tileObjectDraw == null)
			{
				return DrawFlipHorizontal;
			}
			return _tileObjectDraw.flipHorizontal;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectDraw)
			{
				if (_tileObjectDraw.flipHorizontal == value)
				{
					return;
				}
				_hasOwnTileObjectDraw = true;
				_tileObjectDraw = new TileObjectDrawModule(_tileObjectDraw);
			}
			_tileObjectDraw.flipHorizontal = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawFlipHorizontal = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, the tile placement preview will draw this tile flipped vertically at even Y tile coordinates. This effect must be replicated in <see cref="M:Terraria.ModLoader.ModTile.SetSpriteEffects(System.Int32,System.Int32,Microsoft.Xna.Framework.Graphics.SpriteEffects@)" /> to work for the placed tile. The <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleLamp.cs#L66">ExampleLamp.SetSpriteEffects method</see> shows an example of this required code, albeit the example is for DrawFlipHorizontal usage.
	/// <para /> Use this for visual variety and to break up visual repetition.
	/// <para /> Note that this should only be used for multitiles that are 1 tile high, the effect will not work correctly for taller tiles.
	/// <para /> Defaults to false.
	/// </summary>
	public bool DrawFlipVertical
	{
		get
		{
			if (_tileObjectDraw == null)
			{
				return DrawFlipVertical;
			}
			return _tileObjectDraw.flipVertical;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectDraw)
			{
				if (_tileObjectDraw.flipVertical == value)
				{
					return;
				}
				_hasOwnTileObjectDraw = true;
				_tileObjectDraw = new TileObjectDrawModule(_tileObjectDraw);
			}
			_tileObjectDraw.flipVertical = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawFlipVertical = value;
				}
			}
		}
	}

	/// <summary>
	/// Similar to <see cref="P:Terraria.ObjectData.TileObjectData.DrawYOffset" />, this also offsets the drawing of this tile in the Y direction, but this offset will only be applied if there is a solid tile above this tile. Torches use this to shift the torch down 2 more pixels so it doesn't look like the roof is being burnt.
	/// <para /> This is only applied to the tile placement preview and <see cref="M:Terraria.ModLoader.ModTile.SetDrawPositions(System.Int32,System.Int32,System.Int32@,System.Int32@,System.Int32@,System.Int16@,System.Int16@)" /> must be used to replicate this behavior in the placed tile. While torches use this, torches don't actually show a placement preview in the first place. The <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L57">ExampleTorch.SetDrawPositions method</see> shows an example of this.
	/// </summary>
	public int DrawStepDown
	{
		get
		{
			if (_tileObjectDraw == null)
			{
				return DrawStepDown;
			}
			return _tileObjectDraw.stepDown;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectDraw)
			{
				if (_tileObjectDraw.stepDown == value)
				{
					return;
				}
				_hasOwnTileObjectDraw = true;
				_tileObjectDraw = new TileObjectDrawModule(_tileObjectDraw);
			}
			_tileObjectDraw.stepDown = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawStepDown = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, tile styles are arranged one after the other horizontally in the spritesheet, otherwise they are arranged vertically. New lines of styles will roll over to the next row once <see cref="P:Terraria.ObjectData.TileObjectData.StyleWrapLimit" /> is reached, if defined.
	/// <para /> Some tiles keep this false but use a small <see cref="P:Terraria.ObjectData.TileObjectData.StyleWrapLimit" /> and <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" /> value to provide space for alternate placements, resulting in the tile styles themselves appearing to be arranged horizontally in the resulting spritesheet with the alternate placements below.
	/// <para /> Defaults to <c>false</c>.
	/// </summary>
	public bool StyleHorizontal
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return StyleHorizontal;
			}
			return _tileObjectStyle.horizontal;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.horizontal == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.horizontal = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].StyleHorizontal = value;
				}
			}
		}
	}

	/// <summary>
	/// Used by alternate placements to offset the calculated placement style.
	/// <para /> Equal to the value passed into <see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" />.
	/// <para /> If using <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" /> note that this is <b>NOT the tile style or placement style</b> of that Tile. Use <see cref="M:Terraria.ObjectData.TileObjectData.GetTileInfo(Terraria.Tile,System.Int32@,System.Int32@)" /> or <see cref="M:Terraria.ObjectData.TileObjectData.GetTileStyle(Terraria.Tile)" /> instead to query those.
	/// <para /> Defaults to 0.
	/// </summary>
	public int Style
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.Style;
			}
			return _tileObjectStyle.style;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.style == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.style = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].Style = value;
				}
			}
		}
	}

	/// <summary>
	/// Defines how many tile placement styles (Number of styles times <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" />) will fit into a line (row if <see cref="P:Terraria.ObjectData.TileObjectData.StyleHorizontal" /> is true, column if StyleHorizontal is false) of the spritesheet before wrapping to the next line. This is used for organizational purposes and to keep spritesheets easy to work with.
	/// <para /> Defaults to 0, which means no wrapping.
	/// </summary>
	public int StyleWrapLimit
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.StyleWrapLimit;
			}
			return _tileObjectStyle.styleWrapLimit;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.styleWrapLimit == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.styleWrapLimit = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].StyleWrapLimit = value;
				}
			}
		}
	}

	/// <summary> A hack used by some vanilla tiles, do not use. </summary>
	public int? StyleWrapLimitVisualOverride
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.StyleWrapLimitVisualOverride;
			}
			return _tileObjectStyle.styleWrapLimitVisualOverride;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.styleWrapLimitVisualOverride == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.styleWrapLimitVisualOverride = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].StyleWrapLimitVisualOverride = value;
				}
			}
		}
	}

	/// <summary> A hack used by some vanilla tiles, do not use. </summary>
	public int? styleLineSkipVisualOverride
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.styleLineSkipVisualOverride;
			}
			return _tileObjectStyle.styleLineSkipVisualoverride;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.styleLineSkipVisualoverride == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.styleLineSkipVisualoverride = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].styleLineSkipVisualOverride = value;
				}
			}
		}
	}

	/// <summary>
	/// How many lines (rows if StyleHorizontal is true, columns if StyleHorizontal is false) will be skipped between each line of styles when wrapping to the next line due to <see cref="P:Terraria.ObjectData.TileObjectData.StyleWrapLimit" />. Skipping lines allows tile spritesheets to have space for animations or toggled states (manual changes to TileFrameX and TileFrameY). For example, <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleCampfire.cs">ExampleCampfire</see> sets this to 9 because it has 8 animation frames and an off state taking up additional lines. Failure to set this for a tile that can change states will result in tile drops being incorrectly calculated.
	/// <para /> Defaults to 1, meaning no lines are skipped.
	/// </summary>
	public int StyleLineSkip
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.StyleLineSkip;
			}
			return _tileObjectStyle.styleLineSkip;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.styleLineSkip == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.styleLineSkip = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].StyleLineSkip = value;
				}
			}
		}
	}

	/// <summary>
	/// Applies a multiplier to style calculations to account for alternate placements (<see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" />) and <see cref="P:Terraria.ObjectData.TileObjectData.RandomStyleRange" />. For example, chair tiles can be placed facing left or right. These are intended to share the same style (<see cref="F:Terraria.Item.placeStyle" />) value, but will have different placement style values. The multiplier accounts for these alternate placements allowing tile styles to be properly determined and consequently the correct item to drop when the tile is destroyed.
	/// <para /> If tiles are mistakenly dropping items from different styles, StyleMultiplier might have the incorrect value.
	/// <para /> Defaults to 1.
	/// </summary>
	public int StyleMultiplier
	{
		get
		{
			if (_tileObjectStyle == null)
			{
				return _baseObject.StyleMultiplier;
			}
			return _tileObjectStyle.styleMultiplier;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectStyle)
			{
				if (_tileObjectStyle.styleMultiplier == value)
				{
					return;
				}
				_hasOwnTileObjectStyle = true;
				_tileObjectStyle = new TileObjectStyleModule(_tileObjectStyle);
			}
			_tileObjectStyle.styleMultiplier = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].StyleMultiplier = value;
				}
			}
		}
	}

	/// <summary>
	/// The width of this multitile in tile coordinates. For example, chests are 2 tiles wide while furnaces are 3 tiles wide.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#width">Width section of the Basic Tile Guide on the wiki</see> for more information and helpful visualizations.
	/// <para /> Defaults to 1.
	/// </summary>
	public int Width
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.Width;
			}
			return _tileObjectBase.width;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.width == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
				if (!_hasOwnTileObjectCoords)
				{
					_hasOwnTileObjectCoords = true;
					_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
					_tileObjectCoords.calculated = false;
				}
			}
			_tileObjectBase.width = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].Width = value;
				}
			}
		}
	}

	/// <summary>
	/// The height of this multitile in tile coordinates. For example, chests are 2 tiles high while grandfather clocks are 5 tiles high.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#height">Height section of the Basic Tile Guide on the wiki</see> for more information and helpful visualizations.
	/// <para /> Defaults to 1. When changing this <see cref="P:Terraria.ObjectData.TileObjectData.CoordinateHeights" /> must be changed as well.
	/// </summary>
	public int Height
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.Height;
			}
			return _tileObjectBase.height;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.height == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
				if (!_hasOwnTileObjectCoords)
				{
					_hasOwnTileObjectCoords = true;
					_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
					_tileObjectCoords.calculated = false;
				}
			}
			_tileObjectBase.height = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].Height = value;
				}
			}
		}
	}

	/// <summary>
	/// The tile coordinate from which this multitile is placed from. This affects the position of the tile in relation to the users mouse and also the placement of the tile when placed through code such as <see cref="M:Terraria.WorldGen.PlaceTile(System.Int32,System.Int32,System.Int32,System.Boolean,System.Boolean,System.Int32,System.Int32)" />
	/// <para /> The <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#origin">Origin section of the Basic Tile wiki guide</see> has more information and useful visualizations of this property.
	/// <para /> Defaults to Point16.Zero (0, 0), or the top left corner tile.
	/// </summary>
	public Point16 Origin
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.Origin;
			}
			return _tileObjectBase.origin;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.origin == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
			}
			_tileObjectBase.origin = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].Origin = value;
				}
			}
		}
	}

	/// <summary>
	/// The direction this TileObjectData will be eligible to be placed in.
	/// <para /> Many furniture tiles allow the player to place them facing right or left controlled by the players current direction. These tiles do this by having the base tile place in one direction while an alternate is added placing in the other direction.
	/// <para /> See <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/Furniture/ExampleChair.cs#L38">ExampleChair</see> for an example of this approach.
	/// <para /> Defaults to <see cref="F:Terraria.Enums.TileObjectDirection.None" />.
	/// </summary>
	public TileObjectDirection Direction
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.Direction;
			}
			return _tileObjectBase.direction;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.direction == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
			}
			_tileObjectBase.direction = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].Direction = value;
				}
			}
		}
	}

	/// <summary>
	/// Allows this tile to place a randomly selected placement style when placing. The placement preview will update randomly as the player attempts to place the tile.
	/// <para /> For example, the <see cref="F:Terraria.ID.TileID.Coral" /> tile uses a RandomStyleRange value of 6, allowing 6 different tile placement styles to be selected when placing.
	/// <para /> This can be used in conjunction with <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" /> to ensure that each of these random placement styles are considered the same tile style, but sometimes they are not.
	/// One interesting advanced usage of this feature is the <see cref="F:Terraria.ID.TileID.Painting2X3" /> tile. It uses RandomStyleRange only for tile style 15, which allows the <see cref="F:Terraria.ID.ItemID.StrangeGrowth" /> item to place one of four tile styles.
	/// <para /> If used with alternate placement styles, the random styles of a specific alternate should be located together on the spritesheet. The <see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" /> parameter should account for RandomStyleRange as well.
	/// <para /> Note that tiles placed with <see cref="M:Terraria.WorldGen.PlaceTile(System.Int32,System.Int32,System.Int32,System.Boolean,System.Boolean,System.Int32,System.Int32)" /> won't randomize if <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" /> is also used, but a specific random placement style choice can be passed into <see cref="M:Terraria.WorldGen.PlaceObject(System.Int32,System.Int32,System.Int32,System.Boolean,System.Int32,System.Int32,System.Int32,System.Int32)" />.
	/// <para /> Defaults to 0.
	/// </summary>
	public int RandomStyleRange
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.RandomStyleRange;
			}
			return _tileObjectBase.randomRange;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.randomRange == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
			}
			_tileObjectBase.randomRange = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].RandomStyleRange = value;
				}
			}
		}
	}

	/// <summary>
	/// Similar to <see cref="P:Terraria.ObjectData.TileObjectData.RandomStyleRange" />, except a random placement style is selected from a selection of random placement styles. This is unused by vanilla tiles, but should work as expected.
	/// </summary>
	public int[] SpecificRandomStyles
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.SpecificRandomStyles;
			}
			return _tileObjectBase.specificRandomStyles;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.specificRandomStyles == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
			}
			_tileObjectBase.specificRandomStyles = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].SpecificRandomStyles = value;
				}
			}
		}
	}

	/// <summary>
	/// If true, placing this tile will cause the anchored solid tiles to become unsloped. Otherwise, slopes on anchor tiles will prevent this tile from placing.
	/// <para /> <see cref="F:Terraria.ID.TileID.Switches" />, <see cref="F:Terraria.ID.TileID.Torches" />, and <see cref="F:Terraria.ID.TileID.ProjectilePressurePad" /> (partially) all use this, allowing these tiles to be placed more conveniently.
	/// <para /> Defaults to false.
	/// </summary>
	public bool FlattenAnchors
	{
		get
		{
			if (_tileObjectBase == null)
			{
				return _baseObject.FlattenAnchors;
			}
			return _tileObjectBase.flattenAnchors;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectBase)
			{
				if (_tileObjectBase.flattenAnchors == value)
				{
					return;
				}
				_hasOwnTileObjectBase = true;
				_tileObjectBase = new TileObjectBaseModule(_tileObjectBase);
			}
			_tileObjectBase.flattenAnchors = value;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].FlattenAnchors = value;
				}
			}
		}
	}

	/// <summary>
	/// An array defining how tall each individual tile within this multitile is, in pixels. This array must have exactly <see cref="P:Terraria.ObjectData.TileObjectData.Height" /> elements in it. In most cases, all values should be 16, but sometimes 18 is suitable for the last value.
	/// <para /> For example, a 3 tile high multitile might set CoordinateHeights as follows: <c>TileObjectData.newTile.CoordinateHeights = [16, 16, 18];</c>.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#coordinateheights">CoordinateHeights section of the Basic Tile Guide on the wiki</see> for more information and helpful visualizations.
	/// <para /> Defaults to <c>[16]</c>. If changing <see cref="P:Terraria.ObjectData.TileObjectData.Height" /> this must be changed as well.
	/// </summary>
	public int[] CoordinateHeights
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinateHeights;
			}
			return _tileObjectCoords.heights;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectCoords)
			{
				if (value.deepCompare(_tileObjectCoords.heights))
				{
					return;
				}
				_hasOwnTileObjectCoords = true;
				_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords, value);
			}
			else
			{
				_tileObjectCoords.heights = value;
			}
			_tileObjectCoords.calculated = false;
			if (!_linkedAlternates)
			{
				return;
			}
			for (int i = 0; i < _alternates.data.Count; i++)
			{
				int[] coordinateHeights = value;
				if (value != null)
				{
					coordinateHeights = (int[])value.Clone();
				}
				_alternates.data[i].CoordinateHeights = coordinateHeights;
			}
		}
	}

	/// <summary>
	/// How wide each individual tile within this multitile is, in pixels.
	/// <para /> This should almost always be 16 since each tile in the world is 16x16 pixels, but in rare situations a different value might be suitable, such as in this <see cref="F:Terraria.ID.TileID.Coral" /> example on the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#non-16-or-18-values">Basic Tile Guide on the wiki</see> where the extra-wide tile will spill out and draw outside the normal tile bounds.
	/// <para /> Defaults to 0, but most templates default this to 16. If not copying from a template or existing tile <b>be sure to set this to 16</b>.
	/// </summary>
	public int CoordinateWidth
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinateWidth;
			}
			return _tileObjectCoords.width;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectCoords)
			{
				if (_tileObjectCoords.width == value)
				{
					return;
				}
				_hasOwnTileObjectCoords = true;
				_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
			}
			_tileObjectCoords.width = value;
			_tileObjectCoords.calculated = false;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].CoordinateWidth = value;
				}
			}
		}
	}

	/// <summary>
	/// The number of empty padding pixels between each individual tile of a multitile in its spritesheet.
	/// <para /> All vanilla tiles set this to 2. While it may be possible to use a value of 0 to make it easier on artists, there are plenty of places in the Terraria code that still assume a padding of 2 so it is not recommended. The examples shown in <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#framed-vs-frameimportant-tiles">the Basic Tile wiki guide</see> show what the padding looks like in practice.
	/// <para /> The community has made tools such as <see href="https://forums.terraria.org/index.php?threads/tspritepadder-ready-to-use-sprites-for-terraria-tiles.96177/">tSpritePadder</see> to simplify adding padding pixels to tile sprites.
	/// <para /> Defaults to 0 but most templates default this to 2.
	/// </summary>
	public int CoordinatePadding
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinatePadding;
			}
			return _tileObjectCoords.padding;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectCoords)
			{
				if (_tileObjectCoords.padding == value)
				{
					return;
				}
				_hasOwnTileObjectCoords = true;
				_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
			}
			_tileObjectCoords.padding = value;
			_tileObjectCoords.calculated = false;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].CoordinatePadding = value;
				}
			}
		}
	}

	/// <summary>
	/// The number of pixels between each tile placement style of a multitile in its spritesheet in addition to the normal <see cref="P:Terraria.ObjectData.TileObjectData.CoordinatePadding" /> between each individual tile. Can be used to give more or less room between tile styles for the artist's convenience.
	/// <para /> Defaults to (0, 0).
	/// </summary>
	public Point16 CoordinatePaddingFix
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinatePaddingFix;
			}
			return _tileObjectCoords.paddingFix;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectCoords)
			{
				if (_tileObjectCoords.paddingFix == value)
				{
					return;
				}
				_hasOwnTileObjectCoords = true;
				_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
			}
			_tileObjectCoords.paddingFix = value;
			_tileObjectCoords.calculated = false;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].CoordinatePaddingFix = value;
				}
			}
		}
	}

	/// <summary>
	/// The full width of a multitile in terms of the spritesheet texture coordinates (NOT world coordinates).
	/// <para /> Used to determine which style and alternate placement a specific Tile with <see cref="P:Terraria.Tile.TileFrameX" /> and <see cref="P:Terraria.Tile.TileFrameY" /> values belongs to.
	/// <para /> Equal to <c>(<see cref="P:Terraria.ObjectData.TileObjectData.CoordinateWidth" /> + <see cref="P:Terraria.ObjectData.TileObjectData.CoordinatePadding" />) * <see cref="P:Terraria.ObjectData.TileObjectData.Width" /> + <see cref="P:Terraria.ObjectData.TileObjectData.CoordinatePaddingFix" />.X</c>.
	/// </summary>
	public int CoordinateFullWidth
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinateFullWidth;
			}
			if (!_tileObjectCoords.calculated)
			{
				Calculate();
			}
			return _tileObjectCoords.styleWidth;
		}
	}

	/// <summary>
	/// The full height of a multitile in terms of the spritesheet texture coordinates (NOT world coordinates).
	/// <para /> Used to determine which style and alternate placement a specific Tile with <see cref="P:Terraria.Tile.TileFrameX" /> and <see cref="P:Terraria.Tile.TileFrameY" /> values belongs to.
	/// <para /> Equal to <c><see cref="P:Terraria.ObjectData.TileObjectData.CoordinateHeights" />.Sum() + (<see cref="P:Terraria.ObjectData.TileObjectData.CoordinatePadding" /> * <see cref="P:Terraria.ObjectData.TileObjectData.Height" />) + <see cref="P:Terraria.ObjectData.TileObjectData.CoordinatePaddingFix" />.Y</c>.
	/// </summary>
	public int CoordinateFullHeight
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.CoordinateFullHeight;
			}
			if (!_tileObjectCoords.calculated)
			{
				Calculate();
			}
			return _tileObjectCoords.styleHeight;
		}
	}

	/// <summary>
	/// Offsets the tile placement preview by a number of placement styles.
	/// <para /> This can be used for tiles that have custom draw code to show something resembling the final placed tile despite the actual placement sprite missing parts of the final visuals. <see cref="F:Terraria.ID.TileID.Mannequin" /> and <see cref="F:Terraria.ID.TileID.GolfCupFlag" /> use this.
	/// <para /> Defaults to 0.
	/// </summary>
	public int DrawStyleOffset
	{
		get
		{
			if (_tileObjectCoords == null)
			{
				return _baseObject.DrawStyleOffset;
			}
			return _tileObjectCoords.drawStyleOffset;
		}
		set
		{
			WriteCheck();
			if (!_hasOwnTileObjectCoords)
			{
				if (_tileObjectCoords.drawStyleOffset == value)
				{
					return;
				}
				_hasOwnTileObjectCoords = true;
				_tileObjectCoords = new TileObjectCoordinatesModule(_tileObjectCoords);
			}
			_tileObjectCoords.drawStyleOffset = value;
			_tileObjectCoords.calculated = false;
			if (_linkedAlternates)
			{
				for (int i = 0; i < _alternates.data.Count; i++)
				{
					_alternates.data[i].DrawStyleOffset = value;
				}
			}
		}
	}

	public int AlternatesCount => Alternates.Count;

	public TileObjectData(TileObjectData copyFrom = null)
	{
		_parent = null;
		_linkedAlternates = false;
		if (copyFrom == null)
		{
			_usesCustomCanPlace = false;
			_alternates = null;
			_anchor = null;
			_anchorTiles = null;
			_tileObjectBase = null;
			_liquidDeath = null;
			_liquidPlacement = null;
			_placementHooks = null;
			_tileObjectDraw = null;
			_tileObjectStyle = null;
			_tileObjectCoords = null;
		}
		else
		{
			CopyFrom(copyFrom);
		}
	}

	/// <summary>
	/// Copies all tile object data from the <paramref name="copy" /> to this TileObjectData <b>except</b> the subtiles. <see cref="M:Terraria.ObjectData.TileObjectData.FullCopyFrom(Terraria.ObjectData.TileObjectData)" /> will additionally copy subtiles.
	/// <para /> Copying tile data will copy anchors, sizes, origin, and all other properties of the tile dictating their placement behavior.
	/// <para /> The most common usage is to copy the tile properties of one of the existing tile templates found in the TileObjectData class: <c>TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);</c>. After copying from a template, the properties would be further customized if desired.
	/// <para /> Another common usage would be to copy a vanilla furniture tile to easily create tiles matching the behavior of existing furniture: <c>TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(TileID.OpenDoor, 0));</c>. CopyFrom is used instead of FullCopyFrom in this situation because copying subtiles would incorrectly copy over subtile specific behavior, such as lava immunity for some subtiles.
	/// <para /> One more usage is to copy over tile properties from <see cref="F:Terraria.ObjectData.TileObjectData.newTile" /> to either <see cref="F:Terraria.ObjectData.TileObjectData.newAlternate" /> or <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" />: <c>TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);</c>.
	/// <para /> Copying tile properties is a good approach to writing clean code and avoiding hard to troubleshoot bugs. Make sure to call this method before modifying any other properties as this method will overwrite any changes made earlier in the code.
	/// </summary>
	/// <param name="copy"></param>
	public void CopyFrom(TileObjectData copy)
	{
		if (copy == null)
		{
			return;
		}
		_usesCustomCanPlace = copy._usesCustomCanPlace;
		_alternates = copy._alternates;
		_anchor = copy._anchor;
		_anchorTiles = copy._anchorTiles;
		_tileObjectBase = copy._tileObjectBase;
		_liquidDeath = copy._liquidDeath;
		_liquidPlacement = copy._liquidPlacement;
		_placementHooks = copy._placementHooks;
		_tileObjectDraw = copy._tileObjectDraw;
		_tileObjectStyle = copy._tileObjectStyle;
		_tileObjectCoords = copy._tileObjectCoords;
		if (_hasOwnAlternates || _hasOwnAnchor || _hasOwnAnchorTiles || _hasOwnLiquidDeath || _hasOwnLiquidPlacement || _hasOwnPlacementHooks || _hasOwnSubTiles || _hasOwnTileObjectBase || _hasOwnTileObjectDraw || _hasOwnTileObjectStyle || _hasOwnTileObjectCoords)
		{
			_hasOwnAlternates = (_hasOwnAnchor = (_hasOwnAnchorTiles = (_hasOwnLiquidDeath = (_hasOwnLiquidPlacement = (_hasOwnPlacementHooks = (_hasOwnSubTiles = (_hasOwnTileObjectBase = (_hasOwnTileObjectDraw = (_hasOwnTileObjectStyle = (_hasOwnTileObjectCoords = false))))))))));
			if (ModCompile.activelyModding)
			{
				Logging.tML.Warn((object)$"Bad TileObjectData values detected.\nLook at this ModTile and the ModTile that would load immediately before this ModTile and ensure that all modifications to TileObjectData.newTile are done between CopyFrom and AddTile.\nThe previous ModTile would typically be alphabetically before the tile in this stack trace.\nSee https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#basic-tileobjectdatanewtile-structure for more information.\n{new StackTrace(fNeedFileInfo: true)}");
			}
		}
	}

	/// <inheritdoc cref="M:Terraria.ObjectData.TileObjectData.FullCopyFrom(Terraria.ObjectData.TileObjectData)" />
	public void FullCopyFrom(ushort tileType)
	{
		FullCopyFrom(GetTileData(tileType, 0));
	}

	/// <summary>
	/// Similar to <see cref="M:Terraria.ObjectData.TileObjectData.CopyFrom(Terraria.ObjectData.TileObjectData)" />, except subtile data will also be copied. This is useful for inheriting subtile specific data, such as how each herb tile type (ImmatureHerbs, MatureHerbs, BloomingHerbs) share the same water placement and lava death properties for their Fireblossom and Moonglow styles.
	/// </summary>
	/// <param name="copy"></param>
	public void FullCopyFrom(TileObjectData copy)
	{
		if (copy != null)
		{
			_usesCustomCanPlace = copy._usesCustomCanPlace;
			_alternates = copy._alternates;
			_anchor = copy._anchor;
			_anchorTiles = copy._anchorTiles;
			_tileObjectBase = copy._tileObjectBase;
			_liquidDeath = copy._liquidDeath;
			_liquidPlacement = copy._liquidPlacement;
			_placementHooks = copy._placementHooks;
			_tileObjectDraw = copy._tileObjectDraw;
			_tileObjectStyle = copy._tileObjectStyle;
			_tileObjectCoords = copy._tileObjectCoords;
			_subTiles = new TileObjectSubTilesModule(copy._subTiles);
			_hasOwnSubTiles = true;
		}
	}

	private void SetupBaseObject()
	{
		_alternates = new TileObjectAlternatesModule();
		_hasOwnAlternates = true;
		Alternates = new List<TileObjectData>();
		_anchor = new AnchorDataModule();
		_hasOwnAnchor = true;
		AnchorTop = default;
		AnchorBottom = default;
		AnchorLeft = default;
		AnchorRight = default;
		AnchorWall = false;
		_anchorTiles = new AnchorTypesModule();
		_hasOwnAnchorTiles = true;
		AnchorValidTiles = null;
		AnchorInvalidTiles = null;
		AnchorAlternateTiles = null;
		AnchorValidWalls = null;
		_liquidDeath = new LiquidDeathModule();
		_hasOwnLiquidDeath = true;
		WaterDeath = false;
		LavaDeath = false;
		_liquidPlacement = new LiquidPlacementModule();
		_hasOwnLiquidPlacement = true;
		WaterPlacement = LiquidPlacement.Allowed;
		LavaPlacement = LiquidPlacement.NotAllowed;
		_placementHooks = new TilePlacementHooksModule();
		_hasOwnPlacementHooks = true;
		HookCheckIfCanPlace = default;
		HookPostPlaceEveryone = default;
		HookPostPlaceMyPlayer = default;
		HookPlaceOverride = default;
		SubTiles = new List<TileObjectData>(TileID.Count);
		_tileObjectBase = new TileObjectBaseModule();
		_hasOwnTileObjectBase = true;
		Width = 1;
		Height = 1;
		Origin = Point16.Zero;
		Direction = TileObjectDirection.None;
		RandomStyleRange = 0;
		FlattenAnchors = false;
		_tileObjectCoords = new TileObjectCoordinatesModule();
		_hasOwnTileObjectCoords = true;
		CoordinateHeights = new int[1] { 16 };
		CoordinateWidth = 0;
		CoordinatePadding = 0;
		CoordinatePaddingFix = Point16.Zero;
		_tileObjectDraw = new TileObjectDrawModule();
		_hasOwnTileObjectDraw = true;
		DrawYOffset = 0;
		DrawFlipHorizontal = false;
		DrawFlipVertical = false;
		DrawStepDown = 0;
		_tileObjectStyle = new TileObjectStyleModule();
		_hasOwnTileObjectStyle = true;
		Style = 0;
		StyleHorizontal = false;
		StyleWrapLimit = 0;
		StyleMultiplier = 1;
	}

	private void Calculate()
	{
		if (_tileObjectCoords.calculated)
		{
			return;
		}
		_tileObjectCoords.calculated = true;
		_tileObjectCoords.styleWidth = (_tileObjectCoords.width + _tileObjectCoords.padding) * Width + _tileObjectCoords.paddingFix.X;
		int num = 0;
		_tileObjectCoords.styleHeight = 0;
		for (int i = 0; i < _tileObjectCoords.heights.Length; i++)
		{
			num += _tileObjectCoords.heights[i] + _tileObjectCoords.padding;
		}
		num += _tileObjectCoords.paddingFix.Y;
		_tileObjectCoords.styleHeight = num;
		if (_hasOwnLiquidDeath)
		{
			if (_liquidDeath.lava)
			{
				LavaPlacement = LiquidPlacement.NotAllowed;
			}
			if (_liquidDeath.water)
			{
				WaterPlacement = LiquidPlacement.NotAllowed;
			}
		}
		if (Height == _tileObjectCoords.heights.Length || Height < _tileObjectCoords.heights.Length)
		{
			return;
		}
		FixNewTile();
		throw new Exception($"TileObjectData Height and CoordinateHeights are incorrect. Height must equal the number of values in CoordinateHeights: [{string.Join(", ", _tileObjectCoords.heights)}] is not {Height} elements long.")
		{
			HelpLink = "https://github.com/tModLoader/tModLoader/wiki/Basic-Tile"
		};
	}

	private void WriteCheck()
	{
		if (readOnlyData)
		{
			throw new FieldAccessException("Tile data is locked and only accessible during startup.");
		}
	}

	private void LockWrites()
	{
		readOnlyData = true;
	}

	public bool LiquidPlace(Tile checkTile, bool checkStay = false)
	{
		if (checkTile == null)
		{
			return false;
		}
		if (checkTile.liquid > 0)
		{
			switch (checkTile.liquidType())
			{
			case 1:
				if (!checkStay || LavaDeath)
				{
					if (LavaPlacement == LiquidPlacement.NotAllowed)
					{
						return false;
					}
					if (LavaPlacement == LiquidPlacement.OnlyInFullLiquid && checkTile.liquid != byte.MaxValue)
					{
						return false;
					}
				}
				break;
			case 0:
			case 2:
			case 3:
				if (!checkStay || WaterDeath)
				{
					if (WaterPlacement == LiquidPlacement.NotAllowed)
					{
						return false;
					}
					if (WaterPlacement == LiquidPlacement.OnlyInFullLiquid && checkTile.liquid != byte.MaxValue)
					{
						return false;
					}
				}
				break;
			}
		}
		else
		{
			switch (checkTile.liquidType())
			{
			case 1:
				if (LavaPlacement == LiquidPlacement.OnlyInFullLiquid || LavaPlacement == LiquidPlacement.OnlyInLiquid)
				{
					return false;
				}
				break;
			case 0:
			case 2:
			case 3:
				if (WaterPlacement == LiquidPlacement.OnlyInFullLiquid || WaterPlacement == LiquidPlacement.OnlyInLiquid)
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	/// <summary>
	/// If the tile type is in <see cref="P:Terraria.ObjectData.TileObjectData.AnchorInvalidTiles" />, returns false. If <see cref="P:Terraria.ObjectData.TileObjectData.AnchorValidTiles" /> exists and tile type isn't in it, returns false. Otherwise returns true.
	/// </summary>
	public bool isValidTileAnchor(int type)
	{
		int[] array;
		int[] array2;
		if (_anchorTiles == null)
		{
			array = null;
			array2 = null;
		}
		else
		{
			array = _anchorTiles.tileValid;
			array2 = _anchorTiles.tileInvalid;
		}
		if (array2 != null)
		{
			for (int i = 0; i < array2.Length; i++)
			{
				if (type == array2[i])
				{
					return false;
				}
			}
		}
		if (array == null)
		{
			return true;
		}
		for (int j = 0; j < array.Length; j++)
		{
			if (type == array[j])
			{
				return true;
			}
		}
		return false;
	}

	public bool isValidWallAnchor(int type)
	{
		int[] array = ((_anchorTiles != null) ? _anchorTiles.wallValid : null);
		if (array == null)
		{
			if (type == 0)
			{
				return false;
			}
			return true;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (type == array[i])
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// If the tile type is in <see cref="P:Terraria.ObjectData.TileObjectData.AnchorAlternateTiles" />, returns true.
	/// </summary>
	public bool isValidAlternateAnchor(int type)
	{
		if (_anchorTiles == null)
		{
			return false;
		}
		int[] tileAlternates = _anchorTiles.tileAlternates;
		if (tileAlternates == null)
		{
			return false;
		}
		for (int i = 0; i < tileAlternates.Length; i++)
		{
			if (type == tileAlternates[i])
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Calculates a placement style from a provided tile style and random style offset. A placement style is the tile style multiplied by <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" /> plus the alternate and random placement style offsets.
	/// <para /> The <paramref name="alternate" /> parameter is unused, but the <see cref="P:Terraria.ObjectData.TileObjectData.Style" /> of this TileObjectData (equivalent to the value passed into <see cref="M:Terraria.ObjectData.TileObjectData.addAlternate(System.Int32)" />) is used to offset the style to account for alternate placements. As such, this method should be called on the alternate TileObjectData if applicable, not the root TileObjectData for this tile style. Do this by first retrieving it by calling the <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" /> method.
	/// </summary>
	public int CalculatePlacementStyle(int style, int alternate, int random)
	{
		int num = style * StyleMultiplier;
		num += Style;
		if (random >= 0)
		{
			num += random;
		}
		return num;
	}

	/// <summary>
	/// Similar to <see cref="M:Terraria.ObjectData.TileObjectData.addTile(System.Int32)" /> except the TileObjectData is not registered to a specific tile type. Instead, the completed TileObjectData is assigned to <paramref name="baseTile" />. This can be used by modders to create their own tile templates.
	/// </summary>
	public static void addBaseTile(out TileObjectData baseTile)
	{
		newTile.Calculate();
		baseTile = newTile;
		baseTile._parent = _baseObject;
		newTile = new TileObjectData(_baseObject);
	}

	/// <summary>
	/// Registers the current <see cref="F:Terraria.ObjectData.TileObjectData.newTile" /> with the supplied tile type. Make sure to call this method last after all edits to <see cref="F:Terraria.ObjectData.TileObjectData.newTile" />, <see cref="F:Terraria.ObjectData.TileObjectData.newAlternate" />, and <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" />.
	/// </summary>
	public static void addTile(int tileType)
	{
		newTile.Calculate();
		_data[tileType] = newTile;
		newTile = new TileObjectData(_baseObject);
	}

	/// <inheritdoc cref="M:Terraria.ObjectData.TileObjectData.addSubTile(System.Int32)" />
	public static void addSubTile(params int[] styles)
	{
		newSubTile.Calculate();
		foreach (int num in styles)
		{
			List<TileObjectData> list;
			if (!newTile._hasOwnSubTiles)
			{
				list = new List<TileObjectData>(num + 1);
				newTile.SubTiles = list;
			}
			else
			{
				list = newTile.SubTiles;
			}
			if (list.Count <= num)
			{
				for (int j = list.Count; j <= num; j++)
				{
					list.Add(null);
				}
			}
			newSubTile._parent = newTile;
			list[num] = newSubTile;
		}
		newSubTile = new TileObjectData(_baseObject);
	}

	/// <summary>
	/// Registers <see cref="F:Terraria.ObjectData.TileObjectData.newSubTile" /> as a subtile of <see cref="F:Terraria.ObjectData.TileObjectData.newTile" />. The style or styles parameter correspond to the tiles styles that should use these tile properties. This corresponds to the <see cref="F:Terraria.Item.placeStyle" /> of the item that will place this tile.
	/// <para /> Read the <see href="https://github.com/tModLoader/tModLoader/wiki/Basic-Tile#multiple-styles">Multiple Styles section of the Basic Tile wiki guide</see> for more information and visualizations.
	/// </summary>
	private static void addSubTile(int style)
	{
		newSubTile.Calculate();
		List<TileObjectData> list;
		if (!newTile._hasOwnSubTiles)
		{
			list = new List<TileObjectData>(style + 1);
			newTile.SubTiles = list;
		}
		else
		{
			list = newTile.SubTiles;
		}
		if (list.Count <= style)
		{
			for (int i = list.Count; i <= style; i++)
			{
				list.Add(null);
			}
		}
		newSubTile._parent = newTile;
		list[style] = newSubTile;
		newSubTile = new TileObjectData(_baseObject);
	}

	/// <summary>
	/// Registers <see cref="F:Terraria.ObjectData.TileObjectData.newAlternate" /> as an alternate placement of <see cref="F:Terraria.ObjectData.TileObjectData.newTile" />. The <paramref name="baseStyle" /> parameter corresponds to which placement style sprite this alternate placement will use. This value will apply an offset to the tile style being placed.
	/// <para /> <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/ExampleTorch.cs#L44">ExampleTorch</see>, for example, shows alternate placements using the 0, 1, and 2 placement style sprites, showing how different anchors can result in different placements sprites. <see href="https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/Furniture/ExampleDoorClosed.cs#L59">ExampleDoorClosed</see>, on the other hand, adds multiple alternates all using the same 0 placement sprite but different <see cref="P:Terraria.ObjectData.TileObjectData.Origin" /> to allow the user an easier time placing the door in a doorway.
	/// <para /> <see cref="P:Terraria.ObjectData.TileObjectData.StyleMultiplier" /> is usually used to account for alternate placements to allow them to be considered the same tile style. If <see cref="P:Terraria.ObjectData.TileObjectData.RandomStyleRange" /> is used, the random style will be added after the alternate placement style has been applied, so usually multiples of RandomStyleRange would be used for each addAlternate parameter. (For example, if RandomStyleRange is 5 for a tile with left and right placements, addAlternate(5) could be used for the right facing placement and StyleMultiplier should be set to 10.)
	/// </summary>
	public static void addAlternate(int baseStyle)
	{
		newAlternate.Calculate();
		if (!newTile._hasOwnAlternates)
		{
			newTile.Alternates = new List<TileObjectData>();
		}
		newAlternate.Style = baseStyle;
		newAlternate._parent = newTile;
		newTile.Alternates.Add(newAlternate);
		newAlternate = new TileObjectData(_baseObject);
	}

	public static void Initialize()
	{
		_baseObject = new TileObjectData();
		_baseObject.SetupBaseObject();
		_data = new List<TileObjectData>(TileID.Count);
		for (int i = 0; i < TileID.Count; i++)
		{
			_data.Add(null);
		}
		newTile = new TileObjectData(_baseObject);
		newSubTile = new TileObjectData(_baseObject);
		newAlternate = new TileObjectData(_baseObject);
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.StyleMultiplier = 27;
		newTile.StyleWrapLimit = 27;
		newTile.UsesCustomCanPlace = false;
		newTile.LavaDeath = true;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(13, 43, 47);
		addTile(19);
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.StyleMultiplier = 27;
		newTile.StyleWrapLimit = 27;
		newTile.UsesCustomCanPlace = false;
		newTile.LavaDeath = true;
		addTile(427);
		for (int j = 435; j <= 439; j++)
		{
			newTile.CoordinateHeights = new int[1] { 16 };
			newTile.CoordinateWidth = 16;
			newTile.CoordinatePadding = 2;
			newTile.StyleHorizontal = true;
			newTile.StyleMultiplier = 27;
			newTile.StyleWrapLimit = 27;
			newTile.UsesCustomCanPlace = false;
			newTile.LavaDeath = true;
			addTile(j);
		}
		newTile.Width = 4;
		newTile.Height = 8;
		newTile.Origin = new Point16(1, 7);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.HookPlaceOverride = new PlacementHook(WorldGen.PlaceXmasTree_Direct, -1, 0, processedCoordinates: true);
		newTile.CoordinateHeights = new int[8] { 16, 16, 16, 16, 16, 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 0;
		addTile(171);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.EmptyTile, newTile.Width, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[1] { 38 };
		newTile.CoordinateWidth = 32;
		newTile.CoordinatePadding = 2;
		newTile.DrawYOffset = -20;
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		addBaseTile(out StyleDye);
		newTile.CopyFrom(StyleDye);
		newSubTile.CopyFrom(StyleDye);
		newSubTile.AnchorValidWalls = new int[1];
		addSubTile(3);
		newSubTile.CopyFrom(StyleDye);
		newSubTile.AnchorValidWalls = new int[1];
		addSubTile(4);
		newSubTile.CopyFrom(StyleDye);
		newSubTile.WaterPlacement = LiquidPlacement.OnlyInFullLiquid;
		addSubTile(5);
		newSubTile.CopyFrom(StyleDye);
		newSubTile.AnchorValidTiles = new int[1] { 80 };
		newSubTile.AnchorLeft = new AnchorData(AnchorType.EmptyTile, 1, 1);
		newSubTile.AnchorRight = new AnchorData(AnchorType.EmptyTile, 1, 1);
		addSubTile(6);
		newSubTile.CopyFrom(StyleDye);
		newSubTile.DrawYOffset = -6;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		newSubTile.AnchorTop = new AnchorData(AnchorType.SolidTile, newSubTile.Width, 0);
		newSubTile.AnchorBottom = new AnchorData(AnchorType.EmptyTile, newSubTile.Width, 0);
		addSubTile(7);
		addTile(227);
		newTile.CopyFrom(StyleDye);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.CoordinateWidth = 20;
		newTile.CoordinatePadding = 2;
		newTile.DrawYOffset = -2;
		newTile.AnchorTop = AnchorData.Empty;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.Table, newTile.Width, 0);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = false;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(579);
		newTile.Width = 1;
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = false;
		newTile.StyleWrapLimit = 36;
		newTile.StyleLineSkip = 3;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 1);
		addAlternate(0);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 2);
		addAlternate(0);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(19, 48);
		addTile(10);
		newTile.Width = 2;
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile, 1, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = false;
		newTile.StyleWrapLimit = 36;
		newTile.StyleLineSkip = 2;
		newTile.Direction = TileObjectDirection.PlaceRight;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 1);
		addAlternate(0);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 2);
		addAlternate(0);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.Direction = TileObjectDirection.PlaceLeft;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 1);
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.Direction = TileObjectDirection.PlaceLeft;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 2);
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 1);
		newAlternate.Direction = TileObjectDirection.PlaceLeft;
		addAlternate(1);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(19, 48);
		addTile(11);
		newTile.Width = 1;
		newTile.Height = 5;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = -2;
		newTile.CoordinateHeights = new int[5] { 18, 16, 16, 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleMultiplier = 2;
		newTile.StyleWrapLimit = 2;
		for (int k = 1; k < 5; k++)
		{
			newAlternate.CopyFrom(newTile);
			newAlternate.Origin = new Point16(0, k);
			addAlternate(0);
		}
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		for (int l = 1; l < 5; l++)
		{
			newAlternate.CopyFrom(newTile);
			newAlternate.Origin = new Point16(0, l);
			newAlternate.Direction = TileObjectDirection.PlaceRight;
			addAlternate(1);
		}
		addTile(388);
		newTile.FullCopyFrom(388);
		addTile(389);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.Table, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		addBaseTile(out StyleOnTable1x1);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		addTile(13);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawYOffset = -4;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(25, 41);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(39);
		addTile(33);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawYOffset = -4;
		addTile(49);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, newTile.Width, 0);
		newTile.DrawYOffset = 2;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TEFoodPlatter.Hook_AfterPlacement, -1, 0, processedCoordinates: true);
		newTile.StyleHorizontal = true;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(520);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawYOffset = -4;
		addTile(372);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawYOffset = -4;
		addTile(646);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.StyleHorizontal = true;
		newTile.RandomStyleRange = 5;
		addTile(50);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, newTile.Width, 0);
		newTile.DrawYOffset = 2;
		addTile(494);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		addTile(78);
		newTile.CopyFrom(StyleOnTable1x1);
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawYOffset = -4;
		addTile(174);
		newTile.Width = 1;
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		addBaseTile(out Style1xX);
		newTile.CopyFrom(Style1xX);
		newTile.StyleWrapLimitVisualOverride = 37;
		newTile.StyleLineSkip = 2;
		newTile.DrawYOffset = 2;
		newTile.WaterDeath = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(23, 42);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(40);
		addTile(93);
		newTile.CopyFrom(Style1xX);
		newTile.Height = 6;
		newTile.Origin = new Point16(0, 5);
		newTile.CoordinateHeights = new int[6] { 16, 16, 16, 16, 16, 16 };
		addTile(92);
		newTile.CopyFrom(Style1xX);
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		newTile.StyleHorizontal = true;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(453);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.PlanterBox, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style1x2Top);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(270);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(271);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(581);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(660);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newTile.StyleWrapLimit = 6;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(572);
		newTile.CopyFrom(Style1x2Top);
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(32, 48);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(46);
		addTile(42);
		newTile.CopyFrom(Style1x2Top);
		newTile.Height = 3;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.StyleHorizontal = true;
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, newTile.Width, 0);
		newTile.StyleWrapLimit = 111;
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(91);
		newTile.Width = 4;
		newTile.Height = 2;
		newTile.Origin = new Point16(1, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		addTile(487);
		newTile.Width = 4;
		newTile.Height = 2;
		newTile.Origin = new Point16(1, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleMultiplier = 2;
		newTile.StyleWrapLimit = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addBaseTile(out Style4x2);
		newTile.CopyFrom(Style4x2);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(25, 42);
		addTile(90);
		newTile.CopyFrom(Style4x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinatePaddingFix = new Point16(0, -2);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(8, 42);
		addTile(79);
		newTile.Width = 4;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 2, 1);
		newTile.UsesCustomCanPlace = true;
		newTile.LavaDeath = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.StyleHorizontal = true;
		newTile.CoordinatePadding = 2;
		addTile(209);
		newTile.Width = 3;
		newTile.Height = 2;
		newTile.Origin = new Point16(1, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = 0;
		addBaseTile(out StyleSmallCage);
		newTile.CopyFrom(StyleSmallCage);
		addTile(285);
		newTile.CopyFrom(StyleSmallCage);
		addTile(286);
		newTile.CopyFrom(StyleSmallCage);
		addTile(582);
		newTile.CopyFrom(StyleSmallCage);
		addTile(619);
		newTile.CopyFrom(StyleSmallCage);
		addTile(298);
		newTile.CopyFrom(StyleSmallCage);
		addTile(299);
		newTile.CopyFrom(StyleSmallCage);
		addTile(310);
		newTile.CopyFrom(StyleSmallCage);
		addTile(532);
		newTile.CopyFrom(StyleSmallCage);
		addTile(533);
		newTile.CopyFrom(StyleSmallCage);
		addTile(339);
		newTile.CopyFrom(StyleSmallCage);
		addTile(538);
		newTile.CopyFrom(StyleSmallCage);
		addTile(555);
		newTile.CopyFrom(StyleSmallCage);
		addTile(556);
		newTile.CopyFrom(StyleSmallCage);
		addTile(629);
		newTile.Width = 6;
		newTile.Height = 3;
		newTile.Origin = new Point16(3, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.DrawYOffset = 0;
		newTile.LavaDeath = true;
		addBaseTile(out Style6x3);
		newTile.CopyFrom(Style6x3);
		addTile(275);
		newTile.CopyFrom(Style6x3);
		addTile(276);
		newTile.CopyFrom(Style6x3);
		addTile(413);
		newTile.CopyFrom(Style6x3);
		addTile(414);
		newTile.CopyFrom(Style6x3);
		addTile(277);
		newTile.CopyFrom(Style6x3);
		addTile(278);
		newTile.CopyFrom(Style6x3);
		addTile(279);
		newTile.CopyFrom(Style6x3);
		addTile(280);
		newTile.CopyFrom(Style6x3);
		addTile(281);
		newTile.CopyFrom(Style6x3);
		addTile(632);
		newTile.CopyFrom(Style6x3);
		addTile(640);
		newTile.CopyFrom(Style6x3);
		addTile(643);
		newTile.CopyFrom(Style6x3);
		addTile(644);
		newTile.CopyFrom(Style6x3);
		addTile(645);
		newTile.CopyFrom(Style6x3);
		addTile(296);
		newTile.CopyFrom(Style6x3);
		addTile(297);
		newTile.CopyFrom(Style6x3);
		addTile(309);
		newTile.CopyFrom(Style6x3);
		addTile(550);
		newTile.CopyFrom(Style6x3);
		addTile(551);
		newTile.CopyFrom(Style6x3);
		addTile(553);
		newTile.CopyFrom(Style6x3);
		addTile(554);
		newTile.CopyFrom(Style6x3);
		addTile(558);
		newTile.CopyFrom(Style6x3);
		addTile(559);
		newTile.CopyFrom(Style6x3);
		addTile(599);
		newTile.CopyFrom(Style6x3);
		addTile(600);
		newTile.CopyFrom(Style6x3);
		addTile(601);
		newTile.CopyFrom(Style6x3);
		addTile(602);
		newTile.CopyFrom(Style6x3);
		addTile(603);
		newTile.CopyFrom(Style6x3);
		addTile(604);
		newTile.CopyFrom(Style6x3);
		addTile(605);
		newTile.CopyFrom(Style6x3);
		addTile(606);
		newTile.CopyFrom(Style6x3);
		addTile(607);
		newTile.CopyFrom(Style6x3);
		addTile(608);
		newTile.CopyFrom(Style6x3);
		addTile(609);
		newTile.CopyFrom(Style6x3);
		addTile(610);
		newTile.CopyFrom(Style6x3);
		addTile(611);
		newTile.CopyFrom(Style6x3);
		addTile(612);
		newTile.Width = 5;
		newTile.Height = 4;
		newTile.Origin = new Point16(2, 3);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[4] { 16, 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style5x4);
		newTile.CopyFrom(Style5x4);
		addTile(464);
		newTile.CopyFrom(Style5x4);
		addTile(466);
		newTile.Width = 2;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = true;
		addBaseTile(out Style2x1);
		newTile.CopyFrom(Style2x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.Table, newTile.Width, 0);
		addTile(29);
		newTile.CopyFrom(Style2x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.Table, newTile.Width, 0);
		addTile(103);
		newTile.CopyFrom(Style2x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.Table, newTile.Width, 0);
		addTile(462);
		newTile.CopyFrom(Style2x1);
		newTile.CoordinateHeights = new int[1] { 18 };
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(14, 43);
		addTile(18);
		newTile.CopyFrom(Style2x1);
		newTile.CoordinateHeights = new int[1] { 18 };
		addTile(16);
		newTile.CopyFrom(Style2x1);
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		addTile(134);
		newTile.CopyFrom(Style2x1);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.AnchorLeft = new AnchorData(AnchorType.SolidTile, newTile.Height, 0);
		newTile.AnchorRight = new AnchorData(AnchorType.SolidTile, newTile.Height, 0);
		addTile(387);
		newTile.CopyFrom(Style2x1);
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimit = 53;
		addTile(649);
		newTile.CopyFrom(Style2x1);
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidBottom, newTile.Width, 0);
		newAlternate.DrawYOffset = -2;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidBottom, newTile.Width, 0);
		newAlternate.DrawYOffset = -2;
		addAlternate(3);
		addTile(443);
		newTile.Width = 2;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		addBaseTile(out Style2xX);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 5;
		newTile.Origin = new Point16(1, 4);
		newTile.CoordinateHeights = new int[5] { 16, 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = true;
		addTile(547);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 5;
		newTile.Origin = new Point16(1, 4);
		newTile.CoordinateHeights = new int[5] { 16, 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = true;
		addTile(623);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 4;
		newTile.Origin = new Point16(1, 3);
		newTile.CoordinateHeights = new int[4] { 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		addTile(207);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 18 };
		addTile(410);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.DrawYOffset = 2;
		addTile(480);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.DrawYOffset = 2;
		addTile(509);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.DrawYOffset = 2;
		addTile(657);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.DrawYOffset = 2;
		addTile(658);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		addTile(489);
		newTile.CopyFrom(Style2xX);
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimit = 7;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(7);
		addTile(349);
		newTile.CopyFrom(Style2xX);
		addTile(337);
		newTile.CopyFrom(Style2xX);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.DrawYOffset = 2;
		addTile(560);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = default;
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, newTile.Width, 0);
		newTile.LavaDeath = true;
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.PlatformNonHammered, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(465);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = default;
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom, newTile.Width, 0);
		newTile.LavaDeath = true;
		addTile(531);
		newTile.CopyFrom(Style2xX);
		addTile(320);
		newTile.CopyFrom(Style2xX);
		addTile(456);
		newTile.CopyFrom(Style2xX);
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TETrainingDummy.Hook_AfterPlacement, -1, 0, processedCoordinates: false);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleMultiplier = 2;
		newTile.StyleWrapLimit = 2;
		newTile.DrawYOffset = 2;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(378);
		newTile.CopyFrom(Style2xX);
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimit = 55;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(165);
		addTile(105);
		newTile.CopyFrom(Style2xX);
		newTile.Origin = new Point16(0, 2);
		newTile.RandomStyleRange = 2;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleWrapLimit = 2;
		newTile.StyleMultiplier = 2;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(2);
		addTile(545);
		newTile.CopyFrom(Style2xX);
		newTile.DrawYOffset = 2;
		newTile.Height = 5;
		newTile.Origin = new Point16(0, 4);
		newTile.CoordinateHeights = new int[5] { 16, 16, 16, 16, 16 };
		newTile.LavaDeath = true;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(17, 43);
		addTile(104);
		newTile.CopyFrom(Style2xX);
		newTile.Origin = new Point16(0, 2);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		newTile.LavaDeath = true;
		addTile(128);
		newTile.CopyFrom(Style2xX);
		newTile.Origin = new Point16(0, 2);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = 2;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(506);
		newTile.CopyFrom(Style2xX);
		newTile.Origin = new Point16(0, 2);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		newTile.LavaDeath = true;
		addTile(269);
		newTile.CopyFrom(Style2xX);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.Origin = new Point16(0, 2);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.DrawStyleOffset = 4;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TEDisplayDoll.Hook_AfterPlacement, -1, 0, processedCoordinates: false);
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(470);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = default;
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, newTile.Width, 0);
		newTile.LavaDeath = true;
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.PlatformNonHammered, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(591);
		newTile.CopyFrom(Style2xX);
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = default;
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, newTile.Width, 0);
		newTile.LavaDeath = true;
		newTile.DrawYOffset = -2;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.PlatformNonHammered, newTile.Width, 0);
		newAlternate.DrawYOffset = -10;
		addAlternate(0);
		addTile(592);
		newTile.Width = 3;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		addBaseTile(out Style3x3);
		newTile.CopyFrom(Style3x3);
		newTile.Height = 6;
		newTile.Origin = new Point16(1, 5);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.CoordinateHeights = new int[6] { 16, 16, 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = true;
		newTile.StyleHorizontal = true;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(7);
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(8);
		addTile(548);
		newTile.CopyFrom(Style3x3);
		newTile.Height = 5;
		newTile.Origin = new Point16(1, 4);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.CoordinateHeights = new int[5] { 16, 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		newTile.LavaPlacement = LiquidPlacement.Allowed;
		newTile.StyleHorizontal = true;
		addTile(613);
		newTile.CopyFrom(Style3x3);
		newTile.Height = 6;
		newTile.Origin = new Point16(1, 5);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.CoordinateHeights = new int[6] { 16, 16, 16, 16, 16, 16 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		newTile.LavaPlacement = LiquidPlacement.Allowed;
		newTile.StyleHorizontal = true;
		addTile(614);
		newTile.CopyFrom(Style3x3);
		newTile.Origin = new Point16(1, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 1, 1);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.LavaDeath = true;
		newTile.StyleWrapLimit = 37;
		newTile.StyleHorizontal = false;
		newTile.StyleLineSkip = 2;
		newTile.DrawYOffset = -2;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(32, 48);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(46);
		addTile(34);
		newTile.CopyFrom(Style3x3);
		newTile.Width = 4;
		newTile.Origin = new Point16(2, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 1, 1);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = -2;
		addTile(454);
		newTile.Width = 3;
		newTile.Height = 2;
		newTile.Origin = new Point16(1, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = true;
		addBaseTile(out Style3x2);
		newTile.CopyFrom(Style3x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newSubTile.CopyFrom(Style3x2);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(13);
		newSubTile.CopyFrom(Style3x2);
		newSubTile.Height = 1;
		newSubTile.Origin = new Point16(1, 0);
		newSubTile.CoordinateHeights = new int[1] { 16 };
		addSubTile(25);
		addTile(14);
		newTile.CopyFrom(Style3x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newSubTile.CopyFrom(Style3x2);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(11);
		addTile(469);
		newTile.CopyFrom(Style3x2);
		newTile.StyleWrapLimitVisualOverride = 37;
		newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, processedCoordinates: true);
		newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, processedCoordinates: false);
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newTile.LavaDeath = false;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(9, 42);
		addTile(88);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		newTile.LavaPlacement = LiquidPlacement.Allowed;
		addTile(237);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(244);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(647);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimit = 35;
		addTile(648);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(651);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		addTile(26);
		newTile.CopyFrom(Style3x2);
		addTile(86);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(377);
		newTile.CopyFrom(Style3x2);
		newTile.StyleWrapLimitVisualOverride = 37;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(15, 42);
		addTile(87);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		addTile(486);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.AnchorValidTiles = new int[4] { 2, 477, 109, 492 };
		addTile(488);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimitVisualOverride = 37;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(10, 46);
		addTile(89);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		addTile(114);
		newTile.CopyFrom(Style3x2);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newSubTile.CopyFrom(newTile);
		newSubTile.AnchorValidTiles = new int[2] { 59, 70 };
		addSubTile(32, 33, 34);
		newSubTile.CopyFrom(newTile);
		newSubTile.AnchorValidTiles = new int[7] { 147, 161, 163, 200, 164, 162, 224 };
		addSubTile(26, 27, 28, 29, 30, 31);
		addTile(186);
		newTile.CopyFrom(Style3x2);
		newTile.StyleWrapLimit = 35;
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newSubTile.CopyFrom(newTile);
		newSubTile.AnchorValidTiles = new int[3] { 59, 60, 226 };
		addSubTile(0, 1, 2, 3, 4, 5);
		newSubTile.CopyFrom(newTile);
		newSubTile.AnchorValidTiles = new int[4] { 57, 58, 75, 76 };
		addSubTile(6, 7, 8);
		newSubTile.CopyFrom(newTile);
		newSubTile.AnchorValidTiles = new int[12]
		{
			53, 397, 396, 112, 398, 400, 234, 399, 401, 116,
			402, 403
		};
		addSubTile(29, 30, 31, 32, 33, 34);
		addTile(187);
		newTile.CopyFrom(Style3x2);
		newTile.AnchorValidTiles = new int[4] { 53, 112, 234, 116 };
		newTile.WaterDeath = true;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = 2;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		newTile.RandomStyleRange = 4;
		addTile(552);
		newTile.CopyFrom(Style3x2);
		newTile.StyleWrapLimit = 16;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		newTile.WaterDeath = true;
		newTile.LavaDeath = true;
		newTile.DrawYOffset = 2;
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(1);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(4);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(9);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(1 + newTile.StyleWrapLimit);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(4 + newTile.StyleWrapLimit);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(9 + newTile.StyleWrapLimit);
		addTile(215);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(217);
		newTile.CopyFrom(Style3x2);
		newTile.DrawYOffset = 2;
		addTile(218);
		newTile.CopyFrom(Style3x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		addTile(17);
		newTile.CopyFrom(Style3x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.LavaDeath = false;
		addTile(77);
		newTile.CopyFrom(Style3x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.DrawYOffset = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		addTile(133);
		newTile.CopyFrom(Style3x2);
		addTile(405);
		newTile.Width = 3;
		newTile.Height = 1;
		newTile.Origin = new Point16(1, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		addTile(235);
		newTile.Width = 3;
		newTile.Height = 4;
		newTile.Origin = new Point16(1, 3);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[4] { 16, 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = true;
		addBaseTile(out Style3x4);
		newTile.CopyFrom(Style3x4);
		newTile.StyleWrapLimitVisualOverride = 37;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(4, 43);
		addTile(101);
		newTile.CopyFrom(Style3x4);
		newTile.DrawYOffset = 2;
		addTile(102);
		newTile.CopyFrom(Style3x4);
		newTile.DrawYOffset = 2;
		addTile(463);
		newTile.CopyFrom(Style3x4);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TEHatRack.Hook_AfterPlacement, -1, 0, processedCoordinates: false);
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(475);
		newTile.CopyFrom(Style3x4);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.HookCheckIfCanPlace = new PlacementHook(TETeleportationPylon.PlacementPreviewHook_CheckIfCanPlace, 1, 0, processedCoordinates: true);
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TETeleportationPylon.PlacementPreviewHook_AfterPlacement, -1, 0, processedCoordinates: false);
		newTile.StyleHorizontal = true;
		addTile(597);
		newTile.CopyFrom(Style3x4);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleHorizontal = false;
		newTile.StyleWrapLimitVisualOverride = 2;
		newTile.StyleMultiplier = 2;
		newTile.StyleWrapLimit = 2;
		newTile.styleLineSkipVisualOverride = 0;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(617);
		newTile.Width = 2;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style2x2);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, processedCoordinates: true);
		newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, processedCoordinates: false);
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		addTile(21);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, processedCoordinates: true);
		newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, processedCoordinates: false);
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		addTile(467);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		addTile(441);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.AnchorInvalidTiles = new int[5] { 127, 138, 664, 665, 484 };
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		addTile(468);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.StyleWrapLimit = 6;
		newTile.StyleMultiplier = 6;
		newTile.RandomStyleRange = 6;
		newTile.AnchorValidTiles = new int[4] { 2, 477, 109, 492 };
		addTile(254);
		newTile.CopyFrom(Style2x2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.StyleHorizontal = true;
		addTile(96);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.StyleWrapLimit = 4;
		newTile.StyleMultiplier = 1;
		newTile.RandomStyleRange = 4;
		newTile.StyleHorizontal = true;
		addTile(485);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.DrawYOffset = 2;
		newTile.RandomStyleRange = 5;
		newTile.StyleHorizontal = true;
		addTile(457);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.DrawYOffset = 2;
		newTile.StyleHorizontal = true;
		addTile(490);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.StyleWrapLimitVisualOverride = 56;
		newTile.styleLineSkipVisualOverride = 2;
		addTile(139);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.RandomStyleRange = 9;
		addTile(35);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		newTile.StyleHorizontal = true;
		addTile(652);
		int styleWrapLimit = 3;
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		newTile.StyleHorizontal = true;
		newTile.StyleWrapLimit = styleWrapLimit;
		addTile(653);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(1, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.DrawYOffset = -2;
		addTile(95);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(1, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.DrawYOffset = -2;
		addTile(126);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(1, 0);
		newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.DrawYOffset = -2;
		addTile(444);
		newTile.CopyFrom(Style2x2);
		newTile.WaterDeath = true;
		addTile(98);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(13, 43);
		addTile(172);
		newTile.CopyFrom(Style2x2);
		addTile(94);
		newTile.CopyFrom(Style2x2);
		newTile.LavaDeath = false;
		addTile(411);
		newTile.CopyFrom(Style2x2);
		addTile(97);
		newTile.CopyFrom(Style2x2);
		newTile.LavaDeath = false;
		addTile(99);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		newSubTile.CopyFrom(newTile);
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(25, 42);
		newSubTile.CopyFrom(newTile);
		newSubTile.WaterDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(40);
		addTile(100);
		newTile.CopyFrom(Style2x2);
		addTile(125);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(621);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(622);
		newTile.CopyFrom(Style2x2);
		addTile(173);
		newTile.CopyFrom(Style2x2);
		addTile(287);
		newTile.CopyFrom(Style2x2);
		addTile(319);
		newTile.CopyFrom(Style2x2);
		addTile(287);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.StyleHorizontal = true;
		addTile(376);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.LavaDeath = false;
		addTile(138);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.LavaDeath = false;
		addTile(664);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = true;
		addTile(654);
		newTile.CopyFrom(Style2x2);
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.LavaDeath = true;
		addTile(484);
		newTile.CopyFrom(Style2x2);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		addTile(142);
		newTile.CopyFrom(Style2x2);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		addTile(143);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(282);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(543);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(598);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(568);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(569);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(570);
		newTile.CopyFrom(Style2x2);
		addTile(288);
		newTile.CopyFrom(Style2x2);
		addTile(289);
		newTile.CopyFrom(Style2x2);
		addTile(290);
		newTile.CopyFrom(Style2x2);
		addTile(291);
		newTile.CopyFrom(Style2x2);
		addTile(292);
		newTile.CopyFrom(Style2x2);
		addTile(293);
		newTile.CopyFrom(Style2x2);
		addTile(294);
		newTile.CopyFrom(Style2x2);
		addTile(295);
		newTile.CopyFrom(Style2x2);
		addTile(316);
		newTile.CopyFrom(Style2x2);
		addTile(317);
		newTile.CopyFrom(Style2x2);
		addTile(318);
		newTile.CopyFrom(Style2x2);
		addTile(360);
		newTile.CopyFrom(Style2x2);
		addTile(580);
		newTile.CopyFrom(Style2x2);
		addTile(620);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(565);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(521);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(522);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(523);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(524);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(525);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(526);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(527);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(505);
		newTile.CopyFrom(Style6x3);
		addTile(358);
		newTile.CopyFrom(Style6x3);
		addTile(359);
		newTile.CopyFrom(Style6x3);
		addTile(542);
		newTile.CopyFrom(StyleSmallCage);
		addTile(361);
		newTile.CopyFrom(StyleSmallCage);
		addTile(362);
		newTile.CopyFrom(StyleSmallCage);
		addTile(363);
		newTile.CopyFrom(StyleSmallCage);
		addTile(364);
		newTile.CopyFrom(StyleSmallCage);
		addTile(544);
		newTile.CopyFrom(StyleSmallCage);
		addTile(391);
		newTile.CopyFrom(StyleSmallCage);
		addTile(392);
		newTile.CopyFrom(StyleSmallCage);
		addTile(393);
		newTile.CopyFrom(StyleSmallCage);
		addTile(394);
		newTile.CopyFrom(Style2x2);
		addTile(287);
		newTile.CopyFrom(Style2x2);
		addTile(335);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(564);
		newTile.CopyFrom(Style2x2);
		newTile.DrawYOffset = 2;
		addTile(594);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(354);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(355);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(491);
		newTile.CopyFrom(Style2xX);
		addTile(356);
		newTile.CopyFrom(Style2xX);
		addTile(663);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newTile.AnchorBottom = AnchorData.Empty;
		newTile.AnchorLeft = new AnchorData(AnchorType.SolidTile, 1, 1);
		newTile.AnchorRight = new AnchorData(AnchorType.SolidTile, 1, 1);
		newTile.Origin = new Point16(0, 1);
		addTile(386);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorWall = true;
		addAlternate(2);
		addTile(132);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(55);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(573);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(425);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(510);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(511);
		newTile.CopyFrom(Style2x2);
		newTile.Origin = new Point16(0, 1);
		newTile.StyleHorizontal = true;
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		addTile(85);
		newTile.CopyFrom(Style2x2);
		newTile.StyleHorizontal = true;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TEItemFrame.Hook_AfterPlacement, -1, 0, processedCoordinates: true);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(0, 0);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = new Point16(1, 0);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(3);
		newTile.Origin = new Point16(0, 1);
		newAlternate.CopyFrom(newTile);
		newAlternate.Origin = Point16.Zero;
		newAlternate.AnchorWall = true;
		newAlternate.AnchorBottom = AnchorData.Empty;
		addAlternate(4);
		addTile(395);
		newTile.CopyFrom(Style2x2);
		addTile(12);
		newTile.CopyFrom(Style2x2);
		addTile(665);
		newTile.CopyFrom(Style2x2);
		addTile(639);
		newTile.Width = 3;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style3x3);
		newTile.CopyFrom(Style3x3);
		addTile(106);
		newTile.CopyFrom(Style3x3);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(212);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(219);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(642);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(220);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(228);
		newTile.CopyFrom(Style3x3);
		newTile.LavaDeath = false;
		newTile.DrawYOffset = 2;
		addTile(231);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(243);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(247);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(283);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(300);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(301);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(302);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(303);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(304);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(305);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(306);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(307);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(308);
		newTile.CopyFrom(Style3x3);
		addTile(406);
		newTile.CopyFrom(Style3x3);
		addTile(452);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(412);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(455);
		newTile.CopyFrom(Style3x3);
		newTile.DrawYOffset = 2;
		addTile(499);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style1x2);
		newTile.CopyFrom(Style1x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleWrapLimit = 2;
		newTile.StyleMultiplier = 2;
		newTile.CoordinatePaddingFix = new Point16(0, 2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(16, 47);
		addTile(15);
		newTile.CopyFrom(Style1x2);
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleWrapLimit = 2;
		newTile.StyleMultiplier = 2;
		newTile.CoordinatePaddingFix = new Point16(0, 2);
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.LavaDeath = false;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(14, 42);
		addTile(497);
		newTile.CopyFrom(Style1x2);
		newTile.CoordinateHeights = new int[2] { 16, 20 };
		addTile(216);
		newTile.CopyFrom(Style1x2);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		addTile(390);
		newTile.CopyFrom(Style1x2);
		addTile(338);
		newTile.CopyFrom(Style1x2);
		newTile.StyleHorizontal = true;
		newTile.StyleWrapLimit = 6;
		newTile.DrawStyleOffset = 13 * newTile.StyleWrapLimit;
		addTile(493);
		newTile.CopyFrom(Style1x2);
		newTile.RandomStyleRange = 5;
		newTile.CoordinateHeights = new int[2] { 18, 18 };
		newTile.CoordinateWidth = 26;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		addTile(567);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style1x1);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
		newTile.LavaDeath = false;
		addTile(420);
		newTile.CopyFrom(Style1x1);
		addTile(624);
		newTile.CopyFrom(Style1x1);
		addTile(656);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, newTile.Width, 0);
		newTile.CoordinateHeights = new int[1] { 18 };
		newTile.CoordinateWidth = 20;
		newTile.LavaDeath = false;
		addTile(476);
		newTile.CopyFrom(Style1x1);
		newTile.LavaDeath = false;
		newTile.AnchorBottom = new AnchorData(AnchorType.AlternateTile, newTile.Width, 0);
		newTile.AnchorAlternateTiles = new int[2] { 420, 419 };
		newTile.StyleHorizontal = true;
		addTile(419);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
		newTile.LavaDeath = false;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TELogicSensor.Hook_AfterPlacement, -1, 0, processedCoordinates: true);
		addTile(423);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
		newTile.LavaDeath = false;
		addTile(424);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
		newTile.LavaDeath = false;
		addTile(445);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
		newTile.LavaDeath = false;
		addTile(429);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorTop = new AnchorData(AnchorType.EmptyTile, newTile.Width, 0);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.DrawFlipHorizontal = true;
		newTile.CoordinateHeights = new int[1] { 26 };
		newTile.CoordinateWidth = 24;
		newTile.DrawYOffset = -8;
		newTile.RandomStyleRange = 6;
		newTile.StyleHorizontal = true;
		addTile(81);
		newTile.CopyFrom(Style1x1);
		newTile.CoordinateHeights = new int[1] { 18 };
		newTile.CoordinatePadding = 0;
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		addTile(135);
		newTile.CopyFrom(Style1x1);
		newTile.CoordinateHeights = new int[1] { 18 };
		newTile.CoordinatePadding = 0;
		newTile.DrawYOffset = 2;
		newTile.LavaDeath = false;
		addTile(428);
		newTile.CopyFrom(Style1x1);
		newTile.RandomStyleRange = 2;
		newTile.LavaDeath = false;
		addTile(141);
		newTile.CopyFrom(Style1x1);
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		addTile(144);
		newTile.CopyFrom(Style1x1);
		newTile.DrawYOffset = 2;
		addTile(210);
		newTile.CopyFrom(Style1x1);
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		addTile(239);
		newTile.CopyFrom(Style1x1);
		newTile.DrawYOffset = 2;
		newTile.StyleHorizontal = true;
		addTile(650);
		newTile.CopyFrom(Style1x1);
		newTile.StyleHorizontal = true;
		newTile.RandomStyleRange = 7;
		addTile(36);
		newTile.CopyFrom(Style1x1);
		newTile.UsesCustomCanPlace = true;
		newTile.DrawFlipHorizontal = true;
		newTile.RandomStyleRange = 3;
		newTile.StyleMultiplier = 3;
		newTile.StyleWrapLimit = 3;
		newTile.StyleHorizontal = true;
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.CoordinateWidth = 20;
		newTile.DrawYOffset = -2;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		addTile(324);
		newTile.CopyFrom(Style1x1);
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.DrawYOffset = 2;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, newTile.Width, 0);
		addTile(593);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide | AnchorType.AlternateTile, newTile.Width, 0);
		newTile.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.AlternateTile | AnchorType.SolidBottom, newTile.Width, 0);
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		addAlternate(3);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorWall = true;
		addAlternate(4);
		addTile(630);
		newTile.CopyFrom(Style1x1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide | AnchorType.AlternateTile, newTile.Width, 0);
		newTile.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newTile.StyleHorizontal = true;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.AlternateTile | AnchorType.SolidBottom, newTile.Width, 0);
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		addAlternate(3);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorBottom = AnchorData.Empty;
		newAlternate.AnchorWall = true;
		addAlternate(4);
		addTile(631);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.FlattenAnchors = true;
		addBaseTile(out StyleSwitch);
		newTile.CopyFrom(StyleSwitch);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, newTile.Width, 0);
		newAlternate.CopyFrom(StyleSwitch);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newAlternate.DrawXOffset = -2;
		addAlternate(1);
		newAlternate.CopyFrom(StyleSwitch);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newAlternate.DrawXOffset = 2;
		addAlternate(2);
		newAlternate.CopyFrom(StyleSwitch);
		newAlternate.AnchorWall = true;
		addAlternate(3);
		newTile.DrawYOffset = 2;
		addTile(136);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.FlattenAnchors = true;
		newTile.UsesCustomCanPlace = false;
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawStepDown = 2;
		newTile.CoordinateWidth = 20;
		newTile.CoordinatePadding = 2;
		newTile.StyleMultiplier = 6;
		newTile.StyleWrapLimit = 6;
		newTile.StyleHorizontal = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		newTile.WaterDeath = true;
		newTile.LavaDeath = true;
		addBaseTile(out StyleTorch);
		newTile.CopyFrom(StyleTorch);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, newTile.Width, 0);
		newAlternate.CopyFrom(StyleTorch);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		addAlternate(1);
		newAlternate.CopyFrom(StyleTorch);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		addAlternate(2);
		newAlternate.CopyFrom(StyleTorch);
		newAlternate.AnchorWall = true;
		addAlternate(0);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.WaterDeath = false;
		newSubTile.LavaDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(8);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.WaterDeath = false;
		newSubTile.LavaDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(11);
		newSubTile.CopyFrom(newTile);
		newSubTile.LinkedAlternates = true;
		newSubTile.WaterDeath = false;
		newSubTile.LavaDeath = false;
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		addSubTile(17);
		addTile(4);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = new Point16(0, 0);
		newTile.FlattenAnchors = true;
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.DrawStepDown = 2;
		newTile.CoordinateWidth = 20;
		newTile.CoordinatePadding = 2;
		newTile.StyleHorizontal = true;
		newTile.WaterDeath = false;
		newTile.LavaDeath = false;
		newTile.StyleWrapLimit = 4;
		newTile.StyleMultiplier = 4;
		newTile.HookCheckIfCanPlace = new PlacementHook(WorldGen.CanPlaceProjectilePressurePad, -1, 0, processedCoordinates: true);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile | AnchorType.EmptyTile | AnchorType.SolidBottom, newTile.Width, 0);
		newAlternate.DrawStepDown = 0;
		newAlternate.DrawYOffset = -4;
		addAlternate(1);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile | AnchorType.EmptyTile | AnchorType.SolidBottom, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newAlternate.DrawXOffset = -2;
		newAlternate.DrawYOffset = -2;
		addAlternate(2);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile | AnchorType.EmptyTile | AnchorType.SolidBottom, newTile.Height, 0);
		newAlternate.AnchorAlternateTiles = new int[7] { 124, 561, 574, 575, 576, 577, 578 };
		newAlternate.DrawXOffset = 2;
		newAlternate.DrawYOffset = -2;
		addAlternate(3);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile | AnchorType.EmptyTile | AnchorType.SolidBottom, newTile.Width, 0);
		addTile(442);
		newTile.Width = 1;
		newTile.Height = 1;
		newTile.Origin = Point16.Zero;
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[1] { 20 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.DrawYOffset = -1;
		newTile.StyleHorizontal = true;
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.AlternateTile, newTile.Width, 0);
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = true;
		newTile.LavaPlacement = LiquidPlacement.NotAllowed;
		addBaseTile(out StyleAlch);
		newTile.CopyFrom(StyleAlch);
		newTile.AnchorValidTiles = new int[4] { 2, 477, 109, 492 };
		newTile.AnchorAlternateTiles = new int[1] { 78 };
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[1] { 60 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		addSubTile(1);
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[2] { 0, 59 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		addSubTile(2);
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[4] { 199, 203, 25, 23 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		addSubTile(3);
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[2] { 53, 116 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(4);
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[2] { 57, 633 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		newSubTile.LavaPlacement = LiquidPlacement.Allowed;
		newSubTile.LavaDeath = false;
		addSubTile(5);
		newSubTile.CopyFrom(StyleAlch);
		newSubTile.AnchorValidTiles = new int[5] { 147, 161, 163, 164, 200 };
		newSubTile.AnchorAlternateTiles = new int[1] { 78 };
		newSubTile.WaterPlacement = LiquidPlacement.Allowed;
		addSubTile(6);
		addTile(82);
		newTile.FullCopyFrom(82);
		addTile(83);
		newTile.FullCopyFrom(83);
		addTile(84);
		newTile.Width = 3;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 1);
		newTile.AnchorWall = true;
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[3] { 16, 16, 16 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.LavaDeath = true;
		addBaseTile(out Style3x3Wall);
		newTile.CopyFrom(Style3x3Wall);
		newTile.StyleHorizontal = true;
		newTile.StyleWrapLimit = 36;
		addTile(240);
		newTile.CopyFrom(Style3x3Wall);
		newTile.StyleHorizontal = true;
		newTile.StyleWrapLimit = 36;
		addTile(440);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(334);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Direction = TileObjectDirection.PlaceLeft;
		newTile.StyleHorizontal = true;
		newTile.LavaDeath = false;
		newTile.HookPostPlaceMyPlayer = new PlacementHook(TEWeaponsRack.Hook_AfterPlacement, -1, 0, processedCoordinates: true);
		newAlternate.CopyFrom(newTile);
		newAlternate.Direction = TileObjectDirection.PlaceRight;
		addAlternate(1);
		addTile(471);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Width = 2;
		newTile.Height = 3;
		newTile.Origin = new Point16(0, 1);
		newTile.StyleHorizontal = true;
		newSubTile.CopyFrom(newTile);
		newSubTile.RandomStyleRange = 4;
		addSubTile(15);
		addTile(245);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Width = 3;
		newTile.Height = 2;
		newTile.Origin = new Point16(1, 0);
		newTile.CoordinateHeights = new int[2] { 16, 16 };
		addTile(246);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Width = 4;
		newTile.Height = 3;
		newTile.Origin = new Point16(1, 1);
		newTile.RandomStyleRange = 9;
		addTile(241);
		newTile.CopyFrom(Style3x3Wall);
		newTile.Width = 6;
		newTile.Height = 4;
		newTile.Origin = new Point16(2, 2);
		newTile.CoordinateHeights = new int[4] { 16, 16, 16, 16 };
		newTile.StyleWrapLimit = 27;
		addTile(242);
		newTile.Width = 2;
		newTile.Height = 4;
		newTile.Origin = new Point16(0, 3);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[4] { 16, 16, 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.AnchorValidTiles = new int[6] { 2, 477, 109, 60, 492, 633 };
		newTile.StyleHorizontal = true;
		newTile.RandomStyleRange = 3;
		newTile.LavaDeath = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		addTile(27);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.AnchorValidTiles = new int[2] { 2, 477 };
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = true;
		newTile.RandomStyleRange = 3;
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 147 };
		addAlternate(3);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 60 };
		addAlternate(6);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[2] { 23, 661 };
		addAlternate(9);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[2] { 199, 662 };
		addAlternate(12);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[2] { 109, 492 };
		addAlternate(15);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 53 };
		addAlternate(18);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 116 };
		addAlternate(21);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 234 };
		addAlternate(24);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 112 };
		addAlternate(27);
		newAlternate.CopyFrom(newTile);
		newAlternate.AnchorValidTiles = new int[1] { 633 };
		addAlternate(30);
		addTile(20);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.AnchorValidTiles = new int[15]
		{
			1, 25, 117, 203, 182, 180, 179, 381, 183, 181,
			534, 536, 539, 625, 627
		};
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = false;
		newTile.RandomStyleRange = 3;
		newTile.StyleMultiplier = 3;
		newTile.StyleHorizontal = true;
		addTile(590);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.AnchorValidTiles = new int[7] { 2, 477, 492, 60, 109, 199, 23 };
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = false;
		newTile.RandomStyleRange = 3;
		newTile.StyleMultiplier = 3;
		newTile.StyleHorizontal = true;
		addTile(595);
		newTile.Width = 1;
		newTile.Height = 2;
		newTile.Origin = new Point16(0, 1);
		newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, newTile.Width, 0);
		newTile.UsesCustomCanPlace = true;
		newTile.CoordinateHeights = new int[2] { 16, 18 };
		newTile.CoordinateWidth = 16;
		newTile.CoordinatePadding = 2;
		newTile.AnchorValidTiles = new int[7] { 2, 477, 492, 60, 109, 199, 23 };
		newTile.StyleHorizontal = true;
		newTile.DrawFlipHorizontal = true;
		newTile.WaterPlacement = LiquidPlacement.NotAllowed;
		newTile.LavaDeath = false;
		newTile.RandomStyleRange = 3;
		newTile.StyleMultiplier = 3;
		newTile.StyleHorizontal = true;
		addTile(615);
	}

	public static bool CustomPlace(int type, int style)
	{
		if (type < 0 || type >= _data.Count || style < 0)
		{
			return false;
		}
		TileObjectData tileObjectData = _data[type];
		if (tileObjectData == null)
		{
			return false;
		}
		List<TileObjectData> subTiles = tileObjectData.SubTiles;
		if (subTiles != null && style < subTiles.Count)
		{
			TileObjectData tileObjectData2 = subTiles[style];
			if (tileObjectData2 != null)
			{
				return tileObjectData2._usesCustomCanPlace;
			}
		}
		return tileObjectData._usesCustomCanPlace;
	}

	public static bool CheckLiquidPlacement(int type, int style, Tile checkTile)
	{
		return GetTileData(type, style)?.LiquidPlace(checkTile) ?? LiquidPlace(type, checkTile);
	}

	public static bool LiquidPlace(int type, Tile checkTile)
	{
		if (checkTile == null)
		{
			return false;
		}
		if (checkTile.liquid > 0)
		{
			switch (checkTile.liquidType())
			{
			case 1:
				if (Main.tileLavaDeath[type])
				{
					return false;
				}
				break;
			case 0:
			case 2:
			case 3:
				if (Main.tileWaterDeath[type])
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	public static bool CheckWaterDeath(int type, int style)
	{
		return GetTileData(type, style)?.WaterDeath ?? Main.tileWaterDeath[type];
	}

	public static bool CheckWaterDeath(Tile checkTile)
	{
		if (!checkTile.active())
		{
			return false;
		}
		return GetTileData(checkTile)?.WaterDeath ?? Main.tileWaterDeath[checkTile.type];
	}

	public static bool CheckLavaDeath(int type, int style)
	{
		return GetTileData(type, style)?.LavaDeath ?? Main.tileLavaDeath[type];
	}

	public static bool CheckLavaDeath(Tile checkTile)
	{
		if (!checkTile.active())
		{
			return false;
		}
		return GetTileData(checkTile)?.LavaDeath ?? Main.tileLavaDeath[checkTile.type];
	}

	public static int PlatformFrameWidth()
	{
		return _data[19].CoordinateFullWidth;
	}

	/// <summary>
	/// Retrieves the TileObjectData corresponding to the passed in tile <paramref name="type" />, tile <paramref name="style" />, and optional <paramref name="alternate" /> parameter. Terrain tiles will return null.
	/// <para /> Note that the alternate parameter in this method retrieves the alternate TileObjectData according to the order that they were registered, not according to a calculated alternate placement style. This parameter counts from 1, not 0, so the default parameter value of 0 will not attempt to retrieve an alternate TileObjectData. As such, it is not suitable for retrieving the TileObjectData of a placed Tile, use  <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" /> for that.
	/// <para /> Other related methods include <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" />, <see cref="M:Terraria.ObjectData.TileObjectData.GetTileInfo(Terraria.Tile,System.Int32@,System.Int32@)" />, and <see cref="M:Terraria.ObjectData.TileObjectData.GetTileStyle(Terraria.Tile)" />.
	/// </summary>
	public static TileObjectData GetTileData(int type, int style, int alternate = 0)
	{
		if (type < 0 || type >= _data.Count)
		{
			throw new ArgumentOutOfRangeException("Function called with a bad type argument");
		}
		if (style < 0)
		{
			throw new ArgumentOutOfRangeException("Function called with a bad style argument");
		}
		TileObjectData tileObjectData = _data[type];
		if (tileObjectData == null)
		{
			return null;
		}
		List<TileObjectData> subTiles = tileObjectData.SubTiles;
		if (subTiles != null && style < subTiles.Count)
		{
			TileObjectData tileObjectData2 = subTiles[style];
			if (tileObjectData2 != null)
			{
				tileObjectData = tileObjectData2;
			}
		}
		alternate--;
		List<TileObjectData> alternates = tileObjectData.Alternates;
		if (alternates != null && alternate >= 0 && alternate < alternates.Count)
		{
			TileObjectData tileObjectData3 = alternates[alternate];
			if (tileObjectData3 != null)
			{
				tileObjectData = tileObjectData3;
			}
		}
		return tileObjectData;
	}

	/// <summary>
	/// Retrieves the TileObjectData corresponding to the passed in Tile. Empty tiles and terrain tiles will return null. Any individual Tile of the multitile works. Will correctly retrieve the style or alternate specific TileObjectData if it exists.
	/// <para /> Other related methods include <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(System.Int32,System.Int32,System.Int32)" />, <see cref="M:Terraria.ObjectData.TileObjectData.GetTileInfo(Terraria.Tile,System.Int32@,System.Int32@)" />, and <see cref="M:Terraria.ObjectData.TileObjectData.GetTileStyle(Terraria.Tile)" />.
	/// </summary>
	public static TileObjectData GetTileData(Tile getTile)
	{
		if (getTile == null || !getTile.active())
		{
			return null;
		}
		int type = getTile.type;
		if (type < 0 || type >= _data.Count)
		{
			throw new ArgumentOutOfRangeException("Function called with a bad tile type");
		}
		TileObjectData tileObjectData = _data[type];
		if (tileObjectData == null)
		{
			return null;
		}
		int num = getTile.frameX / tileObjectData.CoordinateFullWidth;
		int num2 = getTile.frameY / tileObjectData.CoordinateFullHeight;
		int num3 = tileObjectData.StyleWrapLimit;
		if (num3 == 0)
		{
			num3 = 1;
		}
		int styleLineSkip = tileObjectData.StyleLineSkip;
		int num4 = ((!tileObjectData.StyleHorizontal) ? (num / styleLineSkip * num3 + num2) : (num2 / styleLineSkip * num3 + num));
		int num5 = num4 / tileObjectData.StyleMultiplier;
		int num6 = num4 % tileObjectData.StyleMultiplier;
		if (tileObjectData.SubTiles != null && num5 >= 0 && num5 < tileObjectData.SubTiles.Count)
		{
			TileObjectData tileObjectData2 = tileObjectData.SubTiles[num5];
			if (tileObjectData2 != null)
			{
				tileObjectData = tileObjectData2;
			}
		}
		if (tileObjectData._alternates != null)
		{
			for (int i = 0; i < tileObjectData.Alternates.Count; i++)
			{
				TileObjectData tileObjectData3 = tileObjectData.Alternates[i];
				if (tileObjectData3 != null && num6 >= tileObjectData3.Style && num6 <= tileObjectData3.Style + tileObjectData3.RandomStyleRange)
				{
					return tileObjectData3;
				}
			}
		}
		return tileObjectData;
	}

	public static void SyncObjectPlacement(int tileX, int tileY, int type, int style, int dir)
	{
		NetMessage.SendData(17, -1, -1, null, 1, tileX, tileY, type, style);
		GetTileData(type, style);
	}

	public static bool CallPostPlacementPlayerHook(int tileX, int tileY, int type, int style, int dir, int alternate, TileObject data)
	{
		TileObjectData tileData = GetTileData(type, style, data.alternate);
		if (tileData == null || tileData._placementHooks == null || tileData._placementHooks.postPlaceMyPlayer.hook == null)
		{
			return false;
		}
		PlacementHook postPlaceMyPlayer = tileData._placementHooks.postPlaceMyPlayer;
		if (postPlaceMyPlayer.processedCoordinates)
		{
			tileX -= tileData.Origin.X;
			tileY -= tileData.Origin.Y;
		}
		return postPlaceMyPlayer.hook(tileX, tileY, type, style, dir, data.alternate) == postPlaceMyPlayer.badReturn;
	}

	public static void OriginToTopLeft(int type, int style, ref Point16 baseCoords)
	{
		TileObjectData tileData = GetTileData(type, style);
		if (tileData != null)
		{
			baseCoords = new Point16(baseCoords.X - tileData.Origin.X, baseCoords.Y - tileData.Origin.Y);
		}
	}

	public static void FixNewTile()
	{
		newTile = new TileObjectData(_baseObject);
	}

	/// <summary>
	/// Retrieves the tile style corresponding to the passed in Tile. Empty tiles and terrain tiles will return -1. Any Tile of the multitile works.
	/// <para /> This is most useful in replacing hard-coded math where the tile style is calculated from <see cref="P:Terraria.Tile.TileFrameX" /> and <see cref="P:Terraria.Tile.TileFrameY" /> directly, such as mouse over icons and other tile style specific behaviors.
	/// <para /> Other related methods include <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" />, <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(System.Int32,System.Int32,System.Int32)" />, and <see cref="M:Terraria.ObjectData.TileObjectData.GetTileInfo(Terraria.Tile,System.Int32@,System.Int32@)" />.
	/// </summary>
	public static int GetTileStyle(Tile getTile)
	{
		if (getTile == null || !getTile.active())
		{
			return -1;
		}
		int type = getTile.type;
		if (type < 0 || type >= _data.Count)
		{
			throw new ArgumentOutOfRangeException("getTile", "Function called with a bad tile type");
		}
		TileObjectData tileObjectData = _data[type];
		if (tileObjectData == null)
		{
			return -1;
		}
		int num = getTile.frameX / tileObjectData.CoordinateFullWidth;
		int num2 = getTile.frameY / tileObjectData.CoordinateFullHeight;
		int num3 = tileObjectData.StyleWrapLimit;
		if (num3 == 0)
		{
			num3 = 1;
		}
		int styleLineSkip = tileObjectData.StyleLineSkip;
		return ((!tileObjectData.StyleHorizontal) ? (num / styleLineSkip * num3 + num2) : (num2 / styleLineSkip * num3 + num)) / tileObjectData.StyleMultiplier;
	}

	/// <summary>
	/// Retrieves the tile <paramref name="style" /> and <paramref name="alternate" /> placement corresponding to the passed in Tile. Empty tiles and terrain tiles will return without setting the ref parameters. Any Tile of the multitile works.
	/// <para /> Other related methods include <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(Terraria.Tile)" />, <see cref="M:Terraria.ObjectData.TileObjectData.GetTileData(System.Int32,System.Int32,System.Int32)" />, and <see cref="M:Terraria.ObjectData.TileObjectData.GetTileStyle(Terraria.Tile)" />.
	/// </summary>
	public static void GetTileInfo(Tile getTile, ref int style, ref int alternate)
	{
		if (getTile == null || !getTile.active())
		{
			return;
		}
		int type = getTile.type;
		if (type < 0 || type >= _data.Count)
		{
			throw new ArgumentOutOfRangeException("getTile", "Function called with a bad tile type");
		}
		TileObjectData tileObjectData = _data[type];
		if (tileObjectData != null)
		{
			int num = getTile.frameX / tileObjectData.CoordinateFullWidth;
			int num2 = getTile.frameY / tileObjectData.CoordinateFullHeight;
			int num3 = tileObjectData.StyleWrapLimit;
			if (num3 == 0)
			{
				num3 = 1;
			}
			int styleLineSkip = tileObjectData.StyleLineSkip;
			int num4 = ((!tileObjectData.StyleHorizontal) ? (num / styleLineSkip * num3 + num2) : (num2 / styleLineSkip * num3 + num));
			int num5 = num4 / tileObjectData.StyleMultiplier;
			int num6 = num4 % tileObjectData.StyleMultiplier;
			style = num5;
			alternate = num6;
		}
	}

	/// <summary>
	/// Returns true if the <see cref="T:Terraria.Tile" /> at the location provided has a placed tile and is the top left tile of a multitile (supports alternate placements and states as well). Returns false otherwise.
	/// <para /> Can be used for logic that should only run once per multitile, such as custom tile rendering.
	/// </summary>
	public static bool IsTopLeft(int i, int j)
	{
		return IsTopLeft(Main.tile[i, j]);
	}

	/// <summary>
	/// Returns true if the <see cref="T:Terraria.Tile" /> provided has a placed tile and is the top left tile of a multitile (supports alternate placements and states as well). Returns false otherwise.
	/// <para /> Can be used for logic that should only run once per multitile, such as custom tile rendering.
	/// </summary>
	public static bool IsTopLeft(Tile tile)
	{
		if (!tile.HasTile)
		{
			return false;
		}
		TileObjectData tileData = GetTileData(tile);
		if (tileData == null)
		{
			return false;
		}
		int num = tile.TileFrameX % tileData.CoordinateFullWidth;
		int num2 = tile.TileFrameY % tileData.CoordinateFullHeight;
		if (num == 0)
		{
			return num2 == 0;
		}
		return false;
	}

	/// <summary>
	/// Returns the coordinates of the top left tile of the multitile at the location provided. Returns <see cref="F:Terraria.DataStructures.Point16.NegativeOne" /> if no tile exists at the coordinates. If the tile does not have a TileObjectData, such as if it were a normal terrain tile, the provided coordinates will be returned.
	/// </summary>
	/// <param name="i"></param>
	/// <param name="j"></param>
	/// <returns></returns>
	public static Point16 TopLeft(int i, int j)
	{
		Tile getTile = Main.tile[i, j];
		if (!getTile.HasTile)
		{
			return Point16.NegativeOne;
		}
		TileObjectData tileData = GetTileData(getTile);
		if (tileData == null)
		{
			return new Point16(i, j);
		}
		int num = getTile.TileFrameX % tileData.CoordinateFullWidth;
		int num2 = getTile.TileFrameY % tileData.CoordinateFullHeight;
		int num3 = num / (tileData.CoordinateWidth + tileData.CoordinatePadding);
		int k = 0;
		for (int num4 = num2; k + 1 < tileData.Height && num4 - tileData.CoordinateHeights[k] - tileData.CoordinatePadding >= 0; k++)
		{
			num4 -= tileData.CoordinateHeights[k] + tileData.CoordinatePadding;
		}
		i -= num3;
		j -= k;
		return new Point16(i, j);
	}

	/// <inheritdoc cref="M:Terraria.ObjectData.TileObjectData.TopLeft(System.Int32,System.Int32)" />
	public static Point16 TopLeft(Point16 point)
	{
		return TopLeft(point.X, point.Y);
	}
}
