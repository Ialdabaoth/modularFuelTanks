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
	public class ModuleFuelTanks : PartModule
	{

		// A FuelTank is a single TANK {} entry from the part.cfg file.
		// it defines four properties:
		// name = the name of the resource that can be stored
		// efficiency = how much of the tank is devoted to that resource (vs. how much is wasted in cryogenics or pumps)
		// mass = how much the part's mass is increased per volume unit of tank installed for this resource type
		// loss_rate = how quickly this resource type bleeds out of the tank

		[Serializable]
		public class FuelTank: IConfigNode
		{
			//------------------- fields
			public string name;
			public float efficiency = 1.0f;
			public float mass = 0.0f;
			public double loss_rate = 0.0f;

			[System.NonSerialized]
			public ModuleFuelTanks module;

			//------------------- virtual properties
			public int id
			{
				get {
					if(name == null)
						return 0;
					return name.GetHashCode ();
				}
			}
			
			public Part part
			{
				get {
					if(module == null)
						return null;
					return module.part;
				}
			}
			
			public PartResource resource
			{
				get {
					if (part == null)
						return null;
					return part.Resources [name];
				}
			}
			

			public double amount {
				get {
					if (resource == null)
						return 0.0f;
					else
						return resource.amount;
				}
				set {
					double newAmount = value;
					if(newAmount > maxAmount)
						newAmount = maxAmount;
					
					if(resource != null)
						resource.amount = newAmount;
					
				}
			}

			public double maxAmount {
				get {
					if(resource == null)
						return 0.0f;
					else
						return (float) resource.maxAmount;
				}
				
				set {
					
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
						node.AddValue ("name", name);
						node.AddValue ("amount", newMaxAmount);
						node.AddValue ("maxAmount", newMaxAmount);
						print (node.ToString ());
						part.AddResource (node);
						resource.enabled = true;
						
					}
					
					part.mass = module.basemass + module.tank_mass;
					
				}
			}

			//------------------- implicit type conversions
			public static implicit operator bool(FuelTank f)
			{
				return (f != null);
			}
			
			public static implicit operator string(FuelTank f)
			{
				return f.name;
			}

			public override string ToString ()
			{
				if (name == null)
					return "NULL";
				return name;
			}
			
			//------------------- IConfigNode implementation
			public void Load(ConfigNode node)
			{
				if (node.name.Equals ("TANK") && node.HasValue ("name")) {
					name = node.GetValue ("name");
					if(node.HasValue ("efficiency"))
						float.TryParse (node.GetValue("efficiency"), out efficiency);
					if(node.HasValue ("loss_rate"))
						double.TryParse (node.GetValue("loss_rate"), out loss_rate);
					if(node.HasValue ("mass"))
						float.TryParse (node.GetValue("mass"), out mass);
				};
			}
			
			public void Save(ConfigNode node)
			{
				if (name != null) {
					node.AddValue ("name", name);
					node.AddValue ("efficiency", efficiency);
					node.AddValue ("mass", mass);
					node.AddValue ("loss_rate", loss_rate);
				}
			}

			//------------------- Constructor
			public FuelTank()
			{
			}
			
		}


		//------------- this is all my non-KSP stuff
		
		public float usedVolume {
			get {
				float v = 0;
				foreach (FuelTank fuel in fuelList)
				{
					if(fuel.maxAmount > 0 && fuel.efficiency > 0)
						v += (float) fuel.maxAmount / fuel.efficiency;
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
				foreach (FuelTank fuel in fuelList)
				{
					if(fuel.maxAmount > 0 && fuel.efficiency > 0)
						m += (float) fuel.maxAmount * fuel.mass;
				}
				return m;
			}
		}

		//------------------- this is all KSP stuff
		
		[KSPField(isPersistant = true)] 
		public double timestamp = 0.0;
		
		[KSPField(isPersistant = true)] 
		public float basemass = 0.0f;
		
		[KSPField(isPersistant = true)] 
		public float volume = 0.0f;

		public List<FuelTank> fuelList;
		
		public override void OnLoad(ConfigNode node)
		{
			print ("========OnLoad called. Node is:=======")
				print (node.ToString ());
			foreach (ConfigNode tankNode in node.nodes) {
				if(tankNode.name.Equals ("TANK")) {
					print ("loading FuelTank from node " + node.ToString ());
					FuelTank tank = new FuelTank();
					tank.module = this;
					tank.Load (tankNode);
					fuelList.Add (tank);
				}
			}

			print ("ModuleFuelTanks.onLoad loaded " + fuelList.Count + " fuels");
			
			print ("ModuleFuelTanks loaded. ");
		}

		
		
		public override void OnSave (ConfigNode node)
		{
			foreach (FuelTank tank in fuelList) {
				ConfigNode subNode = new ConfigNode("TANK");
				tank.Save (subNode);
				node.AddNode (subNode);
				tank.module = this;
			}
		}

		public override void OnAwake()
		{
			if (fuelList == null) {
				fuelList = new List<ModuleFuelTanks.FuelTank> ();
			}
		}

		public override void OnStart (StartState state)
		{
			//fuelList.FuelTankle = this;
			print ("========OnStart called. State is " + state.ToString () + "=======")

			if (basemass == 0 && part != null)
				basemass = part.mass;
			if(fuelList == null) {
				print ("[ERROR]: ModuleFuelTanks.OnStart with null fuelList.");
			} else if (fuelList.Count == 0) {
				print ("ModuleFuelTanks.OnStart with empty fuelList.");
			} else {
				foreach(FuelTank tank in fuelList)
					tank.module = this;
			}

			switch(state) {
			case StartState.Editor:
				// when we get called from the editor, the fuelList won't be populated
				// because OnLoad() was never called. This is a hack to fix that.

				//FIXME: Hack caused crashing and memory leaks.

				break;
			default:
				break;
			}
		}


		public override void OnUpdate ()
		{
			if (HighLogic.LoadedSceneIsEditor) {
				if(EditorActionGroups.Instance.GetSelectedParts().Contains (part) ) {
					print ("selected.");
				}

			} else {
				double delta_t = Planetarium.GetUniversalTime () - timestamp;
				foreach (FuelTank tank in fuelList) {
					if (tank.amount > 0 && tank.loss_rate > 0) {
						double loss = tank.maxAmount * tank.loss_rate * part.temperature * delta_t / 300.0; // loss_rate is calibrated to 300 degrees.
						print ("fuel loss for RESOURCE {" + tank + "}:" + loss);
						if (loss > tank.amount)
							tank.amount = 0;
						else
							tank.amount -= loss;
					}
				}
				timestamp = Planetarium.GetUniversalTime ();
			}
		}


		public override string GetInfo ()
		{
			string info = "Modular Fuel Tank: \n"
				+ "  Max Volume: " + (Math.Floor (volume * 1000) / 1000.0).ToString () + "\n" 
					+ "  Tank can hold:";
			foreach(FuelTank tank in fuelList)
			{
				info += "\n   " + tank;
				if(tank.efficiency < 1.0)
					info += " (+" + Math.Floor(100 - 100 * tank.efficiency) + "% cryo)";
				if(tank.mass > 0.0)
					info += ", tank mass: " + tank.mass + " x volume";
				
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
				if (EditorActionGroups.Instance.GetSelectedParts ().Find (p => p.Modules.Contains ("ModuleFuelTanks")))
					screenRect = GUILayout.Window (1, new Rect(0, 400, 350, 128), fuelManagerGUI, "Fuel Tanks");
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
			Part part = EditorActionGroups.Instance.GetSelectedParts ().Find (p => p.Modules.Contains ("ModuleFuelTanks"));
			if (!part) {
				GUILayout.BeginHorizontal();
				GUILayout.Label("No part found.");
				GUILayout.EndHorizontal ();
				return;
			}
			
			ModuleFuelTanks fuel = (ModuleFuelTanks) part.Modules["ModuleFuelTanks"];
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

			if (fuel.fuelList.Count == 0) {

				GUILayout.BeginHorizontal();
				GUILayout.Label ("This fuel tank cannot resources.");
				GUILayout.EndHorizontal ();
				return;
			}

			GUILayout.BeginHorizontal();
			GUILayout.Label ("Available volume: " + fuel.availableVolume + " / " + fuel.volume);
			GUILayout.EndHorizontal ();
			
			int text_field = 0;
			
			foreach (ModuleFuelTanks.FuelTank tank in fuel.fuelList) {
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
							tank.maxAmount = newMaxAmount;
							print ("set to " + tank.maxAmount);
							
						}
						
						if(newAmount != amount) {
							print ("Setting " + tank + " amount to " + newAmount);
							tank.amount = newAmount;
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
						tank.maxAmount = 0;
					}
					
					GUILayout.Label ("  tank mass: " + Math.Round (1000 * amount * (part.Resources[tank].info.density) + tank.mass)/1000.0, GUILayout.Width(150));
					GUILayout.Label ("tank volume: " + (Math.Round (100 * maxAmount / tank.efficiency)/100).ToString (), GUILayout.Width (180));
					
				} else if(fuel.availableVolume > 0) {
					GUILayout.Label(tank, GUILayout.Width (100));
					
					if(GUILayout.Button("Add", GUILayout.Width (60))) {
						print ("Clicked Add " + tank);
						textFields.Clear ();
						tank.maxAmount = fuel.availableVolume;
					}
					if(tank.efficiency < 1.0 && tank.mass > 0.0) {
						GUILayout.Label(" (cryo: " + (Math.Floor (1000 - 1000 * tank.efficiency) / 10.0f) + "%, " 
						                + (Math.Floor (1000*tank.mass)/1000.0) + " Tons/volume )", GUILayout.Width (200));
					} else if(tank.efficiency < 1.0) {
						GUILayout.Label(" (cryo: " + (Math.Floor (1000 - 1000 * tank.efficiency) / 10.0f) + "%)", GUILayout.Width (200));
					} else if(tank.mass > 0.0) {
						GUILayout.Label(" (pumps: " + (Math.Floor (1000*tank.mass)/1000.0) + " Tons/volume )", GUILayout.Width (200));
						
					}
				} else {
					GUILayout.Label ("no room to add a " + tank + " tank.");
					
				}
				GUILayout.EndHorizontal ();
				
			}
			
			GUILayout.BeginHorizontal();
			if(GUILayout.Button ("Remove All Tanks")) {
				textFields.Clear ();
				foreach(ModuleFuelTanks.FuelTank tank in fuel.fuelList) {
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
						ModuleFuelTanks pFuel = (ModuleFuelTanks) sPart.Modules["ModuleFuelTanks"];
						if(pFuel)
						{
							foreach(ModuleFuelTanks.FuelTank tank in fuel.fuelList)
							{
								ModuleFuelTanks.FuelTank pTank = pFuel.fuelList.Find (t => t.name.Equals (tank.name));
								if(pTank) {
									pTank.maxAmount = tank.maxAmount;
									pTank.amount = tank.amount;
								}
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
								ModuleFuelTanks.FuelTank tank = fuel.fuelList.Find (f => f.ToString ().Equals (tfuel.name ));
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
								
								foreach(ModuleFuelTanks.FuelTank tank in fuel.fuelList) {
									tank.amount = 0;
									tank.maxAmount = 0;
								}
								
								float total_volume = (float) (fuel.availableVolume * (1 - inefficiency / ratio_factor));
								foreach(ModuleEngines.Propellant tfuel in thruster.propellants)
								{
									if(PartResourceLibrary.Instance.GetDefinition(tfuel.name).resourceTransferMode != ResourceTransferMode.NONE) {
										ModuleFuelTanks.FuelTank tank = fuel.fuelList.Find (t => t.name.Equals (tfuel.name));
										if(tank) {
											tank.maxAmount = total_volume * tfuel.ratio / ratio_factor;
											tank.amount = total_volume * tfuel.ratio / ratio_factor;
										}
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