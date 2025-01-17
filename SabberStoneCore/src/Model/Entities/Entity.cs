﻿#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Exceptions;
using SabberStoneCore.Kettle;
using System.Text;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCore.Model.Entities
{
	/// <summary>
	/// Exposes the properties for each implementing entity.
	/// </summary>
	public interface IEntity : IEnumerable<KeyValuePair<GameTag, int>>
	{
		/// <summary>Gets the identifier of this entity (EntityID).</summary>
		/// <value>The identifier.</value>
		int Id { get; }

		/// <summary>Gets or sets the game instance from which this entity is part of.</summary>
		/// <value>The game instance.</value>
		Game Game { get; set; }

		/// <summary>Gets the card from which this entity was derived from.</summary>
		/// <value>The card object.</value>
		Card Card { get; set; }

		/// <summary>Gets or sets the owner of this entity, the controller who played the entity.</summary>
		/// <value>The controller/owner object.</value>
		Controller Controller { get; set; }

		/// <summary>Gets or sets the zone in which the entity exists.</summary>
		/// <value>The zone, <see cref="IZone"/>.</value>
		/// <autogeneratedoc />
		IZone Zone { get; set; }

		/// <summary>Gets or sets the specific <see cref="GameTag"/> for this entity.</summary>
		/// <value><see cref="System.Int32"/>.</value>
		/// <param name="t">The gametag which represents a property of this entity.</param>
		/// <returns></returns>
		int this[GameTag t] { get; set; }

		/// <summary>Resets all tags (properties) to default values derived from the orginal card object.</summary>
		void Reset();

		/// <summary>Get a string which uniquely defines this entity object.</summary>
		/// <param name="ignore">All tags to ignore when generating the hash.</param>
		/// <returns></returns>
		string Hash(params GameTag[] ignore);

		/// <summary>
		/// A simple container for saving tag value perturbations from external Auras. Call indexer to get value for a particular Tag.
		/// </summary>
		AuraEffects AuraEffects { get; set; }

		/// <summary>
		/// Attached or overwritten tags of this entity instance. This does not contain Card tags.
		/// </summary>
		IDictionary<GameTag, int> NativeTags { get; }

		/// <summary>
		/// Gets or sets a list for enchantments applied to this entity.
		/// </summary>
		List<Enchantment> AppliedEnchantments { get; set; }
	}

	/// <summary>
	/// The base class of all data-holding/action-performing/visible or invisible objects in a SabberStone game.
	/// An entity is defined as a collection of properties, called Tags.
	/// 
	/// <seealso cref="HeroPower"/>
	/// <seealso cref="Hero"/>
	/// <seealso cref="Minion"/>
	/// <seealso cref="Spell"/>
	/// </summary>
	/// <seealso cref="IEntity" />
	public partial class Entity : IEntity
	{
		/// <summary>
		/// This object holds the original tag values, defined through the constructor 
		/// of this instance.
		/// These tags are usefull when values are needed without any buffs/debuffs applied.
		/// </summary>
		internal readonly EntityData _data;

		/// <summary>Gets the ranking order of the moment this entity was played.</summary>
		/// <value>The ranking order.</value>
		public int OrderOfPlay { get; set; }

		/// <summary>Gets or sets the owner of this entity, the controller who played the entity.</summary>
		/// <value>The controller/owner object.</value>
		public Controller Controller { get; set; }

		/// <summary>Gets or sets the game instance from which this entity is part of.</summary>
		/// <value>The game instance.</value>
		public Game Game { get; set; }

		/// <summary>Gets or sets the zone in which the entity exists.</summary>
		/// <value>The zone, <see cref="T:SabberStoneCore.Model.Zones.IZone" />.</value>
		public IZone Zone { get; set; }

		/// <summary>Gets the card from which this entity was derived from.</summary>
		/// <value>The card object.</value>
		public Card Card { get; set; }

		/// <summary>Initializes a new instance of the <see cref="Entity"/> class.</summary>
		/// <param name="game">The game.</param>
		/// <param name="card">The card.</param>
		/// <param name="tags">The tags.</param>
		/// <param name="id">The id.</param>
		/// <autogeneratedoc />
		protected internal Entity(in Game game, in Card card, in IDictionary<GameTag, int> tags, in int id = -1)
		{
			if (tags == null)
				throw new ArgumentException("Tag dictionary is required to create an entity");

			Game = game;
			Card = card;
			//_data = new EntityData(in tags);
			_data = tags is EntityData entityData ? entityData : new EntityData(in tags);

			Id = id < 0 ? game?.NextId ?? 1 : id;

			if (game == null) return;
			_history = game.History;
			_logging = game.Logging;
			if (_history)
			{
				if (!tags.ContainsKey(GameTag.ENTITY_ID))
					tags.Add(GameTag.ENTITY_ID, Id);
			}
		}

		/// <summary>
		/// A copy constructor. This constructor is only used to the inherited copy constructors.
		/// </summary>
		/// <param name="game"><see cref="Model.Game"/> instance of a copied entity.</param>
		/// <param name="entity">A source <see cref="Entity"/>.</param>
		protected Entity(in Game game, in Entity entity)
		{
			Game = game;
			_data = new EntityData(entity._data);
			Card = entity.Card;
			Id = entity.Id;
			OrderOfPlay = entity.OrderOfPlay;
			AuraEffects = entity.AuraEffects?.Clone();
			_toBeDestroyed = entity._toBeDestroyed;
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

		public virtual string Hash(params GameTag[] ignore)
		{
			var str = new StringBuilder();
			str.Append($"[{Card.Id}]");
			str.Append(_data.Hash(ignore));
			str.Append(AuraEffects?.Hash());
			str.Append("[O:");
			str.Append(OrderOfPlay);
			str.Append("]");
			if (AppliedEnchantments?.Count > 0)
			{
				str.Append("[EN:");
				AppliedEnchantments.OrderBy(e => e.Card.Id).ToList().ForEach(e => { str.Append($"{{{e}}}"); });
				str.Append("]");
			}
			return str.ToString();
		}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>
		/// Gets the tag value without any auras applied.
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public int GetNativeGameTag(GameTag t)
		{
			if (!_data.TryGetValue(t, out int value))
				Card.Tags.TryGetValue(t, out value);
			return value;
		}

		/// <summary>
		/// This is the call for a gametag value.
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public virtual int this[GameTag t]
		{
			get
			{
				//int value = _data[t];
				if (!_data.TryGetValue(t, out int value))
					Card.Tags.TryGetValue(t, out value);

				value += AuraEffects?[in t] ?? 0;

				return value > 0 ? value : 0;
			}
			set
			{
				if (_logging)
					Game.Log(LogLevel.DEBUG, BlockType.TRIGGER, "Entity", !Game.Logging? "":$"{this} set data {t} to {value}");
				if (_history && (int)t < 1000)
					if (value + (AuraEffects?[t] ?? 0) != this[t])
						Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(Id, t, value));

				_data[t] = value;
			}
		}

		/// <summary>
		/// All gametag values that where changed after creation are wiped.
		/// Any enchants and trigger is removed.
		/// </summary>
		public virtual void Reset()
		{
			_data.Reset();
		}

		/// <summary>Builds a new subclass of entity that can be added to a SabberStone game instance.</summary>
		/// <param name="controller">The controller of the entity.</param>
		/// <param name="card">The card from which the entity must be derived.</param>
		/// <param name="tags">The tags preset for the entity.</param>
		/// <param name="zone">The zone in which the entity must spawn.</param>
		/// <param name="id">The EntityID to assign to the newly created entity.</param>
		/// <param name="zonePos">The position to be placed when the entity is summoned to Board.</param>
		/// <param name="creator">The creator entity of the new entity.</param>
		/// <returns></returns>
		/// <exception cref="EntityException"></exception>
		public static IPlayable FromCard(in Controller controller, in Card card,
			IDictionary<GameTag, int> tags = null,
			in IZone zone = null, in int id = -1, in int zonePos = -1,
			in IEntity creator = null)
		{
			Game game = controller.Game;

			tags = tags ?? new EntityData();
			//tags[GameTag.CARD_ID] = card.AssetId;

			//if (creator != null)
			//	tags.Add(GameTag.CREATOR, creator.Id);

			IPlayable result;
			switch (card.Type)
			{
				case CardType.MINION:
					result = new Minion(in controller, in card, in tags, in id);
					break;

				case CardType.SPELL:
					result = new Spell(in controller, in card, in tags, in id);
					break;

				case CardType.WEAPON:
					result = new Weapon(in controller, in card, in tags, in id);
					break;

				case CardType.HERO:

					// removing this because it's always the cards health or it is given by previous heros like for deathknight
					//tags[GameTag.HEALTH] = card[GameTag.HEALTH];

					//tags[GameTag.ZONE] = (int)Enums.Zone.PLAY;
					//tags[GameTag.FACTION] = card[GameTag.FACTION];
					//tags[GameTag.CARDTYPE] = card[GameTag.CARDTYPE];
					//tags[GameTag.RARITY] = card[GameTag.RARITY];
					//tags[GameTag.HERO_POWER] = card[GameTag.HERO_POWER];

					result = new Hero(in controller, in card, in tags, in id);
					break;

				case CardType.HERO_POWER:
					//tags[GameTag.COST] = card[GameTag.COST];
					tags[GameTag.ZONE] = (int)Enums.Zone.PLAY;
					//tags[GameTag.FACTION] = card[GameTag.FACTION];
					//tags[GameTag.CARDTYPE] = card[GameTag.CARDTYPE];
					//tags[GameTag.RARITY] = card[GameTag.RARITY];
					//tags[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = card[GameTag.COST];
					result = new HeroPower(in controller, in card, in tags, in id);
					controller.AppliedEnchantments?.ForEach(p =>
					{
						if (p.OngoingEffect is Aura a && a.Type == AuraType.HEROPOWER)
							a.EntityAdded(result);

					});
					break;

				default:
					throw new EntityException($"Couldn't create entity, because of an unknown cardType {card.Type}.");
			}

			// add entity to the game dic
			game.IdEntityDic[result.Id] = result;

			// add power history full entity 
			if (game.History)
			{
				if (zone is DeckZone)
				{
					controller.Game.PowerHistory.Add(new PowerHistoryFullEntity
					{
						Entity = new PowerHistoryEntity
						{
							Id = result.Id,
							Name = "",
							Tags = new Dictionary<GameTag, int>(tags)
						}
					});
				}
				else
					controller.Game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(result));
			}

			if (zone != null) // add entity to the appropriate zone if it was given
				switch (zone.Type)
				{

					case Enums.Zone.PLAY:
						Generic.SummonBlock.Invoke(game, (Minion) result, zonePos, creator);
						break;
					case Enums.Zone.HAND:
						Generic.AddHandPhase.Invoke(controller, result);
						break;
					default:
						zone?.Add(result, zonePos);
						break;
				}

			if (result.ChooseOne)
			{
				if (result.Card.Id == "TRL_343")
				{ // Wardruid Loti
					var data = new EntityData
					{
						[GameTag.CREATOR] = result.Id,
						[GameTag.PARENT_CARD] = result.Id
					};
					IPlayable[] playables = new[]
					{
						FromCard(in controller, Cards.FromId("TRL_343at1"), data, controller.SetasideZone),
						FromCard(in controller, Cards.FromId("TRL_343ct1"), data, controller.SetasideZone),
						FromCard(in controller, Cards.FromId("TRL_343dt1"), data, controller.SetasideZone),
						FromCard(in controller, Cards.FromId("TRL_343bt1"), data, controller.SetasideZone)
					};

					result.ChooseOnePlayables = playables;
				}
				else
				{

					result.ChooseOnePlayables = new IPlayable[2];
					result.ChooseOnePlayables[0] =
						id < 0 ? FromCard(controller,
								Cards.FromId(result.Card.Id + "a"),
								new EntityData
								{
									[GameTag.CREATOR] = result.Id,
									[GameTag.PARENT_CARD] = result.Id
								},
								controller.SetasideZone) :
							controller.SetasideZone.ToList().Find(p => p[GameTag.CREATOR] == result.Id && p.Card.Id == result.Card.Id + "a");

					result.ChooseOnePlayables[1] =
						id < 0 ? FromCard(controller,
								Cards.FromId(result.Card.Id + "b"),
								new EntityData
								{
									[GameTag.CREATOR] = result.Id,
									[GameTag.PARENT_CARD] = result.Id
								},
								controller.SetasideZone) :
							controller.SetasideZone.ToList().Find(p => p[GameTag.CREATOR] == result.Id && p.Card.Id == result.Card.Id + "b");
				}
			}

			return result;
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

		public override string ToString()
		{
			return $"'{Card.Name}[{Id}]'";
		}

		public IEnumerator<KeyValuePair<GameTag, int>> GetEnumerator()
		{
			// Entity ID
			var allTags = new Dictionary<GameTag, int>(Card.Tags);

			// Entity tags override card tags
			foreach (KeyValuePair<GameTag, int> tag in _data)
				allTags[tag.Key] = tag.Value;

			return allTags.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}

	public partial class Entity
	{
		protected readonly bool _history;
		protected readonly bool _logging;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public int Id { get; }

		public bool TurnStart
		{
			get { return this[GameTag.TURN_START] == 1; }
			set { this[GameTag.TURN_START] = value ? 1 : 0; }
		}

		public void TurnEnd()
		{
			this[GameTag.TURN_START] = 0;
		}

		public virtual bool ToBeDestroyed
		{
			get => _toBeDestroyed;
			set
			{
				_toBeDestroyed = value;

				if (_history)
					this[GameTag.TO_BE_DESTROYED] = value ? 1 : 0;
			}
		}

		private bool _toBeDestroyed;

		//public int NumTurnsInPlay
		//{
		//	get { return this[GameTag.NUM_TURNS_IN_PLAY]; }
		//	set { this[GameTag.NUM_TURNS_IN_PLAY] = value; }
		//}

		public bool IsIgnoreDamage
		{
			get { return this[GameTag.IGNORE_DAMAGE] == 1; }
			set { this[GameTag.IGNORE_DAMAGE] = value ? 1 : 0; }
		}

		public int JadeGolem
		{
			get { return this[GameTag.JADE_GOLEM]; }
			set { this[GameTag.JADE_GOLEM] = value; }
		}

		public int CardTarget
		{
			get { return this[GameTag.CARD_TARGET]; }
			set { this[GameTag.CARD_TARGET] = value; }
		}


#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

	}

	public partial class Entity
	{
		/// <summary>
		/// A simple container for saving tag value perturbations from external Auras. Call indexer to get value for a particular Tag.
		/// </summary>
		public AuraEffects AuraEffects { get; set; }

		/// <summary>
		/// Gets or sets a list for enchantments applied to this entity.
		/// </summary>
		public List<Enchantment> AppliedEnchantments { get; set; }

		/// <summary>
		/// Attached or overwritten tags of this entity instance. This does not contain Card tags.
		/// </summary>
		public IDictionary<GameTag, int> NativeTags => _data;
	}
}
