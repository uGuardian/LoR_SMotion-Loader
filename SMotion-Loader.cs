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
using Workshop;
using UI;
using System.Xml.Serialization;
using System.Collections.Concurrent;

namespace SMotionLoader
{
	public static class Globals {
		public const string Version = "1.3.1";
	}
	#if BepInEx
	[BepInPlugin("LoR.uGuardian.SMotionLoader", "SMotion-Loader", SMotionLoader.Globals.Version)]
	public class SMotionLoader_BepInEx : BaseUnityPlugin {
		public static bool BepInEx = false;
		#pragma warning disable IDE0051
		void Awake() {
			BepInEx = true;
			Debug.Log($"SMotionLoader: Using Version {Assembly.GetExecutingAssembly().GetName().Version}");
			SMotion_Patch.Patch(null);
			SMotion_Patch.ErrorRemoval_BepInEx();
		}
		#pragma warning restore IDE0051
	}
	#endif
	public class SMotionLoader_Vanilla : ModInitializer
	{
		#if !BepInEx
		public static readonly LogHandler logHandler = ((EntryScene)UnityEngine.Object.FindObjectOfType(typeof(EntryScene))).gameObject.AddComponent<LogHandler>();
		public override void OnInitializeMod() {
			if (Harmony.HasAnyPatches("LoR.uGuardian.SMotionLoader")) {
				if (Singleton<ModContentManager>.Instance.GetErrorLogs()
					.Contains("BepInEx version of SMotion-Loader is outdated, please update!")) {
						return;
				}
				var thisVersion = Assembly.GetExecutingAssembly().GetName().Version;
				foreach (var assembly2 in AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => a.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name)) {
						if (thisVersion > assembly2.GetName().Version
							&& ((bool?)assembly2.GetType("SMotionLoader.SMotionLoader_BepInEx")?
								.GetField("BepInEx", BindingFlags.Static | BindingFlags.Public)?.GetValue(null)).GetValueOrDefault()) {
									Singleton<ModContentManager>.Instance.AddErrorLog("BepInEx version of SMotion-Loader is outdated, please update!");
						}
				}
				goto ErrorRemoval;
			}
			var currentAssembly = Assembly.GetExecutingAssembly();
			var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.GetName().Name == currentAssembly.GetName().Name);
			var assembly = loadedAssemblies.OrderByDescending(v => v.GetName().Version).First();

			Debug.Log($"SMotionLoader: Using Version {assembly.GetName().Version}");
			assembly.GetType("SMotionLoader.SMotion_Patch").GetMethod(nameof(SMotion_Patch.Patch))
				.Invoke(null, new object[]{new V2Tuple(currentAssembly, loadedAssemblies, logHandler)});
			ErrorRemoval:
			Singleton<ModContentManager>.Instance.GetErrorLogs().RemoveAll(x => dllList.Any(x.Contains));
		}
		#endif
		const string exists = "The same assembly name already exists. : "; 
		public readonly List<string> dllList = new List<string> {
			exists+"0Harmony",
			exists+"Mono.Cecil",
			exists+"MonoMod.RuntimeDetour",
			exists+"MonoMod.Utils",
			exists+"1SMotion-Loader",
		};
	}
	public class V2Tuple : Tuple<Assembly, IEnumerable<Assembly>, LogHandler> {
		public V2Tuple(Assembly currentAssembly, IEnumerable<Assembly> loadedAssemblies, LogHandler logHandler) : base(currentAssembly, loadedAssemblies, logHandler) {}
	}
	public static class SMotion_Patch {
		static readonly Harmony harmony = new Harmony("LoR.uGuardian.SMotionLoader");
		public static void ErrorRemoval_BepInEx() {
			harmony.Patch(typeof(AssemblyManager).GetMethod(nameof(AssemblyManager.LoadAllAssembly), AccessTools.all),
				postfix: new HarmonyMethod(typeof(SMotion_Patch).GetMethod(nameof(ErrorRemoval_BepInEx_PostFix))));
		}
		public static void ErrorRemoval_BepInEx_PostFix() {
			Singleton<ModContentManager>.Instance.GetErrorLogs().RemoveAll(x => new SMotionLoader_Vanilla().dllList.Any(x.Contains));
		}
		#if !BepInEx
		public static IEnumerable<LogHandler> GetHandlers() {
			var currentAssembly = Assembly.GetExecutingAssembly();
			var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.GetName().Name == currentAssembly.GetName().Name);
			return GetHandlers(loadedAssemblies);
		}
		public static IEnumerable<LogHandler> GetHandlers(IEnumerable<Assembly> loadedAssemblies) {
			var sceneObject = ((EntryScene)UnityEngine.Object.FindObjectOfType(typeof(EntryScene))).gameObject;
			return loadedAssemblies.Select(a => a.GetType("SMotionLoader.SMotionLoader_Vanilla")?
				.GetField(nameof(SMotionLoader_Vanilla.logHandler))?.GetValue(null) as LogHandler)
				.Where(handler => handler != null).Distinct();
		}
		#endif
		#if !NoAsync
		public static async void Patch(object input) {
		#else
		public static void Patch(object input) {
		#endif
			try
			{
				var tasks = new List<Task>();
				var patch = new HarmonyMethod(typeof(SMotion_Patch).GetMethod("Transpiler"));
				#if !NoAsync
				Debug.Log("SMotionLoader: Waiting for async transpilers...");
				tasks.Add(AsyncPatch(typeof(WorkshopAppearanceItemLoader).GetMethod("LoadCustomAppearanceInfo", AccessTools.all), null, null, patch, null, null));
				#else
				harmony.Patch(typeof(WorkshopAppearanceItemLoader).GetMethod("LoadCustomAppearanceInfo", AccessTools.all), null, null, patch, null, null);
				#endif
				var entryFixer = new HarmonyMethod(typeof(SMotion_Patch).GetMethod(nameof(EntryTranspiler)));
				#if !NoAsync
				harmony.Patch(typeof(WorkshopSkinDataSetter).GetMethod("get_Appearance", AccessTools.all), null, null, entryFixer, null, null);
				#else
				tasks.Add(AsyncPatch(typeof(WorkshopSkinDataSetter).GetMethod("get_Appearance", AccessTools.all), null, null, entryFixer, null, null));
				#endif
				/*
				var initPostfix = new HarmonyMethod(typeof(SMotion_Patch).GetMethod(nameof(InitPostfix)));
				#if !NoAsync
				harmony.Patch(typeof(WorkshopSkinDataSetter).GetMethod(nameof(WorkshopSkinDataSetter.Init)), null, initPostfix, null, null, null);
				#else
				tasks.Add(AsyncPatch(typeof(WorkshopSkinDataSetter).GetMethod(nameof(WorkshopSkinDataSetter.Init))), null, initPostfix, null, null, null));
				#endif
				*/
				/*
				var loadTranspiler =  new HarmonyMethod(typeof(SMotion_Patch).GetMethod("LoadTranspiler"));
				var methods1 = typeof(AssetBundleManagerRemake).GetMethods(AccessTools.allDeclared);
				var methods2 = typeof(SdCharacterUtil).GetMethods(AccessTools.allDeclared);
				var methods3 = typeof(UICharacterRenderer).GetMethods(AccessTools.allDeclared);
				foreach (var method in methods1) {
					#if !NoAsync
					tasks.Add(AsyncPatch(method, null, null, loadTranspiler, null, null));
					#else
					harmony.Patch(method, null, null, loadTranspiler, null, null);
					#endif
				}
				foreach (var method in methods2) {
					#if !NoAsync
					tasks.Add(AsyncPatch(method, null, null, loadTranspiler, null, null));
					#else
					harmony.Patch(method, null, null, loadTranspiler, null, null);
					#endif
				}
				foreach (var method in methods3) {
					#if !NoAsync
					tasks.Add(AsyncPatch(method, null, null, loadTranspiler, null, null));
					#else
					harmony.Patch(method, null, null, loadTranspiler, null, null);
					#endif
				}
				*/
				#if !NoAsync
				await Task.WhenAll(tasks);
				Debug.Log("SMotionLoader: Finished async transpilers");
				#endif
			} catch (AggregateException ex) {
				ex.Handle((e) => {
						Singleton<ModContentManager>.Instance.AddErrorLog(e);
						return true;
					}
				);
			}
			catch (Exception ex)
			{
				Singleton<ModContentManager>.Instance.AddErrorLog(ex);
			}
			#if !BepInEx
			ParseInput(input);
			#endif
		}
		#if !BepInEx
		public static void ParseInput(object input) {
			if (input == null) {
				return;
			}
			IEnumerable<LogHandler> handlers;
			if (input is V2Tuple v2Tuple) {
				try {
					v2Tuple.Deconstruct(out var currentAssembly2, out var loadedAssemblies2, out var logHandler2);
					handlers = GetHandlers(loadedAssemblies2).SkipWhile(handler => handler == logHandler2);
					LogHandler currentHandler = logHandler2;
					int currentHandlerCount = currentHandler.SMotionAssemblies.Count();
					foreach (var handler in handlers) {
						int count = handler.SMotionAssemblies.Count();
						if (count > currentHandlerCount) {
							currentHandler = handler;
							currentHandlerCount = count;
						}
					}
					IEnumerable<System.IO.FileInfo> assembliesList = currentHandler.SMotionAssemblies
						.Append(new System.IO.FileInfo(currentAssembly2.Location));
					var handlerAssembly = currentHandler.GetExecutingAssembly();
					if (currentAssembly2 != handlerAssembly) {
						assembliesList = assembliesList.Append(new System.IO.FileInfo(handlerAssembly.Location));
					}
					ReloadAllModSkins(assembliesList.Distinct());
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
			if (input is Assembly currentAssembly) {
				try {
					Singleton<ModContentManager>.Instance.AddErrorLog($"SMotion-Loader {Assembly.GetExecutingAssembly().GetName().Version}: First Mod to initialize is using version {currentAssembly.GetName().Version}, any version lower than 1.3.0.0 can cause some skins to not load properly");
					handlers = GetHandlers();
					LogHandler currentHandler = handlers.First();
					int currentHandlerCount = currentHandler.SMotionAssemblies.Count();
					foreach (var handler in handlers) {
						int count = handler.SMotionAssemblies.Count();
						if (count > currentHandlerCount) {
							currentHandler = handler;
							currentHandlerCount = count;
						}
					}
					IEnumerable<System.IO.FileInfo> assembliesList = currentHandler.SMotionAssemblies
						.Append(new System.IO.FileInfo(currentAssembly.Location));
					if (currentAssembly != currentHandler.GetExecutingAssembly()) {
						assembliesList = assembliesList.Append(new System.IO.FileInfo(currentHandler.GetExecutingAssembly().Location));
					}
					ReloadAllModSkins(assembliesList.Distinct());
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
		}
		#endif
		public static void ReloadAllModSkins(IEnumerable<System.IO.FileInfo> assemblyInfos) {
			Debug.Log("Reloading skins from all SMotion-Loader mods");
			foreach (var assembly in assemblyInfos) {
				#if DEBUG
					Debug.Log(assembly.FullName);
				#endif
				DirectoryInfo dirInfo = assembly.Directory.Parent;
				var files = dirInfo.EnumerateFiles("StageModInfo.xml");
				while (files.FirstOrDefault() == null) {
					dirInfo = dirInfo.Parent;
					files = dirInfo.EnumerateFiles("StageModInfo.xml");
				}
				LoadBookSkins(dirInfo, files.First());
			}
		}
		public static void LoadBookSkins(DirectoryInfo dirInfo, System.IO.FileInfo stageModInfo) {
			string uniqueId;
			using (StreamReader streamReader = new StreamReader(stageModInfo.FullName)) {
				NormalInvitation invInfo = (NormalInvitation) new XmlSerializer(typeof(NormalInvitation)).Deserialize(streamReader);
				if (string.IsNullOrEmpty(invInfo.workshopInfo.uniqueId) || invInfo.workshopInfo.uniqueId == "-1") {
					invInfo.workshopInfo.uniqueId = dirInfo.Name;
				}
				uniqueId = invInfo.workshopInfo.uniqueId;
			}
			string path = Path.Combine(dirInfo.FullName, "Resource/CharacterSkin");
			List<WorkshopSkinData> list = new List<WorkshopSkinData>();
			if (!Directory.Exists(path))
				return;
			string[] directories = Directory.GetDirectories(path);
			for (int index = 0; index < directories.Length; ++index) {
				WorkshopAppearanceInfo workshopAppearanceInfo = WorkshopAppearanceItemLoader.LoadCustomAppearance(directories[index], true);
				if (workshopAppearanceInfo != null) {
					string[] strArray = directories[index].Split('\\');
					string str = strArray[strArray.Length - 1];
					workshopAppearanceInfo.path = directories[index];
					workshopAppearanceInfo.uniqueId = uniqueId;
					workshopAppearanceInfo.bookName = str;
					Debug.Log("workshop bookName : " + workshopAppearanceInfo.bookName);
					if (workshopAppearanceInfo.isClothCustom)
						list.Add(new WorkshopSkinData()
						{
							dic = workshopAppearanceInfo.clothCustomInfo,
							dataName = workshopAppearanceInfo.bookName,
							contentFolderIdx = workshopAppearanceInfo.uniqueId,
							id = index
						});
				}
			}
			// Singleton<CustomizingBookSkinLoader>.Instance.AddBookSkinData(uniqueId, list);
			Singleton<CustomizingBookSkinLoader>.Instance._bookSkinData[uniqueId] = list;
		}
		public static Task AsyncPatch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null, HarmonyMethod ilmanipulator = null) {
			harmony.Patch(original, prefix, postfix, transpiler, finalizer, ilmanipulator);
			return Task.CompletedTask;
		}
		public static void EntryFix(WorkshopSkinDataSetter Instance, CharacterAppearance appearance) {
			try {
				#if StopWatch
				var stopWatch = System.Diagnostics.Stopwatch.StartNew();
				#endif
				CharacterMotion genericMotion = GetGenericMotion(appearance);
				#if TranspilerDebug
					#if StopWatch
					stopWatch.Stop();
					#endif
				Debug.Log("Dictionary Check Start");
					#if StopWatch
					stopWatch.Start();
					#endif
				#endif
				var newDic = new Dictionary<ActionDetail, ClothCustomizeData>(Instance.dic);
				var empties = new HashSet<ActionDetail>();
				foreach (var entry in newDic) {
					// empties.Remove(entry.Key);
				}
				foreach (var motion in appearance._motionList) {
					if (newDic.Remove(motion.actionDetail)) {
						#if TranspilerDebug
							#if StopWatch
							stopWatch.Stop();
							#endif
						Debug.Log(motion.actionDetail);
							#if StopWatch
							stopWatch.Start();
							#endif
						#endif
					}
					else {
						empties.Add(motion.actionDetail);
						#if TranspilerDebug
							#if StopWatch
							stopWatch.Stop();
							#endif
						if (motion.actionDetail != ActionDetail.Standing) {
							Debug.LogWarning(motion.actionDetail);
						} else {
							Debug.Log(motion.actionDetail);
						}
							#if StopWatch
							stopWatch.Start();
							#endif
						#endif
					}
				}
				#if TranspilerDebug
					#if StopWatch
					stopWatch.Stop();
					#endif
				Debug.Log("Dictionary Check End");
					#if StopWatch
					stopWatch.Start();
					#endif
				#endif
				foreach (var entry in newDic) {
					var action = entry.Key;
					#if TranspilerDebug
						#if StopWatch
						stopWatch.Stop();
						#endif
					Debug.LogWarning(action);
						#if StopWatch
						stopWatch.Start();
						#endif
					#endif
					var motion = UnityEngine.Object.Instantiate(genericMotion, genericMotion.transform.parent);
					motion.name = "Custom_"+Enum.GetName(typeof(ActionDetail), action);
					motion.actionDetail = action;
					appearance._motionList.Add(motion);
				}
				// Checks to see if a mod has preemptively added Character Motions.
				foreach (var motion in appearance.CharacterMotions.Keys) {
					if (empties.Remove(motion)) {
						Debug.LogWarning($"SMotion-Loader: Motion {motion} was already defined by something else");
					}
				}
				// Sets all empty basic actions to use Penetrate, as generally handled by the base game.
				foreach (var action in empties) {
					if (action == ActionDetail.NONE || action == ActionDetail.Standing) {
						continue;
					}
					appearance.CharacterMotions.Add(action, genericMotion);
				}
				#if StopWatch
				stopWatch.Stop();
				Debug.LogWarning(stopWatch.Elapsed);
				Debug.LogWarning(stopWatch.ElapsedMilliseconds);
				#endif
			} catch (ArgumentNullException) {
				Debug.LogWarning("SMotion-Loader: Current skin does not exist in dictionary, this is most likely caused by another mod handling skin loading differently");
			} catch (NullReferenceException ex) {
				Debug.LogError("SMotion-Loader: A variable was null, most likely the skin dictionary itself, please report this bug immediately!");
				Debug.LogException(ex);
			} catch (Exception ex) {
				Debug.LogError("SMotion-Loader: Unknown exception");
				Debug.LogException(ex);
			}
		}
		public static void InitPostfix(WorkshopSkinDataSetter __instance) {
			try {
				var appearance = __instance.Appearance;
				var empties = new List<ActionDetail>((ActionDetail[])Enum.GetValues(typeof(ActionDetail)));
				foreach (var motion in __instance.Appearance.CharacterMotions) {
					empties.Remove(motion.Key);
				}
				var genericMotion = GetGenericMotion(appearance);
				foreach (var motion in empties) {
					switch (motion) {
						case ActionDetail.NONE:
							break;
						case ActionDetail.Slash2:
							appearance.CharacterMotions.Add(motion, appearance.GetCharacterMotion(ActionDetail.Slash) ?? genericMotion);
							break;
						case ActionDetail.Penetrate2:
							appearance.CharacterMotions.Add(motion, appearance.GetCharacterMotion(ActionDetail.Penetrate) ?? genericMotion);
							break;
						case ActionDetail.Hit2:
							appearance.CharacterMotions.Add(motion, appearance.GetCharacterMotion(ActionDetail.Hit) ?? genericMotion);
							break;
						default:
							appearance.CharacterMotions.Add(motion, genericMotion);
							break;
					}
				}
			} catch (Exception ex) {
				Debug.LogError("SMotion-Loader: Init Postfix Exception");
				Debug.LogException(ex);
			}
		}
		private static CharacterMotion GetGenericMotion(CharacterAppearance appearance) {
			// var genericMotion = appearance._motionList.Find((CharacterMotion x) => x.actionDetail == ActionDetail.Penetrate);
			var genericMotion = appearance.GetCharacterMotion(ActionDetail.Penetrate);
			if (genericMotion == null) {
				Debug.LogError("Penetrate motion doesn't exist, the game uses this as a default for missing non-SMotions! Attempting to use Default motion...");
				// genericMotion = appearance._motionList.Find((CharacterMotion x) => x.actionDetail == ActionDetail.Default);
				genericMotion = appearance.GetCharacterMotion(ActionDetail.Default);
				if (genericMotion == null) {
					Debug.LogError("Default motion not found, attempting to use Standing motion...");
					// genericMotion = appearance._motionList.Find((CharacterMotion x) => x.actionDetail == ActionDetail.Standing);
					genericMotion = appearance.GetCharacterMotion(ActionDetail.Standing);
					if (genericMotion == null) {
						Debug.LogError("All normal defaults are missing, cancelling SMotion loading.");
					}
				}
			}
			return genericMotion;
		}

		public static IEnumerable<CodeInstruction> EntryTranspiler(IEnumerable<CodeInstruction> instructions) {
			#pragma warning disable CS0252
			var codes = new List<CodeInstruction>(instructions);

			for (var i = 0; i < codes.Count; i++) {
				if (codes[i].opcode == OpCodes.Ldarg_0
					&& codes[i+3].opcode == OpCodes.Callvirt
					&& codes[i+3].operand == typeof(CharacterAppearance).GetMethod(nameof(CharacterAppearance.Initialize))) {
						var newcodes = new List<CodeInstruction>() {
							codes[0],
							codes[0],
							codes[1],
							CodeInstruction.Call(typeof(SMotion_Patch), "EntryFix"),
						};
						codes.InsertRange(i, newcodes);
						#if TranspilerDebug
							// highlight.Add(codes[i+3]);
							highlight.AddRange(newcodes);
						#endif
						break;
				}
			}

			#if TranspilerDebug
				DebugPrint(codes);
			#endif
			return codes.AsEnumerable();
		}

		public static GameObject PrefabReplacement() {
			if (Appearance_Custom == null) {
				Appearance_Custom = Resources.Load<GameObject>("Prefabs/Characters/[Prefab]Appearance_Custom");
				var appearance = Appearance_Custom.GetComponent<CharacterAppearance>();
				var motionDefault = appearance._motionList.Find((CharacterMotion x) => x.actionDetail == ActionDetail.Penetrate);
				foreach (ActionDetail action in Enum.GetValues(typeof(ActionDetail))) {
					if (appearance._motionList.Find((CharacterMotion x) => x.actionDetail == action) == null) {
						var motion = UnityEngine.Object.Instantiate(motionDefault, motionDefault.transform.parent);
						motion.name = "Custom_"+Enum.GetName(typeof(ActionDetail), action);
						motion.actionDetail = action;
						appearance._motionList.Add(motion);
					}
				}
			}
			return UnityEngine.Object.Instantiate(Appearance_Custom);
		}
		public static GameObject Appearance_Custom;

		public static IEnumerable<CodeInstruction> LoadTranspiler(IEnumerable<CodeInstruction> instructions) {
			#pragma warning disable CS0252
			var codes = new List<CodeInstruction>(instructions);

			for (var i = 0; i < codes.Count; i++) {
				if (codes[i].opcode == OpCodes.Ldstr
					&& codes[i].operand == "Prefabs/Characters/[Prefab]Appearance_Custom"
					&& codes[i+1].opcode == OpCodes.Call) {
						codes[i].opcode = OpCodes.Nop;
						codes[i+1] = CodeInstruction.Call(typeof(SMotion_Patch), "PrefabReplacement");
						Debug.Log("SMotionLoader: PrefabReplacement transpiler Successful");
						#if TranspilerDebug
							highlight.Add(codes[i]);
							highlight.Add(codes[i+1]);
							DebugPrint(codes);
						#endif
						break;
				}
			}
			return codes.AsEnumerable();
		}


		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			#pragma warning disable CS0252
			var codes = new List<CodeInstruction>(instructions);
			int currentIndex = 0;

			#if !TEMP
			for (var i = currentIndex; i < codes.Count; i++) {
				if (codes[i].opcode == OpCodes.Ldloc_S
					&& codes[i+1].opcode == OpCodes.Brtrue
					&& codes[i+2].opcode == OpCodes.Ldstr
					&& codes[i+2].operand == "Workshop :: ") {
						for (var x = i+3; x < codes.Count; x++) {
							if (codes[x].opcode == OpCodes.Brfalse) {
								#if TranspilerDebug
									// highlight.AddRange(codes.GetRange(i, x-i));
									foreach (var code in codes.GetRange(i, x-i-1)) {
										Debug.LogWarning(code);
									}
								#endif
								codes.RemoveRange(i, x-i-1);
								Debug.Log("SMotionLoader: Optimization transpiler Successful");
								currentIndex = i;
								break;
							}
						}
						#if TranspilerDebug
							highlight.Add(codes[i]);
						#endif
						break;
				}
			}
			#endif

			sbyte enumLength = (sbyte)(Enum.GetValues(typeof(ActionDetail)).Length - 1);
			for (var i = currentIndex; i < codes.Count; i++) {
				if (codes[i].opcode == OpCodes.Ldc_I4_S
					&& codes[i].LoadsConstant(11)) {
						codes[i].operand = enumLength;
						// Debug.Log(codes[i].operand.GetType());
						#if TranspilerDebug
							highlight.Add(codes[i]);
						#endif
						Debug.Log("SMotionLoader: Entry Expansion transpiler Successful");
						break;
					}
			}

			#if TranspilerDebug
				DebugPrint(codes);
			#endif
			return codes.AsEnumerable();
		}

		#if TranspilerDebug
			public static void DebugPrint(List<CodeInstruction> codes) {
				Debug.LogWarning("Begin Method:");
				int d = 0;
				foreach (var code in codes) {
					if (highlight.Contains(code)) {
						Debug.LogWarning($"{d}: {code}");
					} else {
						Debug.Log($"{d}: {code}");
					}
					d++;
				}
				Debug.Log("End Method");
			}
			static readonly List<CodeInstruction> highlight = new List<CodeInstruction>();
		#endif
	}
}
