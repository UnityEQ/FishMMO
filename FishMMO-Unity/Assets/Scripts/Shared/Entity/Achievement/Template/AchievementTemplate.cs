﻿using System.Collections.Generic;
using UnityEngine;

namespace FishMMO.Shared
{
	[CreateAssetMenu(fileName = "New Achievement", menuName = "FishMMO/Character/Achievement/Achievement", order = 1)]
	public class AchievementTemplate : CachedScriptableObject<AchievementTemplate>, ICachedObject
	{
		public Sprite Icon;
		public AchievementCategory Category;
		public string Description;
		public List<AchievementTier> Tiers;

		public string Name { get { return this.name; } }
	}
}