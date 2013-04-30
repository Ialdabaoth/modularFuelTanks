using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

//using PluginUtilities;
using UnityEngine;

using KSP;
using KSP.IO;


namespace FuelModule
{
	public class moduleFuelTanks : PartModule
	{
		
		public class FuelEntryList: List<FuelEntry>, IConfigNode
		{
			public moduleFuelTanks module;
			
			public Part part 
			{
				get {
					if(module == null) return null;
					return module.part;
				}
			}
			
			
			public void Load(ConfigNode node)
			{
				foreach (ConfigNode subNode in node.nodes) {
					if(subNode.name.Equals("TANK")) {
						FuelEntry fuel = Find(t => t.ToString ().Equals(subNode.GetValue ("name")));
						if(!fuel) {
							fuel = new FuelEntry ();
							Add (fuel);
						}
						fuel.Load (subNode);
						fuel.module = module;

						print (subNode);
						if(fuel.tank.HasValue ("maxAmount")) {
							if(part != null && fuel.GetNumber ("maxAmount") > 0) {
								if(part.Resources.Contains (fuel)) {
									part.Resources[fuel].maxAmount = fuel.GetNumber("maxAmount");
									part.Resources[fuel].amount = fuel.GetNumber("amount");
								} else {
									ConfigNode newNode = new ConfigNode("RESOURCE");
									newNode.AddValue ("name", fuel);
									newNode.AddValue ("amount", fuel.tank.GetValue("amount"));
									newNode.AddValue ("maxAmount", fuel.tank.GetValue("amount"));
									part.AddResource (newNode);
									part.Resources[fuel].enabled = true;
								}
							} else if (part != null && part.Resources.Contains (fuel)) {
								part.Resources.list.Remove (part.Resources[fuel]);
							}
							fuel.tank.RemoveValue ("amount");
							fuel.tank.RemoveValue ("maxAmount");
						}
					}
				}
				
			}
			
			public void Save(ConfigNode node)
			{
				foreach (FuelEntry fuel in this) {
					ConfigNode subNode = new ConfigNode("TANK");

					fuel.tank.CopyTo (subNode);
					if(HighLogic.LoadedSceneIsEditor) {
						subNode.AddValue ("amount", fuel.amount);
						subNode.AddValue ("maxAmount", fuel.maxAmount);
					}
					print (subNode);
					node.AddNode (subNode);
				}
			}
			
			
		}
		
		public class FuelEntry: IConfigNode
		{
			
			public void Load(ConfigNode node)
			{
				tank = node;
			}
			
			public void Save(ConfigNode node)
			{
				foreach(ConfigNode.Value v in tank.values)
					node.AddValue (v.name, v.value);
			}
			
			public static FuelEntry Copy(FuelEntry f)
			{
				return f.Copy ();
			}
			
			
			public FuelEntry Copy()
			{
				FuelEntry fuel = new FuelEntry ();
				fuel.module = this.module;
				tank.CopyTo (fuel.tank);
				return fuel;
			}
			
			public override string ToString ()
			{
				return tank.GetValue ("name").Trim ();
			}
			
			public static implicit operator bool(FuelEntry f)
			{
				return (f != null);
			}
			
			public static implicit operator string(FuelEntry f)
			{
				return f.ToString ();
			}
			
			public static implicit operator ConfigNode(FuelEntry f)
			{
				return f.tank;
			}
			
			public string GetValue(string key)
			{
				return tank.GetValue (key);
			}
			
			public Part part
			{
				get {
					if(module == null)
						return null;
					return module.part;
				}
			}
			
			public moduleFuelTanks module;
			
			public float efficiency {
				get {return GetNumber("efficiency");}
			}
			
			public float tank_mass {
				get {return GetNumber("mass");}
			}
			
			public float loss_rate {
				get {return GetNumber("loss_rate");}
			}
			
			public float amount {
				get {
					PartResource resource = part.Resources [GetValue ("name")];
					if (resource == null)
						return 0.0f;
					else
						return (float)resource.amount;
				}
				set {
					float newAmount = value;
					if(newAmount > maxAmount)
						newAmount = maxAmount;
					
					PartResource resource = part.Resources [GetValue ("name")];
					if(resource != null)
						resource.amount = newAmount;
					
				}
			}
			
			public float maxAmount {
				get {
					PartResource resource = part.Resources[GetValue ("name")];
					if(resource == null)
						return 0.0f;
					else
						return (float) resource.maxAmount;
				}
				
				set {
					
					print ("attempting to set " + GetValue ("name") + " to " + value.ToString ());
					
					PartResource resource = part.Resources[GetValue ("name")];
					
					double newMaxAmount = value;
					if (newMaxAmount > (module.availableVolume * efficiency + maxAmount)) {
						newMaxAmount = (module.availableVolume * efficiency + maxAmount) ;
					}
					
					
					
					if (resource != null && newMaxAmount <= 0.0) {
						part.Resources.list.Remove (resource);
					} else if (resource != null) {
						if(resource.amount > newMaxAmount)
							resource.amount = newMaxAmount;
						resource.maxAmount = newMaxAmount;
					} else if(newMaxAmount > 0.0) {
						ConfigNode node = new ConfigNode("RESOURCE");
						node.AddValue ("name", GetValue ("name"));
						node.AddValue ("amount", newMaxAmount);
						node.AddValue ("maxAmount", newMaxAmount);
						print (node.ToString ());
						part.AddResource (node);
						part.Resources[GetValue ("name")].enabled = true;
						
					}
					
					part.mass = module.basemass + module.tank_mass;
					
				}
			}
			public float GetNumber(string key)
			{
				float number = 0.0f;
				if (tank.HasValue (key) && float.TryParse (tank.GetValue (key), out number))
					return number;
				
				return 0.0f;
			}
			string name {
				get {
					if(tank != null && tank.GetValue ("name") != null)
						return tank.GetValue ("name");
					return "";
				}
				set {
					tank.SetValue ("name", value);
				}
			}
			public ConfigNode tank;
			public FuelEntry()
			{
				tank = new ConfigNode("TANK");
				tank.AddValue ("name", "UnknownFuel");
				tank.AddValue ("mass", "0");
				tank.AddValue ("loss_rate", "0");
				tank.AddValue ("efficiency", "1.0");
				
			}
			
		}
		//------------- this is all my non-KSP stuff
		
		public float usedVolume {
			get {
				float v = 0;
				foreach (FuelEntry fuel in fuelList)
				{
					if(part.Resources.Contains (fuel) && fuel.efficiency > 0)
						v += (float) part.Resources[fuel].maxAmount / fuel.efficiency;
				}
				return v;
			}
		}
		
		public float availableVolume {
			get {return volume - usedVolume;}
		}
		
		public float tank_mass {
			get {
				float m = 0.0f;
				foreach (FuelEntry fuel in fuelList)
				{
					if(part.Resources.Contains (fuel) && fuel.efficiency > 0)
						m += (float) part.Resources[fuel].maxAmount * fuel.tank_mass;
				}
				return m;
			}
		}
		
		
		public float GetAmount(string fuel)
		{
			FuelEntry f = fuelList.Find (rn => rn.ToString().Equals(fuel));
			
			if (!f)
				return 0.0f;
			return f.amount;
		}
		
		public float SetAmount(string fuel, float amount)
		{
			if (fuel == null) {
				print ("fuel.SetMaxAmount: fuel is null");
				return 0.0f;
			}
			
			print ("fuel.SetAmount(" + fuel + ", " + amount + ")");
			
			FuelEntry f = fuelList.Find (rn => rn.ToString().Equals(fuel));
			if (!f) {
				print ("fuelList did not find fuel " + fuel);
				return 0.0f;
			}
			
			print ("currently " + f.amount + ", maxAmount = " + f.maxAmount);
			
			f.amount = amount;
			return (float) part.Resources[f].amount;
		}
		
		public float GetMaxAmount(string fuel)
		{
			FuelEntry f = fuelList.Find (rn => rn.ToString().Equals(fuel));
			
			if (!f)
				return 0.0f;
			return f.maxAmount;
		}
		
		public float SetMaxAmount(string fuel, float amount)
		{
			if (fuel == null) {
				print ("fuel.SetMaxAmount: fuel is null");
				return 0.0f;
			}
			
			print ("fuel.SetMaxAmount(" + fuel + ", " + amount + ")");
			
			
			print ("Attempting to set " + fuel + ".maxAmount to " + amount);
			FuelEntry f = fuelList.Find (rn => rn.ToString().Equals(fuel));
			if (!f) {
				print ("fuelList did not find fuel " + fuel);
				return 0.0f;
			}
			
			print ("currently " + f.maxAmount + ", availableVolume = " + availableVolume);
			
			f.maxAmount = amount;
			return (float) part.Resources[f].maxAmount;
		}
		
		
		
		
		//------------------- this is all KSP stuff
		
		[KSPField(isPersistant = true)] 
		public double timestamp = 0.0;
		
		[KSPField(isPersistant = true)] 
		public float basemass = 0.0f;
		
		[KSPField(isPersistant = true)] 
		public float volume = 0.0f;

		public FuelEntryList fuelList;
		
		public override void OnLoad(ConfigNode node)
		{
			print ("moduleFuelTanks.onLoad");
			fuelList.module = this;

			if(basemass == 0 && part != null)
				basemass = part.mass;
			print(node.ToString ());

			fuelList.Load (node);

			print ("moduleFuelTanks.onLoad loaded " + fuelList.Count + " fuels");
			
			print ("moduleFuelTanks loaded. ");
		}
		
		public override void OnAwake()
		{
			print ("moduleFuelTanks.onAwake");
			
			if (fuelList == null) {
				print ("moduleFuelTanks.onAwake - fuelList is null");
				
				fuelList = new FuelEntryList ();
			}
			
			print ("moduleFuelTanks awake.");
			
		}
		
		public override void OnStart (StartState state)
		{
			fuelList.module = this;

			if(basemass == 0 && part != null)
				basemass = part.mass;
			
			switch(state) {
			case StartState.Editor:
				// when we get called from the editor, the fuelList won't be populated
				// because OnLoad() was never called. This is a hack to fix that.
				print ("moduleFuelTanks.OnStart with empty fuelList.");
				ProtoPartModuleSnapshot proto = part.protoPartRef.protoModules.Find (m => m.moduleName.Equals ("moduleFuelTanks"));
				if(proto != null) {
					print ("found prototype module");
					ConfigNode node = new ConfigNode("MODULE");
					proto.moduleRef.Save (node); //PluginUtility.Unpack (storeFuelList);

					print ("loading from converted node:" + node.ToString());
					fuelList.Load (node);
				}
				break;
			default:
				break;
			}
		}
		
		
		public override void OnSave (ConfigNode node)
		{
			fuelList.Save (node);
		}
		
		public override void OnUpdate ()
		{

			double delta_t = Planetarium.GetUniversalTime() - timestamp;
			foreach (FuelEntry fuel in fuelList) {
				if(fuel.loss_rate > 0) {

					PartResource resource = part.Resources.list.Find (r => r.name.Equals (fuel.ToString ()));
					if(resource != null && resource.amount > 0) {
						double loss = resource.maxAmount * fuel.loss_rate * part.temperature * delta_t / 300.0; // loss_rate is calibrated to 300 degrees.
						print ("fuel loss for RESOURCE {" + fuel + "}:" + loss);
						if(loss > resource.amount)
							resource.amount = 0;
						else
							resource.amount -= loss;
					}
				}
			}
			timestamp = Planetarium.GetUniversalTime ();

		}
		
		public override string GetInfo ()
		{
			string info = "Modular Fuel Tank: \n"
				+ "  Max Volume: " + (Math.Floor (volume * 1000) / 1000.0).ToString () + "\n" 
					+ "  Tank can hold:";
			foreach(FuelEntry fuel in fuelList)
			{
				info += "\n   " + fuel;
				if(fuel.efficiency < 1.0)
					info += " (+" + Math.Floor(100 - 100 * fuel.efficiency) + "% cryo)";
				if(fuel.tank_mass > 0.0)
					info += ", tank mass: " + fuel.tank_mass + " x volume";
				
			}
			return info + "\n";
		}
	}
	
	public class fuelManager : MonoBehaviour
	{
		public static GameObject GameObjectInstance;
		
		public void Awake()
		{
			DontDestroyOnLoad(this);
		}
		
		public void OnGUI()
		{
			EditorLogic editor = EditorLogic.fetch;
			if (!HighLogic.LoadedSceneIsEditor || !editor) {
				return;
			}
			
			if (editor.editorScreen == EditorLogic.EditorScreen.Actions) {
				if (EditorActionGroups.Instance.GetSelectedParts ().Find (p => p.Modules.Contains ("moduleFuelTanks")))
					screenRect = GUILayout.Window (1, screenRect, fuelManagerGUI, "Fuel Tanks");
				else
					textFields.Clear ();
				
				if(screenRect.width < 350)
					screenRect.width = 350;
				if(screenRect.height < 128)
					screenRect.height = 128;
			} else {
				textFields.Clear ();
			}
		}
		
		Rect screenRect = new Rect(0, 400, 350, 128);
		private List<string> textFields = new List<string>();
		
		private void fuelManagerGUI(int WindowID)
		{
			Part part = EditorActionGroups.Instance.GetSelectedParts ().Find (p => p.Modules.Contains ("moduleFuelTanks"));
			if (!part) {
				GUILayout.BeginHorizontal();
				GUILayout.Label("No part found.");
				GUILayout.EndHorizontal ();
				return;
			}
			
			moduleFuelTanks fuel = (moduleFuelTanks) part.Modules["moduleFuelTanks"];
			if (!fuel) {
				GUILayout.BeginHorizontal();
				GUILayout.Label ("Part has no fuel Module.");
				GUILayout.EndHorizontal ();
				return;
			}
			
			
			GUILayout.BeginHorizontal();
			GUILayout.Label ("Current mass: " + Math.Round(1000*(part.mass + part.GetResourceMass()))/1000.0 + " Ton(s)");
			GUILayout.Label ("Dry mass: " + Math.Round(1000*part.mass)/1000.0 + " Ton(s)");
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal();
			GUILayout.Label ("Available volume: " + fuel.availableVolume + " / " + fuel.volume + " Liter(s)");
			GUILayout.EndHorizontal ();
			
			int text_field = 0;
			
			foreach (moduleFuelTanks.FuelEntry tank in fuel.fuelList) {
				GUILayout.BeginHorizontal();
				
				int amountField = text_field;
				text_field++;
				if(textFields.Count < text_field) {
					textFields.Add ("");
					textFields[amountField] = tank.amount.ToString();
				}
				int maxAmountField = text_field;
				text_field++;
				if(textFields.Count < text_field) {
					textFields.Add ("");
					textFields[maxAmountField] = tank.maxAmount.ToString();
				}
				if(part.Resources.Contains(tank) && part.Resources[tank].maxAmount > 0.0) {
					
					GUILayout.Label(tank, GUILayout.Width (100));
					
					float amount = (float) part.Resources[tank].amount;
					float maxAmount = (float) part.Resources[tank].maxAmount;
					
					GUIStyle color = new GUIStyle(GUI.skin.textField);
					if(textFields[amountField].Equals (amount.ToString ()))
						color.normal.textColor = Color.white;
					else
						color.normal.textColor = Color.yellow;
					textFields[amountField] = GUILayout.TextField(textFields[amountField], color, GUILayout.Width (80));
					
					GUILayout.Label("/", GUILayout.Width (10));
					
					
					color = new GUIStyle(GUI.skin.textField);
					if(textFields[maxAmountField].Equals (maxAmount.ToString ()))
						color.normal.textColor = Color.white;
					else
						color.normal.textColor = Color.yellow;
					textFields[maxAmountField] = GUILayout.TextField(textFields[maxAmountField], color, GUILayout.Width (80));
					

					if(GUILayout.Button ("Update", GUILayout.Width (60))) {
						
						print ("Clicked Update {" + tank + "}");
						float newMaxAmount = maxAmount;
						if(!float.TryParse (textFields[maxAmountField], out newMaxAmount))
							newMaxAmount = maxAmount;
						
						float newAmount = amount;
						if(!float.TryParse(textFields[amountField], out newAmount))
							newAmount = amount;
						
						print (" current amount = " + amount + ", new value = " + newAmount);
						print (" current maxAmount = " + maxAmount + ", new value = " + newMaxAmount);
						
						if(newMaxAmount != maxAmount) {
							print ("Setting " + tank + " maxAmount to " + newMaxAmount);
							fuel.SetMaxAmount(tank, newMaxAmount);
							print ("set to " + tank.maxAmount);
							
						}
						
						if(newAmount != amount) {
							print ("Setting " + tank + " amount to " + newMaxAmount);
							fuel.SetAmount (tank, newAmount);
							print ("set to " + tank.amount);
						}
						
						textFields[amountField] = tank.amount.ToString();
						textFields[maxAmountField] = tank.maxAmount.ToString();
					}
					
					GUILayout.EndHorizontal ();
					GUILayout.BeginHorizontal ();
					if(GUILayout.Button ("Remove", GUILayout.Width (60))) {
						print ("Clicked Remove " + tank);
						
						textFields.Clear ();
						fuel.SetMaxAmount (tank, 0);
					}
					
					GUILayout.Label ("  tank mass: " + Math.Round (1000 * amount * (part.Resources[tank].info.density) + tank.tank_mass)/1000.0, GUILayout.Width(150));
					GUILayout.Label ("tank volume: " + (Math.Round (100 * maxAmount / tank.efficiency)/100).ToString (), GUILayout.Width (180));
					
				} else if(fuel.availableVolume > 0) {
					GUILayout.Label(tank, GUILayout.Width (100));
					
					if(GUILayout.Button("Add", GUILayout.Width (60))) {
						print ("Clicked Add " + tank);
						textFields.Clear ();
						fuel.SetMaxAmount (tank, fuel.availableVolume);
					}
					if(tank.efficiency < 1.0 && tank.tank_mass > 0.0) {
						GUILayout.Label(" (cryo: " + (Math.Floor (1000 - 1000 * tank.efficiency) / 10.0f) + "%, " 
						                + (Math.Floor (1000*tank.tank_mass)/1000.0) + " Tons/volume )", GUILayout.Width (200));
					} else if(tank.efficiency < 1.0) {
						GUILayout.Label(" (cryo: " + (Math.Floor (1000 - 1000 * tank.efficiency) / 10.0f) + "%)", GUILayout.Width (200));
					} else if(tank.tank_mass > 0.0) {
						GUILayout.Label(" (pumps: " + (Math.Floor (1000*tank.tank_mass)/1000.0) + " Tons/volume )", GUILayout.Width (200));
						
					}
				} else {
					GUILayout.Label ("no room to add a " + tank + " tank.");
					
				}
				GUILayout.EndHorizontal ();
				
			}
			
			GUILayout.BeginHorizontal();
			if(GUILayout.Button ("Remove All Tanks")) {
				textFields.Clear ();
				foreach(moduleFuelTanks.FuelEntry tank in fuel.fuelList) {
					tank.amount = 0;
					tank.maxAmount = 0;
				}
			}	
			GUILayout.EndHorizontal();
			if(part.symmetryCounterparts.Count > 0) {
				GUILayout.BeginHorizontal();
				if(GUILayout.Button ("Copy to Symmetry Group")) {
					foreach(Part sPart in part.symmetryCounterparts)
					{
						moduleFuelTanks pFuel = (moduleFuelTanks) sPart.Modules["moduleFuelTanks"];
						if(pFuel)
						{
							foreach(string fuelName in fuel.fuelList)
							{
								pFuel.SetAmount (fuelName, fuel.GetAmount(fuelName));
								pFuel.SetMaxAmount (fuelName, fuel.GetMaxAmount(fuelName) );
							}
						}
					}
				}
				GUILayout.EndHorizontal();
			}			
			
			if(GetEnginesFedBy(part).Count > 0)
			{
				List<string> check_dupes = new List<string>();
				
				GUILayout.BeginHorizontal();
				GUILayout.Label ("Configure for Engines:");
				GUILayout.EndHorizontal();
				foreach(Part engine in GetEnginesFedBy(part))
				{
					if(!check_dupes.Contains (engine.partInfo.title)) {
						check_dupes.Add (engine.partInfo.title);
						double ratio_factor = 0.0;
						double inefficiency = 0.0;
						ModuleEngines thruster = (ModuleEngines) engine.Modules["ModuleEngines"];
						
						// tank math:
						// inefficiency = sum[(1 - efficiency) * ratio]
						// fluid_v = v * (1 - inefficiency)
						// f = fluid_v * ratio
						
						
						foreach(ModuleEngines.Propellant tfuel in thruster.propellants)
						{
							if(PartResourceLibrary.Instance.GetDefinition(tfuel.name) == null) {
								print ("Unknown RESOURCE {" + tfuel.name + "}");
								ratio_factor = 0.0;
								break;
							} else if(PartResourceLibrary.Instance.GetDefinition(tfuel.name).resourceTransferMode == ResourceTransferMode.NONE) {
								//ignore this propellant, since it isn't serviced by fuel tanks
							} else {
								moduleFuelTanks.FuelEntry tank = fuel.fuelList.Find (f => f.ToString ().Equals (tfuel.name ));
								if(tank) {
									inefficiency += (1 - tank.efficiency) * tfuel.ratio;
									ratio_factor += tfuel.ratio;
								} else {
									ratio_factor = 0.0;
									break;
								}
							}
						}
						if(ratio_factor > 0.0)
						{
							string label = "";
							foreach(ModuleEngines.Propellant tfuel in thruster.propellants)
							{
								if(PartResourceLibrary.Instance.GetDefinition(tfuel.name).resourceTransferMode != ResourceTransferMode.NONE) {
									if(label.Length > 0)
										label += " / ";
									label += Math.Round (100 * tfuel.ratio / ratio_factor).ToString () + "% " + tfuel.name;	
								}
								
							}
							label = "Configure to " + label + "\n for " + engine.partInfo.title + "";
							GUILayout.BeginHorizontal();
							if(GUILayout.Button (label)) {
								textFields.Clear ();
								
								foreach(moduleFuelTanks.FuelEntry tank in fuel.fuelList) {
									tank.amount = 0;
									tank.maxAmount = 0;
								}
								
								float total_volume = (float) (fuel.availableVolume * (1 - inefficiency / ratio_factor));
								foreach(ModuleEngines.Propellant tfuel in thruster.propellants)
								{
									if(PartResourceLibrary.Instance.GetDefinition(tfuel.name).resourceTransferMode != ResourceTransferMode.NONE) {
										fuel.SetMaxAmount (tfuel.name, (float) (total_volume * tfuel.ratio / ratio_factor));
										fuel.SetAmount (tfuel.name, (float) (total_volume * tfuel.ratio / ratio_factor));
									}
								}
							}
							GUILayout.EndHorizontal ();
						}
					}
				}
			}
		}
		
		public List<Part> GetEnginesFedBy(Part part)
		{
			
			return new List<Part>(part.FindChildParts<Part> (true)).FindAll (p => p.Modules.Contains ("ModuleEngines"));
		}
		
	}
	
	public class fuelManagerInit : KSP.Testing.UnitTest
	{
		public fuelManagerInit()
		{
			var gameobject = new GameObject("fuelManager", typeof(fuelManager));
			UnityEngine.Object.DontDestroyOnLoad(gameobject);
		}
	}
}