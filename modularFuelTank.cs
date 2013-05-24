using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using UnityEngine;

using KSP;


namespace FuelModule
{

	public class RefuelingPump: PartModule
	{
		[KSPField(isPersistant = true)] 
		double timestamp = 0.0;

		[KSPField(isPersistant = true)] 
		double pump_rate = 100.0; // 625 liters/second seems reasonable.

		public override string GetInfo ()
		{
			return "\nPump rate: " + pump_rate + "/s";
		}

		public override void OnUpdate ()
		{
			if (HighLogic.LoadedSceneIsEditor) {

			} else if (timestamp > 0 && part.parent != null && part.parent.Modules.Contains ("ModuleFuelTanks")) {
				// We're connected to a fuel tank, so let's top off any depleting resources

				// first, get the time since the last OnUpdate()
				double delta_t = Planetarium.GetUniversalTime () - timestamp;

				// now, let's look at what we're connected to.
				ModuleFuelTanks m = (ModuleFuelTanks) part.parent.Modules["ModuleFuelTanks"];

				// look through all tanks inside this part
				foreach(ModuleFuelTanks.FuelTank tank in m.fuelList) {
					// if a tank isn't full, start filling it.
					if(tank.amount < tank.maxAmount) {
						double top_off = delta_t * pump_rate;
						if(tank.amount + top_off < tank.maxAmount)
							tank.amount += top_off;
						else
							tank.amount = tank.maxAmount;
					}

				}
				// save the time so we can tell how much time has passed on the next update, even in Warp
				timestamp = Planetarium.GetUniversalTime ();
			} else {
				// save the time so we can tell how much time has passed on the next update, even in Warp
				timestamp = Planetarium.GetUniversalTime ();
			}
		}
	}
	public class ModuleEngineConfigs : PartModule
	{
		[KSPField(isPersistant = true)] 
		public string configuration = "";
		[KSPField(isPersistant = true)] 
		public bool modded = false;

		public List<ConfigNode> configs;
		public ConfigNode config;

		public override void OnAwake ()
		{
			if(configs == null)
				configs = new List<ConfigNode>();
		}

		public override string GetInfo ()
		{
			string info = "\nAlternate configurations:\n";
			foreach (ConfigNode config in configs) {
				if(!config.GetValue ("name").Equals (configuration)) 
					info += "   " + config.GetValue ("name") + "\n";
				

			}
			return info;
		}

		public void OnGUI()
		{
			EditorLogic editor = EditorLogic.fetch;
			if (!HighLogic.LoadedSceneIsEditor || !editor || editor.editorScreen != EditorLogic.EditorScreen.Actions) {
				return;
			}
			
			if (EditorActionGroups.Instance.GetSelectedParts ().Contains (part)) {
				Rect screenRect = new Rect(0, 365, 430, (Screen.height - 365));
				GUILayout.Window (part.name.GetHashCode (), screenRect, engineManagerGUI, "Configure " + part.partInfo.title);
			}
		}

		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			if (configs == null)
				configs = new List<ConfigNode> ();
			else
				configs.Clear ();

			foreach (ConfigNode subNode in node.GetNodes ("CONFIG")) {
				ConfigNode newNode = new ConfigNode("CONFIG");
				subNode.CopyTo (newNode);
				configs.Add (newNode);
				if(newNode.GetValue ("name").Equals (configuration)) {
					config = new ConfigNode("MODULE");
					subNode.CopyTo (config);
					config.name = "MODULE";
					config.SetValue ("name", "ModuleEngines");
				}
			}
		}

		public override void OnSave (ConfigNode node)
		{
			foreach (ConfigNode subNode in configs) {
				node.AddNode (subNode);
			}
			base.OnSave (node);
		}

		public override void OnStart (StartState state)
		{
			ConfigNode config = null;
			if(modded)
				config = configs.Find (c => c.GetValue ("name").Equals (configuration));

			if (config != null) {
				ModuleEngines thruster = (ModuleEngines) part.Modules["ModuleEngines"];
				ConfigNode newConfig = new ConfigNode ("MODULE");
				config.CopyTo (newConfig);
				newConfig.name = "MODULE";
				newConfig.SetValue ("name", "ModuleEngines");
#if DEBUG
				print ("replacing ModuleEngines with:");
				print (newConfig.ToString ());
#endif
				thruster.Load (newConfig);
			}
		}

		private void engineManagerGUI(int WindowID)
		{
			foreach (ConfigNode node in configs) {
				GUILayout.BeginHorizontal();
				if(node.GetValue ("name").Equals (configuration)) {
					GUILayout.Label ("Current configuration: " + configuration);
				} else if(GUILayout.Button ("Configure to " + node.GetValue ("name"))) {
					configuration = node.GetValue ("name");
					modded = true;
					config = new ConfigNode("MODULE");
					node.CopyTo (config);
					config.name = "MODULE";
					config.SetValue ("name", "ModuleEngines");
					#if DEBUG
					print ("replacing ModuleEngines with:");
					print (engine.config.ToString ());
					#endif
					part.Modules["ModuleEngines"].Load (config);
					UpdateSymmetryCounterparts();
				}
				GUILayout.EndHorizontal ();
			}
		}

		public int UpdateSymmetryCounterparts()
		{
			int i = 0;
			foreach (Part sPart in part.symmetryCounterparts) {
				ModuleEngineConfigs engine = (ModuleEngineConfigs)sPart.Modules ["ModuleEngineConfigs"];
				if (engine) {
					i++;
					engine.configuration = configuration;
					engine.config = config;
					engine.modded = true;
					ModuleEngines thruster = (ModuleEngines)sPart.Modules ["ModuleEngines"];
					thruster.Load (engine.config);
				}
			}
			return i;
		}
	}


	public class ModuleFuelTanks : PartModule
	{
		public static float RoundTo4SigFigs(double f)
		{
			if(f >= 1000)
				return (float) Math.Floor (f);
			else if(f >= 100)
				return (float) Math.Floor (f * 10.0) / 10;
			else if(f >= 10)
				return (float) Math.Floor (f * 100.0) / 100;
			else
				return (float) Math.Floor (f * 1000.0) / 1000;
		}

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
			public string name = "UnknownFuel";
			public string note = "";
			public float efficiency = 1.0f;
			public float mass = 0.0f;
			public double loss_rate = 0.0;
			public float temperature = 300.0f;

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
						return 0.0;
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
						return RoundTo4SigFigs(resource.maxAmount);
				}
				
				set {
					
					double newMaxAmount = RoundTo4SigFigs(value);
					if (newMaxAmount > RoundTo4SigFigs(module.availableVolume * efficiency + maxAmount)) {
						newMaxAmount = RoundTo4SigFigs(module.availableVolume * efficiency + maxAmount) ;
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
#if DEBUG
						print (node.ToString ());
#endif
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
					if(node.HasValue ("note"))
						note = node.GetValue ("note");
					if(node.HasValue ("efficiency"))
						float.TryParse (node.GetValue("efficiency"), out efficiency);
					if(node.HasValue ("temperature"))
						float.TryParse (node.GetValue("temperature"), out temperature);
					if(node.HasValue ("loss_rate"))
						double.TryParse (node.GetValue("loss_rate"), out loss_rate);
					if(node.HasValue ("mass"))
						float.TryParse (node.GetValue("mass"), out mass);
					if(node.HasValue ("maxAmount")) {
						double v;
						double.TryParse(node.GetValue ("maxAmount").Replace ("volume", "").Replace ("*", "").Trim (), out v);
						if(node.GetValue ("maxAmount").Contains ("*") && node.GetValue ("maxAmount").Contains ("volume"))
							v = RoundTo4SigFigs(v * module.volume);
						maxAmount = v;
						if(node.HasValue ("amount")) {
							double.TryParse(node.GetValue ("amount").Replace ("volume", "").Replace ("*", "").Trim (), out v);
							if(node.GetValue ("amount").Contains ("*") && node.GetValue ("amount").Contains ("volume"))
								v = RoundTo4SigFigs(v * module.volume);
							amount = v;
						} else {
							amount = 0.0;
						}

					}
				};
			}
			
			public void Save(ConfigNode node)
			{
				if (name != null) {
					node.AddValue ("name", name);
					node.AddValue ("efficiency", efficiency);
					node.AddValue ("mass", mass);
					node.AddValue ("temperature", temperature);
					node.AddValue ("loss_rate", loss_rate);
					//if(HighLogic.LoadedSceneIsEditor) {
					// You would think we only want to do this in the editor, but 
					// as it turns out, KSP is terrible about consistently setting
					// up resources between the editor and the launchpad.
						node.AddValue ("amount", amount);
						node.AddValue ("maxAmount", amount);
					//}
					node.AddValue ("note", note);

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
				double v = 0;
				foreach (FuelTank fuel in fuelList)
				{
					if(fuel.maxAmount > 0 && fuel.efficiency > 0)
						v += fuel.maxAmount / fuel.efficiency;
				}
				return RoundTo4SigFigs(v);
			}
		}
		
		public float availableVolume {
			get {
				return RoundTo4SigFigs(volume - usedVolume);
			}
		}
		
		public float tank_mass {
			get {
				float m = 0.0f;
				foreach (FuelTank fuel in fuelList)
				{
					if(fuel.maxAmount > 0 && fuel.efficiency > 0)
						m += (float) fuel.maxAmount * fuel.mass;
				}
				return RoundTo4SigFigs(m);
			}
		}

		//------------------- this is all KSP stuff
		
		[KSPField(isPersistant = true)] 
		public double timestamp = 0.0;
		
		[KSPField(isPersistant = true)] 
		public float radius = 0.0f;

		[KSPField(isPersistant = true)] 
		public float rscale = 1.0f;

		[KSPField(isPersistant = true)] 
		public float length = 1.0f;

		[KSPField(isPersistant = true)] 
		public float basemass = 0.0f;
		
		[KSPField(isPersistant = true)] 
		public float volume = 0.0f;

		public List<FuelTank> fuelList;

		public static ConfigNode TankDefinition(string name)
		{
			List<ConfigNode> TankNodes = new List<ConfigNode>(GameDatabase.Instance.GetConfigNodes ("TANK_DEFINITION"));
			if (TankNodes == null || TankNodes.Count == 0) {
				print ("explicitly loading from file, because GameDatabase is unhelpful.");
				ConfigNode n = ConfigNode.Load (KSPUtil.ApplicationRootPath.Replace ("\\", "/") + "GameData/Ialdabaoth/PluginData/TankTypes.cfg");
				if(n != null)
					TankNodes = new List<ConfigNode>(n.GetNodes("TANK_DEFINITION"));
			}
			//print ("Searching " + (GameDatabase.Instance.GetConfigNodes ("TANK_DEFINITION").Length) + " TANK_DEFINITION nodes for " + name);
			return TankNodes.Find (n => n.HasValue ("name") && n.GetValue ("name").Equals (name));
		}

		public override void OnLoad(ConfigNode node)
		{
			print ("========ModuleFuelTanks.OnLoad called. Node is:=======");
			print (node.ToString ());
			if (node.HasValue ("type") && node.HasValue ("volume")) {
				//ConfigNode config = ConfigNode.Load (appPath + "tanktypes.cfg");

				string volume = node.GetValue ("volume");
				string tank_type = node.GetValue ("type");
				if(TankDefinition (tank_type) != null) {
					node = new ConfigNode();
					TankDefinition(tank_type).CopyTo (node);
					node.AddValue ("volume", volume);
				}
			}
			base.OnLoad (node);
			if (node.HasValue ("basemass")) {
				if(node.GetValue ("basemass").Contains ("*") && node.GetValue ("basemass").Contains ("volume"))
				{
					float.TryParse(node.GetValue ("basemass").Replace ("volume", "").Replace ("*", "").Trim (), out basemass);
					basemass = RoundTo4SigFigs(basemass * volume);
				}
			}

			if (fuelList == null)
				fuelList = new List<FuelTank> ();
			else
				fuelList.Clear ();

			foreach (ConfigNode tankNode in node.nodes) {
				if(tankNode.name.Equals ("TANK")) {
#if DEBUG
					print ("loading FuelTank from node " + tankNode.ToString ());
#endif
					FuelTank tank = new FuelTank();
					tank.module = this;
					tank.Load (tankNode);
					fuelList.Add (tank);
				}
			}
#if DEBUG
			print ("ModuleFuelTanks.onLoad loaded " + fuelList.Count + " fuels");

			print ("ModuleFuelTanks loaded. ");
#endif
			part.mass = basemass + tank_mass;
		}

		
		
		public override void OnSave (ConfigNode node)
		{
#if DEBUG
			print ("========ModuleFuelTanks.OnSave called. Node is:=======");
			print (node.ToString ());
#endif
			if (fuelList == null)
				fuelList = new List<FuelTank> ();
			foreach (FuelTank tank in fuelList) {
				ConfigNode subNode = new ConfigNode("TANK");
				tank.Save (subNode);
#if DEBUG
				print ("========ModuleFuelTanks.OnSave adding subNode:========");
				print (subNode.ToString());
#endif
				node.AddNode (subNode);
				tank.module = this;
			}
		}


		public override void OnStart (StartState state)
		{
#if DEBUG
			print ("========ModuleFuelTanks.OnStart( State == " + state.ToString () + ")=======");
#endif
			if (basemass == 0 && part != null)
				basemass = part.mass;
			if(fuelList == null) {
				fuelList = new List<ModuleFuelTanks.FuelTank> ();
			}

			if (fuelList.Count == 0) {
				// when we get called from the editor, the fuelList won't be populated
				// because OnLoad() was never called. This is a hack to fix that.
				Part prefab = part.symmetryCounterparts.Find(pf => pf.Modules.Contains ("ModuleFuelTanks") 
				                                             && ((ModuleFuelTanks)pf.Modules["ModuleFuelTanks"]).fuelList.Count >0);
				if(prefab) {
#if DEBUG
					print ("ModuleFuelTanks.OnStart: copying from a symmetryCounterpart with a ModuleFuelTanks PartModule");
#endif
				} else {
					AvailablePart partData = PartLoader.getPartInfoByName (part.partInfo.name);
					if(partData == null) {
						print ("ModuleFuelTanks.OnStart could not find AvailablePart for " + part.partInfo.name);
					} else if(partData.partPrefab == null) {
						print ("ModuleFuelTanks.OnStart: AvailablePart.partPrefab is null.");
					} else {
						prefab = partData.partPrefab;
						if(!prefab.Modules.Contains ("ModuleFuelTanks"))
						{
							print ("ModuleFuelTanks.OnStart: AvailablePart.partPrefab does not contain a ModuleFuelTanks.");
							prefab = null;
						} 
					}
				}
				if(prefab) {
					ModuleFuelTanks pModule = (ModuleFuelTanks) prefab.Modules["ModuleFuelTanks"];
					if(pModule == this)
						print ("ModuleFuelTanks.OnStart: Copying from myself won't do any good.");
					else {
						ConfigNode node = new ConfigNode("MODULE");
						pModule.OnSave (node);
						#if DEBUG
						print ("ModuleFuelTanks.OnStart node from prefab:" + node);
						#endif
						this.OnLoad (node);
					}
				}
			} 
			foreach(FuelTank tank in fuelList)
				tank.module = this;

			if (radius > 0 && length > 0) {
				part.transform.localScale = new Vector3(rscale / radius, length, rscale / radius);
				foreach(AttachNode n in part.attachNodes) {
					if(n.nodeType == AttachNode.NodeType.Stack)
						n.offset.y *= length;
				}
			}
			part.mass = basemass + tank_mass;

			if(HighLogic.LoadedSceneIsEditor) {
				UpdateSymmetryCounterparts();
					// if we detach and then re-attach a configured tank with symmetry on, make sure the copies are configured.
			}

		}

		public void CheckSymmetry()
		{
			#if DEBUG
			print ("ModuleFuelTanks.CheckSymmetry for " + part.partInfo.name);
			#endif
			EditorLogic editor = EditorLogic.fetch;
			if (editor != null && editor.editorScreen == EditorLogic.EditorScreen.Parts && part.symmetryCounterparts.Count > 0) {
				#if DEBUG
				print ("ModuleFuelTanks.CheckSymmetry: updating " + part.symmetryCounterparts.Count + " other parts.");
				#endif
				UpdateSymmetryCounterparts();
			}
			#if DEBUG
			print ("ModuleFuelTanks checked symmetry");
			#endif
		}
		public override void OnUpdate ()
		{
			if (HighLogic.LoadedSceneIsEditor) {

			} else if (timestamp > 0) {
				double delta_t = Planetarium.GetUniversalTime () - timestamp;
				foreach (FuelTank tank in fuelList) {
					if (tank.amount > 0 && tank.loss_rate > 0 && part.temperature > tank.temperature) {
						double loss = tank.maxAmount * tank.loss_rate * (part.temperature - tank.temperature) * delta_t; // loss_rate is calibrated to 300 degrees.
						if (loss > tank.amount)
							tank.amount = 0;
						else
							tank.amount -= loss;
					}
				}
				timestamp = Planetarium.GetUniversalTime ();
			} else {
				timestamp = Planetarium.GetUniversalTime ();
			}
		}



		public override string GetInfo ()
		{
			string info = "Modular Fuel Tank: \n"
				+ "  Max Volume: " + volume.ToString () + "\n" 
					+ "  Tank can hold:";
			foreach(FuelTank tank in fuelList)
			{
				info += "\n   " + tank + " " + tank.note;
			}
			return info + "\n";
		}


		public void OnGUI()
		{
			EditorLogic editor = EditorLogic.fetch;
			if (!HighLogic.LoadedSceneIsEditor || !editor || editor.editorScreen != EditorLogic.EditorScreen.Actions) {
				return;
			}
			
			if (EditorActionGroups.Instance.GetSelectedParts ().Contains (part)) {
				Rect screenRect = new Rect(0, 365, 430, (Screen.height - 365));
				GUILayout.Window (part.name.GetHashCode (), screenRect, fuelManagerGUI, "Fuel Tanks for " + part.partInfo.title);
			} else if(textFields.Count > 0)
				textFields.Clear ();
		}
		
		private List<string> textFields = new List<string>();
		private Part lastPart;		
		private void fuelManagerGUI(int WindowID)
		{
			GUILayout.BeginVertical ();

			GUILayout.BeginHorizontal();
			GUILayout.Label ("Current mass: " + ModuleFuelTanks.RoundTo4SigFigs(part.mass + part.GetResourceMass()) + " Ton(s)");
			GUILayout.Label ("Dry mass: " + Math.Round(1000 * part.mass) / 1000.0 + " Ton(s)");
			GUILayout.EndHorizontal ();
			
			if (fuelList.Count == 0) {
				
				GUILayout.BeginHorizontal();
				GUILayout.Label ("This fuel tank cannot hold resources.");
				GUILayout.EndHorizontal ();
				return;
			}
			
			print ("fuelManagerGUI Row 2");
			GUILayout.BeginHorizontal();
			GUILayout.Label ("Available volume: " + availableVolume + " / " + volume);
			GUILayout.EndHorizontal ();
			
			int text_field = 0;
			
			foreach (ModuleFuelTanks.FuelTank tank in fuelList) {
				print ("fuelManagerGUI Row 3." + tank.ToString ());
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
				GUILayout.Label(" " + tank, GUILayout.Width (120));
				if(part.Resources.Contains(tank) && part.Resources[tank].maxAmount > 0.0) {					
					double amount = ModuleFuelTanks.RoundTo4SigFigs (part.Resources[tank].amount);
					double maxAmount = ModuleFuelTanks.RoundTo4SigFigs (part.Resources[tank].maxAmount);
					
					GUIStyle color = new GUIStyle(GUI.skin.textField);
					if(textFields[amountField].Trim().Equals ("")) // I'm not sure why this happens, but we'll fix it here.
						textFields[amountField] = tank.amount.ToString();
					else if(textFields[amountField].Equals (amount.ToString ()))
						color.normal.textColor = Color.white;
					else
						color.normal.textColor = Color.yellow;
					textFields[amountField] = GUILayout.TextField(textFields[amountField], color, GUILayout.Width (65));
					
					GUILayout.Label("/", GUILayout.Width (5));
					
					
					
					color = new GUIStyle(GUI.skin.textField);
					if(textFields[maxAmountField].Equals (maxAmount.ToString ()))
						color.normal.textColor = Color.white;
					else
						color.normal.textColor = Color.yellow;
					textFields[maxAmountField] = GUILayout.TextField(textFields[maxAmountField], color, GUILayout.Width (65));
					
					GUILayout.Label(" ", GUILayout.Width (5));
					
					if(GUILayout.Button ("Update", GUILayout.Width (60))) {
						
						double newMaxAmount = maxAmount;
						if(!double.TryParse (textFields[maxAmountField], out newMaxAmount))
							newMaxAmount = maxAmount;
						
						double newAmount = amount;
						if(!double.TryParse(textFields[amountField], out newAmount))
							newAmount = amount;
						
						if(newMaxAmount != maxAmount) {
							tank.maxAmount = newMaxAmount;
							
						}
						
						if(newAmount != amount) {
							tank.amount = newAmount;
						}
						
						textFields[amountField] = tank.amount.ToString();
						textFields[maxAmountField] = tank.maxAmount.ToString();
						
						if(part.symmetryCounterparts.Count > 0) 
							UpdateSymmetryCounterparts();
						
					}
					if(GUILayout.Button ("Remove", GUILayout.Width (60))) {
						tank.maxAmount = 0;
						textFields.Clear ();
						if(part.symmetryCounterparts.Count > 0) 
							UpdateSymmetryCounterparts();
						
					}
					
				} else if(availableVolume >= 0.001) {
					string extraData = "Max: " + ModuleFuelTanks.RoundTo4SigFigs(availableVolume * tank.efficiency).ToString () + " (+" + ModuleFuelTanks.RoundTo4SigFigs(availableVolume * tank.efficiency * tank.mass) + " tons)" ;
					
					GUILayout.Label(extraData, GUILayout.Width (150));
					
					if(GUILayout.Button("Add", GUILayout.Width (130))) {
						tank.maxAmount = Math.Floor (1000 * availableVolume * tank.efficiency) / 1000.0;
						tank.amount = tank.maxAmount;
						
						textFields.Clear ();
						
						if(part.symmetryCounterparts.Count > 0) 
							UpdateSymmetryCounterparts();
						
					}
				} else {
					GUILayout.Label ("  No room for tank.", GUILayout.Width (150));
					
				}
				GUILayout.EndHorizontal ();
				
			}
			
			print ("fuelManagerGUI Row 4");
			GUILayout.BeginHorizontal();
			if(GUILayout.Button ("Remove All Tanks")) {
				textFields.Clear ();
				foreach(ModuleFuelTanks.FuelTank tank in fuelList) {
					tank.amount = 0;
					tank.maxAmount = 0;
				}
				if(part.symmetryCounterparts.Count > 0) 
					UpdateSymmetryCounterparts();
				
			}	
			GUILayout.EndHorizontal();
			if(GetEnginesFedBy(part).Count > 0)
			{
				List<string> check_dupes = new List<string>();
				
				print ("fuelManagerGUI Row 5");
				GUILayout.BeginHorizontal();
				GUILayout.Label ("Configure remaining volume for engines:");
				GUILayout.EndHorizontal();
				
				foreach(Part engine in GetEnginesFedBy(part))
				{
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
							ModuleFuelTanks.FuelTank tank = fuelList.Find (f => f.ToString ().Equals (tfuel.name ));
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
						if(!check_dupes.Contains (label)) {
							check_dupes.Add (label);
							print ("fuelManagerGUI Row 6." + label);
							
							GUILayout.BeginHorizontal();
							if(GUILayout.Button (label)) {
								textFields.Clear ();
								
								double total_volume = availableVolume * (1 - inefficiency / ratio_factor);
								foreach(ModuleEngines.Propellant tfuel in thruster.propellants)
								{
									if(PartResourceLibrary.Instance.GetDefinition(tfuel.name).resourceTransferMode != ResourceTransferMode.NONE) {
										ModuleFuelTanks.FuelTank tank = fuelList.Find (t => t.name.Equals (tfuel.name));
										if(tank) {
											tank.maxAmount += Math.Floor (1000 * total_volume * tfuel.ratio / ratio_factor) / 1000.0;
											tank.amount += Math.Floor (1000 * total_volume * tfuel.ratio / ratio_factor) / 1000.0;
										}
									}
								}
								if(part.symmetryCounterparts.Count > 0) 
									UpdateSymmetryCounterparts();
							}
							GUILayout.EndHorizontal ();
						}
					}
				}
			}
			GUILayout.EndVertical ();
			print ("fuelManagerGUI Done.");
		}
		

		public static List<Part> GetEnginesFedBy(Part part)
		{
			Part ppart = part;
			while (ppart.parent != null && ppart.parent != ppart)
				ppart = ppart.parent;
			
			return new List<Part>(ppart.FindChildParts<Part> (true)).FindAll (p => p.Modules.Contains ("ModuleEngines"));
		}

		public int UpdateSymmetryCounterparts()
		{
			int i = 0;
			foreach(Part sPart in part.symmetryCounterparts)
			{
				ModuleFuelTanks fuel = (ModuleFuelTanks) sPart.Modules["ModuleFuelTanks"];
				if(fuel)
				{
					i++;
					foreach(ModuleFuelTanks.FuelTank tank in fuel.fuelList) {
						tank.amount = 0;
						tank.maxAmount = 0;
					}
					foreach(ModuleFuelTanks.FuelTank tank in this.fuelList)
					{
						if(tank.maxAmount > 0)
						{
							ModuleFuelTanks.FuelTank pTank = fuel.fuelList.Find (t => t.name.Equals (tank.name));
							if(pTank) {
								pTank.maxAmount = tank.maxAmount;
								pTank.amount = tank.amount;
							}
						}
					}
				}
			}
			return i;
		}
	}
}