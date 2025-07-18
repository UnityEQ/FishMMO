﻿using UnityEngine;
using TMPro;
using FishMMO.Shared;

namespace FishMMO.Client
{
	public sealed class Cached3DLabel : MonoBehaviour, IReference
	{
		private bool manualCache;
		private float remainingTime;

		public TextMeshPro TMP;

		void Update()
		{
			if (!manualCache)
			{
				if (remainingTime < 0.0f)
				{
					LabelMaker.Cache(this);
					return;
				}
				remainingTime -= Time.deltaTime;
			}
		}

		public void Initialize(string text, Vector3 position, Color color, float fontSize, float persistTime, bool manualCache)
		{
			string hex = color.ToHex();
			if (!string.IsNullOrWhiteSpace(hex))
			{
				TMP.text = "<color=#" + hex + ">" + text + "</color>";
			}
			else
			{
				TMP.text = text;
			}
			TMP.fontSize = fontSize;
			
			// Adjust position offset based on the text mesh height
			TMP.ForceMeshUpdate();
			float textHeight = TMP.textBounds.size.y;
			position.y += textHeight;
			transform.position = position;

			remainingTime = persistTime;
			this.manualCache = manualCache;
		}

		public void SetPosition(Vector3 position)
		{
			transform.position = position;
		}

		public void SetText(string text)
		{
			if (TMP != null)
			{
				TMP.text = text;
			}
		}
	}
}
