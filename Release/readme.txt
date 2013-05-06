Ialdabaoth's Modular Fuel Tank system
source at: https://github.com/Ialdabaoth/modularFuelTanks

Released through GPL3.0 (http://www.gnu.org/licenses/gpl.html)


HOW IT WORKS:

1. First, you need a part configured with a ModuleFuelTanks, like so:


MODULE
{
	name = ModuleFuelTanks
	volume = 400
	basemass = 0.125
	TANK
	{
	   name = LiquidH2
	   efficiency = 0.975
	   mass = 0.000
	   temperature = -253
	   loss_rate = 0.000015
	} 
	TANK
	{
	   name = LiquidOxygen
	   efficiency = 0.995
	   mass = 0.0003125
	   temperature = -183
	   loss_rate = 0.0000025
	} 
	TANK
	{
	  name = LiquidFuel
	  efficiency = 1.0
	  mass = 0.0003125
	}
	TANK
	{
	  name = Oxidizer
	  efficiency = 1.0
	  mass = 0.0003125
	}
	TANK
	{
	  name = MonoPropellant
	  efficiency = 1.0
	  mass = 0.0005
	}
}


This tells the module that this tank can hold LiquidFuel, Oxidizer, MonoPropellant, LiquidH2 and LiquidOxygen. It also tells the module how much additional mass or volume each type of tank consumes (beyond the mass and volume of the fuel itself), and that LiquidHydrogen is slightly leaky.

The values are:

 name 		= LiquidOxygen 	// the RESOURCE name that this kind of tank holds
 efficiency 	= 0.995		// the percentage of tank volume that can store fuel
 mass 		= 0.0003125	// the additional dry mass added per volume of this tank
 temperature	= -183		// the temperature that fuel begins evaporating
 loss_rate 	= 0.0000000002	// the rate at which fuel evaporates of the container.

Note that 'mass' and 'efficiency' is per volume of fuel, not per volume of tank+fuel. So
if you want a tank to store 3600 units of LiquidOxygen, in the RESOURCES column say:

RESOURCE
{
 name = LiquidOxygen
 amount = 3600
 maxAmount = 3600
}

Assuming the TANK configuration above, MaxAmount = 3600 will create a tank that takes up (3600 / .995) = 3618.09 of the available volume, and which adds 3600*.0003125 = 1.125 tons to the dry mass of the tank, in addition to the mass of the LiquidOxygen itself. As long
as the tank's Part is at a temperature below -183 Celsius, no LiquidOxygen will leak out;
otherwise, LiquidOxygen will begin leaking out at a rate of (3600*0.0000000001) = 0.00000036 units per second per degree Celcius that the part's temperature exceeds -183. At room temperature (25C), this works out to about 0.27 units per hour.


2. Once you have the module specified correctly in part.cfg, you can load up the tank in the editor. After attaching it to your ship, go to the Action Groups menu and click on the tank. There you will see various options - each fuel type will have a button to add a tank, or text boxes to adjust the tank volume and initial amount of that fuel, as well as a button to remove all tanks from this part. When you edit a If the system detects an engine attached, it will (usually) also give you a button to remove all tanks and then fill the entire volume with that engine's fuel, in the ratio that that engine consumes it.

When you edit a fuel tank, its load out is automatically copied to all identical symmetrical fuel tanks. If you want to have an asymmetrical load-out, attach each tank separately with symmetry off, then edit each tank individually.


3. You can also configure engines to use multiple fuel types, like so:

MODULE
{
	name = ModuleEngineConfigs
	configuration = LiquidFuel+Oxidizer (50 Thrust, 370 Isp)
	modded = false
	CONFIG
	{
		name = LiquidFuel+Oxidizer (50 Thrust, 370 Isp)
		thrustVectorTransformName = thrustTransform
		exhaustDamage = True
		ignitionThreshold = 0.1
		minThrust = 0
		maxThrust = 50
		heatProduction = 300
		fxOffset = 0, 0, 0.21
		PROPELLANT
		{
		 	name = LiquidFuel
       	  		ratio = 0.4
			DrawGauge = True
		}
		PROPELLANT
		{
			name = Oxidizer
			ratio = 0.6
		}
		atmosphereCurve
 		{
   		 key = 0 370
  		 key = 1 270
	 	}
	
	}

	CONFIG
	{
		name = LiquidFuel+LiquidOxygen (55 Thrust, 390 Isp)
		thrustVectorTransformName = thrustTransform
		exhaustDamage = True
		ignitionThreshold = 0.1
		minThrust = 0
		maxThrust = 55
		heatProduction = 275
		fxOffset = 0, 0, 0.21
		PROPELLANT
		{
		 	name = LiquidFuel
       	  		ratio = 0.35
			DrawGauge = True
		}
		PROPELLANT
		{
			name = LiquidOxygen
			ratio = 0.65
		}
		atmosphereCurve
 		{
   		 key = 0 390
  		 key = 1 300
	 	}
	
	}

	CONFIG
	{
		name = LiquidH2+LiquidOxygen (40 Thrust, 460 Isp)
		thrustVectorTransformName = thrustTransform
		exhaustDamage = True
		ignitionThreshold = 0.1
		minThrust = 0
		maxThrust = 40
		heatProduction = 250
		fxOffset = 0, 0, 0.21
		PROPELLANT
		{
		 	name = LiquidH2
       	  		ratio = 0.73
			DrawGauge = True
		}
		PROPELLANT
		{
			name = LiquidOxygen
			ratio = 0.27
		}
		atmosphereCurve
 		{
   		 key = 0 460
  		 key = 1 310
	 	}
	
	}
}

Each CONFIG node is effectively a ModuleEngines MODULE node that will overwrite the part's original ModuleEngines node when selected in the editor. Note that configuration=<name> MUST point to a CONFIG which matches the default ModuleEngines node.