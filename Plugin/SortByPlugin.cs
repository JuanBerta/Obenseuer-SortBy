using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using OS.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SortByMod
{
	public enum SortMode { Category, Name, Value, StackValue, Amount, Quality, Freshness }

	/// <summary>
	/// Sort By: the inventory's Sort buttons open a "Sort by" menu (category, name, value, stack value, amount,
	/// quality, freshness, optionally reversed), and hotkeys sort your inventory or whatever storage the mouse is
	/// over (bank, backpack, a container...) with the chosen order. Sorting itself is the game's own.
	/// </summary>
	[BepInPlugin(Guid, "Obenseuer Sort By", Version)]
	public class SortByPlugin : BaseUnityPlugin
	{
		public const string Guid = "com.juanberta.obenseuer.sortby";
		public const string Version = "1.0.2";

		internal static SortByPlugin Instance;
		internal static ManualLogSource Log;
		internal static ConfigEntry<SortMode> Mode;
		internal static ConfigEntry<bool> Reverse;
		internal static ConfigEntry<bool> ReplaceButton;
		internal static ConfigEntry<KeyboardShortcut> InventoryKey;
		internal static ConfigEntry<KeyboardShortcut> HoveredKey;
		internal static ConfigEntry<int> FastThreshold;

		internal static bool Bypass;      // our own call into the game's Sort: don't open the menu
		internal static bool MenuOpen;

		private void Awake()
		{
			Instance = this;
			Log = Logger;
			Mode = Config.Bind("Sorting", "SortBy", SortMode.Category,
				"Order used by the Sort buttons and the hotkeys: Category (the game's order), Name, Value (price of one), StackValue (price x amount), Amount, Quality, Freshness. Also changed from the Sort by menu.");
			Reverse = Config.Bind("Sorting", "Reverse", false, "Reverse the order.");
			ReplaceButton = Config.Bind("Sorting", "SortButtonOpensMenu", true,
				"The inventory/storage Sort buttons open the Sort by menu. Off: they sort right away with the current order.");
			InventoryKey = Config.Bind("Keys", "SortInventoryKey", new KeyboardShortcut(KeyCode.U),
				"While the inventory is open: sort your inventory.");
			HoveredKey = Config.Bind("Keys", "SortHoveredKey", new KeyboardShortcut(KeyCode.Y),
				"While the inventory is open: sort the storage under the mouse (your inventory, the bank, the backpack, a container...).");

			FastThreshold = Config.Bind("Sorting", "FastSortFromSlots", 100,
				"Storages with at least this many slots (the bank, big containers) are sorted with a faster routine that places every stack once, instead of the game's one that searches the whole storage for each stack (which freezes the game for a moment on a multi-page bank). 0 = always use the game's routine.");
			try
			{
				var h = new Harmony(Guid);
				h.Patch(AccessTools.Method(typeof(Inventory), "SortSlots"), prefix: new HarmonyMethod(typeof(FastSort), nameof(FastSort.Prefix)));
				MethodInfo cmp = AccessTools.Method(typeof(Inventory), "CompareSortKeys");
				if (cmp != null) h.Patch(cmp, prefix: new HarmonyMethod(typeof(Order), nameof(Order.Prefix)));
				else Logger.LogWarning("Inventory.CompareSortKeys not found: custom orders won't apply.");
				h.Patch(AccessTools.Method(typeof(InventoryPanelUI), "SortInventory"), prefix: new HarmonyMethod(typeof(SortByPlugin), nameof(SortInventoryPrefix)));
				h.Patch(AccessTools.Method(typeof(InventoryPanelUI), "SortStorage"), prefix: new HarmonyMethod(typeof(SortByPlugin), nameof(SortStoragePrefix)));
				Mute.Patch(h);
			}
			catch (Exception e) { Logger.LogError("Patching failed: " + e); }
			gameObject.AddComponent<SortMenu>();
			Logger.LogInfo("Obenseuer Sort By " + Version + " loaded.");
		}

		// ---- The game's Sort buttons ----

		private static bool SortInventoryPrefix() { return Intercept(false); }
		private static bool SortStoragePrefix() { return Intercept(true); }

		private static bool Intercept(bool storage)
		{
			if (Bypass || !ReplaceButton.Value) return true;
			SortMenu.Open(storage);
			return false;
		}

		// ---- Doing the sort ----

		internal static InventoryPanelUI PanelUI
		{
			get
			{
				try { return UnityEngine.Object.FindObjectOfType<InventoryPanelUI>(); } catch (Exception) { return null; }
			}
		}

		internal static bool InventoryVisible
		{
			get
			{
				InventoryPanelUI ui = PanelUI;
				return ui != null && ui.isActiveAndEnabled && Cursor.visible;
			}
		}

		internal static void SortTarget(bool storage)
		{
			InventoryPanelUI ui = PanelUI;
			Bypass = true;
			try
			{
				if (ui != null) { if (storage) ui.SortStorage(); else ui.SortInventory(); }
				else if (Inventory.instance != null) Inventory.instance.SortSlots(storage ? Inventory.instance.ForeignSlots : null);
			}
			catch (Exception e) { Log.LogWarning("Sort failed: " + e.Message); }
			finally { Bypass = false; }
		}

		internal static void SortSlots(SlotController[] slots)
		{
			if (Inventory.instance == null || slots == null || slots.Length == 0) return;
			if (slots == Inventory.instance.Slots) { SortTarget(false); return; }
			if (slots == Inventory.instance.ForeignSlots) { SortTarget(true); return; }
			try
			{
				Inventory.instance.SortSlots(slots);
				InventoryPanelUI ui = PanelUI;
				if (ui != null)
				{
					var sound = Traverse.Create(ui).Field("sortSound").GetValue<RandomAudio>();
					if (sound != null) MiscAudioPlayer.PlayRandomSoundEverywhere(sound, ui.gameObject);
				}
			}
			catch (Exception e) { Log.LogWarning("Sort failed: " + e.Message); }
		}

		// The slots of whatever storage the mouse is over.
		internal static SlotController[] HoveredSlots()
		{
			SlotController sc = null;
			try
			{
				if (EventSystem.current != null)
				{
					var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
					var hits = new List<RaycastResult>();
					EventSystem.current.RaycastAll(data, hits);
					foreach (RaycastResult r in hits)
					{
						if (r.gameObject == null) continue;
						Slot slot = r.gameObject.GetComponentInParent<Slot>();
						if (slot != null && slot.slotcontrol != null) { sc = slot.slotcontrol; break; }
						ItemData item = r.gameObject.GetComponentInParent<ItemData>();
						if (item != null && item.slotController != null) { sc = item.slotController; break; }
					}
				}
			}
			catch (Exception) { }
			if (sc == null && ItemData.currentHoverItemData != null) sc = ItemData.currentHoverItemData.slotController;
			if (sc == null || sc.Siblings == null) return null;
			if (Inventory.instance != null && sc.Siblings == Inventory.instance.CharacterSlots) return null;   // equipment isn't sorted
			return sc.Siblings;
		}

		private void Update()
		{
			if (MenuOpen || !InventoryVisible) return;
			if (GUIUtility.keyboardControl != 0) return;   // someone is typing in a text box
			try
			{
				if (InventoryKey.Value.IsDown()) SortTarget(false);
				else if (HoveredKey.Value.IsDown())
				{
					SlotController[] slots = HoveredSlots();
					if (slots != null) SortSlots(slots);
				}
			}
			catch (Exception e) { Log.LogWarning("Sort key: " + e.Message); }
		}

		internal static string Label(SortMode m)
		{
			switch (m)
			{
				case SortMode.Category: return "Category (game)";
				case SortMode.Name: return "Name";
				case SortMode.Value: return "Value (each)";
				case SortMode.StackValue: return "Value (stack)";
				case SortMode.Amount: return "Amount";
				case SortMode.Quality: return "Quality";
				case SortMode.Freshness: return "Freshness";
			}
			return m.ToString();
		}
	}

	/// <summary>Replaces the game's sort comparison with the chosen order (the game's order breaks ties).</summary>
	internal static class Order
	{
		private static FieldInfo fStack, fCat, fName, fQuality, fIntegrity, fPrice;

		private static void Fields(object k)
		{
			if (fStack != null) return;
			Type t = k.GetType();
			fStack = AccessTools.Field(t, "stack"); fCat = AccessTools.Field(t, "categories"); fName = AccessTools.Field(t, "name");
			fQuality = AccessTools.Field(t, "quality"); fIntegrity = AccessTools.Field(t, "integrity"); fPrice = AccessTools.Field(t, "price");
		}

		private static string Title(ItemStack s)
		{
			try { return s?.itemReference?.Item?.Title ?? ""; } catch (Exception) { return ""; }
		}

		internal static bool Prefix(object a, object b, ref int __result)
		{
			SortMode mode = SortByPlugin.Mode.Value;
			bool reverse = SortByPlugin.Reverse.Value;
			if (mode == SortMode.Category && !reverse) return true;
			try
			{
				Fields(a);
				var sa = (ItemStack)fStack.GetValue(a); var sb = (ItemStack)fStack.GetValue(b);
				int ca = (int)fCat.GetValue(a), cb = (int)fCat.GetValue(b);
				string na = (string)fName.GetValue(a) ?? "", nb = (string)fName.GetValue(b) ?? "";
				float qa = (float)fQuality.GetValue(a), qb = (float)fQuality.GetValue(b);
				float ia = (float)fIntegrity.GetValue(a), ib = (float)fIntegrity.GetValue(b);
				float pa = (float)fPrice.GetValue(a), pb = (float)fPrice.GetValue(b);
				int aa = sa != null ? sa.itemAmount : 0, ab = sb != null ? sb.itemAmount : 0;

				int r = 0;
				switch (mode)
				{
					case SortMode.Name: r = string.Compare(Title(sa), Title(sb), StringComparison.OrdinalIgnoreCase); break;
					case SortMode.Value: r = pb.CompareTo(pa); break;
					case SortMode.StackValue: r = (pb * ab).CompareTo(pa * aa); break;
					case SortMode.Amount: r = ab.CompareTo(aa); break;
					case SortMode.Quality: r = qb.CompareTo(qa); break;
					case SortMode.Freshness: r = ib.CompareTo(ia); break;
				}
				// The game's own order for everything else.
				if (r == 0 && ca != cb) r = cb.CompareTo(ca);
				if (r == 0) r = string.CompareOrdinal(na, nb);
				if (r == 0 && qa != qb) r = qb.CompareTo(qa);
				if (r == 0 && ia != ib) r = ib.CompareTo(ia);
				if (r == 0 && pa != pb) r = pb.CompareTo(pa);
				if (r == 0) r = ab.CompareTo(aa);
				__result = reverse ? -r : r;
				return false;
			}
			catch (Exception) { return true; }
		}
	}

	/// <summary>The "Sort by" menu that the Sort buttons open.</summary>
	internal class SortMenu : MonoBehaviour
	{
		private static SortMenu instance;
		private bool storage;
		private Vector2 at;
		private int openedFrame;
		private BaseInputModule pausedInput;
		private GUIStyle panel, title, item, itemOn, small;

		internal static void Open(bool storage)
		{
			if (instance == null) return;
			instance.storage = storage;
			Vector3 m = Input.mousePosition;
			instance.at = new Vector2(m.x, Screen.height - m.y);
			instance.openedFrame = Time.frameCount;
			SortByPlugin.MenuOpen = true;
			// Clicks on the menu must not reach the inventory slots underneath. Only the input module is paused:
			// the EventSystem itself stays, because the game's inventory navigation raycasts through it every frame.
			BaseInputModule input = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
			if (input != null && input.enabled) { instance.pausedInput = input; input.enabled = false; }
		}

		private void Close()
		{
			SortByPlugin.MenuOpen = false;
			if (pausedInput != null) { pausedInput.enabled = true; pausedInput = null; }
		}

		private void Awake() { instance = this; }

		private void Update()
		{
			if (SortByPlugin.MenuOpen && !SortByPlugin.InventoryVisible) Close();
		}

		private static Texture2D Tex(Color fill, Color edge)
		{
			var t = new Texture2D(8, 8) { hideFlags = HideFlags.HideAndDontSave };
			for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) t.SetPixel(x, y, x == 0 || y == 0 || x == 7 || y == 7 ? edge : fill);
			t.Apply();
			return t;
		}

		private void Styles()
		{
			if (panel != null && panel.normal.background != null) return;
			Color text = new Color(0.98f, 0.93f, 0.8f), gold = new Color(0.79f, 0.6f, 0.23f);
			panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(6, 6, 6, 6) };
			panel.normal.background = Tex(new Color(0.09f, 0.08f, 0.07f, 0.97f), gold);
			title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 2, 2) };
			title.normal.textColor = gold;
			item = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 5, 5), border = new RectOffset(2, 2, 2, 2) };
			item.normal.background = Tex(new Color(0.15f, 0.13f, 0.11f, 1f), new Color(0.15f, 0.13f, 0.11f, 1f));
			item.hover.background = Tex(new Color(0.42f, 0.29f, 0.08f, 1f), gold);
			item.active.background = Tex(new Color(0.6f, 0.42f, 0.12f, 1f), gold);
			item.normal.textColor = item.hover.textColor = item.active.textColor = text;
			itemOn = new GUIStyle(item) { fontStyle = FontStyle.Bold };
			itemOn.normal.background = Tex(new Color(0.3f, 0.21f, 0.06f, 1f), gold);
			small = new GUIStyle(item) { fontSize = 13 };
		}

		private void OnGUI()
		{
			if (!SortByPlugin.MenuOpen) return;
			Styles();
			var modes = (SortMode[])Enum.GetValues(typeof(SortMode));
			const float w = 210f, rowH = 30f;
			float h = 28f + modes.Length * (rowH + 2f) + 8f + rowH + 12f;
			var box = new Rect(Mathf.Min(at.x, Screen.width - w - 4f), Mathf.Min(at.y + 6f, Screen.height - h - 4f), w, h);
			GUI.depth = -1000;
			Event e = Event.current;

			if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); Close(); return; }
			if (e.type == EventType.MouseDown && !box.Contains(e.mousePosition) && Time.frameCount != openedFrame) { e.Use(); Close(); return; }

			GUI.Box(box, GUIContent.none, panel);
			float y = box.y + 6f;
			GUI.Label(new Rect(box.x + 2f, y, w - 4f, 22f), storage ? "SORT STORAGE BY" : "SORT INVENTORY BY", title);
			y += 26f;
			foreach (SortMode m in modes)
			{
				bool on = SortByPlugin.Mode.Value == m;
				if (GUI.Button(new Rect(box.x + 6f, y, w - 12f, rowH), (on ? "▸ " : "   ") + SortByPlugin.Label(m), on ? itemOn : item))
				{
					SortByPlugin.Mode.Value = m;
					Close();
					SortByPlugin.SortTarget(storage);
					return;
				}
				y += rowH + 2f;
			}
			y += 6f;
			bool rev = SortByPlugin.Reverse.Value;
			if (GUI.Button(new Rect(box.x + 6f, y, w - 12f, rowH), (rev ? "[x] " : "[  ] ") + "Reverse order", small))
				SortByPlugin.Reverse.Value = !rev;
		}
	}

	/// <summary>
	/// The game's sort removes every stack and re-adds each one by scanning the storage from the first slot
	/// (and refreshing slots along the way), which gets slow on a bank with hundreds of slots. This does the
	/// same thing - same sort keys and order, same stack merging through the game's own add - but places each
	/// stack once, in order, and refreshes every slot a single time at the end.
	/// </summary>
	internal static class FastSort
	{
		private static MethodInfo getKey, compare;
		private static FieldInfo keyStack;

		internal static bool Prefix(Inventory __instance, SlotController[] slotToSort)
		{
			SlotController[] slots = slotToSort ?? __instance.Slots;
			int threshold = SortByPlugin.FastThreshold.Value;
			if (threshold <= 0 || slots == null || slots.Length < threshold) return true;
			try { Run(__instance, slots); return false; }
			catch (Exception e) { SortByPlugin.Log.LogWarning("Fast sort failed, using the game's sort: " + e.Message); return true; }
		}

		private static void Run(Inventory inv, SlotController[] slots)
		{
			if (getKey == null)
			{
				getKey = AccessTools.Method(typeof(Inventory), "GetSortKey");
				compare = AccessTools.Method(typeof(Inventory), "CompareSortKeys");
				if (getKey == null || compare == null) throw new Exception("sort methods not found");
			}

			// 1) Take every stack out (without refreshing each slot).
			var stacks = new List<ItemStack>();
			foreach (SlotController sc in slots)
			{
				ItemStack st = sc != null ? sc.itemStack : null;
				if (st == null || st.itemId < 0 || st.itemAmount <= 0) continue;
				if (st.itemReference == null) st.SetItemReference();
				if (st.itemReference == null) continue;
				stacks.Add(st.CopyItemStack());
				st.DragItemFromPanel(st);
			}

			// 2) Sort with the game's keys and comparison (which Sort By's chosen order also applies to).
			List<ItemStack> sorted;
			try
			{
				var keys = new List<object>(stacks.Count);
				foreach (ItemStack st in stacks) keys.Add(getKey.Invoke(inv, new object[] { st }));
				if (keyStack == null && keys.Count > 0) keyStack = AccessTools.Field(keys[0].GetType(), "stack");
				var args = new object[2];
				keys.Sort((a, b) => { args[0] = a; args[1] = b; return (int)compare.Invoke(null, args); });
				sorted = keys.Select(k => (ItemStack)keyStack.GetValue(k)).ToList();
			}
			catch (Exception e)
			{
				SortByPlugin.Log.LogWarning("Sorting keys failed, keeping the old order: " + e.Message);
				sorted = stacks;
			}

			// 3) Put them back in order, topping up the previous stack of the same item first.
			int next = 0;
			SlotController last = null;
			foreach (ItemStack st in sorted)
			{
				if (st.itemAmount == 0 || st.itemId < 0 || st.itemReference == null) continue;
				ItemLiquidData liquid = st.GetMetaOfType<ItemLiquidData>();
				if (liquid != null && ItemLiquidData.IsSameAs(liquid.GetLiquidAmount(st.itemAmount), 0f))
				{
					liquid.liquidItem = null;
					liquid.liquidItemMeta = new object[0];
				}
				Item item = st.itemReference.Item;
				object[] meta = st.Meta;
				int amount = st.itemAmount;
				int rem;
				if (last != null && last.itemStack.itemId == st.itemId && last.itemStack.ownerId == st.ownerId)
				{
					if (last.itemStack.AddItemToPanel(ref item, out rem, ref meta, amount, 0, st.ownerId, false)) amount = Mathf.Max(0, rem);
				}
				while (amount > 0)
				{
					while (next < slots.Length && slots[next] != null && slots[next].itemStack != null
					       && slots[next].itemStack.itemId >= 0 && slots[next].itemStack.itemAmount > 0) next++;
					if (next >= slots.Length) break;
					SlotController target = slots[next];
					if (target == null || target.itemStack == null) { next++; continue; }
					if (target.itemStack.AddItemToPanel(ref item, out rem, ref meta, amount, 0, st.ownerId, false) && rem < amount)
					{
						amount = Mathf.Max(0, rem);
						last = target;
					}
					else next++;   // this slot won't take it (category rules): try the next one
				}
				if (amount > 0)
				{
					// Out of room (shouldn't happen: everything came from these slots). Let the game place the rest.
					var restStack = st.CopyItemStack();
					restStack.itemAmount = amount;
					ItemOperations.AddItems(restStack, slots.First(), notify: false, ignoreCrime: true);
				}
			}

			// 4) Refresh each slot once.
			foreach (SlotController sc in slots) if (sc != null && sc.slot != null) sc.slot.UpdateSlot();
		}
	}

	/// <summary>While the menu is open the game's key bindings report nothing (Esc closes the menu, not the inventory).</summary>
	internal static class Mute
	{
		private static readonly string[] Names = { "IsKeyDown", "IsKeyUp", "IsKey", "GetKeyDown", "GetKeyDownKeyboardOnly", "GetKeyUp", "GetKey", "GetKeyWithHold", "GetAuxiliaryReloadDown" };

		internal static void Patch(Harmony h)
		{
			var prefix = new HarmonyMethod(typeof(Mute), nameof(Prefix));
			foreach (MethodInfo m in AccessTools.GetDeclaredMethods(typeof(InputManager)).Where(m => Names.Contains(m.Name) && m.ReturnType == typeof(bool)))
			{
				try { h.Patch(m, prefix: prefix); } catch (Exception) { }
			}
		}

		private static bool Prefix(ref bool __result)
		{
			if (!SortByPlugin.MenuOpen) return true;
			__result = false;
			return false;
		}
	}
}
