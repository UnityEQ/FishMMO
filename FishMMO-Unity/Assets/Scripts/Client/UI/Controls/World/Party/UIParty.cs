﻿using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;
using FishMMO.Shared;

namespace FishMMO.Client
{
	public class UIParty : UICharacterControl
	{
		public RectTransform PartyMemberParent;
		public UIPartyMember PartyMemberPrefab;
		public Dictionary<long, UIPartyMember> Members = new Dictionary<long, UIPartyMember>();

		public override void OnDestroying()
		{
			foreach (UIPartyMember member in new List<UIPartyMember>(Members.Values))
			{
				Destroy(member.gameObject);
			}
			Members.Clear();
		}

		public override void OnPostSetCharacter()
		{
			base.OnPostSetCharacter();

			if (Character.TryGet(out IPartyController partyController))
			{
				partyController.OnPartyCreated += OnPartyCreated;
				partyController.OnReceivePartyInvite += PartyController_OnReceivePartyInvite;
				partyController.OnAddPartyMember += OnPartyAddMember;
				partyController.OnValidatePartyMembers += PartyController_OnValidatePartyMembers;
				partyController.OnRemovePartyMember += OnPartyRemoveMember;
				partyController.OnLeaveParty += OnLeaveParty;
			}
		}

		public override void OnPreUnsetCharacter()
		{
			base.OnPreUnsetCharacter();

			if (Character.TryGet(out IPartyController partyController))
			{
				partyController.OnPartyCreated -= OnPartyCreated;
				partyController.OnReceivePartyInvite -= PartyController_OnReceivePartyInvite;
				partyController.OnAddPartyMember -= OnPartyAddMember;
				partyController.OnValidatePartyMembers -= PartyController_OnValidatePartyMembers;
				partyController.OnRemovePartyMember -= OnPartyRemoveMember;
				partyController.OnLeaveParty -= OnLeaveParty;
			}
		}

		public void PartyController_OnReceivePartyInvite(long inviterCharacterID)
		{
			ClientNamingSystem.SetName(NamingSystemType.CharacterName, inviterCharacterID, (n) =>
			{
				if (UIManager.TryGet("UIDialogBox", out UIDialogBox uiTooltip))
				{
					uiTooltip.Open("You have been invited to join " + n + "'s party. Would you like to join?",
					() =>
					{
						Client.Broadcast(new PartyAcceptInviteBroadcast(), Channel.Reliable);
					},
					() =>
					{
						Client.Broadcast(new PartyDeclineInviteBroadcast(), Channel.Reliable);
					});
				}
			});
		}

		public void PartyController_OnValidatePartyMembers(HashSet<long> newMembers)
		{
			foreach (long id in new HashSet<long>(Members.Keys))
			{
				if (!newMembers.Contains(id))
				{
					OnPartyRemoveMember(id);
				}
			}
		}

		public void OnPartyCreated(string location)
		{
			if (Character != null &&
				PartyMemberPrefab != null &&
				PartyMemberParent != null &&
				Character.TryGet(out IPartyController partyController))
			{
				UIPartyMember member = Instantiate(PartyMemberPrefab, PartyMemberParent);
				if (member != null)
				{
					if (member.Name != null)
						member.Name.text = Character.CharacterName;
					if (member.Rank != null)
					{
						member.Rank.gameObject.name = partyController.Rank.ToString();
						member.Rank.text = partyController.Rank == PartyRank.Leader ? "*" : "";
					}
					if (member.Health != null)
						member.Health.value = Character.TryGet(out ICharacterAttributeController attributeController) ? attributeController.GetHealthResourceAttributeCurrentPercentage() : 0.0f;
					
					member.gameObject.SetActive(true);
					Members.Add(Character.ID, member);
				}
			}
		}

		public void OnLeaveParty()
		{
			foreach (UIPartyMember member in new List<UIPartyMember>(Members.Values))
			{
				Destroy(member.gameObject);
			}
			Members.Clear();
		}

		public void OnPartyAddMember(long characterID, PartyRank rank, float healthPCT)
		{
			if (PartyMemberPrefab != null && PartyMemberParent != null)
			{
				if (!Members.TryGetValue(characterID, out UIPartyMember partyMember))
				{
					partyMember = Instantiate(PartyMemberPrefab, PartyMemberParent);
					partyMember.gameObject.SetActive(true);
					Members.Add(characterID, partyMember);
				}
				if (partyMember.Name != null)
				{
					ClientNamingSystem.SetName(NamingSystemType.CharacterName, characterID, (n) =>
					{
						partyMember.Name.text = n;
					});
				}
				if (partyMember.Rank != null)
				{
					partyMember.Rank.gameObject.name = rank.ToString();
					partyMember.Rank.text = rank == PartyRank.Leader ? "*" : "";
				}
				if (partyMember.Health != null)
					partyMember.Health.value = healthPCT;
			}
		}

		public void OnPartyRemoveMember(long characterID)
		{
			if (Members.TryGetValue(characterID, out UIPartyMember member))
			{
				Members.Remove(characterID);
				Destroy(member.gameObject);
			}
		}

		public void OnButtonCreateParty()
		{
			if (Character != null &&
				Character.TryGet(out IPartyController partyController) &&
				partyController.ID < 1)
			{
				Client.Broadcast(new PartyCreateBroadcast(), Channel.Reliable);
			}
		}

		public void OnButtonLeaveParty()
		{
			if (Character != null &&
				Character.TryGet(out IPartyController partyController) &&
				partyController.ID > 0)
			{
				if (UIManager.TryGet("UIDialogBox", out UIDialogBox tooltip))
				{
					tooltip.Open("Are you sure you want to leave your party?", () =>
					{
						Client.Broadcast(new PartyLeaveBroadcast(), Channel.Reliable);
					}, () => { });
				}
			}
		}

		public void OnButtonInviteToParty()
		{
			if (Character != null &&
				Character.TryGet(out IPartyController partyController) &&
				partyController.ID > 0 &&
				Client.NetworkManager.IsClientStarted)
			{
				if (Character.TryGet(out ITargetController targetController) &&
					targetController.Current.Target != null)
				{
					IPlayerCharacter targetCharacter = targetController.Current.Target.GetComponent<IPlayerCharacter>();
					if (targetCharacter != null)
					{
						Client.Broadcast(new PartyInviteBroadcast()
						{
							TargetCharacterID = targetCharacter.ID,
						}, Channel.Reliable);

						return;
					}
				}

				if (UIManager.TryGet("UIDialogInputBox", out UIDialogInputBox tooltip))
				{
					tooltip.Open("Please type the name of the person you wish to invite.", (s) =>
					{
						if (Constants.Authentication.IsAllowedCharacterName(s))
						{
							ClientNamingSystem.GetCharacterID(s, (id) =>
							{
								if (id != 0)
								{
									if (Character.ID != id)
									{
										Client.Broadcast(new PartyInviteBroadcast()
										{
											TargetCharacterID = id,
										}, Channel.Reliable);
									}
									else if (UIManager.TryGet("UIChat", out UIChat chat))
									{
										chat.InstantiateChatMessage(ChatChannel.System, "", "You can't invite yourself to the party.");
									}
								}
								else if (UIManager.TryGet("UIChat", out UIChat chat))
								{
									chat.InstantiateChatMessage(ChatChannel.System, "", "A person with that name could not be found.");
								}
							});
						}
					}, null);
				}
			}
		}
	}
}