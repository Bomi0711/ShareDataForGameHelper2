// <copyright file="Structs.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace ShareData2
{
    /// <summary>
    ///     Represents an entity in the game world.
    /// </summary>
    public class EntityData
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public List<int> GridPosition { get; set; } = new();
        public List<int> WorldPosition { get; set; } = new();
        public List<int> ScreenPosition { get; set; } = new();
        public string Rarity { get; set; } = string.Empty;
        public int IsHostile { get; set; }
        public int IsAttackable { get; set; }
        public int IsTargetable { get; set; }
        public int IsTargeted { get; set; }
        public int IsOpened { get; set; }
        public int HasBounds { get; set; }
        public string RenderName { get; set; } = string.Empty;
        public List<int> LifeData { get; set; } = new();
    }

    /// <summary>
    ///     Represents player information.
    /// </summary>
    public class PlayerInfo
    {
        public List<int> GridPosition { get; set; } = new();
        public List<int> LifeData { get; set; } = new();
        public List<string> Buffs { get; set; } = new();
        public int IsMoving { get; set; }
        public int Level { get; set; }
    }

    /// <summary>
    ///     Represents the main data object returned by the API.
    /// </summary>
    public class GameData
    {
        public int GameState { get; set; }
        public List<int> WindowBounds { get; set; } = new();
        public List<int> MousePosition { get; set; } = new();
        public string TerrainString { get; set; } = string.Empty;
        public uint AreaHash { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public bool IsLoadingBackground { get; set; }
        public bool InvitesPanelVisible { get; set; }
        public List<EntityData> AwakeEntities { get; set; } = new();
        public List<EntityData> VisibleLabels { get; set; } = new();
        public List<EntityData> ItemsOnGround { get; set; } = new();
        public PlayerInfo Player { get; set; } = new();
        public int ControllerType { get; set; }
    }
}
