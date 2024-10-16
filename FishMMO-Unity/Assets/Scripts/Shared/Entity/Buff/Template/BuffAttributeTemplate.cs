﻿using System;
using UnityEngine;

namespace FishMMO.Shared
{
	[Serializable]
	public class BuffAttributeTemplate
	{
		public long MinValue;
		public long MaxValue;
		[Tooltip("Character Attribute the buff will apply its values to.")]
		public CharacterAttributeTemplate Template;
	}
}