﻿using FishNet.Transporting;
using FishNet.Transporting.Multipass;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
#endif
using System.Runtime.InteropServices;
using UnityEngine;
using Cysharp.Text;
using System.Runtime.CompilerServices;
using FishMMO.Shared;

namespace FishMMO.Server
{
	public class ServerWindowTitleUpdater : ServerBehaviour
	{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleTitle(string title);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
		private const int PR_SET_NAME = 15;

		[DllImport("libc.so.6", SetLastError=true)]
		private static extern int prctl(int option, string arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX

		[DllImport("libc.dylib", SetLastError=true)]
		private static extern void setproctitle(string fmt, string str_arg);
#endif

		public string Title = "";
		public float UpdateRate = 15.0f;
		public float NextUpdate = 0.0f;

		public override void InitializeOnce()
		{
			if (ServerManager != null)
			{
				UpdateWindowTitle();
			}
			else
			{
				enabled = false;
			}
		}

		public override void Destroying()
		{
		}

		void LateUpdate()
		{
			if (ServerManager == null ||
				!ServerManager.Started)
			{
				return;
			}
			if (NextUpdate < 0)
			{
				NextUpdate = UpdateRate;

				UpdateWindowTitle();
			}
			NextUpdate -= Time.deltaTime;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UpdateWindowTitle()
		{
			Title = null;
			Title = BuildWindowTitle();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			SetConsoleTitle(Title);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			prctl(PR_SET_NAME, Title, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
			setproctitle("{0}", Title);
#endif
		}

		public string BuildWindowTitle()
		{
			if (Server == null)
			{
				return "";
			}
			using (var windowTitle = ZString.CreateStringBuilder())
			{
				if (Configuration.GlobalSettings.TryGetString("ServerName", out string title))
				{
					windowTitle.Append(title);
				}

				if (Server.NetworkManager != null &&
					Server.NetworkManager.TransportManager != null)
				{
					Multipass multipass = Server.NetworkManager.TransportManager.GetTransport<Multipass>();
					if (multipass != null)
					{
						for (int i = 0; i < multipass.Transports.Count; ++i)
						{
							Transport transport = multipass.Transports[i];

							windowTitle.Append($" [{transport.GetType().Name}]");
							windowTitle.Append(transport.GetConnectionState(true) == LocalConnectionState.Started ? "[Online]" : "[Offline]");
						}
					}
					else
					{
						Transport transport = Server.NetworkManager.TransportManager.Transport;
						if (transport != null)
						{
							windowTitle.Append($" [{transport.GetType().Name}]");
							windowTitle.Append(transport.GetConnectionState(true) == LocalConnectionState.Started ? "[Online]" : "[Offline]");
						}
					}

					if (Configuration.GlobalSettings.TryGetUShort("Port", out ushort port))
					{
						windowTitle.Append(" [Server:");
						windowTitle.Append(Server.RemoteAddress);
						windowTitle.Append(":");
						windowTitle.Append(port);
						windowTitle.Append(" Clients:");
						windowTitle.Append(ServerBehaviour.TryGet(out WorldSceneSystem worldSceneSystem) ? worldSceneSystem.ConnectionCount : ServerManager.Clients.Count);
						windowTitle.Append("]");
					}
				}
				return windowTitle.ToString();
			}
		}
	}
}