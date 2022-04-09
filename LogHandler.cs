#if DEBUG
#define TranspilerDebug
#define StopWatch
#endif

using System;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.IO;
using HarmonyLib;
using UnityEngine;
#if BepInEx
using BepInEx;
using BepInEx.Harmony;
#endif
using Mod;
using UI;
using System.Xml.Serialization;
using System.Collections.Concurrent;
#pragma warning disable IDE0051

namespace SMotionLoader {
	public class LogHandler : MonoBehaviour {
		public readonly ConcurrentBag<FileInfo> SMotionAssemblies = new ConcurrentBag<FileInfo>();
		public Assembly GetExecutingAssembly() => Assembly.GetExecutingAssembly();
		void OnEnable() {
			Application.logMessageReceivedThreaded += GetPids;
		}
		void OnDisable() {
			Application.logMessageReceivedThreaded -= GetPids;
			SMotionAssemblies.Clear();
		}
		void GetPids(string logString, string stackTrace, LogType type) {
			if (type == LogType.Log && logString.Contains("1SMotion-Loader.dll") && logString.StartsWith("load : ")) {
				SMotionAssemblies.Add(new FileInfo(logString.Substring(7)));
			}
		}
		public void StopLoggingSML() {
			Application.logMessageReceivedThreaded -= GetPids;
		}
	}
}