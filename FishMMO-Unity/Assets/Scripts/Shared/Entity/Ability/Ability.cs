﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FishMMO.Shared
{
	public class Ability
	{
		public long ID;
		public float ActivationTime;
		public float LifeTime;
		public float Cooldown;
		public float Range { get { return Speed * LifeTime; } }
		public float Speed;

		public AbilityTemplate Template { get; private set; }
		public string Name { get; set; }
		public string CachedTooltip { get; private set; }
		public AbilityResourceDictionary Resources { get; private set; }
		public AbilityResourceDictionary RequiredAttributes { get; private set; }

		public Dictionary<int, AbilityEvent> AbilityEvents { get; private set; }
		public Dictionary<int, SpawnEvent> PreSpawnEvents { get; private set; }
		public Dictionary<int, SpawnEvent> SpawnEvents { get; private set; }
		public Dictionary<int, MoveEvent> MoveEvents { get; private set; }
		public Dictionary<int, HitEvent> HitEvents { get; private set; }
		public AbilityTypeOverrideEventType TypeOverride { get; private set; }

		public List<Trigger> OnTickTriggers = new List<Trigger>();
		public List<Trigger> OnHitTriggers = new List<Trigger>();
		public List<Trigger> OnPreSpawnTriggers = new List<Trigger>();
		public List<Trigger> OnSpawnTriggers = new List<Trigger>();
		public List<Trigger> OnDestroyTriggers = new List<Trigger>();

		/// <summary>
		/// Cache of all active ability Objects. <ContainerID, <AbilityObjectID, AbilityObject>>
		/// </summary>
		public Dictionary<int, Dictionary<int, AbilityObject>> Objects { get; set; }

		public int TotalResourceCost
		{
			get
			{
				int totalCost = 0;
				foreach (int cost in Resources.Values)
				{
					totalCost += cost;
				}
				return totalCost;
			}
		}

		public Ability(AbilityTemplate template, List<int> abilityEvents = null)
		{
			ID = -1;
			Template = template;
			Name = Template.Name;
			CachedTooltip = null;

			if (AbilityEvents == null)
			{
				AbilityEvents = new Dictionary<int, AbilityEvent>();
			}

			InternalAddTemplateModifiers(Template);

			if (abilityEvents != null)
			{
				for (int i = 0; i < abilityEvents.Count; ++i)
				{
					AbilityEvent abilityEvent = AbilityEvent.Get<AbilityEvent>(abilityEvents[i]);
					if (abilityEvent == null)
					{
						continue;
					}
					AddAbilityEvent(abilityEvent);
				}
			}
		}

		public Ability(long abilityID, int templateID, List<int> abilityEvents = null)
		{
			ID = abilityID;
			Template = AbilityTemplate.Get<AbilityTemplate>(templateID);
			Name = Template.Name;
			CachedTooltip = null;

			if (AbilityEvents == null)
			{
				AbilityEvents = new Dictionary<int, AbilityEvent>();
			}

			InternalAddTemplateModifiers(Template);

			if (abilityEvents != null)
			{
				for (int i = 0; i < abilityEvents.Count; ++i)
				{
					AbilityEvent abilityEvent = AbilityEvent.Get<AbilityEvent>(abilityEvents[i]);
					if (abilityEvent == null)
					{
						continue;
					}
					AddAbilityEvent(abilityEvent);
				}
			}
		}

		public Ability(long abilityID, AbilityTemplate template, List<int> abilityEvents = null)
		{
			ID = abilityID;
			Template = template;
			Name = Template.Name;
			CachedTooltip = null;

			if (AbilityEvents == null)
			{
				AbilityEvents = new Dictionary<int, AbilityEvent>();
			}

			InternalAddTemplateModifiers(Template);

			if (abilityEvents != null)
			{
				for (int i = 0; i < abilityEvents.Count; ++i)
				{
					AbilityEvent abilityEvent = AbilityEvent.Get<AbilityEvent>(abilityEvents[i]);
					if (abilityEvent == null)
					{
						continue;
					}
					AddAbilityEvent(abilityEvent);
				}
			}
		}

		internal void InternalAddTemplateModifiers(AbilityTemplate template)
		{
			ActivationTime += template.ActivationTime;
			LifeTime += template.LifeTime;
			Cooldown += template.Cooldown;
			Speed += template.Speed;

			if (Resources == null)
			{
				Resources = new AbilityResourceDictionary();
			}

			if (RequiredAttributes == null)
			{
				RequiredAttributes = new AbilityResourceDictionary();
			}

			foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in template.Resources)
			{
				if (!Resources.ContainsKey(pair.Key))
				{
					Resources[pair.Key] = pair.Value;
				}
				else
				{
					Resources[pair.Key] += pair.Value;
				}
			}

			foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in template.RequiredAttributes)
			{
				if (!RequiredAttributes.ContainsKey(pair.Key))
				{
					RequiredAttributes[pair.Key] = pair.Value;
				}
				else
				{
					RequiredAttributes[pair.Key] += pair.Value;
				}
			}

			foreach (AbilityEvent abilityEvent in template.Events)
			{
				AddAbilityEvent(abilityEvent);
			}
		}

		public bool TryGetAbilityEvent<T>(int templateID, out T modifier) where T : AbilityEvent
		{
			if (AbilityEvents != null && AbilityEvents.TryGetValue(templateID, out AbilityEvent result))
			{
				if ((modifier = result as T) != null)
				{
					return true;
				}
			}
			modifier = null;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasAbilityEvent(int templateID)
		{
			return AbilityEvents?.ContainsKey(templateID) ?? false;
		}

		public void AddAbilityEvent(AbilityEvent abilityEvent)
		{
			if (AbilityEvents == null)
			{
				AbilityEvents = new Dictionary<int, AbilityEvent>();
			}

			if (!AbilityEvents.ContainsKey(abilityEvent.ID))
			{
				CachedTooltip = null;

				AbilityEvents.Add(abilityEvent.ID, abilityEvent);

				switch (abilityEvent)
				{
					case SpawnEvent spawnEvent:
						if (PreSpawnEvents == null)
						{
							PreSpawnEvents = new Dictionary<int, SpawnEvent>();
						}
						if (SpawnEvents == null)
						{
							SpawnEvents = new Dictionary<int, SpawnEvent>();
						}
						switch (spawnEvent.SpawnEventType)
						{
							case SpawnEventType.OnPreSpawn:
								if (!PreSpawnEvents.ContainsKey(spawnEvent.ID))
								{
									PreSpawnEvents.Add(spawnEvent.ID, spawnEvent);
								}
								break;
							case SpawnEventType.OnSpawn:
								if (!SpawnEvents.ContainsKey(spawnEvent.ID))
								{
									SpawnEvents.Add(spawnEvent.ID, spawnEvent);
								}
								break;
							default:
								break;
						}
						break;
					case HitEvent hitEvent:
						if (HitEvents == null)
						{
							HitEvents = new Dictionary<int, HitEvent>();
						}
						HitEvents.Add(abilityEvent.ID, hitEvent);
						break;
					case MoveEvent moveEvent:
						if (MoveEvents == null)
						{
							MoveEvents = new Dictionary<int, MoveEvent>();
						}
						MoveEvents.Add(abilityEvent.ID, moveEvent);
						break;
					case AbilityTypeOverrideEventType overrideTypeEvent:
						TypeOverride = overrideTypeEvent;
						break;
				}

				ActivationTime += abilityEvent.ActivationTime;
				LifeTime += abilityEvent.LifeTime;
				Cooldown += abilityEvent.Cooldown;
				Speed += abilityEvent.Speed;
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in abilityEvent.Resources)
				{
					if (!Resources.ContainsKey(pair.Key))
					{
						Resources[pair.Key] = pair.Value;
					}
					else
					{
						Resources[pair.Key] += pair.Value;
					}
				}
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in abilityEvent.RequiredAttributes)
				{
					if (!RequiredAttributes.ContainsKey(pair.Key))
					{
						RequiredAttributes[pair.Key] = pair.Value;
					}
					else
					{
						RequiredAttributes[pair.Key] += pair.Value;
					}
				}
			}
		}

		public void RemoveAbilityEvent(AbilityEvent abilityEvent)
		{
			if (AbilityEvents == null)
			{
				return;
			}

			if (AbilityEvents.ContainsKey(abilityEvent.ID))
			{
				CachedTooltip = null;

				AbilityEvents.Remove(abilityEvent.ID);

				switch (abilityEvent)
				{
					case SpawnEvent spawnEvent:
						switch (spawnEvent.SpawnEventType)
						{
							case SpawnEventType.OnPreSpawn:
								PreSpawnEvents.Remove(spawnEvent.ID);
								break;
							case SpawnEventType.OnSpawn:
								SpawnEvents.Remove(spawnEvent.ID);
								break;
							default:
								break;
						}
						break;
					case HitEvent:
						HitEvents.Remove(abilityEvent.ID);
						break;
					case MoveEvent:
						MoveEvents.Remove(abilityEvent.ID);
						break;
					case AbilityTypeOverrideEventType:
						TypeOverride = null;
						break;
				}

				ActivationTime -= abilityEvent.ActivationTime;
				LifeTime -= abilityEvent.LifeTime;
				Cooldown -= abilityEvent.Cooldown;
				Speed -= abilityEvent.Speed;
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in abilityEvent.Resources)
				{
					if (Resources.ContainsKey(pair.Key))
					{
						Resources[pair.Key] -= pair.Value;
					}
				}
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in abilityEvent.RequiredAttributes)
				{
					if (RequiredAttributes.ContainsKey(pair.Key))
					{
						RequiredAttributes[pair.Key] += pair.Value;
					}
				}
			}
		}

		public bool MeetsRequirements(ICharacter character)
		{
			if (!character.TryGet(out ICharacterAttributeController attributeController))
			{
				return false;
			}
			foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in RequiredAttributes)
			{
				if (!attributeController.TryGetResourceAttribute(pair.Key.ID, out CharacterResourceAttribute requirement) ||
					requirement.CurrentValue < pair.Value)
				{
					return false;
				}
			}
			return true;
		}

		public bool HasResource(ICharacter character, AbilityEvent bloodResourceConversion)
		{
			if (!character.TryGet(out ICharacterAttributeController attributeController))
			{
				return false;
			}
			if (AbilityEvents != null &&
				bloodResourceConversion != null &&
				AbilityEvents.ContainsKey(bloodResourceConversion.ID))
			{
				int totalCost = TotalResourceCost;

				CharacterResourceAttribute resource;
				if (!attributeController.TryGetHealthAttribute(out resource) ||
					resource.CurrentValue < totalCost)
				{
					return false;
				}
			}
			else
			{
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in Resources)
				{
					CharacterResourceAttribute resource;
					if (!attributeController.TryGetResourceAttribute(pair.Key.ID, out resource) ||
						resource.CurrentValue < pair.Value)
					{
						return false;
					}
				}
			}
			return true;
		}

		public void ConsumeResources(ICharacter character, AbilityEvent bloodResourceConversion)
		{
			if (!character.TryGet(out ICharacterAttributeController attributeController))
			{
				return;
			}
			if (AbilityEvents != null &&
				bloodResourceConversion != null &&
				AbilityEvents.ContainsKey(bloodResourceConversion.ID))
			{
				int totalCost = TotalResourceCost;

				CharacterResourceAttribute resource;
				if (attributeController.TryGetHealthAttribute(out resource) &&
					resource.CurrentValue >= totalCost)
				{
					resource.Consume(totalCost);
				}
			}
			else
			{
				foreach (KeyValuePair<CharacterAttributeTemplate, int> pair in Resources)
				{
					CharacterResourceAttribute resource;
					if (attributeController.TryGetResourceAttribute(pair.Key.ID, out resource) &&
						resource.CurrentValue >= pair.Value)
					{
						resource.Consume(pair.Value);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveAbilityObject(int containerID, int objectID)
		{
			if (Objects.TryGetValue(containerID, out Dictionary<int, AbilityObject> container))
			{
				container.Remove(objectID);
			}
		}

		public string Tooltip()
		{
			if (!string.IsNullOrWhiteSpace(CachedTooltip))
			{
				return CachedTooltip;
			}

			if (AbilityEvents != null)
			{
				CachedTooltip = Template.Tooltip(new List<ITooltip>(AbilityEvents.Values));
			}

			if (TypeOverride != null)
			{
				CachedTooltip += RichText.Format($"\r\nType: {TypeOverride.OverrideAbilityType}", true, "f5ad6eFF", "120%");
			}
			else
			{
				CachedTooltip += RichText.Format($"\r\nType: {Template.Type}", true, "f5ad6eFF", "120%");
			}

			return CachedTooltip;
		}
	}
}