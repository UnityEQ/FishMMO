﻿using System.Collections.Generic;

namespace FishMMO.Shared
{
	public class QuestController : CharacterBehaviour, IQuestController
	{
		private Dictionary<string, QuestInstance> quests = new Dictionary<string, QuestInstance>();

		public override void OnStartCharacter()
		{
			base.OnStartCharacter();

			if (!base.IsOwner)
			{
				enabled = false;
				return;
			}
		}

		public Dictionary<string, QuestInstance> Quests
		{
			get
			{
				return this.quests;
			}
		}

		public bool TryGetQuest(string name, out QuestInstance quest)
		{
			return this.quests.TryGetValue(name, out quest);
		}

		void Update()
		{
		}

		public void Acquire(QuestTemplate quest)
		{

		}
	}
}