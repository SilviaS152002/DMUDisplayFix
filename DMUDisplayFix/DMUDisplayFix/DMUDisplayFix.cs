using HarmonyLib;
using Model;
using Model.Definition.Data;
using Model.Ops;
using Model.Ops.Definition;
using RollingStock;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DMUDisplayFix
{
	[HarmonyPatch(typeof(CarPickable))]
	public static class CarPickablePatches
	{
		

		[HarmonyPatch("TooltipText"), HarmonyPrefix]
		public static bool TooltipTextPrefix(CarPickable __instance, ref string __result) => false;

		[HarmonyPatch("TooltipText"), HarmonyPostfix]
		public static void TooltipTextPostfix(CarPickable __instance, ref string __result)
		{
			string cachedTooltipText = (string)AccessTools.Field(typeof(CarPickable), "_cachedTooltipText").GetValue(__instance);
			float cachedTooltipTextTime = (float)AccessTools.Field(typeof(CarPickable), "_cachedTooltipTextTime").GetValue(__instance);
			float unscaledTime = Time.unscaledTime;
			if (cachedTooltipText != null && cachedTooltipTextTime + 1f > unscaledTime)
			{
				__result = cachedTooltipText;
				return;
			}
			List<string> list = new List<string>();
			OpsController shared = OpsController.Shared;
			if (shared != null && shared.TryGetDestinationInfo(__instance.car, out var destinationName, out var isAtDestination, out var _, out var _))
			{
				string item = (isAtDestination ? "<sprite name=\"Spotted\">" : "<sprite name=\"Destination\">") + " " + destinationName;
				list.Add(item);
			}
			if (__instance.car.TryGetTrainName(out var trainName))
			{
				list.Add(trainName);
			}
			bool flag = false;
			bool flag2 = false;
			if (__instance.car.Definition.LoadSlots.Count > 0)
			{
				flag2 = true;
				StringBuilder stringBuilder = new StringBuilder();
				foreach (var item5 in __instance.car.Definition.DisplayOrderLoadSlots())
				{
					LoadSlot item2 = item5.slot;
					int item3 = item5.index;
					CarLoadInfo? loadInfo = __instance.car.GetLoadInfo(item3);
					if (loadInfo.HasValue && loadInfo.Value.LoadId != "passengers")
					{
						CarLoadInfo value = loadInfo.Value;
						Load load = CarPrototypeLibrary.instance.LoadForId(value.LoadId);
						if (load == null)
						{
							Debug.LogWarning("Load unknown to library: " + value.LoadId);
							continue;
						}
						stringBuilder.Append(TextSprites.PiePercent(value.Quantity, item2.MaximumCapacity));
						stringBuilder.Append(" ");
						stringBuilder.Append(value.LoadString(load));
						list.Add(stringBuilder.ToString());
						stringBuilder.Clear();
						flag = true;
						// Debug.Log(__instance.car + " has added load " + load.id);
					}
				}
			}
			if (__instance.car.IsPassengerCar())
			{
				// Debug.Log(__instance.car + " Is passenger car");
				flag2 = true;
				PassengerMarker passengerMarker = __instance.car.GetPassengerMarker() ?? PassengerMarker.Empty();
				string item4 = (string)AccessTools.Method(typeof(CarPickable), "PassengerString").Invoke(__instance, [__instance.car, passengerMarker]);
				list.Add(item4);
				flag = true;
			}
			if (flag2 && !flag)
			{
				list.Add(TextSprites.PiePercent(0f, 1f) + " Empty");
			}
			if (__instance.car.air.handbrakeApplied)
			{
				list.Add("<sprite name=\"HandbrakeWheel\"> Handbrake");
			}
			if (__instance.car.HasHotbox)
			{
				list.Add("<sprite name=\"Flame\"> Hotbox");
			}
			__result = string.Join("\n", list);
			AccessTools.Field(typeof(CarPickable), "_cachedTooltipText").SetValue(__instance, __result);
			AccessTools.Field(typeof(CarPickable), "_cachedTooltipTextTime").SetValue(__instance, unscaledTime);
			return;
		}

		/*
		[HarmonyPatch("TooltipText"), HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> TooltipTextTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			List<CodeInstruction> codes = instructions.ToList();

			Label branchTo = default;

			Label lblExtract = il.DefineLabel();

			for (int i = 0; i < codes.Count; i++)
			{
				// Patch 1 for ignoring if load ID is "passengers"
				if (codes[i].opcode == OpCodes.Ldloca_S && (int)codes[i].operand == 16 &&
					codes[i + 1].opcode == OpCodes.Call && codes[i + 1].operand is MethodInfo methodInfo1 && methodInfo1.Name == "get_HasValue" &&
					codes[i + 2].opcode == OpCodes.Brfalse)
				{
					branchTo = (Label)codes[i + 2].operand;

					yield return codes[i];
					yield return codes[i + 1];

					// Duplicate and check again
					yield return new CodeInstruction(OpCodes.Dup);
					yield return new CodeInstruction(OpCodes.Brfalse, branchTo);


					yield return new CodeInstruction(OpCodes.Ldloca_S, 16);

					MethodInfo getValue = AccessTools.PropertyGetter(typeof(Nullable<CarLoadInfo>), "Value");
					yield return new CodeInstruction(OpCodes.Call, getValue);

					MethodInfo getLoadId = AccessTools.PropertyGetter(typeof(CarLoadInfo), "LoadId");
					yield return new CodeInstruction(OpCodes.Callvirt, getLoadId);

					yield return new CodeInstruction(OpCodes.Ldstr, "passengers");

					MethodInfo opInequality = AccessTools.Method(typeof(string), "op_Inequality", new Type[] { typeof(string), typeof(string) });
					yield return new CodeInstruction(OpCodes.Call, opInequality);

					yield return new CodeInstruction(OpCodes.And);

					yield return new CodeInstruction(OpCodes.Brfalse, branchTo);

					i+=2;
					continue;
				}

				// Patch 2 for creating PassengerMarker.Empty()
				if (codes[i].opcode == OpCodes.Ldarg_0 &&
					codes[i + 1].opcode == OpCodes.Ldfld && codes[i + 1].operand is FieldInfo fieldInfo2 && fieldInfo2.Name == "car" &&
					codes[i + 2].opcode == OpCodes.Call && codes[i + 2].operand is MethodInfo methodInfo2 && methodInfo2.Name == "GetPassengerMarker" &&
					codes[i + 3].opcode == OpCodes.Stloc_S)
				{
					int targetStoreLocation = (int)codes[i + 4].operand;

					yield return new CodeInstruction(codes[i]);
					yield return new CodeInstruction(codes[i + 1]);
					yield return new CodeInstruction(codes[i + 2]);

					yield return new CodeInstruction(OpCodes.Dup);

					MethodInfo getHasValue = AccessTools.PropertyGetter(typeof(Nullable<PassengerMarker>), "HasValue");
					yield return new CodeInstruction(OpCodes.Call, getHasValue);

					yield return new CodeInstruction(OpCodes.Brtrue_S, lblExtract);

					// Note: I gave up halfway

				}

				yield return codes[i];

			}
		}

		*/

	}
}
