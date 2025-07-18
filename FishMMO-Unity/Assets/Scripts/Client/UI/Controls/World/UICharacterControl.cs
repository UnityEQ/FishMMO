﻿using FishMMO.Shared;
using System;

namespace FishMMO.Client
{
	public class UICharacterControl : UIControl
	{
		public Action<IPlayerCharacter> OnSetCharacter;
		public Action OnUnsetCharacter;

		public IPlayerCharacter Character { get; private set; }

		public override void OnStarting()
		{
		}

		public override void OnDestroying()
		{
			Character = null;
		}

		/// <summary>
		/// Invoked before Character is set.
		/// </summary>
		public virtual void OnPreSetCharacter() { }

		/// <summary>
		/// Invoked immediately after Character is set.
		/// </summary>
		public virtual void OnPostSetCharacter() { }

		public void SetCharacter(IPlayerCharacter character)
		{
			OnPreSetCharacter();

			Character = character;

			OnSetCharacter?.Invoke(character);

			OnPostSetCharacter();
		}

		/// <summary>
		/// Invoked before Character is unset.
		/// </summary>
		public virtual void OnPreUnsetCharacter() { }

		/// <summary>
		/// Invoked immediately after Character is unset.
		/// </summary>
		public virtual void OnPostUnsetCharacter() { }

		public void UnsetCharacter()
		{
			OnPreUnsetCharacter();

			OnUnsetCharacter?.Invoke();

			Character = null;

			OnPostUnsetCharacter();
		}
	}
}